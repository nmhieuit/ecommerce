/**
 * The one backend address the storefront knows.
 *
 * Spec FR-014 and SC-010: every request goes to the single backend surface — the gateway — and
 * nothing addresses the BFF or a domain service directly. Addressing the BFF would also skip the
 * only hop that resolves the tenant and the caller, so those requests would fail at the services'
 * own gates anyway (research.md Decision 11).
 *
 * There is deliberately no per-service configuration here. A map of service URLs is the shape that
 * lets a screen quietly acquire its own downstream dependency.
 */

const DEFAULT_GATEWAY_ORIGIN = 'http://localhost:5300';

export function resolveGatewayOrigin(): string {
  const configured = import.meta.env.VITE_GATEWAY_ORIGIN;

  return configured !== undefined && configured.length > 0
    ? configured
    : DEFAULT_GATEWAY_ORIGIN;
}
