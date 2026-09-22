import { describe, expect, it } from 'vitest';
import { formatMoney } from '@/shared/money';

/**
 * Spec FR-024: every amount the shopper sees is US dollars, with the symbol and two decimal
 * places. Phase 1 has exactly one currency, so the amounts the backend returns are bare numbers —
 * this is the single place they become something a person reads.
 */
describe('formatMoney', () => {
  /**
   * Kiểm tra: các số 12.5, 48, 34.25 và 0 được định dạng thành "$12.50", "$48.00", "$34.25",
   * "$0.00" (`it.each` chạy 4 lần, mỗi lần 1 cặp).
   * Lý do: FR-024: người mua chỉ thấy đô la với 2 chữ số thập phân; đây là chỗ duy nhất biến số
   * trần từ backend thành chuỗi hiển thị.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it.each([
    [12.5, '$12.50'],
    [48, '$48.00'],
    [34.25, '$34.25'],
    [0, '$0.00'],
  ])('formats %d as %s', (amount, expected) => {
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Đỏ khi thiếu ký hiệu $ hoặc thiếu số 0 thập phân.
    expect(formatMoney(amount)).toBe(expected);
  });

  /**
   * Kiểm tra: 1234.5 được định dạng "$1,234.50".
   * Lý do: FR-024: tổng lớn vẫn phải đọc được.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('groups thousands so a large total stays readable', () => {
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Đỏ khi thiếu dấu phân cách nghìn hoặc thiếu
    // chữ số thập phân.
    expect(formatMoney(1234.5)).toBe('$1,234.50');
  });

  /**
   * Kiểm tra: luôn làm tròn 2 chữ số thập phân.
   * Lý do: giá hiển thị $12.499 sẽ không khớp tổng tính từ nó, và người mua có lý khi nghi ngờ cả
   * hai.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('rounds to two decimal places', () => {
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Làm tròn lên.
    expect(formatMoney(12.499)).toBe('$12.50');
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Làm tròn xuống; đỏ khi cắt cụt thay vì làm
    // tròn.
    expect(formatMoney(12.494)).toBe('$12.49');
  });

  /**
   * Kiểm tra: số tiền âm giữ nguyên dấu trừ.
   * Lý do: không tính năng nào sinh số âm; ghim hành vi nếu sau này có — dấu trừ hiển thị rõ chứ
   * không bị lặng lẽ mất.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('keeps the sign on a negative amount', () => {
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác. Dấu trừ đứng trước ký hiệu tiền; đỏ khi mất
    // dấu hoặc đặt sau ký hiệu.
    expect(formatMoney(-5)).toBe('-$5.00');
  });

  /**
   * Kiểm tra: nhận cả dạng chuỗi ("12.50") mà hợp đồng cho phép cho kiểu decimal.
   * Lý do: .NET sinh OpenAPI với union number-hoặc-string để giữ độ chính xác; chỉ xử lý dạng số sẽ
   * làm màn hình hiện "NaN" lần đầu producer dùng dạng chuỗi.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('accepts the string form the contract also permits', () => {
    // toBe(kỳ vọng): so bằng chặt (===), đỏ khi khác.
    expect(formatMoney('12.50')).toBe('$12.50');
    expect(formatMoney('48')).toBe('$48.00');
  });

  /**
   * Kiểm tra: giá trị không phải số tiền ("not-a-price") làm hàm ném `TypeError`.
   * Lý do: thất bại to tiếng thay vì hiển thị 1 số tiền sai cho người mua.
   * Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T022, nền tảng (FR-024).
   */
  it('refuses a value that is not an amount at all', () => {
    // toThrow(loại lỗi): xanh khi hàm ném lỗi đúng loại. Đỏ khi im lặng trả "$NaN".
    expect(() => formatMoney('not-a-price')).toThrow(TypeError);
  });
});
