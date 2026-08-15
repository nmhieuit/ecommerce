# Implementation Plan: Gateway → BFF Routing for Products, Baskets, Orders, and Parties

**Branch**: `002-gateway-bff-routing` | **Date**: 2026-08-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-gateway-bff-routing/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Stand up two new stateless edge services — a YARP-based API gateway and an ASP.NET Core Minimal API BFF — and wire them so every client request flows gateway → BFF → (products | baskets | orders | parties). The gateway routes all API traffic to the BFF without exposing service topology; the BFF proxies/aggregates calls to the four domain services, applies explicit per-call timeouts and resilience policies, and returns a clear structured error when a downstream service is unavailable instead of hanging. The BFF contains no business logic beyond aggregation and response shaping (ADR-0002, ADR-0003).

## Technical Context

**Language/Version**: C# / .NET 10 (matches every other service in the fleet; constitution Technology Constraints)

**Primary Dependencies**: YARP (reverse proxy, gateway — ADR-0002) · ASP.NET Core Minimal APIs (BFF — ADR-0003) · `Microsoft.Extensions.Http` typed clients · `Microsoft.Extensions.Resilience` (timeout/retry/circuit-breaker, constitution Principle VIII) · `Microsoft.AspNetCore.OpenApi` (native OpenAPI generation, ADR-0004 feeds from this) · shared `ServiceDefaults` component (telemetry, correlation ID, health checks — constitution Principle VII, reused as-is from `shared/ServiceDefaults`)

**Storage**: N/A — the gateway and BFF are stateless proxy/aggregation layers and own no database (constitution Principle I: only services that own business invariants get persistence)

**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`), matching the products/baskets/orders/parties services. BFF integration tests host real in-process instances of the Products/Baskets/Orders/Parties `Program` classes as the downstream (not a mocking library), consistent with constitution Principle III's "no hand-rolled fakes for infrastructure" — the BFF's dependency here is another of our own HTTP services, not a database, so "real" means the real service, not Testcontainers.

**Target Platform**: Linux containers on Kubernetes (existing platform, provisioned via Ansible)

**Project Type**: web-service — two new service shells (`gateway`, `bff`) added to the existing multi-service repo, alongside `parties`/`products`/`baskets`/`orders`

**Performance Goals**: Client-facing BFF read p95 ≤ 300 ms / p99 ≤ 800 ms (constitution Principle VIII default for client-facing BFF reads); BFF → downstream-service calls p95 ≤ 150 ms / p99 ≤ 500 ms each (constitution's internal-service-API default)

**Constraints**: Every outbound call the gateway or BFF makes MUST declare an explicit timeout and be wrapped in retry/circuit-breaker policy — unbounded waits MUST NOT exist (constitution Principle VIII; spec FR-006). The BFF MUST contain no business logic beyond aggregation/shaping (spec FR-005). The SPA MUST call only the gateway/BFF, never a domain service directly (spec FR-002) — enforced by contract/convention in this feature since no SPA yet exists (SCRUM-14 builds it next).

**Scale/Scope**: Routes and aggregates for all four Phase-1 domain services — products, baskets, orders, and parties. This is broader than the source ticket's literal "three services" wording; scope was expanded during planning per direct stakeholder instruction (spec Assumptions) since parties was scaffolded alongside the other three under the identical shell convention.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | Gateway and BFF own no data and read no service's database directly; each is independently deployable. | PASS |
| II. Contract-First Integration | BFF endpoints get an OpenAPI contract generated from Minimal API definitions (ADR-0004 pipeline) before the frontend (SCRUM-14) consumes them; this feature produces `contracts/` in Phase 1 ahead of implementation. | PASS |
| III. Test-First Development | Plan requires failing integration tests (real downstream `Program` instances, no mocks) before proxy/aggregation code — enforced at task-generation/implementation time, not a plan-time gate. | PASS (deferred to tasks) |
| IV. Event-Driven by Default | N/A — gateway→BFF→service is synchronous HTTP by definition; the constitution's own Technology Constraints section fixes this edge chain as synchronous, so this is not a Principle IV deviation requiring justification. | PASS |
| V. Tenant Isolation Is a Security Boundary | Full tenant-context resolution is SCRUM-12's scope (stub identity, hardcoded single tenant), not yet implemented in this repo. This feature's gateway/BFF will forward a tenant/correlation header end-to-end if present but does not itself resolve or fabricate tenant identity. | DEVIATION — see Complexity Tracking |
| VI. Secure by Default | No identity server exists yet (ADR-0001 decided the product, SCRUM-23/Phase 3 implements it). Gateway/BFF endpoints cannot enforce real token validation or authorization policy in this feature. | DEVIATION — see Complexity Tracking |
| VII. Observable by Default | Both new services call `builder.AddServiceDefaults()` / `app.UseServiceDefaults()` from the existing shared component — no new observability wiring needed. | PASS |
| VIII. Performance and Resilience Budgets | Every downstream call gets an explicit timeout + resilience pipeline; both new services declare SLOs in a `service-manifest.yaml`, matching the products/baskets/orders convention. | PASS |
| IX. Frontend Discipline | No frontend code in this feature; the constraint ("frontends talk to the BFF only") is upheld structurally by only exposing gateway/BFF routes, with the actual SPA arriving in SCRUM-14. | PASS |
| X. Toggle-Gated, Reversible Delivery | New routes are additive (no existing traffic to migrate); rollback is redeploying without the new services. A feature toggle is not needed for a net-new routing path with no prior behavior to preserve. | PASS |

Two Principle V/VI deviations are carried forward from Phase-1 sequencing (see roadmap: SCRUM-12 precedes but is not yet built; SCRUM-23/Phase 3 implements real identity). Documented below per Governance.

## Project Structure

### Documentation (this feature)

```text
specs/002-gateway-bff-routing/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
services/
├── gateway/
│   ├── src/
│   │   └── Gateway.Api/
│   │       ├── Gateway.Api.csproj
│   │       ├── Program.cs
│   │       ├── appsettings.json          # YARP ReverseProxy routes/clusters (→ bff only)
│   │       ├── service-manifest.yaml
│   │       └── Dockerfile
│   └── tests/
│       ├── Gateway.Api.IntegrationTests/
│       └── Gateway.Api.UnitTests/
│
├── bff/
│   ├── src/
│   │   └── Bff.Api/
│   │       ├── Bff.Api.csproj
│   │       ├── Program.cs
│   │       ├── Features/
│   │       │   ├── Products/             # GET /bff/products (proxy + shape)
│   │       │   ├── Baskets/               # GET/POST /bff/baskets/{id} (proxy + shape)
│   │       │   ├── Orders/                # GET/POST /bff/orders/{id} (proxy + shape)
│   │       │   └── Parties/               # GET /bff/parties/{id} (proxy + shape)
│   │       ├── DownstreamClients/         # typed HttpClients: Products, Baskets, Orders, Parties
│   │       ├── appsettings.json           # Services:ProductsApi:BaseUrl, etc.
│   │       ├── service-manifest.yaml
│   │       └── Dockerfile
│   └── tests/
│       ├── Bff.Api.IntegrationTests/      # hosts real Products/Baskets/Orders/Parties Program in-process
│       └── Bff.Api.UnitTests/
│
├── products/   # existing — unchanged by this feature
├── baskets/    # existing — unchanged by this feature
├── orders/     # existing — unchanged by this feature
└── parties/    # existing — unchanged by this feature; now in scope as a fourth BFF-fronted service

shared/
└── ServiceDefaults/   # existing — reused as-is by gateway and bff
```

**Structure Decision**: Extends the existing `services/<name>/{src,tests}` convention (already used by parties/products/baskets/orders) with two new peer service shells, `gateway` and `bff`. Both reference `shared/ServiceDefaults` exactly as the domain services do. No frontend directory exists yet in this repo (SCRUM-14 adds the SPA in a later feature), so only Option "backend service" applies — there is no `frontend/` tree to extend here.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle V (Tenant Isolation): gateway/BFF do not resolve or enforce tenant context in this feature. | No tenant-resolution mechanism exists in the repo yet — that is SCRUM-12's explicit scope (stub identity, hardcoded single tenant), sequenced as a sibling Phase-1 story, not a prerequisite this plan can silently assume is done. Building a one-off tenant stub inside this feature would duplicate SCRUM-12's work and likely conflict with it once SCRUM-12 lands. | Rejected: implementing a throwaway tenant stub here just to satisfy the gate, then deleting it when SCRUM-12 ships, is pure rework with no interim benefit — the gateway/BFF already forward any tenant/correlation header present, so wiring SCRUM-12's actual resolution in is a small follow-on change, not a redesign. **Time-bound**: must land before this routing path carries real (non-demo) traffic; tracked by SCRUM-12. |
| Principle VI (Secure by Default): gateway/BFF endpoints have no real token validation or authorization policy in this feature. | No identity server is implemented yet (ADR-0001 picked the product; SCRUM-23/Phase 3 stands it up). Requiring real auth now would block a routing-focused Phase-1 story on Phase-3 work that hasn't started. | Rejected: stubbing fake JWT validation here would need to be torn out and replaced once SCRUM-23 lands, and risks the stub silently becoming load-bearing. Phase 1 is explicitly scoped as "no meaningful hardening yet" per the roadmap. **Time-bound**: must land before this routing path is exposed outside the local/demo environment; tracked by SCRUM-23. |

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

No new violations were introduced by the Phase 1 design:

- **Contract-first (II)** is satisfied by `contracts/bff-openapi.yaml` existing before any endpoint implementation, matching the products-listing route required by spec Test Scenario 1, with basket/order routes explicitly marked as shape-TBD rather than falsely finalized.
- **Resilience budgets (VIII)** are satisfied by `data-model.md`'s Downstream Service Client entity naming an explicit timeout, retry policy, and circuit breaker for every one of the four downstream clients — no client was designed without one.
- **No business logic in the BFF (spec FR-005)** is satisfied by the Aggregated Response design containing only proxy/shape fields (`data-model.md`), with no domain rule, computed business field, or persistence concern introduced during design.
- The two documented Principle V/VI deviations are unchanged by Phase 1 design — `research.md` Decision 7 narrows the deviation to "forward headers if present, resolve nothing," which is strictly less scope than the plan-time Constitution Check already accounted for, not more.

Gate: **PASS** (with the two carried-forward, time-bounded deviations recorded above).
