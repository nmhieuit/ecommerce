# ADR-0006: Consumer-Driven Contract Testing Tool

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle III requires consumer-driven contract tests for every HTTP and event boundary, and that "breaking a published contract MUST fail the producer's build" — meaning contract verification has to run in the producer's own CI pipeline, aware of every consumer that depends on it.

## Decision

Use **Pact**, with a self-hosted Pact Broker, and Pact's message-pact feature (via a thin MassTransit adapter) for event boundaries.

## Options Considered

### Option A: Pact
| Dimension | Assessment |
|---|---|
| Complexity | Medium — Broker to run, message-pact needs an adapter |
| Cost | Free (OSS); self-hosted Broker is a small extra service |
| Scalability | High |
| Team familiarity | Low initially, high community support |

**Pros:** "Producer's build fails on a broken contract" is Pact's exact core workflow (`can-i-deploy`), including automatic tracking of which consumers depend on which producer — building that dependency graph by hand would be a bigger undertaking than adopting Pact; mature .NET SDK; HTTP-pact support is first-class.
**Cons:** Message/event-pact support is less mature than HTTP-pact and will need a MassTransit-specific adapter; the Pact Broker is one more stateful service to operate (lightweight compared to a schema registry, but still infrastructure).

### Option B: Custom contract-test harness
| Dimension | Assessment |
|---|---|
| Complexity | High to build, low to run once built |
| Cost | Engineering time instead of new infrastructure |
| Scalability | N/A |
| Team familiarity | High (plain XUnit + JSON Schema validation) |

**Pros:** Zero new infrastructure; reuses the exact JSON Schema chosen in ADR-0005 for both HTTP and event contracts, so there's one format and one validation approach everywhere; no new DSL to learn beyond what the team already uses.
**Cons:** The "producer's build fails when it breaks a consumer" guarantee requires building and maintaining a cross-service dependency map by hand — exactly the hardest part of what Pact already solves; no ready-made dashboard of who depends on what; more up-front engineering, and a less industry-standard onboarding story for new hires.

## Trade-off Analysis

The requirement isn't just "validate a schema" — it's "know which consumers exist and fail the right build when one of them would break," which is a distributed dependency-tracking problem. Pact solves that problem directly; a custom harness would need to reinvent it. The Broker's operational cost is judged smaller than the engineering cost of building consumer-tracking from scratch.

## Consequences

- A Pact Broker must be deployed and kept available — CI depends on reaching it during PR builds.
- Event-boundary contract testing carries integration risk (MassTransit adapter) that should be piloted on one service pair before rolling out platform-wide.

## Action Items

1. [ ] Stand up a self-hosted Pact Broker
2. [ ] Pilot HTTP-pact on one BFF↔service boundary before rolling out to all six services
3. [ ] Build and validate the MassTransit message-pact adapter on one event boundary
