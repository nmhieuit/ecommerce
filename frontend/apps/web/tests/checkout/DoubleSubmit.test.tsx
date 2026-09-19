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
   * Lý do phải test: FR-016/SC-008: chốt chặn phía client làm request không bao giờ được gửi lần
   * thứ hai; chốt chặn còn lại (server từ chối giỏ đã rỗng) do CheckoutTests của BFF đảm nhiệm.
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
    await waitFor(() => expect(button).toBeDisabled());

    // The second click lands while the first is still in flight — the exact race FR-016 is about.
    await userEvent.click(button, { pointerEventsCheck: 0 });

    release?.();

    await waitFor(() => expect(attempts).toHaveLength(1));
  });

  /**
   * Kiểm tra: hai cú bấm rơi cùng 1 tick, trước khi React kịp render lại, vẫn chỉ phát ra 1
   * request.
   * Lý do phải test: không phải giả định: chạy walkthrough 004 trên stack container từng tạo 2 đơn
   * cách nhau 6 ms cho 1 lần double-click, trong khi dev-server pass vì timing. Dùng `fireEvent`
   * thay `userEvent` có chủ đích vì userEvent chờ giữa các bước nên không tái hiện được race.
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

    await waitFor(() => expect(attempts.length).toBeGreaterThan(0));

    expect(attempts).toHaveLength(1);
  });

  /**
   * Kiểm tra: đơn vừa tạo được báo lên đúng 1 lần.
   * Lý do phải test: chống việc callback thành công bị gọi lặp khiến màn hình xác nhận/điều hướng
   * chạy 2 lần.
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

    await waitFor(() => expect(confirmations).toHaveLength(1));
    expect(confirmations[0]?.id).toBe('aaaaaaaa-0000-4000-8000-000000000001');
  });

  /**
   * Kiểm tra: checkout thất bại thì hiện lỗi rõ ràng, không báo đơn nào và giữ nguyên giỏ.
   * Lý do phải test: US3 kịch bản 4: người mua phải thử lại được mà không mất giỏ.
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

    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(confirmations).toHaveLength(0);
  });

  /**
   * Kiểm tra: đơn có thêm trường client chưa biết vẫn hoàn tất checkout và giữ đủ 3 trường màn hình
   * xác nhận đọc.
   * Lý do phải test: Principle II (tolerant reader), đặt ở đây vì đây là file chạy vòng checkout
   * thật; `Confirmation` chỉ nhận prop cứng và không parse response.
   * Task nguồn: bổ sung sau spec 004 (tolerant reader — Constitution Principle II); không thuộc
   * danh sách task T001-T071 của 004.
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

    await waitFor(() => expect(confirmations).toHaveLength(1));

    // toMatchObject, not toEqual: the unknown field surviving alongside the known ones is the
    // tolerant-reader behaviour, not a defect to assert against.
    expect(confirmations[0]).toMatchObject({
      id: 'aaaaaaaa-0000-4000-8000-000000000002',
      placedAtUtc: '2026-08-16T12:00:00Z',
      total: 12.5,
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
