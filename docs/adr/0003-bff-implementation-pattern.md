# ADR-0003: BFF Implementation Pattern

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle II requires HTTP APIs to be defined by OpenAPI with generated (not hand-written) clients. Constraints say the BFF must "aggregate rather than implement business logic" and serve two client apps (web SPA, mobile-web) for two tenants.

## Decision

Use **ASP.NET Core Minimal APIs**.

## Options Considered

### Option A: Minimal APIs
| Dimension | Assessment |
|---|---|
| Complexity | Low — thin handlers, little ceremony |
| Cost | N/A |
| Scalability | High |
| Team familiarity | High |

**Pros:** Native, built-in OpenAPI document generation (no third-party dependency for the contract itself); low ceremony per endpoint matches "aggregates rather than implements business logic"; fast startup, lean footprint appropriate for a pure aggregation layer.
**Cons:** Less mature filter/model-binding conventions than MVC for very complex validation scenarios; large route counts need deliberate organization (route groups) to stay readable.

### Option B: Controllers (MVC)
| Dimension | Assessment |
|---|---|
| Complexity | Medium — more boilerplate |
| Cost | N/A |
| Scalability | High |
| Team familiarity | High |

**Pros:** Mature, well-understood conventions; battle-tested Swashbuckle/OpenAPI integration; familiar to the broadest pool of .NET developers.
**Cons:** More boilerplate per endpoint than a pure-aggregation layer needs; heavier DI/action-filter surface than the BFF's job requires.

### Option C: GraphQL BFF (e.g., HotChocolate)
| Dimension | Assessment |
|---|---|
| Complexity | High |
| Cost | N/A |
| Scalability | Medium — N+1 resolver risk needs active management |
| Team familiarity | Low |

**Pros:** Clients (web vs. mobile-web) can each request exactly the shape they need, reducing over/under-fetching; resolver model is a natural fit for "stitch multiple downstream services."
**Cons:** Directly conflicts with Principle II — "HTTP APIs are defined by OpenAPI"; GraphQL doesn't produce an OpenAPI contract, so it would need a parallel contract-first discipline and would count as a documented constitutional deviation; adds real complexity (N+1 problem, bespoke caching/rate-limiting) disproportionate to a layer that's supposed to only aggregate.

## Trade-off Analysis

GraphQL is rejected primarily on constitutional grounds, not technical merit — it would require either amending Principle II or accepting an undocumented-until-now deviation. Between Minimal APIs and Controllers, Minimal APIs wins on being the leaner fit for a layer whose entire job is aggregation, not business logic, and its OpenAPI generation is now first-class in .NET.

## Consequences

- BFF endpoints stay intentionally thin; any endpoint that starts accumulating real business logic is a signal it belongs in a service, not the BFF.
- If per-client-shape flexibility becomes a real pain point later (many bespoke endpoints per client), revisit GraphQL as a documented amendment rather than working around Principle II informally.

## Action Items

1. [ ] Establish route-group conventions for BFF endpoint organization
2. [ ] Confirm Minimal API OpenAPI output feeds cleanly into the ADR-0004 codegen tool
