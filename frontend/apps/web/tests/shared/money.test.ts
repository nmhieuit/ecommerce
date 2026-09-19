import { describe, expect, it } from 'vitest';
import { formatMoney } from '@/shared/money';

/**
 * Spec FR-024: every amount the shopper sees is US dollars, with the symbol and two decimal
 * places. Phase 1 has exactly one currency, so the amounts the backend returns are bare numbers —
 * this is the single place they become something a person reads.
 */
describe('formatMoney', () => {
  it.each([
    [12.5, '$12.50'],
    [48, '$48.00'],
    [34.25, '$34.25'],
    [0, '$0.00'],
  ])('formats %d as %s', (amount, expected) => {
    expect(formatMoney(amount)).toBe(expected);
  });

  /**
   * Kiểm tra: 1234.5 được định dạng "$1,234.50".
   * Lý do phải test: FR-024: tổng lớn vẫn phải đọc được.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('groups thousands so a large total stays readable', () => {
    expect(formatMoney(1234.5)).toBe('$1,234.50');
  });

  /**
   * Kiểm tra: luôn làm tròn 2 chữ số thập phân.
   * Lý do phải test: giá hiển thị $12.499 sẽ không khớp tổng tính từ nó, và người mua có lý khi
   * nghi ngờ cả hai.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('rounds to two decimal places', () => {
    expect(formatMoney(12.499)).toBe('$12.50');
    expect(formatMoney(12.494)).toBe('$12.49');
  });

  /**
   * Kiểm tra: số tiền âm giữ nguyên dấu trừ.
   * Lý do phải test: không tính năng nào sinh số âm; ghim hành vi nếu sau này có — dấu trừ hiển thị
   * rõ chứ không bị lặng lẽ mất.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('keeps the sign on a negative amount', () => {
    expect(formatMoney(-5)).toBe('-$5.00');
  });

  /**
   * Kiểm tra: nhận cả dạng chuỗi ("12.50") mà hợp đồng cho phép cho kiểu decimal.
   * Lý do phải test: .NET sinh OpenAPI với union number-hoặc-string để giữ độ chính xác; chỉ xử lý
   * dạng số sẽ làm màn hình hiện "NaN" lần đầu producer dùng dạng chuỗi.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('accepts the string form the contract also permits', () => {
    expect(formatMoney('12.50')).toBe('$12.50');
    expect(formatMoney('48')).toBe('$48.00');
  });

  /**
   * Kiểm tra: giá trị không phải số tiền ("not-a-price") làm hàm ném `TypeError`.
   * Lý do phải test: thất bại to tiếng thay vì hiển thị 1 số tiền sai cho người mua.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('refuses a value that is not an amount at all', () => {
    expect(() => formatMoney('not-a-price')).toThrow(TypeError);
  });
});
