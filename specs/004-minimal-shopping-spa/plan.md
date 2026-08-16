# Implementation Plan: Minimal Shopping SPA — Browse, Basket, Checkout, Confirmation

**Branch**: `004-minimal-shopping-spa` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-minimal-shopping-spa/spec.md`

## Summary

Deliver the storefront that closes the walking skeleton — browse, add to basket, check out, see a
confirmation — and the backend capability it needs, which does not exist yet.

The frontend is a new `frontend/` Turborepo workspace holding one Vite + React + TypeScript-strict
app and one Orval-generated TanStack Query client package, talking only to the gateway. The backend
gains the first real write paths in the platform: the products catalog is seeded by migration, the
baskets service grows line items with quantities and captured unit prices plus caller-scoped
add/read/clear routes, and the orders service grows a place-order route that computes the order
total from the lines it is sent. The BFF composes those into three client-facing routes without
performing any arithmetic itself. A new `X-Subject-Id` header carries the shopper's identity from
the gateway to the services, mirroring exactly how `X-Tenant-Id` already travels, so the basket can
be resolved from who is asking rather than from anything the browser holds.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, unchanged) · TypeScript 5.x in `strict` mode with
`noUncheckedIndexedAccess`, on Node.js 22 LTS (frontend, new)

**Primary Dependencies**: React 19 + Vite 6 + TanStack Query v5 (constitution: Frontend) ·
Tailwind CSS + Radix UI primitives (ADR-0009, consumed directly — see Decision 2) · Orval
(ADR-0004) · Turborepo + pnpm workspaces (ADR-0010) · `size-limit` for the bundle gate · existing
backend dependencies only — EF Core 10, `Microsoft.Extensions.Http.Resilience`, the shared
`Tenancy` and `ServiceDefaults` libraries. **No new backend package is required.**

**Storage**: SQL Server via EF Core, unchanged topology — products, baskets, and orders each keep
their own database. This feature adds one table (`BasketLineItem`), changes one column
(`Basket.CustomerId` → `CustomerRef`), and seeds three catalog rows. No Redis; the basket is
relational for Phase 1, consistent with how the baskets service is already built.

**Testing**: xUnit + Testcontainers (SQL Server) for the backend, unchanged · Vitest + Testing
Library + MSW for frontend behaviour, asserting through accessible roles (Principle III) ·
Playwright for one end-to-end walkthrough spec (research Decision 4)

**Target Platform**: Linux containers on Kubernetes for the services; a static-asset bundle served
over HTTP for the storefront. Browsers: current Chrome, Firefox, Safari, and mobile Safari/Chrome.

**Project Type**: Web application — existing .NET microservices plus a new frontend monorepo.

**Performance Goals**: Storefront entry screen usable within 2.5 s, first interaction answered
within 200 ms, layout shift under 0.1, all at p75 on mid-range mobile (spec SC-012 — declared here,
measured in Phase 4). BFF read routes stay inside the existing p95 ≤ 300 ms client-facing budget.

**Constraints**: 150 kB gzipped per storefront entry screen, enforced in the build and failing it
(spec FR-025, SC-011) · a clear error reaches the shopper within 5 s of any backend failure (spec
SC-006), which the BFF's existing 3 s total-request budget already sits inside · every request
carries a resolved tenant *and* a resolved subject before touching persistence.

**Scale/Scope**: One tenant, one shopper, three seeded products, four screens (catalog, basket,
confirmation, error). Roughly 6 backend endpoints touched or added, 1 new shared-library concept,
2 new frontend packages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see the bottom of this
file.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| I. Service Autonomy and Bounded Context | No service reads another's database. The baskets service stores a `ProductId` and a captured `UnitPrice` supplied over HTTP, never a join to the products catalog; the orders service receives lines in a request. Each service keeps vertical-slice structure under `Features/`. | PASS |
| II. Contract-First Integration | Contracts are written before code ([contracts/](./contracts/)); the authoritative document remains the BFF's generated OpenAPI, and the frontend client is generated from it by Orval with a CI drift check. The `Basket` response shape changes rather than being versioned — 002's contract marks it "to be finalized" and it has no released consumer. | PASS (with the shape-change note) |
| III. Test-First Development | Red-Green-Refactor throughout. Backend: unit tests for the quantity-merge and total-computation rules, Testcontainers integration tests for every new endpoint, no in-memory providers. Frontend: Vitest + Testing Library asserting through accessible roles, plus one Playwright walkthrough. | PASS |
| IV. Event-Driven by Default | **Checkout is a synchronous BFF-orchestrated two-step, not a saga.** No messaging infrastructure exists in this repository — no MassTransit, no RabbitMQ container, no outbox. Order-first ordering bounds the damage, and compensation is absent. | DEVIATION — see Complexity Tracking |
| V. Tenant Isolation Is a Security Boundary | Propagation and enforcement hold: the tenant is resolved once at the gateway, travels on every hop, and `RequireTenantId()` gates every `AddDbContext`. **But the required physical separation does not exist** — schema-per-tenant was specified in 003 and is absent from the code (research, Finding). This feature creates the first tenant-owned business data on top of that gap. | FAIL (pre-existing) — see Complexity Tracking |
| VI. Secure by Default | Carried forward unchanged from 002 and 003: the stub identity always succeeds and no authorization policy exists. This feature adds no new deviation and narrows nothing. Server-side validation is present on every new write path (quantity ≥ 1, non-empty basket, unit price resolved server-side and never accepted from the client). | DEVIATION (carried, tracked by SCRUM-23) |
| VII. Observable by Default | Services keep the shared `ServiceDefaults` instrumentation; the subject identifier joins the tenant in the logging scope at every hop. **Frontend correlation-ID propagation is not implemented** — SCRUM-26 owns it in Phase 3, and it is the only observability item this feature leaves open. | PASS (frontend correlation deferred to SCRUM-26) |
| VIII. Performance and Resilience Budgets | The download-size budget is declared and enforced in the build (FR-025). Every outbound BFF call keeps its existing timeout, retry, and circuit-breaker pipeline; the new routes reuse it by construction. Core Web Vitals targets are declared but not measured. | DEVIATION (measurement only) — see Complexity Tracking |
| IX. Frontend Discipline | React + TypeScript strict + Vite; `any` requires written justification; server state lives in TanStack Query and is never copied into a global store; the API client is generated, never hand-written; the storefront calls the BFF (through the gateway) only. **One shared design-system package is deferred** — with a single app there is no cross-app duplication to prevent, per the spec's Assumptions. | PASS (design-system package deferred — see Complexity Tracking) |
| X. Toggle-Gated, Reversible Delivery | Net-new surface: a storefront that does not exist yet and endpoints nothing calls yet. Rollback is redeploying the prior version, exactly as 002 and 003 reasoned for their equivalent net-new surfaces. Migrations are additive (one new table, three seeded rows); the one column change is the sole non-additive step and is covered in Complexity Tracking. | PASS (with the column-change note) |

**Gate result**: proceed, with four entries in Complexity Tracking. One of them (Principle V) is a
pre-existing failure this feature did not create but does build on, and it needs a maintainer
decision rather than a plan-level assertion — it is called out in the completion report.

## Project Structure

### Documentation (this feature)

```text
specs/004-minimal-shopping-spa/
├── plan.md                          # This file
├── research.md                      # Phase 0 — 11 decisions + 1 finding
├── data-model.md                    # Phase 1
├── quickstart.md                    # Phase 1 — 9 validation scenarios
├── contracts/                       # Phase 1
│   ├── bff-openapi.yaml             # client-facing additions/changes
│   ├── downstream-openapi.yaml      # domain-service additions/changes
│   └── subject-id-header.md         # the X-Subject-Id propagation contract
├── checklists/requirements.md       # spec quality checklist (16/16)
└── tasks.md                         # Phase 2 — created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
frontend/                                    # NEW — pnpm workspace, Turborepo (ADR-0010)
├── package.json / pnpm-workspace.yaml / turbo.json / tsconfig.base.json
├── apps/
│   └── web/
│       ├── src/
│       │   ├── features/
│       │   │   ├── catalog/                 # product list, empty state, add-to-basket control
│       │   │   ├── basket/                  # basket view, line items, total
│       │   │   └── checkout/                # checkout action, confirmation screen
│       │   ├── shared/                      # error boundary, money formatting (FR-024), a11y primitives
│       │   ├── app/                         # routing, QueryClient, backend-origin config
│       │   └── main.tsx
│       ├── e2e/                             # one Playwright walkthrough spec
│       ├── index.html · vite.config.ts · .size-limit.json · package.json
│       └── tests/                           # Vitest + Testing Library + MSW
└── packages/
    └── api-client/                          # Orval-generated TanStack Query hooks (ADR-0004)
        ├── orval.config.ts
        └── src/generated/                   # committed; CI fails on drift

shared/Tenancy/                              # CHANGED
├── CallerContext.cs                         # NEW — SubjectId + RequireSubjectId()
├── CallerContextMiddleware.cs               # NEW — reads X-Subject-Id, mirrors TenantContextMiddleware
└── TenancyExtensions.cs                     # CHANGED — AddTenancy/UseTenancy also wire the caller context

services/gateway/src/Gateway.Api/
└── Identity/SubjectHeaderPropagationMiddleware.cs   # NEW — stamps X-Subject-Id from the subject claim

services/products/src/Products.Api/
└── Migrations/                              # NEW migration — three seeded products via HasData

services/baskets/src/Baskets.Api/
├── Data/Basket.cs                           # CHANGED — CustomerRef (string, unique) + LineItems
├── Data/BasketLineItem.cs                   # NEW
├── Data/BasketsDbContext.cs                 # CHANGED — line items, unique indexes, decimal precision
├── Features/Baskets/BasketEndpoints.cs      # CHANGED — /baskets/current, .../items, .../clear
└── Migrations/                              # NEW migration

services/orders/src/Orders.Api/
└── Features/Orders/OrderEndpoints.cs        # CHANGED — POST /orders, total computed here

services/bff/src/Bff.Api/
├── DownstreamClients/BasketsApiClient.cs    # CHANGED — current-basket read, add item, clear
├── DownstreamClients/OrdersApiClient.cs     # CHANGED — place order
├── DownstreamClients/TenantPropagationHandler.cs  # CHANGED — relays X-Subject-Id too
├── Features/Baskets/BasketsEndpoints.cs     # CHANGED — GET /bff/basket, POST /bff/basket/items
├── Features/Checkout/CheckoutEndpoints.cs   # NEW — POST /bff/checkout
└── appsettings.Development.json             # FIXED — BasketsApi/OrdersApi base URLs are swapped
```

**Structure Decision**: the frontend lives in a new top-level `frontend/` workspace rather than
under `services/`, because `tests/StructureConventionTests` scans `services/` for the C#
vertical-slice convention and every entry there is a `.csproj` in `Ecommerce.slnx` — a Node package
there would either break those scanners or force exceptions into them. Backend changes stay inside
each service's existing `Features/` vertical slices; no new project is added to the solution.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Principle V** — no schema-per-tenant separation. Tenant access is gated but every tenant's rows would share one set of tables. | Not introduced by this feature. 003's plan specified `HasDefaultSchema` per tenant and marked all 39 of its tasks complete; the code contains no schema resolution and every migration targets `dbo` (research, Finding). This feature adds the first tenant-owned business data on top of that gap. | Nothing simpler was rejected — **this is an open gap, not a considered trade-off**. Closing it is contained (resolve the schema from the tenant context at each `AddDbContext` call site, plus one migration per service) but sits outside this spec's clarified scope. **Time-bound**: must land before a second tenant is configured, and before this path is exposed outside local/demo. Needs a maintainer decision on whether it joins this feature or becomes its own ticket. |
| **Principle IV** — checkout is a synchronous BFF-orchestrated two-step (create order, then clear basket) rather than a saga with compensation. | No messaging infrastructure exists at all: no MassTransit package, no RabbitMQ container, no outbox table. The roadmap places event schemas at SCRUM-18 (Phase 2) and outbox verification at SCRUM-31 (Phase 4). Building it here would multiply this feature's size several times over. | Rejected standing up RabbitMQ + MassTransit now: correct destination, wrong phase. Residual risk accepted and bounded — the order is created first so a mid-flight failure leaves a real order rather than a lost basket, and FR-016 is protected by the storefront's in-flight guard plus the empty-basket rejection. **Time-bound**: closed by SCRUM-18/SCRUM-31. |
| **Principle VIII** — Core Web Vitals targets are declared (SC-012) but not measured. | Measuring at a 75th percentile needs a production-like environment and real traffic; Phase 1 has neither. SCRUM-32 (Phase 4) is the story that runs performance tests against the constitution's budgets. | Rejected deferring the *whole* principle: the download-size budget is the half Phase 1 genuinely can enforce, so it is enforced from the first commit (FR-025, SC-011) rather than set later to whatever the code already costs. **Time-bound**: closed by SCRUM-32. |
| **Principle IX** — no shared design-system package; Radix + Tailwind are consumed directly inside `apps/web`. | The prohibition is duplication *across apps*, and there is one app. The spec's Assumptions defer the package until a second client exists to share with; ADR-0009's accessibility rationale is still honoured by using Radix primitives, which is where the WCAG 2.2 AA guarantees actually come from. | Rejected creating `packages/design-system` now: a versioned design system with exactly one consumer is ceremony with no reader, and Storybook plus its a11y addon is real overhead for four screens. Cost accepted: extraction is a refactor when mobile-web lands. **Trigger**: the second client application. |

Two further notes that are not violations but should not be silent:

- **`Basket.CustomerId` → `CustomerRef` is a non-additive column change**, against Principle X's
  expand/contract preference. It is taken as a single step because the column has no released
  consumer, no data worth preserving (the table is empty), and no deployed version reads it. Had
  either been true, this would be an expand/contract pair instead.
- **The BFF's Development base URLs for baskets and orders are swapped** in
  `appsettings.Development.json` (baskets listens on 5188, orders on 5041; the config has them the
  other way round). Harmless today because both existing routes are GET-by-id returning 404, but
  this feature's write paths would post basket items to the orders service. Fixing it is a one-line
  change included in this feature.

## Post-Design Constitution Re-Check

Re-evaluated after [data-model.md](./data-model.md) and [contracts/](./contracts/) were written. No
verdict changed. Three points were confirmed rather than assumed during design:

1. **The BFF performs no arithmetic anywhere in the flow** (Principle II / spec 002 FR-005). The
   basket total is computed by the baskets service from its own rows; the order total is computed by
   the orders service from the lines it is sent. The BFF forwards values other services produced —
   including the unit price it read from the catalog, which it copies without transforming. This was
   the design pressure that decided research Decisions 7 and 8, and it survived the contract pass.
2. **No client-supplied value can reach a price or a tenant.** `AddBasketItemRequest` at the
   client-facing edge carries a product and a quantity only; the unit price is resolved server-side.
   The tenant and subject are stamped by the gateway and stripped when unresolved. A client cannot
   discount its own basket or address another shopper's.
3. **The design adds no unbounded wait.** Every new BFF → service call goes through the existing
   typed clients, which are registered uniformly with attempt timeout, total-request timeout, retry,
   and circuit breaker — a new client cannot be added without them, which is why that registration
   was centralised in the first place.

The Principle V gap is unchanged by the design and is the one item that should be decided before
implementation begins rather than after.
