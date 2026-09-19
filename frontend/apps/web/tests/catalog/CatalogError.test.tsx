import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ErrorState } from '@/shared/ErrorState';

/**
 * Spec FR-012 and US1 acceptance scenario 3: when the backend is unavailable or slow, the shopper
 * gets "a clear, human-readable error ... and the page remains usable (the shopper can retry)
 * rather than hanging or going blank".
 */
describe('catalog error state', () => {
  /**
   * Kiểm tra: lỗi hiển thị 1 thông điệp đọc được và được thông báo tới trình đọc màn hình
   * (`role="alert"`).
   * Lý do phải test: FR-012/US1 kịch bản 3: người mua phải hiểu chuyện gì đã xảy ra.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T026, US1 (FR-012).
   */
  it('shows a readable message and announces it', () => {
    render(<ErrorState message="We could not load the products." />);

    const alert = screen.getByRole('alert');

    expect(alert).toHaveTextContent('We could not load the products.');
  });

  /**
   * Kiểm tra: trạng thái lỗi có nút thử lại thao tác được.
   * Lý do phải test: FR-012 yêu cầu người mua có thể thử lại thay vì kẹt trong màn hình lỗi.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T026, US1 (FR-012).
   */
  it('offers a retry the shopper can operate', async () => {
    const onRetry = vi.fn();
    render(<ErrorState message="We could not load the products." onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: /try again/i }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  /**
   * Kiểm tra: chỉ dùng bàn phím vẫn tới được và kích hoạt nút thử lại.
   * Lý do phải test: SC-009/FR-017: cả luồng làm được bằng bàn phím nên đường phục hồi sau lỗi
   * không thể chỉ dành cho con trỏ.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T026, US1 (FR-017, SC-009).
   */
  it('reaches and fires retry by keyboard alone', async () => {
    const onRetry = vi.fn();
    render(<ErrorState message="We could not load the products." onRetry={onRetry} />);

    await userEvent.tab();
    expect(screen.getByRole('button', { name: /try again/i })).toHaveFocus();

    await userEvent.keyboard('{Enter}');
    expect(onRetry).toHaveBeenCalledOnce();
  });

  /**
   * Kiểm tra: không có gì để thử lại thì không hiện nút thử lại.
   * Lý do phải test: một nút không làm gì còn tệ hơn không có nút.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T026, US1 (FR-012).
   */
  it('omits the retry control when no retry is possible', () => {
    render(<ErrorState message="We could not load the products." />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
