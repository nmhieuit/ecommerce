# Phase 0 Research: Minimal Shopping SPA

**Feature**: [spec.md](./spec.md) · **Branch**: `004-minimal-shopping-spa` · **Date**: 2026-08-16

Every decision below was checked against the repository as it actually stands on 2026-08-16, not
against what earlier specs said would be built. Where the two disagree, the repository wins and the
disagreement is called out.

---

## Decision 1 — Frontend lives in a new top-level `frontend/` Turborepo workspace

**Decision**: Create `frontend/` at the repository root, as a pnpm workspace orchestrated by
Turborepo, holding one application (`apps/web`) and one package (`packages/api-client`).

**Rationale**: ADR-0010 is Accepted and names Turborepo + pnpm workspaces, with the first action
item "Initialize Turborepo in the frontend monorepo with pnpm workspaces" — this feature is the
first frontend work, so it is the one that does it. A sibling of `services/` and `shared/` keeps the
existing top-level shape legible (C# services in one place, frontend in another) and keeps the
`.slnx` solution untouched. Turborepo's config is roughly twenty lines and buys correct task
ordering between the generated client and the app that consumes it, which matters from the very
first package.

**Alternatives considered**:

- *Put the SPA inside `services/web/`*: rejected — `services/` is scanned by
  `tests/StructureConventionTests` for the C# vertical-slice convention, and every entry there is a
  `.csproj` in the solution. A Node package there would either break those scanners or force them to
  grow exceptions.
- *Plain pnpm workspaces, no Turborepo*: rejected by ADR-0010 already (no build caching, CI time
  grows linearly), and re-deciding it here would be re-litigating a settled ADR.

---

## Decision 2 — One app, no design-system package yet; Radix + Tailwind used directly

**Decision**: Build `apps/web` with Tailwind CSS and Radix UI primitives consumed directly. Do
**not** create `packages/design-system` or stand up Storybook in this feature.

**Rationale**: The spec's Assumptions defer the shared design-system package until a second client
exists to share with, and Principle IX's actual prohibition is *duplication across apps* — with one
app there is nothing to duplicate. ADR-0009's foundation choice is still honoured: Radix primitives
carry the keyboard handling, ARIA roles, and focus management that FR-017 and SC-009 require, so
accessibility comes from the dependency rather than from hand-rolled markup. Tailwind is
compile-time, which keeps the bundle budget (FR-025) reachable.

**Cost accepted**: when mobile-web lands, the shared primitives must be extracted from `apps/web`
into `packages/design-system` — a refactor this feature is deliberately choosing to defer rather
than pre-pay. Recorded in Complexity Tracking with the trigger that closes it.

**Alternatives considered**:

- *Create `packages/design-system` now*: rejected — it contradicts the spec's stated assumption,
  and a design system with exactly one consumer is a versioning ceremony with no reader.
- *Skip Radix, hand-roll the components*: rejected — WCAG 2.2 AA is a hard constitutional
  requirement, and hand-rolled focus management is where accessibility bugs live.

---

## Decision 3 — `packages/api-client`: Orval-generated TanStack Query hooks

**Decision**: Generate the API client into its own workspace package with Orval, configured against
the BFF's OpenAPI document, producing TanStack Query hooks. Add a CI check that fails when the
checked-in generated output is stale relative to the document.

**Rationale**: ADR-0004 chose Orval precisely because it is the only candidate that generates
TanStack Query hooks directly, closing the gap Principle IX prohibits (hand-written API calls). Even
with one app, a separate package is what makes "one generated client" structural rather than
conventional — and it is nearly free given the workspace already exists. The BFF already publishes
its document at `/openapi/v1.json` in Development (`services/bff/src/Bff.Api/Program.cs`), and its
route definitions already declare their 502/504 failure responses explicitly so the generated client
knows 200 is not the only outcome.

**Consequence**: the BFF must be running (or its document exported to a file) for codegen. The
generated output is committed so that a frontend build does not require a live BFF.

**Alternatives considered**:

- *Generate into `apps/web/src/api/generated`*: rejected — it works today with one app, and quietly
  becomes the duplication Principle IX names as a violation the moment mobile-web appears.
- *Hand-written fetch wrappers*: prohibited outright by Principle II.

---

## Decision 4 — Vitest + Testing Library for behaviour, Playwright for the walkthrough

**Decision**: Vitest with Testing Library and MSW for component and hook behaviour; Playwright for
one end-to-end spec covering the walkthrough and its guarded cases.

**Rationale**: Principle III mandates Vitest and Testing Library asserting through accessible roles,
so that half is not a choice. Playwright is added because four success criteria are not observable
from jsdom at all: SC-002 (zero browser-console errors across the walkthrough), SC-009
(keyboard-only completion with visible focus), SC-010 (no request leaves for anything but the BFF),
and SC-008 (rapid double checkout creates exactly one order). Verifying those by hand only would
make them assertions nobody re-runs.

**Scope control**: exactly one Playwright spec file. The happy path plus empty-basket blocking,
double-submit, and keyboard-only traversal. Broader E2E coverage is Phase 2's retrofit
(SCRUM-20/21), not this feature's.

**Tension noted**: the roadmap describes Phase 1 as having "no meaningful test coverage yet", while
the constitution makes Test-First non-negotiable and supersedes all other documents. The
constitution wins; this is the reading that follows the governance rule rather than the roadmap's
prose.

**Alternatives considered**:

- *Manual walkthrough only, per the source ticket*: rejected — SC-002/008/009/010 would become
  claims rather than checks, and the manual walkthrough in [quickstart.md](./quickstart.md) already
  covers the demo need.
- *Cypress*: rejected — no advantage here, and Playwright's console-message and request-interception
  APIs are what SC-002 and SC-010 are literally asserted with.

---

## Decision 5 — Bundle budget enforced with `size-limit`, failing the build

**Decision**: Declare a gzipped budget per entry screen in `size-limit` configuration and run it as
a Turborepo task that fails on breach. Opening budget: **150 kB gzipped** for the storefront entry.

**Rationale**: FR-025 and SC-011 require a declared budget *checked automatically, failing the
build*. Vite's own `chunkSizeWarningLimit` only warns, so it cannot satisfy "fails the build".
`size-limit` asserts against the built artefact and exits non-zero, which is exactly the gate
required. The 150 kB opening figure is set with headroom over a realistic React + TanStack Query +
minimal-Radix baseline (roughly 90–110 kB gzipped) so the gate is honest rather than immediately
breached — it should be tightened to just above the measured figure once the app actually builds.

**Alternatives considered**:

- *`rollup-plugin-visualizer`*: rejected — reporting only, no gate.
- *No budget until Phase 4*: rejected — it is the half of Principle VIII that Phase 1 genuinely
  can enforce, and budgets set after the code exists are set to whatever the code already costs.

---

## Decision 6 — The caller's subject identity is propagated, mirroring the tenant header

**Decision**: Extend the existing `shared/Tenancy` library and the gateway so that the stub
identity's subject claim travels as an `X-Subject-Id` request header alongside `X-Tenant-Id`,
through gateway → BFF → services, and is read into a request-scoped caller context by the same
`AddTenancy()` / `UseTenancy()` calls services already make.

**Rationale**: FR-006 requires the basket to be "resolved from the shopper's identity rather than
from an identifier the browser supplies or remembers". Today only the tenant is propagated — the
gateway issues a `ClaimTypes.NameIdentifier` claim (`phase1-stub-user`) in
`StubIdentityAuthenticationHandler` but nothing carries it past the gateway. Without this, the
baskets service has no way to know whose basket it is holding. Reusing the exact mechanism spec 003
built means Phase 3's real token swap changes the resolution source only, never the propagation —
the same property FR-007 of spec 003 protects.

**Alternatives considered**:

- *One basket per tenant*: rejected — it satisfies the observable Phase 1 behaviour only because
  there happens to be one shopper, and it bakes in a model that is wrong the moment a second shopper
  exists. FR-006 says "the shopper's identity", not "the tenant".
- *A new shared library for caller identity*: rejected as premature — the mechanism, the middleware
  ordering, and the header conventions are identical to tenancy's, and two libraries doing the same
  thing is how they drift apart. `shared/Tenancy` now carries request caller identity generally; a
  rename is optional cleanup, not a prerequisite.

---

## Decision 7 — Baskets stores the unit price captured at add time; the BFF supplies it

**Decision**: A basket line item stores `ProductId`, `Quantity`, and `UnitPrice`. The BFF's
add-to-basket route reads the product's price from the products service and includes it in the
request it sends to the baskets service. The **baskets service** computes its own basket total from
its own line items.

**Rationale**: Prices change; a basket that recomputes from the live catalog silently reprices what
the shopper already chose. Capturing the price at add time is the standard, correct model. On who
supplies it: the BFF already talks to both services and spec 002's FR-003 names cross-service
composition as its job, whereas making the baskets service call the products service would give one
domain service a synchronous runtime dependency on another — which Principle IV discourages more
strongly. The BFF forwards a value it read; it performs no arithmetic and applies no pricing rule,
so spec 002's FR-005 ("no business logic beyond aggregation and shaping") holds.

**Alternatives considered**:

- *Baskets calls products synchronously to resolve the price*: rejected — a justified Principle IV
  exception is available, but it couples two domain services at runtime for something the
  aggregation layer can already do.
- *Store only product and quantity, price the basket at read time*: rejected — it reprices the
  shopper's basket whenever the catalog changes, and it pushes the multiplication into the BFF,
  which is arithmetic the BFF should not own.

---

## Decision 8 — Orders computes the total from the lines it is sent, and stores only the total

**Decision**: `POST /orders` accepts the basket's line items (product, quantity, unit price). The
orders service computes the order total from them and persists an order carrying its identifier,
placed-at instant, and total. Order line items are not persisted.

**Rationale**: This keeps every monetary computation inside a domain service. The BFF never
multiplies or sums, so no reviewer has to argue about whether summing a basket is "business logic".
The spec's Assumptions explicitly exclude order line items from this feature's minimum surface, and
no acceptance criterion needs them — the confirmation shows a reference and a total (FR-009).

**Alternatives considered**:

- *The BFF computes the total and posts it*: rejected — that is exactly the business logic spec 002
  FR-005 forbids the BFF from holding.
- *Persist order line items too*: rejected as scope the spec deliberately excluded; orders gains
  them with the story that owns them.

---

## Decision 9 — Checkout is a BFF-orchestrated two step, guarded by an empty-basket rule

**Decision**: `POST /bff/checkout` reads the caller's current basket, posts its lines to the orders
service, then clears the basket, and returns the created order. The baskets service rejects a
checkout-clear for a basket that is already empty, and the orders service rejects a place-order
request carrying no lines.

**Rationale**: This is a multi-service workflow, and Principle IV says such workflows must be sagas
with explicit compensation rather than distributed transactions. No messaging infrastructure exists
in this repository at all — no MassTransit package, no RabbitMQ container, no outbox table — and the
roadmap places event schemas at SCRUM-18 (Phase 2) and outbox verification at SCRUM-31 (Phase 4).
Building that infrastructure here would multiply this feature's size several times over.

The order is created first deliberately: an order that exists with a basket that failed to clear is
recoverable by the shopper (they see their confirmation), whereas clearing first and then failing to
create the order loses their basket with nothing to show for it.

**Residual risk, accepted and recorded**: if clearing fails after the order is created, the basket
keeps its items and a second checkout would create a second order. FR-016's guarantee therefore
rests on the in-flight guard in the storefront plus the empty-basket rejection, not on a
compensating transaction. Carried in Complexity Tracking as a time-bound Principle IV deviation
closed by SCRUM-18/SCRUM-31.

**Alternatives considered**:

- *Stand up RabbitMQ + MassTransit and publish `BasketCheckedOut`*: rejected for this feature —
  correct destination, wrong phase; it is exactly what SCRUM-18 and SCRUM-31 exist to do.
- *An idempotency key on place-order*: rejected as the cheaper guard is already required — the
  storefront must disable the control while in flight (spec Edge Cases), and an emptied basket
  cannot be checked out again.

---

## Decision 10 — Catalog seeding via an EF Core migration with fixed identifiers

**Decision**: Seed three products into the products catalog using EF Core's `HasData` in a new
migration, with hardcoded `Guid` values.

**Rationale**: FR-018 requires at least one purchasable product in every environment where the flow
is demonstrated, "without manual data setup". `HasData` makes seeding part of the migration history,
so it is deterministic, applied exactly once, and rolls back with the migration — which is what
Principle X's expand/contract rule wants. Fixed identifiers let the Playwright and integration tests
assert against known products rather than whatever the database happened to generate. Three rather
than one, so a basket with more than a single line and a non-trivial total is demonstrable.

**Alternatives considered**:

- *A startup seeder*: rejected — it runs on every boot, needs its own idempotency logic, and would
  need a resolved tenant context at startup, which by design does not exist outside a request.
- *Seed only in tests*: rejected — FR-018 is about the demonstrated environment, not the test suite.

---

## Decision 11 — Storefront reaches the gateway only, through one configured origin

**Decision**: The SPA is configured with a single backend origin (the gateway, `http://localhost:5300`
in local development) supplied by Vite environment configuration, and the generated client is bound
to it. Nothing in the app addresses the BFF or any domain service directly.

**Rationale**: FR-014 and SC-010 require every request to reach the single backend surface. The
gateway already forwards `{**catch-all}` to the BFF and is the only hop that resolves identity, so
addressing the BFF directly would bypass tenant and subject resolution entirely and hit the
services' `RequireTenantId()` gate. One configured origin also makes SC-010 checkable: any request
to another host is a failure by inspection.

**Alternatives considered**:

- *Vite dev-server proxy in development*: rejected as the primary mechanism — it makes development
  behave unlike production and would let a hardcoded service URL pass unnoticed locally. A proxy may
  still be used for convenience, but the app's configured origin remains the gateway.

---

## Finding — Schema-per-tenant was specified in 003 but is not in the code

**This is not a decision; it is a discrepancy found while researching, and it needs a decision from
the maintainers.**

Spec 003's plan states that each domain service's database connection is resolved from the tenant
context "via schema-per-tenant on the existing per-service database", and names EF Core's
`HasDefaultSchema` as the mechanism. All 39 of that feature's tasks are marked complete.

In the repository today, `HasDefaultSchema` appears nowhere, no migration declares a schema, and
each `DbContext` maps to the default `dbo`. What was built is the *gate* — `RequireTenantId()` at
the single `AddDbContext` call site in each service, which does correctly make a tenant-less request
fail rather than serve data. What was not built is the *separation*: every tenant's rows would share
one set of tables.

Constitution Principle V requires that "each tenant's data MUST reside in a separate database or
schema per service" and calls cross-tenant exposure a Severity-1 security defect. With one tenant
configured (`contoso`), nothing is exposed today, so this is a latent gap rather than a live breach.

It matters *here* because this feature creates the first genuinely tenant-owned business data —
basket contents and placed orders. Every table this feature adds inherits the gap.

Closing it is a contained change (resolve the schema from the tenant context at each `AddDbContext`
call site, plus a migration per service), but it is not in this feature's spec and adding it
unilaterally would widen scope the spec's clarification session deliberately bounded. It is recorded
in the Constitution Check and Complexity Tracking, and raised for a decision rather than silently
adopted or silently ignored.
