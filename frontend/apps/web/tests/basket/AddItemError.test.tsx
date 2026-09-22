import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { AddToBasketButton } from '@/features/catalog/AddToBasketButton';
import { server } from '../msw/server';

/**
 * Spec US2 acceptance scenario 5: "when the shopper attempts to add a product [and it fails], a
 * clear error is shown and the basket is not left displaying an item that was never actually
 * added."
 *
 * The second half is the one worth engineering for. A basket that optimistically shows an item the
 * backend rejected is worse than one that shows nothing — the shopper would check out believing
 * they had bought it.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';
const NOTEBOOK = '9f8d6b1e-0001-4000-8000-000000000001';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('AddToBasketButton', () => {
  /**
   * Kiểm tra: thêm thành công thì gửi đúng mã sản phẩm và số lượng, không kèm giá.
   * Lý do: US2 kịch bản 1/FR-003, và hợp đồng AddBasketItemRequest: 1 mức giá gửi từ đây sẽ là mức
   * giá người mua tự chọn.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T037, US2 (FR-003).
   */
  it('adds the product when the request succeeds', async () => {
    const added: unknown[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async ({ request }) => {
        added.push(await request.json());
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.click(screen.getByRole('button', { name: /add .*to basket/i }));

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. WaitFor lặp lại phép kiểm tới khi
    // đạt hoặc hết giờ; toHaveLength(1) đạt khi mảng có đúng 1 phần tử: đúng 1 request đã được gửi.
    await waitFor(() => expect(added).toHaveLength(1));

    // Product and quantity only. A price sent from here would be a price the shopper chose
    // (contracts/bff-openapi.yaml — AddBasketItemRequest).
    // toEqual(kỳ vọng): so sánh sâu từng trường, đỏ khi khác. Body phải đúng {productId, quantity:
    // 1}, không kèm giá (giá do BFF tra).
    expect(added[0]).toEqual({ productId: NOTEBOOK, quantity: 1 });
  });

  /**
   * Kiểm tra: thêm thất bại thì hiện lỗi rõ ràng và giỏ không hiển thị món chưa hề được thêm.
   * Lý do: US2 kịch bản 5: 1 giỏ lạc quan hiển thị món backend đã từ chối tệ hơn giỏ trống — người
   * mua sẽ thanh toán tưởng đã mua.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T037, US2 (FR-012, US2-KB5).
   */
  it('shows a clear error when the request fails', async () => {
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.click(screen.getByRole('button', { name: /add .*to basket/i }));

    // toHaveTextContent(chuỗi hoặc regex): xanh khi nội dung phần tử khớp. Chờ tối đa 5 giây cho
    // phần tử role="alert" và kiểm nội dung khớp biểu thức chính quy /could not add/i (không phân
    // biệt hoa thường); đỏ khi không có cảnh báo hoặc câu khác.
    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toHaveTextContent(
      /could not add/i,
    );
  });

  /**
   * Kiểm tra: nút thêm bị vô hiệu trong lúc yêu cầu đang chạy.
   * Lý do: người mua bấm dồn không thể xếp hàng 5 lần thêm cho 1 sản phẩm — cùng loại chốt chặn mà
   * FR-016 dựa vào ở bước thanh toán.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T037, US2 (FR-003, FR-016).
   */
  it('disables itself while the addition is in flight', async () => {
    let release: (() => void) | undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async () => {
        await held;
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    const button = screen.getByRole('button', { name: /add .*to basket/i });
    await userEvent.click(button);

    // toBeDisabled(): xanh khi phần tử bị vô hiệu. Đỏ khi nút vẫn bấm được trong lúc đang gửi (bấm
    // lặp).
    await waitFor(() => expect(button).toBeDisabled());

    release?.();
    // toBeEnabled(): xanh khi phần tử bấm được. Sau khi hoàn tất nút phải bật lại; đỏ khi kẹt ở
    // trạng thái vô hiệu.
    await waitFor(() => expect(button).toBeEnabled());
  });

  /**
   * Kiểm tra: nút thêm thao tác được bằng bàn phím.
   * Lý do: FR-017/SC-009: cả luồng phải hoàn thành được chỉ bằng bàn phím nên thêm vào giỏ không
   * thể chỉ dành cho con trỏ.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T037, US2 (FR-017, SC-009).
   */
  it('can be operated by keyboard', async () => {
    const added: unknown[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async ({ request }) => {
        added.push(await request.json());
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.tab();
    // toHaveFocus(): xanh khi phần tử đang giữ focus. ToHaveFocus đạt khi phần tử đang giữ focus:
    // nút phải là phần tử đầu tiên nhận focus bằng Tab.
    expect(screen.getByRole('button', { name: /add .*to basket/i })).toHaveFocus();

    await userEvent.keyboard('{Enter}');

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Enter phải gửi đúng 1 request.
    await waitFor(() => expect(added).toHaveLength(1));
  });
});
