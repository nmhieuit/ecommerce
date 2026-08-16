/**
 * The order a completed checkout produced, as the confirmation screen needs it.
 *
 * Declared here rather than imported from the generated client so the confirmation screen and the
 * checkout control agree on one shape without either depending on the other. The fields mirror
 * `OrderConfirmationResponse` in contracts/bff-openapi.yaml.
 */
export interface PlacedOrder {
  /** Shown verbatim — there is no separate, friendlier order number (spec FR-009). */
  readonly id: string;
  readonly placedAtUtc: string;
  /** The contract permits a decimal as a number or a string; `formatMoney` accepts both. */
  readonly total: number | string;
}
