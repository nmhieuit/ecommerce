import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmptyCatalog } from '@/features/catalog/CatalogStates';

/**
 * Spec FR-002 and US1 acceptance scenario 2: an empty catalog gets an explicit state — "not an
 * empty page, a spinner that never resolves, or an error". Zero products is a legitimate answer,
 * and the shopper is entitled to be told so.
 */
describe('EmptyCatalog', () => {
  /**
   * Kiểm tra: trạng thái catalog rỗng báo cho người mua biết chưa có gì để mua.
   * Lý do: FR-002/US1 kịch bản 2: phải là trạng thái tường minh, không phải trang trống, vòng quay
   * không dứt hay lỗi.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T025, US1 (FR-002).
   */
  it('tells the shopper there is nothing to buy yet', () => {
    render(<EmptyCatalog />);

    // toBeInTheDocument(): xanh khi phần tử có trong DOM. Có dòng "No products available" (không
    // phân biệt hoa thường); getBy ném lỗi ngay nếu thiếu.
    expect(screen.getByText(/no products available/i)).toBeInTheDocument();
  });

  /**
   * Kiểm tra: trạng thái rỗng không tự trình bày như 1 lỗi (không dùng `role="alert"`).
   * Lý do: catalog rỗng không phải thất bại; `role="alert"` sẽ làm trình đọc màn hình ngắt người
   * dùng để báo điều bình thường.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T025, US1 (FR-002).
   */
  it('does not present itself as an error', () => {
    render(<EmptyCatalog />);

    // not.toBeInTheDocument(): xanh khi phần tử KHÔNG có trong DOM (queryBy trả null nếu không
    // thấy). .not đảo điều kiện: đạt khi KHÔNG có phần tử role="alert". Đỏ khi có thông báo lỗi.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
