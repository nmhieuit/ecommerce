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

/**
 * Formats an amount for display. Always two decimal places, always the dollar symbol.
 *
 * Accepts a string as well as a number because that is what the contract allows: .NET's OpenAPI
 * generator describes a `decimal` as `["number", "string"]` so a producer may preserve precision
 * beyond a JSON double, and the generated client types it that way. The BFF sends numbers today,
 * but a client that would break if it ever sent `"12.50"` is a client that trusts the
 * implementation rather than the contract.
 */
export function formatMoney(amount: number | string): string {
  const value = typeof amount === 'number' ? amount : Number(amount);

  if (Number.isNaN(value)) {
    throw new TypeError(`Cannot format ${JSON.stringify(amount)} as an amount of money.`);
  }

  return formatter.format(value);
}
