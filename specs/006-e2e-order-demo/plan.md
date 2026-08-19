# Implementation Plan: End-to-End Order Demo — Phase 1 Exit Proof

**Branch**: `006-e2e-order-demo` | **Date**: 2026-08-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-e2e-order-demo/spec.md`

## Summary

Phase 1 claims a walking skeleton exists. This feature turns that claim into evidence: one command
that drives a real order through the deployed container stack, reads it back from the orders service
showing which tenant it belongs to, proves every named hop served traffic, and leaves behind a
committed walkthrough with stills that a reviewer joining in Phase 4 can read without standing
anything up.

Three things get built. **One persisted field** — the order now records the tenant resolved for the
request that placed it, taken from the tenant context and never from the request body. **One demo
command** — `scripts/demo.ps1` / `demo.sh`, joining the `up`/`down`/`reset` family, layering a small
Compose override that publishes the internal ports and raises collector verbosity for the run.
**One document** — `docs/demo-phase-1.md`, the procedure, the hops, and the exit-criteria mapping.

The storefront does not change. The client-facing BFF contract does not change. No pipeline changes.

## Technical Context

**Language/Version**: C# / .NET 10 for the one service change; TypeScript for the Playwright demo
project; PowerShell and Bash for the command pair. No new language.

**Primary Dependencies**: Nothing new. Playwright is already a devDependency of
`@ecommerce/web`; the OTel collector, the Compose override pattern, and the `--wait` health gate all
already exist from 005.

**Storage**: One additive, nullable column on `orders.Orders`. One EF Core migration,
`AddOrderTenantId`. No data migration, no backfill.

**Testing**: Orders unit tests for `Order.PlaceFrom` rejecting a blank tenant; orders integration
tests against the existing `SqlServerFixture` (Testcontainers) for tenant persisted and returned, and
for the no-tenant path creating nothing; the Playwright demo project as the end-to-end proof. Written
before the code they cover — Principle III is non-negotiable and this feature has no exemption.

**Target Platform**: The 005 container stack on a contributor's machine — Windows, macOS, or Linux
with Docker. Kubernetes is explicitly out of scope (spec Assumptions).

**Project Type**: Web platform — one backend service change plus developer-facing tooling and
documentation. No shopper-facing behaviour changes.

**Performance Goals**: A repeat demo run (`-SkipStart`) completes in under 90 seconds, so re-running
stays cheap enough that people actually re-run it (FR-007, SC-001's 5-minute human budget covers the
manual path). Cold start inherits 005's under-10-minute first-run budget.

**Constraints**: Committed evidence must be small — stills only, no video in git (spec
Clarifications). The default stack's published-port surface must not widen; demo mode is an override
applied only by the demo command. The client-facing contract must not gain a field no screen shows.

**Scale/Scope**: 1 entity field · 1 migration · 1 downstream contract update propagated to 3 YAML
documents · 1 Compose override · 1 collector config variant · 2 scripts · 1 Playwright project ·
1 walkthrough document · 4 stills.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see the bottom of this file.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| I. Service Autonomy and Bounded Context | The field is added by the orders service to its own record, in its own database. The verification step reads it through that service's HTTP surface, never its database — the spec's own assumption forbids the shortcut, and the demo should model the rule it teaches. | PASS |
| II. Contract-First Integration | [contracts/orders-openapi.yaml](./contracts/orders-openapi.yaml) is written before the code. The change is additive, so it is non-breaking under the tolerate-unknown-fields rule and needs no version window. The demo's own interface is fixed up front in [contracts/demo-interface.md](./contracts/demo-interface.md). | PASS |
| III. Test-First Development | Unit and integration tests for the tenant field land before the field. The demo's assertions are written before the demo passes. No hand-rolled infrastructure fakes — the existing Testcontainers `SqlServerFixture` is reused. | PASS |
| IV. Event-Driven by Default | Nothing published, nothing consumed, no outbox. Unchanged from the deviation ADR-0011 records; this feature neither widens nor narrows it. | DEVIATION (carried, tracked by SCRUM-18/31) |
| V. Tenant Isolation Is a Security Boundary | Improved, but the underlying gap stands. The tenant is now recorded on the row from the resolved context, never from the body. **Schema-per-tenant separation still does not exist** — one fixed connection serves every request, and what actually enforces tenancy is the `RequireTenantId()` gate. See the finding in research.md. | FAIL (pre-existing) — see Complexity Tracking |
| VI. Secure by Default | Demo mode publishes internal ports on a local machine only, by explicit opt-in, exactly as the existing debug override already does. No new secret, no new credential. The stub-identity deviation is carried unchanged. | DEVIATION (carried, tracked by SCRUM-23) |
| VII. Observable by Default | The demo consumes the telemetry `ServiceDefaults` already emits rather than adding instrumentation — the first thing in this repository to actually read those spans back. Elastic remains Phase 3. | PASS |
| VIII. Performance and Resilience Budgets | No service budget changes. The demo declares its own repeat-run budget (above) and fails loudly on an unreachable dependency rather than hanging — the existing downstream timeout and resilience policies apply unchanged. | PASS |
| IX. Frontend Discipline | No storefront source change, no new component, no API client regeneration — the client-facing contract is deliberately untouched. The demo adds a Playwright project alongside the existing walkthrough, not a rewrite of it. | PASS |
| X. Toggle-Gated, Reversible Delivery | The migration is expand-only: a nullable column, no default, no destructive step, so the previous version keeps running against the new schema. Rollback is `git revert` plus the down migration. No toggle — a stored attribution field with no behaviour attached has nothing to gate. | PASS |

**Gate result**: proceed, with three entries in Complexity Tracking. None is new. The Principle V
failure is the same one 004 and 005 both recorded; this feature makes it *more* visible by writing
the tenant down, which is an argument for closing it, not a reason to block here.

## Project Structure

### Documentation (this feature)

```text
specs/006-e2e-order-demo/
├── plan.md                          # This file
├── research.md                      # Phase 0 — 12 decisions + 1 finding
├── data-model.md                    # Phase 1 — the one persisted field, and the run's outputs
├── quickstart.md                    # Phase 1 — 9 validation scenarios
├── contracts/
│   ├── orders-openapi.yaml          # the orders service delta: Order gains tenantId
│   └── demo-interface.md            # the demo command, addresses, outputs
├── checklists/requirements.md       # spec quality checklist (16/16)
└── tasks.md                         # Phase 2 — created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
services/orders/src/Orders.Api/
├── Data/Order.cs                                    # CHANGED — TenantId; PlaceFrom takes a tenant
├── Data/OrdersDbContext.cs                          # CHANGED — column config, max length 128
├── Migrations/<stamp>_AddOrderTenantId.cs           # NEW — additive, nullable
└── Features/Orders/OrderEndpoints.cs                # CHANGED — supply tenant on write, return on read

services/orders/tests/
├── Orders.Api.UnitTests/OrderTotalTests.cs          # CHANGED — PlaceFrom's new parameter
├── Orders.Api.UnitTests/OrderTenantTests.cs         # NEW — blank tenant rejected
├── Orders.Api.IntegrationTests/PlaceOrderTests.cs   # CHANGED — tenant persisted
└── Orders.Api.IntegrationTests/OrderEndpointsTests.cs  # CHANGED — tenant returned on read

docker-compose.demo.yml                              # NEW — publishes internal ports, detailed telemetry
docker/otel-collector-config.demo.yaml               # NEW — verbosity: detailed
scripts/demo.ps1                                     # NEW — the one command
scripts/demo.sh                                      # NEW — its POSIX twin

frontend/apps/web/
├── playwright.demo.config.ts                        # NEW — targets :4173, video + stills, no webServer
└── demo/order-demo.spec.ts                          # NEW — the recorded flow

docs/
├── demo-phase-1.md                                  # NEW — the written walkthrough
├── demo/*.png                                       # NEW — committed stills
└── local-development.md                             # CHANGED — links the demo command and walkthrough

.gitignore                                           # CHANGED — artifacts/

specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml   # CHANGED — Order.tenantId
specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml  # CHANGED — Order.tenantId
```

**Structure Decision**: the demo command lives in `scripts/` beside `up`/`down`/`reset` because it is
the same kind of thing — a contributor-facing operation that wraps Compose and does the checking
Compose cannot. The Playwright demo lives in `frontend/apps/web/demo/`, a sibling of `e2e/` rather
than a file inside it, so the existing walkthrough keeps its own config, its own target, and its own
reason to exist (research Decision 1). The walkthrough document goes to `docs/` rather than into this
spec folder: spec folders are read by people planning work, and this artifact is for people asking
what Phase 1 delivered.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Principle V** — no schema-per-tenant separation (pre-existing, carried from 004/005) | Phase 1 runs one tenant and closing this is a story of its own. This feature writes the tenant onto the record, which makes the gap legible and gives future separation something to route on | Implementing schema-per-tenant here would be a multi-service change to four databases, unrelated to demonstrating one order. It needs its own story, not a rider on a demo |
| **Principle IV** — no events, no outbox (carried, ADR-0011) | Checkout stays a synchronous three-call orchestration. Nothing in this feature publishes anything | Introducing messaging to record a demo would add a broker dependency to the thing whose whole purpose is to prove the existing path works |
| **Principle VI** — stub identity, single hardcoded tenant (carried) | The tenant the demo verifies is `contoso` from `StubIdentity`. Real token issuance is SCRUM-23 | Standing up the identity server to prove tenant attribution would replace the thing being demonstrated with a larger untested thing |

All three are carried forward unchanged and are tracked by named stories. This feature adds no new
deviation.

## Post-Design Constitution Re-Check

Re-run after Phase 1 produced the contracts, the data model, and the quickstart.

- **Principle II holds under design.** The additive `tenantId` is confined to the downstream contract.
  The client-facing document is untouched, so no generated client changes and no consumer is forced
  to move. Three YAML documents describe the downstream `Order`; the plan updates all three together,
  because two agreeing and one disagreeing is worse than the original problem.
- **Principle III holds under design.** Every assertion the demo makes has a cheaper test underneath
  it: the tenant field is covered by unit and integration tests, and the demo proves the composition
  rather than substituting for unit coverage.
- **Principle V's verdict is unchanged and its description is now accurate.** The spec assumed
  tenant-scoped stores were the enforcement boundary; research.md's finding shows the resolution gate
  is. The plan does not quietly adopt the spec's wording — FR-005b's intent (don't weaken what
  enforces isolation) is met, since nothing here touches the gate.
- **Principle X holds under design.** The nullable column means a rollback to the previous service
  version runs against the new schema without error. The tightening to `NOT NULL` is a separate
  contract migration and is deliberately not in this feature.
- **One design consequence worth flagging for `/speckit-tasks`:** the collector verbosity decision
  (research Decision 6) rests on `detailed` printing the `service.name` resource attribute. That is
  the documented behaviour but has not been observed in this repository. The task list should verify
  it empirically **before** the hop-evidence assertion is written, so the fallback — parsing spans
  from a different field — is discovered while it is cheap rather than at the end.

**Gate result after design: proceed to `/speckit-tasks`.**
