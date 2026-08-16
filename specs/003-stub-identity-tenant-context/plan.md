# Implementation Plan: Stub Identity with Resolved Tenant Context

**Branch**: `003-stub-identity-tenant-context` | **Date**: 2026-08-15 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-stub-identity-tenant-context/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Give every request a resolved tenant identity, end to end, before any real identity server exists. The gateway authenticates every request as a single fake user (a stub `AuthenticationHandler`, not a hand-rolled bypass) and stamps its tenant claim onto an `X-Tenant-Id` header; that header rides YARP's default forwarding to the BFF and, via a new outbound `DelegatingHandler`, onward to whichever domain service the BFF calls. Each domain service (and the BFF) reads the header into a request-scoped tenant context through a new shared `Tenancy` library; each domain service's database connection is resolved from that context — via schema-per-tenant on the existing per-service database, not a new database per tenant — and refuses to resolve at all when no tenant is present. Swapping the stub for Phase 3's real identity server later means registering a different authentication scheme against the same claim, not rewiring propagation or enforcement.

## Technical Context

**Language/Version**: C# / .NET 10 (matches every other service in the fleet; constitution Technology Constraints)

**Primary Dependencies**: `Microsoft.AspNetCore.Authentication` (shared-framework, no new package — custom `AuthenticationHandler` for the stub identity, ADR-0001-compatible extension point) · EF Core's `HasDefaultSchema` (already-referenced `Microsoft.EntityFrameworkCore`, schema-per-tenant) · a new shared `Tenancy` class library (sibling to `shared/ServiceDefaults`, same "cross-cutting, not hand-wired per service" pattern — constitution Principle VII's precedent applied to Principle V)

**Storage**: SQL Server, unchanged — each domain service keeps its single existing per-service database (`services/{name}/appsettings*.json`, docker-compose.deps.yml); tenancy is expressed as an EF Core default schema resolved per request, not a new database or a new connection string

**Testing**: xUnit, matching every other service. New coverage: a `Tenancy` unit-test project for the shared accessor/guard; per-service integration tests proving persistence fails without a resolved tenant (spec Test Scenario 2); an extension of `tests/CrossServiceIsolation.Tests`' existing scanner convention proving the tenant-gated `AddDbContext` call site exists exactly once per service (spec Test Scenario 3 / SC-003, research.md Decision 6)

**Target Platform**: Linux containers on Kubernetes (existing platform; no new infrastructure — schema-per-tenant needs no new container, port, or compose service)

**Project Type**: web-service — one new shared library (`Tenancy`) plus targeted changes to the existing gateway, BFF, and four domain services; no new deployable service

**Performance Goals**: No new latency budget; tenant resolution is a header read plus a scoped DI lookup, well inside the existing per-service p95 ≤ 150 ms / p99 ≤ 500 ms and BFF p95 ≤ 300 ms / p99 ≤ 800 ms budgets (constitution Principle VIII)

**Constraints**: The gateway is the sole tenant-resolution point (constitution Principle V) — every other hop only propagates or enforces, never resolves or defaults. No persistence connection may be constructed without a resolved tenant (spec FR-004/FR-005). The propagation mechanism (header, middleware, shared accessor) MUST NOT change when Phase 3 swaps the resolution source (spec FR-007) — only the authentication scheme registered at the gateway changes.

**Scale/Scope**: Exactly one tenant is resolvable in Phase 1 (spec FR-008); touches the gateway, the BFF, and all four domain services (parties, products, baskets, orders) from [002-gateway-bff-routing](../002-gateway-bff-routing/), plus one new shared library. No SPA work (SCRUM-14 is separate) and no real identity server (SCRUM-23/Phase 3 is separate).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | No service gains access to another's database; each domain service still owns exactly one database, now schema-partitioned by tenant within itself. | PASS |
| II. Contract-First Integration | The `X-Tenant-Id` header is the interface this feature adds between hops; its contract (values, requiredness, failure behavior) is documented in `contracts/` before implementation. | PASS |
| III. Test-First Development | Tests for propagation (US1) and enforcement (US2) are written and confirmed failing before their implementation tasks — enforced at task-generation/implementation time, matching how [002-gateway-bff-routing](../002-gateway-bff-routing/plan.md) treated this gate. | PASS (deferred to tasks) |
| IV. Event-Driven by Default | N/A — no messaging infrastructure exists yet in this repository (verified: no RabbitMQ/MassTransit reference anywhere); propagating tenant context onto events is out of scope here (spec Assumptions) and revisited when a feature introduces messaging. | PASS (N/A) |
| V. Tenant Isolation Is a Security Boundary | This feature exists to satisfy this principle: resolved once at the edge, propagated explicitly gateway → BFF → services, connection resolved per request from the tenant context, no code path reaches persistence without it. It also **retires** the Principle V deviation [002-gateway-bff-routing/plan.md](../002-gateway-bff-routing/plan.md) carried ("gateway/BFF do not resolve or enforce tenant context... tracked by SCRUM-12"). | PASS |
| VI. Secure by Default | Still a documented deviation, unchanged by this feature: the stub identity is a fake authentication scheme, not real token validation, and no authorization policy is added. Real validation is SCRUM-23/Phase 3's scope, as already carried by [002-gateway-bff-routing/plan.md](../002-gateway-bff-routing/plan.md). This feature's contribution is structural — using the real `AddAuthentication`/`AuthenticationHandler` extension point — so that swap, when it happens, doesn't also require a pipeline rewrite. | DEVIATION — see Complexity Tracking |
| VII. Observable by Default | The tenant identifier is pushed into the same logging-scope mechanism `CorrelationIdMiddleware` already uses, so it appears on every structured log line at every hop with no new observability wiring. | PASS |
| VIII. Performance and Resilience Budgets | Tenant resolution adds a header read and a scoped DI call on the existing request path; no new outbound call, no new timeout to declare. | PASS |
| IX. Frontend Discipline | N/A — no frontend code in this feature. | PASS (N/A) |
| X. Toggle-Gated, Reversible Delivery | This is foundational plumbing every subsequent request depends on, not an optional behavior a toggle could disable mid-flight; rollback is redeploying the prior version, same as [002-gateway-bff-routing](../002-gateway-bff-routing/plan.md)'s equivalent net-new-surface reasoning. | PASS |

One Principle VI deviation is carried forward, unchanged in scope, from [002-gateway-bff-routing](../002-gateway-bff-routing/plan.md) (tracked by SCRUM-23). This feature does not add a new deviation; it narrows an existing one (Principle V, above) instead.

## Project Structure

### Documentation (this feature)

```text
specs/003-stub-identity-tenant-context/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
├── ServiceDefaults/          # existing — unchanged; flat layout, no src/tests split
├── Tenancy/                  # NEW — sibling shared library, same flat layout as ServiceDefaults
│   ├── Tenancy.csproj
│   ├── TenantContext.cs              # scoped tenant-id holder + RequireTenantId() guard
│   ├── TenantContextMiddleware.cs    # inbound X-Tenant-Id header → TenantContext; log scope
│   ├── MissingTenantContextException.cs
│   └── TenancyExtensions.cs          # AddTenancy() / UseTenancy() — mirrors ServiceDefaults' shape
└── Tenancy.UnitTests/        # NEW test project — ServiceDefaults has none today, but
    └── Tenancy.UnitTests.csproj    # TenantContext's throw/return branching is exactly the kind of
                                     # pure logic constitution Principle III (NON-NEGOTIABLE)
                                     # requires a preceding failing test for

services/
├── gateway/src/Gateway.Api/
│   ├── Program.cs                        # + AddAuthentication/AddScheme, UseAuthentication
│   ├── Identity/
│   │   ├── StubIdentityAuthenticationHandler.cs   # always-succeeds fake user + tenant claim
│   │   └── TenantHeaderPropagationMiddleware.cs   # claim → X-Tenant-Id request header (mirrors CorrelationIdMiddleware)
│   └── appsettings.json                  # the one Phase-1 hardcoded tenant id/name
│
├── bff/
│   ├── src/Bff.Api/
│   │   ├── Program.cs                        # + AddTenancy()/UseTenancy(), outbound handler registration
│   │   └── DownstreamClients/
│   │       └── TenantPropagationHandler.cs   # NEW — DelegatingHandler copying TenantContext onto outbound calls
│   └── tests/Bff.Api.IntegrationTests/
│       └── BffTestHost.cs                    # CreateDownstreamAsync sets TenantContext before resolving each DbContext (research.md Decision 7)
│
├── products/
│   ├── src/Products.Api/
│   │   ├── Program.cs                        # AddDbContext switches to the (sp, options) overload
│   │   └── Data/ProductsDbContext.cs         # + HasDefaultSchema(tenantId) from injected TenantContext
│   └── tests/Products.Api.IntegrationTests/
│       └── CatalogEndpointsTests.cs          # seeding sets TenantContext before resolving ProductsDbContext (research.md Decision 7)
├── baskets/                                   # same shape as products (src + tests)
├── orders/                                    # same shape as products (src + tests)
└── parties/                                   # same shape as products (src + tests)

tests/
└── CrossServiceIsolation.Tests/
    └── TenantGatedConnectionScanner.cs   # NEW — extends the existing scanner convention (spec Test Scenario 3 / SC-003)
```

**Structure Decision**: One new shared library, `shared/Tenancy`, following the exact precedent `shared/ServiceDefaults` already set for "cross-cutting concern, wired identically everywhere, not hand-rolled per service." Every other change is additive within existing projects from [001-scaffold-service-shells](../001-scaffold-service-shells/) and [002-gateway-bff-routing](../002-gateway-bff-routing/) — no new deployable service, no new database, no new container.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle VI (Secure by Default): the gateway's `AuthenticationHandler` always succeeds with a fixed identity; no real token validation or authorization policy exists. | No identity server is implemented yet (ADR-0001 picked the product; SCRUM-23/Phase 3 stands it up). This feature's job is the tenant-propagation *mechanism*; gating it on Phase 3 landing first would block the walking skeleton indefinitely — this is the same reasoning [002-gateway-bff-routing/plan.md](../002-gateway-bff-routing/plan.md) already recorded, carried forward unchanged, not re-justified from scratch. | Rejected building real validation now for the same reason 002 rejected it: it would need to be torn out and replaced once SCRUM-23 lands. **Time-bound**: must land before this path is exposed outside the local/demo environment; tracked by SCRUM-23. |

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

No new violations were introduced by the Phase 1 design:

- **Contract-first (II)** is satisfied by `contracts/tenant-id-header.md` existing before any implementation, documenting the header's producers, consumers, and failure modes — the interface this feature adds — ahead of code.
- **No default/fallback tenant (V; spec FR-004)** is satisfied structurally by `data-model.md`'s two-state Tenant Context: there is no third "resolved to a default" state anywhere in the design, and every consumer's failure mode (`contracts/tenant-id-header.md`) resolves to Unresolved rather than to some tenant.
- **Sole resolution point (V)** is satisfied by `research.md` Decision 2: the gateway always overwrites any inbound `X-Tenant-Id` rather than merging or trusting one, so no other hop can smuggle in a self-declared tenant.
- **Swap-only-the-source (FR-007)** is satisfied by `research.md` Decision 1: every component below the gateway's authentication scheme reads only the resolved claim/header, never the scheme's implementation, which `quickstart.md` Scenario 5 makes an explicit, checkable claim rather than an aspiration.
- The one documented Principle VI deviation is unchanged by Phase 1 design — it was already scoped to "no real token validation," and nothing in the design adds authorization policy or token validation, so the deviation's boundary hasn't moved.

Gate: **PASS** (with the one carried-forward, time-bounded deviation recorded above).
