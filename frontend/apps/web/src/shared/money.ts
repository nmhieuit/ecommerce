/**
 * Spec FR-024: product prices, line prices, basket totals, and order totals are all shown in US
 * dollars with the symbol and two decimal places.
 *
 * The backend sends bare numbers — Phase 1 has exactly one currency, so no currency travels with
 * an amount (data-model.md — Product). This module is the only place that fact turns into text,
 * which is what keeps a second, differently-rounded formatter from appearing on a third screen.
 */

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Formats an amount for display. Always two decimal places, always the dollar symbol. */
export function formatMoney(amount: number): string {
  return formatter.format(amount);
}
