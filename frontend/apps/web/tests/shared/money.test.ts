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

  it('groups thousands so a large total stays readable', () => {
    expect(formatMoney(1234.5)).toBe('$1,234.50');
  });

  /**
   * Two decimal places always, even when the amount has more. A price rendered as $12.499 would
   * not match the total computed from it, and the shopper would be right to distrust both.
   */
  it('rounds to two decimal places', () => {
    expect(formatMoney(12.499)).toBe('$12.50');
    expect(formatMoney(12.494)).toBe('$12.49');
  });

  /**
   * Nothing in this feature produces a negative amount, so this pins what happens if something
   * ever does — a visible minus rather than a silently dropped sign.
   */
  it('keeps the sign on a negative amount', () => {
    expect(formatMoney(-5)).toBe('-$5.00');
  });

  /**
   * The contract types a decimal as number-or-string (.NET's OpenAPI generator emits both so a
   * producer may preserve precision), and the generated client passes that union straight through.
   * Handling only the number half would leave a screen showing "NaN" the first time a producer
   * exercised the other one.
   */
  it('accepts the string form the contract also permits', () => {
    expect(formatMoney('12.50')).toBe('$12.50');
    expect(formatMoney('48')).toBe('$48.00');
  });

  it('refuses a value that is not an amount at all', () => {
    expect(() => formatMoney('not-a-price')).toThrow(TypeError);
  });
});
