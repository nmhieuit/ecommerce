# Implementation Plan: Scaffold Parties/Products/Baskets/Orders Service Shells

**Branch**: `001-scaffold-service-shells` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-scaffold-service-shells/spec.md`

## Summary

Stand up four independently runnable microservice shells — parties, products, baskets, orders — each with its own SQL Server database/schema, a liveness+readiness health check, and code organized by vertical slice per the constitution's default internal architecture. No domain/business endpoints are built here; this is the foundation SCRUM-12 (tenant context), SCRUM-13 (gateway routing), and later feature work build on. Technical approach: four ASP.NET Core Minimal API projects on .NET 10, each wired to the shared `ServiceDefaults` component for OpenTelemetry, each with its own EF Core `DbContext` and connection string pointing at a dedicated database, each carrying unit tests (xUnit) written test-first and an integration test (Testcontainers) that proves the service boots and reports ready without any other service running.

## Technical Context

**Language/Version**: C# / .NET 10 (fixed — constitution "Technology and Infrastructure Constraints")

**Primary Dependencies**: ASP.NET Core Minimal APIs (see Research: Decision 1), EF Core (fixed), shared `ServiceDefaults` component for OpenTelemetry wiring (constitution Principle VII), `Microsoft.Extensions.Diagnostics.HealthChecks` (see Research: Decision 3)

**Storage**: SQL Server via EF Core, one database/schema per service (fixed — constitution "Persistence"). Tenant-schema resolution logic is explicitly out of scope for this feature (delivered by SCRUM-12); each service's `DbContext` connects to a single default schema for now, structured so a tenant-keyed connection resolver can be substituted later without a redesign.

**Testing**: xUnit (see Research: Decision 2) for unit tests; Testcontainers (SQL Server) for integration tests (fixed — constitution Principle III, NON-NEGOTIABLE test-first)

**Target Platform**: Linux containers on Kubernetes (fixed — constitution "Platform")

**Project Type**: Backend microservices (this feature: 4 services, no frontend in scope)

**Performance Goals**: Health endpoints must respond well within the platform's internal-API budget (constitution Principle VIII: p95 ≤ 150 ms, p99 ≤ 500 ms) — trivially achievable for a health check, but the SLO is declared now so it's measured from day one, not bolted on later.

**Constraints**: Each service MUST be stateless and independently deployable (constitution "Platform", Principle I); no service may hold any credential or connection reaching another service's database (spec FR-005); readiness MUST reflect actual database connectivity, not process liveness alone (spec FR-003).

**Scale/Scope**: 4 services, local single-developer bring-up target (spec SC-001: under 5 minutes per service from fresh clone). Multi-service orchestration ("one-command local run" of all four together) is SCRUM-15's scope, not this feature's — this plan covers each service being independently runnable, not a combined bring-up script.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Service Autonomy and Bounded Context | **PASS** | Each service gets its own DB/schema (FR-004); vertical-slice is the chosen internal structure, no DDD/CQRS escalation — none of these four shells yet owns a business invariant complex enough to justify it. |
| II. Contract-First Integration | **DEFERRED (N/A this feature)** | No business HTTP/event contract is introduced — the only surface is the health endpoint, documented in `contracts/` for completeness but not a business API. Contract-first begins in earnest with the first domain endpoint (later stories). |
| III. Test-First Development (NON-NEGOTIABLE) | **PASS (binding on implementation)** | Plan requires a failing unit test before the health-check handler exists, and a failing Testcontainers integration test before the DB-isolation guarantee is implemented. `/speckit-tasks` MUST order tasks test-first. |
| IV. Event-Driven by Default | **DEFERRED (N/A this feature)** | No inter-service communication exists yet in a pure scaffold — nothing to publish or consume. |
| V. Tenant Isolation Is a Security Boundary | **PARTIAL — constrained, not fully delivered** | Full tenant-context resolution is explicitly out of scope (spec Assumptions; delivered by SCRUM-12). This plan's constraint: the `DbContext`/connection-resolution design must not foreclose per-tenant schema resolution later (e.g., no hardcoded single connection string baked into startup in a way that can't be parameterized). |
| VI. Secure by Default | **PASS with documented exception** | No authenticated endpoints exist yet. The health endpoint is conventionally anonymous (Kubernetes probes can't present a token) — documented here as the exception, not an oversight. |
| VII. Observable by Default | **PASS** | Every service wires the shared `ServiceDefaults` component (no per-service hand-rolled telemetry) and exposes distinct liveness (`/health/live`) and readiness (`/health/ready`) probes — directly satisfies spec FR-003. |
| VIII. Performance and Resilience Budgets | **PASS** | Each service declares SLOs in its service manifest from creation (health-endpoint budget now; full endpoint budgets as domain endpoints are added later). |
| IX. Frontend Discipline | **N/A** | No frontend in this feature's scope. |
| X. Toggle-Gated, Reversible Delivery | **N/A, justified** | These are brand-new services carrying no existing production behavior — there is nothing yet to toggle or roll back. Toggle-gating begins with the first behavior-changing feature built on top of these shells. |

No unjustified violations — Complexity Tracking table is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-scaffold-service-shells/
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
├── parties/
│   ├── src/
│   │   └── Parties.Api/
│   │       ├── Features/
│   │       │   └── HealthCheck/          # vertical slice: handler + registration for this capability
│   │       ├── Data/
│   │       │   └── PartiesDbContext.cs
│   │       ├── service-manifest.yaml     # SLOs (Principle VIII)
│   │       └── Program.cs
│   └── tests/
│       ├── Parties.Api.UnitTests/
│       └── Parties.Api.IntegrationTests/ # Testcontainers-backed
├── products/            # same shape as parties/
├── baskets/              # same shape as parties/
└── orders/                # same shape as parties/

shared/
└── ServiceDefaults/                       # shared OTel + health-check wiring, referenced by every service (Principle VII)
```

**Structure Decision**: Custom "backend microservices" layout (neither template Option 1 nor Option 2 fits — no frontend is in scope for this feature, and it's multiple independent services, not one library/CLI). Each service is a sibling directory under `services/` with an identical internal shape (`src/` + `tests/`), so the pattern is copy-paste-consistent across parties/products/baskets/orders. `shared/ServiceDefaults` is the one cross-service dependency permitted by the constitution (Principle VII) — no other code is shared between services.

## Complexity Tracking

*No entries — no unjustified Constitution Check violations.*

## Post-Design Constitution Re-check

Re-evaluated after Phase 1 (data-model.md, contracts/, quickstart.md): no change to the table above. The design introduced no business entities, no new inter-service coupling, and no authenticated surface — the three research decisions (Minimal APIs, xUnit, framework-native health checks) don't touch any constitutional principle differently than assessed pre-design. Gate remains **PASS**.
