# ADR-0011: Phase 1 Checkout Orchestration

**Status:** Accepted
**Date:** 2026-08-16
**Deciders:** Platform maintainers
**Supersedes:** nothing. **Superseded by:** nothing yet — expected to be replaced when SCRUM-18 and SCRUM-31 land.

## Context

[SCRUM-14](https://nmhieuit.atlassian.net/browse/SCRUM-14) closes the walking skeleton: a shopper turns their basket into an order and is shown a confirmation. Checkout therefore spans two services — an order must be created in `orders`, and the basket must be emptied in `baskets`.

Constitution Principle IV is explicit about this shape: "Multi-service workflows MUST be modelled as sagas with explicit compensation, never as distributed transactions." It also requires the transactional outbox pattern for every publisher.

The repository has none of the machinery that rule presupposes. There is no MassTransit package reference, no RabbitMQ container in `docker-compose.deps.yml`, no outbox table in any migration, and no event schema anywhere. The roadmap places event schemas at [SCRUM-18](https://nmhieuit.atlassian.net/browse/SCRUM-18) (Phase 2) and outbox verification at [SCRUM-31](https://nmhieuit.atlassian.net/browse/SCRUM-31) (Phase 4).

## Decision

Checkout is a **synchronous two-step orchestrated by the BFF**: read the caller's basket, place an order for its lines, then clear the basket. The order is created **before** the basket is cleared.

This is a **documented, time-bound deviation from Principle IV**, closed by SCRUM-18 and SCRUM-31.

## Options Considered

### Option A: Synchronous BFF orchestration, order-first *(chosen)*

| Dimension | Assessment |
|---|---|
| Complexity | Low — three existing typed clients, no new infrastructure |
| Cost | Free |
| Scalability | Adequate for one tenant and one shopper |
| Team familiarity | High |

**Pros:** Ships the walking skeleton now, which is the entire point of Phase 1. Uses the resilience pipeline the BFF already applies to every outbound call, so no unbounded wait is introduced. Order-first ordering means a failure between the steps leaves the shopper with a *real order they can be shown* — recoverable — rather than an emptied basket and nothing to show for it.

**Cons:** No compensation. If the clear fails after the order is created, the basket keeps its items and a second checkout would create a second order. The BFF holds a workflow, which sits uncomfortably beside its "aggregation only" remit.

### Option B: Stand up RabbitMQ + MassTransit and publish `BasketCheckedOut`

| Dimension | Assessment |
|---|---|
| Complexity | High — new broker, new package, outbox tables, consumers, idempotency |
| Cost | Free (OSS), but real operational surface |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Constitutionally correct. Compensation and idempotency become properties of the design rather than things to apologise for.

**Cons:** Multiplies this feature's size several times over, and duplicates work SCRUM-18 and SCRUM-31 exist to do properly. Correct destination, wrong phase.

### Option C: Clear the basket first, then place the order

**Pros:** Symmetrical to Option A; no extra machinery.

**Cons:** Strictly worse failure mode. A failure between the steps loses the shopper's basket *and* leaves them without an order — the one outcome that is unrecoverable from the shopper's side.

## Trade-off Analysis

The deciding factor is that Option B's infrastructure is already assigned to two later stories with their own acceptance criteria. Building a partial version here would either be torn out when those land, or quietly become the version that ships — neither is a good outcome.

Between A and C, the argument is entirely about which failure a shopper can recover from. An order that exists is a thing they can be shown, quote, and be helped with. A basket that was emptied for an order that was never created is invisible to them and irrecoverable.

## Consequences

- **Residual risk, accepted:** a clear that fails after the order is created leaves a non-empty basket. FR-016's "no second order" guarantee therefore rests on two other guards, not on compensation:
  1. the storefront disables the checkout control while a checkout is in flight, so the second click never becomes a second request;
  2. the baskets service answers `409` to a clear of an already-empty basket, so a repeat checkout is refused.
- The failure is logged, not silent. `CheckoutEndpoints.LogBasketNotCleared` records the order identifier whenever the basket reports nothing to clear, so the gap is greppable in production rather than invisible.
- The BFF performs no arithmetic in this flow. The basket's lines come from `baskets` and the order's total is computed by `orders`, so "aggregation only" still holds for the money even though the workflow does not.

## Action Items

1. [ ] SCRUM-18: define the `BasketCheckedOut` / `OrderPlaced` event schemas in the shared contracts location
2. [ ] SCRUM-31: replace this orchestration with an outbox-backed saga and verify it by killing the process mid-publish
3. [ ] On completion of 1 and 2, mark this ADR superseded and remove the deviation from `specs/004-minimal-shopping-spa/plan.md` Complexity Tracking
