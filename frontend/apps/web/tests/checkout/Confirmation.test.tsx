import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { Confirmation } from '@/features/checkout/Confirmation';

/**
 * The screen offers a way back to the catalog, so it needs a router in scope. A memory router keeps
 * that a detail of the harness rather than something the component has to be reshaped around.
 */
function renderInRouter(ui: ReactNode) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

/**
 * Spec FR-009: the confirmation shows "the created order's generated identifier as-is — a reference
 * the shopper can read off the screen and quote — together with the order's total."
 */
describe('Confirmation', () => {
  const order = {
    id: 'aaaaaaaa-0000-4000-8000-000000000001',
    placedAtUtc: '2026-08-16T12:00:00Z',
    total: 59.25,
  };

  /**
   * Kiểm tra: màn hình xác nhận hiển thị mã đơn hàng nguyên văn.
   * Lý do phải test: FR-009/SC-005: so mã người mua thấy với đơn trong backend; mã bị rút gọn không
   * còn duy nhất một cách đáng tin.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T053, US3 (FR-009, SC-005).
   */
  it('shows the order identifier verbatim', () => {
    renderInRouter(<Confirmation order={order} />);

    // Verbatim, not shortened: SC-005 compares what the shopper sees against the order in the
    // backend, and a truncated reference is no longer reliably unique (Clarifications 2026-08-16).
    expect(screen.getByText(order.id)).toBeInTheDocument();
  });

  /**
   * Kiểm tra: hiển thị tổng đơn bằng USD với 2 chữ số thập phân.
   * Lý do phải test: FR-009 và FR-024: tổng tiền trên màn hình xác nhận phải là số tiền người mua
   * đọc được.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T053, US3 (FR-009, FR-024).
   */
  it('shows the order total in the single Phase 1 currency', () => {
    renderInRouter(<Confirmation order={order} />);

    expect(screen.getByText('$59.25')).toBeInTheDocument();
  });

  /**
   * Kiểm tra: màn hình nói rõ đơn đã được đặt.
   * Lý do phải test: người mua cần lời xác nhận rõ ràng ngoài con số, đúng tên gọi "màn hình xác
   * nhận" của US3.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T053, US3 (FR-009).
   */
  it('tells the shopper their order was placed', () => {
    renderInRouter(<Confirmation order={order} />);

    expect(screen.getByRole('heading', { name: /thank you|order placed|confirmed/i })).toBeInTheDocument();
  });

  /**
   * Kiểm tra: vào thẳng màn hình xác nhận mà chưa thanh toán thì hiện trạng thái "không có gì để
   * hiển thị".
   * Lý do phải test: Edge Cases của spec: không được hiện màn hình hỏng hay bịa ra 1 mã đơn — tuyệt
   * đối không có mã tự chế.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T053, US3 (FR-009).
   */
  it('shows a nothing-to-show state when there is no order', () => {
    renderInRouter(<Confirmation order={undefined} />);

    expect(screen.getByText(/no recent order/i)).toBeInTheDocument();

    // Emphatically no invented identifier.
    expect(screen.queryByText(order.id)).not.toBeInTheDocument();
  });
});
