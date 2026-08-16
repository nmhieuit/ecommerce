import { expect, test, type ConsoleMessage, type Page, type Request } from '@playwright/test';

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
  await page.request.post(`${GATEWAY_ORIGIN}/bff/checkout`, { failOnStatusCode: false });
}

test.describe('shopping walkthrough', () => {
  test.beforeEach(async ({ page }) => {
    await resetBasket(page);
  });

  test('browse, add to basket, check out, and see the confirmation', async ({ page }) => {
    const { consoleErrors, requestOrigins } = watch(page);

    // ---- browse (US1) ----
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
    await expect(page.getByRole('listitem').filter({ hasText: NOTEBOOK })).toBeVisible();
    await expect(page.getByText('$12.50').first()).toBeVisible();

    // ---- add to basket (US2) ----
    await page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` }).click();
    await page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` }).click();
    await page.getByRole('button', { name: `Add ${APRON} to basket` }).click();

    await page.getByRole('link', { name: 'Basket' }).click();

    await expect(page.getByRole('heading', { name: 'Basket' })).toBeVisible();
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
    expect(storedKeys).toHaveLength(0);

    // ---- check out (US3) ----
    await page.getByRole('button', { name: 'Check out' }).click();

    await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();

    const reference = await page.getByText(/^[0-9a-f-]{36}$/i).innerText();
    await expect(page.getByText('$59.25')).toBeVisible();

    // ---- SC-005: the reference names the order the backend actually created ----
    const order = await page.request.get(`${GATEWAY_ORIGIN}/bff/orders/${reference}`);
    expect(order.ok()).toBe(true);
    expect((await order.json()).total).toBe(59.25);

    // ---- FR-010: the basket is empty afterwards ----
    await page.getByRole('link', { name: 'Basket' }).click();
    await expect(page.getByText(/your basket is empty/i)).toBeVisible();

    // ---- SC-010: only the gateway was ever addressed ----
    expect([...requestOrigins]).toEqual([GATEWAY_ORIGIN]);

    // ---- SC-002: no console errors anywhere in that journey ----
    expect(consoleErrors).toEqual([]);
  });

  /**
   * Spec FR-008 and SC-004: blocked in the interface, with **no request sent**. Counting requests
   * is the assertion — a request the server rejects would be a failure of this test, not a pass.
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
    await expect(checkout).toBeDisabled();

    await checkout.click({ force: true }).catch(() => {
      // A disabled control may refuse the click outright; that is the behaviour under test.
    });

    expect(checkoutRequests).toHaveLength(0);
  });

  /**
   * Spec FR-016 and SC-008. The control disables while in flight, so the second click never
   * becomes a second request — and the backend would refuse it anyway, since the basket is emptied.
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
    await expect(checkout).toBeEnabled();

    // Two clicks with no wait between them — the race the requirement is about.
    await Promise.all([
      checkout.click(),
      checkout.click({ force: true }).catch(() => undefined),
    ]);

    await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();

    expect(checkoutRequests).toHaveLength(1);
  });

  /**
   * Spec FR-017 and SC-009: the entire flow by keyboard, with the focused element visible at every
   * step. Asserted by driving the whole journey with Tab and Enter and never a pointer.
   */
  test('the whole flow can be completed using only the keyboard', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();

    // Tab until the first add-to-basket control has focus, then activate it with the keyboard.
    const addNotebook = page.getByRole('button', { name: `Add ${NOTEBOOK} to basket` });
    await focusByTabbing(page, addNotebook);
    await expect(addNotebook).toBeFocused();
    await page.keyboard.press('Enter');

    const basketLink = page.getByRole('link', { name: 'Basket' });
    await focusByTabbing(page, basketLink);
    await page.keyboard.press('Enter');

    await expect(page.getByRole('heading', { name: 'Basket' })).toBeVisible();

    const checkout = page.getByRole('button', { name: 'Check out' });
    await focusByTabbing(page, checkout);
    await expect(checkout).toBeFocused();

    // A visible focus indicator, not merely a focused element (WCAG 2.4.7). The stylesheet sets an
    // outline on :focus-visible; a keyboard-driven focus must actually produce one.
    const outline = await checkout.evaluate((element) =>
      window.getComputedStyle(element).outlineStyle,
    );
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
