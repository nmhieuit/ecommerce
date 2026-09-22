import { expect, test, type ConsoleMessage, type Page, type Request } from '@playwright/test';
import { authorizationHeader, signIn } from './auth';

/**
 * The end-to-end walkthrough: browse → add to basket → check out → confirmation.
 *
 * This spec exists because four success criteria are not observable from jsdom at all, no matter
 * how good the component tests are:
 *
 * - **SC-002** zero browser-console errors across the whole walkthrough
 * - **SC-008** a rapid double checkout creates exactly one order
 * - **SC-009** the entire flow completed by keyboard, with focus visible
 * - **SC-010** every request addressed to the gateway and nothing else
 *
 * It needs the full stack running (see quickstart.md) — the four services and the gateway — plus
 * the dev server, which Playwright starts itself.
 */

const GATEWAY_ORIGIN = process.env.GATEWAY_ORIGIN ?? 'http://localhost:5300';
const STOREFRONT_ORIGIN = process.env.STOREFRONT_URL ?? 'http://localhost:5173';

const NOTEBOOK = 'Field Notes Notebook';
const APRON = 'Linen Apron';

/**
 * Records every console error and every request destination for the life of a page, so the two
 * whole-journey criteria can be asserted once at the end rather than sampled per step.
 */
function watch(page: Page) {
  const consoleErrors: string[] = [];
  const requestOrigins = new Set<string>();

  page.on('console', (message: ConsoleMessage) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });

  page.on('pageerror', (error) => consoleErrors.push(String(error)));

  page.on('request', (request: Request) => {
    const url = new URL(request.url());

    // Only network destinations matter. The dev server's own asset requests are the storefront
    // serving itself, not the app talking to a backend.
    if (url.origin !== STOREFRONT_ORIGIN) {
      requestOrigins.add(url.origin);
    }
  });

  return { consoleErrors, requestOrigins };
}

/** Empties the basket so each test starts from a known state, whatever a previous run left. */
async function resetBasket(page: Page) {
  await page.request.post(`${GATEWAY_ORIGIN}/bff/checkout`, {
    headers: await authorizationHeader(page),
    failOnStatusCode: false,
  });
}

test.describe('shopping walkthrough', () => {
  test.beforeEach(async ({ page }) => {
    // Every screen sits behind sign-in, and the basket is the signed-in shopper's.
    await signIn(page);
    await resetBasket(page);
  });

  /**
   * Kiểm tra: lượt đi trọn vẹn duyệt → thêm vào giỏ → tải lại giữa chừng → thanh toán → xác nhận,
   * không lỗi console, mọi request chỉ đi tới gateway.
   * Lý do: SC-002/005/007/010 không quan sát được trong jsdom: cần trình duyệt thật, cả stack chạy,
   * và ghi lại mọi lỗi console lẫn mọi đích request cho cả hành trình.
   * Lưu ý: assert `storedKeys` hiện ĐỎ: sau spec 004 phiên đăng nhập được lưu ở `sessionStorage`
   * với khoá `storefront.session`, nên danh sách khoá không còn rỗng. Xem QA_Debt mục 004.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, US1-US3 (SC-002, SC-005, SC-007, SC-010).
   */
  test('browse, add to basket, check out, and see the confirmation', async ({ page }) => {
    const { consoleErrors, requestOrigins } = watch(page);

    // ---- browse (US1) ----
    await page.goto('/');

    // toBeVisible(): Playwright tự chờ tới khi phần tử hiển thị, hết giờ thì đỏ.
    await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
    await expect(page.getByRole('listitem').filter({ hasText: NOTEBOOK })).toBeVisible();
    await expect(page.getByText('$12.50').first()).toBeVisible();

    // ---- add to basket (US2) ----
    await page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` }).click();
    await page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` }).click();
    await page.getByRole('button', { name: `Add ${APRON} to basket` }).click();

    await page.getByRole('link', { name: 'Basket' }).click();

    await expect(page.getByRole('heading', { name: 'Basket' })).toBeVisible();
    // toHaveCount(n): xanh khi có đúng n phần tử khớp locator (tự chờ).
    await expect(page.getByRole('list', { name: 'Basket' }).getByRole('listitem')).toHaveCount(2);

    // Two notebooks merged onto one line, not two lines (spec FR-005).
    await expect(page.getByText(/quantity:\s*2\s*×\s*\$12\.50/i)).toBeVisible();

    // The figure quickstart.md quotes.
    await expect(page.getByText('$59.25')).toBeVisible();

    // ---- refresh mid-basket (FR-011, SC-007) ----
    await page.reload();
    await expect(page.getByText('$59.25')).toBeVisible();

    // Nothing was written to browser storage — the basket came back because it is the server's
    // basket for this caller (data-model.md — Client-side state).
    const storedKeys = await page.evaluate(() => [
      ...Object.keys(window.localStorage),
      ...Object.keys(window.sessionStorage),
    ]);
    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử.
    expect(storedKeys).toHaveLength(0);

    // ---- check out (US3) ----
    await page.getByRole('button', { name: 'Check out' }).click();

    await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();

    const reference = await page.getByText(/^[0-9a-f-]{36}$/i).innerText();
    await expect(page.getByText('$59.25')).toBeVisible();

    // ---- SC-005: the reference names the order the backend actually created ----
    const order = await page.request.get(`${GATEWAY_ORIGIN}/bff/orders/${reference}`, {
      headers: await authorizationHeader(page),
    });
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác.
    expect(order.ok()).toBe(true);
    expect((await order.json()).total).toBe(59.25);

    // ---- FR-010: the basket is empty afterwards ----
    await page.getByRole('link', { name: 'Basket' }).click();
    await expect(page.getByText(/your basket is empty/i)).toBeVisible();

    // ---- SC-010: only the gateway was ever addressed ----
    // toEqual(kỳ vọng): so sánh sâu từng trường, đỏ khi khác.
    expect([...requestOrigins]).toEqual([GATEWAY_ORIGIN]);

    // ---- SC-002: no console errors anywhere in that journey ----
    expect(consoleErrors).toEqual([]);
  });

  /**
   * Kiểm tra: giỏ rỗng thì nút thanh toán bị chặn và không có request nào được gửi.
   * Lý do: FR-008/SC-004: đếm số request chính là assertion — 1 request bị server từ chối sẽ là
   * thất bại của test này.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, US3 (FR-008, SC-004).
   */
  test('checkout is blocked, and unsent, when the basket is empty', async ({ page }) => {
    const checkoutRequests: string[] = [];
    page.on('request', (request) => {
      if (request.url().endsWith('/bff/checkout')) {
        checkoutRequests.push(request.url());
      }
    });

    await page.goto('/basket');

    const checkout = page.getByRole('button', { name: 'Check out' });
    // toBeDisabled(): xanh khi phần tử bị vô hiệu. Nút bị vô hiệu khi giỏ rỗng.
    await expect(checkout).toBeDisabled();

    await checkout.click({ force: true }).catch(() => {
      // A disabled control may refuse the click outright; that is the behaviour under test.
    });

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Không có request checkout nào
    // được gửi.
    expect(checkoutRequests).toHaveLength(0);
  });

  /**
   * Kiểm tra: thanh toán 2 lần liên tiếp nhanh chỉ tạo đúng 1 đơn.
   * Lý do: FR-016/SC-008: nút bị vô hiệu khi đang xử lý nên cú bấm thứ 2 không thành request;
   * backend cũng sẽ từ chối vì giỏ đã rỗng.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, US3 (FR-016, SC-008).
   */
  test('checking out twice in rapid succession creates exactly one order', async ({ page }) => {
    const checkoutRequests: string[] = [];
    page.on('request', (request) => {
      if (request.url().endsWith('/bff/checkout') && request.method() === 'POST') {
        checkoutRequests.push(request.url());
      }
    });

    await page.goto('/');
    await page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` }).click();
    await page.getByRole('link', { name: 'Basket' }).click();

    const checkout = page.getByRole('button', { name: 'Check out' });
    // toBeEnabled(): xanh khi phần tử bấm được. Nút bấm được trước khi thử.
    await expect(checkout).toBeEnabled();

    // Two clicks with no wait between them — the race the requirement is about.
    await Promise.all([
      checkout.click(),
      checkout.click({ force: true }).catch(() => undefined),
    ]);

    // toBeVisible(): Playwright tự chờ tới khi phần tử hiển thị, hết giờ thì đỏ. Đơn được đặt.
    await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Đúng 1 POST checkout; đỏ khi 2.
    expect(checkoutRequests).toHaveLength(1);
  });

  /**
   * Kiểm tra: toàn bộ luồng hoàn thành chỉ bằng Tab/Enter, phần tử đang focus luôn nhìn thấy được.
   * Lý do: FR-017/SC-009 (WCAG 2.4.7): phải là chỉ dấu focus nhìn thấy được chứ không chỉ là phần
   * tử đang được focus.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, US1-US3 (FR-017, SC-009).
   */
  test('the whole flow can be completed using only the keyboard', async ({ page }) => {
    await page.goto('/');
    // toBeVisible(): Playwright tự chờ tới khi phần tử hiển thị, hết giờ thì đỏ.
    await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();

    // Tab until the first add-to-basket control has focus, then activate it with the keyboard.
    const addNotebook = page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` });
    await focusByTabbing(page, addNotebook);
    // toBeFocused(): xanh khi phần tử đang giữ focus (tự chờ).
    await expect(addNotebook).toBeFocused();
    await page.keyboard.press('Enter');

    const basketLink = page.getByRole('link', { name: 'Basket' });
    await focusByTabbing(page, basketLink);
    await page.keyboard.press('Enter');

    await expect(page.getByRole('heading', { name: 'Basket' })).toBeVisible();

    const checkout = page.getByRole('button', { name: 'Check out' });

    // Wait for the basket to actually load before tabbing. A disabled control is not in the tab
    // order, so tabbing while the basket is still fetching walks past it forever — and the button
    // is correctly disabled until the basket is known to hold something. Against a local dev server
    // the fetch beat the tabbing; against containers it does not, which is a difference in latency
    // rather than in behaviour.
    // toBeEnabled(): xanh khi phần tử bấm được.
    await expect(checkout).toBeEnabled();

    await focusByTabbing(page, checkout);
    await expect(checkout).toBeFocused();

    // A visible focus indicator, not merely a focused element (WCAG 2.4.7). The stylesheet sets an
    // outline on :focus-visible; a keyboard-driven focus must actually produce one.
    const outline = await checkout.evaluate((element) =>
      window.getComputedStyle(element).outlineStyle,
    );
    // not.toBe(giá trị cấm): xanh khi khác giá trị đó.
    expect(outline).not.toBe('none');

    await page.keyboard.press('Enter');

    await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();
  });
});

/**
 * Presses Tab until the target holds focus. Bounded, so a control that is genuinely unreachable
 * fails the test rather than looping forever — being unreachable by keyboard is exactly the defect
 * SC-009 is written to catch.
 */
async function focusByTabbing(page: Page, target: ReturnType<Page['getByRole']>) {
  for (let press = 0; press < 25; press++) {
    if (await target.evaluate((element) => element === document.activeElement)) {
      return;
    }

    await page.keyboard.press('Tab');
  }

  throw new Error('The target could not be reached by tabbing within 25 presses.');
}
