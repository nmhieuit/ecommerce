import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { CheckoutButton } from '@/features/checkout/CheckoutButton';
import { server } from '../msw/server';

/**
 * Spec FR-008 and SC-004: "the storefront blocks the attempt in the interface and sends no checkout
 * request to the backend at all."
 *
 * The second half is what this suite is really for. A request the server rejects is a failure of
 * this scenario, not a pass — the criterion is zero requests, not zero orders.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('CheckoutButton with an empty basket', () => {
  /**
   * Kiểm tra: nút thanh toán không thao tác được khi giỏ rỗng.
   * Lý do: FR-008: storefront phải chặn thanh toán ngay trên giao diện khi giỏ rỗng.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T054, US3 (FR-008).
   */
  it('is not operable', () => {
    renderWithQueryClient(<CheckoutButton itemCount={0} onCheckedOut={() => {}} />);

    // toBeDisabled(): xanh khi phần tử bị vô hiệu. Đỏ khi cho bấm với giỏ rỗng.
    expect(screen.getByRole('button', { name: /check out/i })).toBeDisabled();
  });

  /**
   * Kiểm tra: người mua cố bấm thì KHÔNG có request thanh toán nào được gửi đi.
   * Lý do: SC-004: tiêu chí là 0 request chứ không phải 0 đơn — 1 request bị server từ chối đã là
   * thất bại của kịch bản này.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T054, US3 (FR-008, SC-004).
   */
  it('sends no checkout request when the shopper tries anyway', async () => {
    const attempts: string[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, ({ request }) => {
        attempts.push(request.url);
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={0} onCheckedOut={() => {}} />);

    // `pointerEventsCheck: 0` so the click is genuinely attempted against the disabled control
    // rather than being refused by the test library before it reaches the component.
    await userEvent.click(screen.getByRole('button', { name: /check out/i }), {
      pointerEventsCheck: 0,
    });

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Không request nào tới server; đỏ
    // khi vẫn gửi.
    expect(attempts).toHaveLength(0);
  });

  /**
   * Kiểm tra: nút thanh toán thao tác được ngay khi giỏ có hàng.
   * Lý do: đối chứng cho 2 test trên: chặn giỏ rỗng không được biến thành chặn vĩnh viễn.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T054, US3 (FR-007, FR-008).
   */
  it('becomes operable once the basket holds something', async () => {
    const attempts: string[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, ({ request }) => {
        attempts.push(request.url);
        return HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        );
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={1} onCheckedOut={() => {}} />);

    const button = screen.getByRole('button', { name: /check out/i });
    // toBeEnabled(): xanh khi phần tử bấm được. Nút bật khi giỏ có hàng.
    expect(button).toBeEnabled();

    await userEvent.click(button);

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Bấm thì gửi đúng 1 request.
    await waitFor(() => expect(attempts).toHaveLength(1));
  });
});
