import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { CheckoutButton } from '@/features/checkout/CheckoutButton';
import type { PlacedOrder } from '@/features/checkout/PlacedOrder';
import { server } from '../msw/server';

/**
 * Spec FR-016 and SC-008: "triggering checkout more than once for the same basket MUST NOT create
 * more than one order."
 *
 * Two guards stand behind that promise. This suite covers the first — the control is unavailable
 * while a checkout is in flight. The second, the backend refusing a checkout of an already-emptied
 * basket, is covered by the BFF's CheckoutTests. Either alone would be thin; the client-side one is
 * what stops the request being sent twice in the first place.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('CheckoutButton double submission', () => {
  /**
   * Kiểm tra: bấm thanh toán 2 lần liên tiếp nhanh chỉ phát ra đúng 1 request.
   * Lý do: FR-016/SC-008: chốt chặn phía client làm request không bao giờ được gửi lần thứ hai;
   * chốt chặn còn lại (server từ chối giỏ đã rỗng) do CheckoutTests của BFF đảm nhiệm.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T055, US3 (FR-016, SC-008).
   */
  it('issues exactly one checkout request when clicked twice in rapid succession', async () => {
    const attempts: string[] = [];
    let release: (() => void) | undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, async ({ request }) => {
        attempts.push(request.url);
        await held;
        return HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        );
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={1} onCheckedOut={() => {}} />);

    const button = screen.getByRole('button', { name: /check out/i });

    await userEvent.click(button);
    // toBeDisabled(): xanh khi phần tử bị vô hiệu. Sau lần bấm đầu, nút phải bị vô hiệu.
    await waitFor(() => expect(button).toBeDisabled());

    // The second click lands while the first is still in flight — the exact race FR-016 is about.
    await userEvent.click(button, { pointerEventsCheck: 0 });

    release?.();

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. ToHaveLength(1) đạt khi đúng 1
    // request tới server; đỏ khi 2 (bấm đúp tạo đơn kép).
    await waitFor(() => expect(attempts).toHaveLength(1));
  });

  /**
   * Kiểm tra: hai cú bấm rơi cùng 1 tick, trước khi React kịp render lại, vẫn chỉ phát ra 1
   * request.
   * Lý do: không phải giả định: chạy walkthrough 004 trên stack container từng tạo 2 đơn cách nhau
   * 6 ms cho 1 lần double-click, trong khi dev-server pass vì timing. Dùng `fireEvent` thay
   * `userEvent` có chủ đích vì userEvent chờ giữa các bước nên không tái hiện được race.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T055, US3 (FR-016) — bổ sung sau lỗi thật phát
   * hiện lúc chạy walkthrough.
   */
  it('issues one request even when both clicks land before React re-renders', async () => {
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

    fireEvent.click(button);
    fireEvent.click(button);

    // toBeGreaterThan(n): xanh khi giá trị lớn hơn n. ToBeGreaterThan đạt khi có ít nhất 1 request
    // (chờ request tới nơi).
    await waitFor(() => expect(attempts.length).toBeGreaterThan(0));

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Tổng cộng đúng 1 request; đỏ khi
    // cả 2 click lọt qua (cần chốt bằng ref chứ không chỉ bằng state).
    expect(attempts).toHaveLength(1);
  });

  /**
   * Kiểm tra: đơn vừa tạo được báo lên đúng 1 lần.
   * Lý do: chống việc callback thành công bị gọi lặp khiến màn hình xác nhận/điều hướng chạy 2 lần.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T055, US3 (FR-016).
   */
  it('reports the created order exactly once', async () => {
    const confirmations: { id: string }[] = [];

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, () =>
        HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        ),
      ),
    );

    renderWithQueryClient(
      <CheckoutButton itemCount={1} onCheckedOut={(order) => confirmations.push(order)} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /check out/i }));

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Callback được gọi đúng 1 lần.
    await waitFor(() => expect(confirmations).toHaveLength(1));
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. ToBe (===) id đơn được báo phải đúng id
    // server trả.
    expect(confirmations[0]?.id).toBe('aaaaaaaa-0000-4000-8000-000000000001');
  });

  /**
   * Kiểm tra: checkout thất bại thì hiện lỗi rõ ràng, không báo đơn nào và giữ nguyên giỏ.
   * Lý do: US3 kịch bản 4: người mua phải thử lại được mà không mất giỏ.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T055, US3 (FR-012, US3-KB4).
   */
  it('shows an error and reports no order when checkout fails', async () => {
    const confirmations: unknown[] = [];

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(
      <CheckoutButton itemCount={1} onCheckedOut={(order) => confirmations.push(order)} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /check out/i }));

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Hiện cảnh báo lỗi.
    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument();
    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Không đơn nào được báo thành
    // công.
    expect(confirmations).toHaveLength(0);
  });

  /**
   * Kiểm tra: phản hồi checkout có thêm trường lạ (vd. ngày giao dự kiến) vẫn hoàn tất checkout và
   * giữ đủ 3 trường (`id`, `placedAtUtc`, `total`) mà màn hình xác nhận đọc.
   * Lý do: FR-006/SC-004 (tolerant reader) đặt ở file này vì đây là nơi chạy vòng checkout thật;
   * `Confirmation` chỉ nhận prop cứng và không parse phản hồi BFF nên không thể kiểm được điều này.
   * Task nguồn: spec 007 (hợp đồng OpenAPI cho BFF) — T012, US3 (FR-006, SC-004).
   */
  it('completes checkout when the order carries a field the client does not know about', async () => {
    const confirmations: PlacedOrder[] = [];

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, () =>
        HttpResponse.json(
          {
            id: 'aaaaaaaa-0000-4000-8000-000000000002',
            placedAtUtc: '2026-08-16T12:00:00Z',
            total: 12.5,
            estimatedDeliveryUtc: '2026-08-20T12:00:00Z',
          },
          { status: 201 },
        ),
      ),
    );

    renderWithQueryClient(
      <CheckoutButton itemCount={1} onCheckedOut={(order) => confirmations.push(order)} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /check out/i }));

    // toHaveLength(n): xanh khi mảng/danh sách có đúng n phần tử. Checkout vẫn hoàn tất.
    await waitFor(() => expect(confirmations).toHaveLength(1));

    // toMatchObject, not toEqual: the unknown field surviving alongside the known ones is the
    // tolerant-reader behaviour, not a defect to assert against.
    // toMatchObject(mẫu): xanh khi object thực tế chứa các trường trong mẫu (trường thừa được
    // phép). ToMatchObject đạt khi object thực tế chứa (ít nhất) các trường và giá trị này;
    expect(confirmations[0]).toMatchObject({
      id: 'aaaaaaaa-0000-4000-8000-000000000002',
      placedAtUtc: '2026-08-16T12:00:00Z',
      total: 12.5,
    });
    // not.toBeInTheDocument(): xanh khi phần tử KHÔNG có trong DOM (queryBy trả null nếu không
    // thấy). .not đảo điều kiện: đạt khi KHÔNG có phần tử role="alert". Đỏ khi có thông báo lỗi.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
