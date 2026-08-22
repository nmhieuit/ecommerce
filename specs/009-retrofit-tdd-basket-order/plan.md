# Implementation Plan: Retrofit TDD for Basket Pricing and Order Creation

**Branch**: `009-retrofit-tdd-basket-order` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-retrofit-tdd-basket-order/spec.md`

## Summary

Codebase audit during planning found that the six behavioral rules this feature specifies
(FR-001–FR-006: basket quantity floor, price-at-add-time stability, line deduplication, empty-order
rejection, invalid-line rejection, total computed not accepted) are **already implemented and
already unit-tested**, in `Basket.cs` / `BasketLineItem.cs` (`Baskets.Api.UnitTests`) and `Order.cs`
(`Orders.Api.UnitTests`), with matching integration coverage in `PlaceOrderTests.cs`. There is no
behavioral gap to build.

The actual gap is what the source ticket's Test Scenario 1 is checking for: `git log` on
`Basket.cs`, `BasketLineMergeTests.cs`, `Order.cs`, and `OrderTotalTests.cs` shows the implementation
and its tests landing together inside single large feature commits (`1bc77a6`, `c99783c`,
`b3873b5`) — a working style, not a broken one, but not one that makes Red-Green-Refactor visible
commit-by-commit the way Principle III and this ticket's AC1 ask for. Rewriting that history to
fabricate a red/green order it didn't actually happen in would misrepresent authorship and is
excluded by this repository's own git-safety rules regardless. This plan therefore scopes to closing
the gap going forward and making today's already-correct state auditable rather than re-implementing
working code:

1. A short, linked practice note (`docs/engineering/test-first-commits.md`) stating the rule this
   ticket exists to enforce for future changes: a commit touching basket-pricing or order-creation
   logic must be preceded or accompanied by a failing test that the change makes pass, and must not
   arrive as a same-day-or-later "add tests" follow-up commit.
2. A `quickstart.md` verification guide a reviewer or the assignee can run to produce the exact
   evidence Test Scenarios 1–3 ask for: the current unit test suite passing, each rule's test failing
   when the rule is reverted (Test Scenario 2), and the empty-basket domain-layer rejection holding
   (Test Scenario 3).
3. `data-model.md` recording the four entities this story is about and, for each rule, its
   already-passing test — an audit trail, not a design for new state.

No `contracts/` output: this feature adds no new externally reachable interface (no new endpoint,
no new event, no request/response shape change) for any consumer to integrate against.

## Technical Context

**Language/Version**: C# 13 / .NET 10, matching `services/baskets` and `services/orders`
(`Directory.Build.props`) — unchanged, no new project.

**Primary Dependencies**: None added. Reuses the existing `Baskets.Api.UnitTests` and
`Orders.Api.UnitTests` xUnit projects and the existing `Baskets.Api.IntegrationTests` /
`Orders.Api.IntegrationTests` Testcontainers (SQL Server) projects.

**Storage**: N/A — no schema change; `Basket`, `BasketLineItem`, `Order` are unchanged.

**Testing**: xUnit (existing `*.UnitTests` projects) for the domain rules; existing
`PlaceOrderTests.cs` / `ClearBasketTests.cs` Testcontainers integration tests already exercise the
same rules over HTTP. This feature verifies and documents that coverage rather than adding a new
test layer.

**Target Platform**: Existing `Baskets.Api` and `Orders.Api` services — no deployment or runtime
change.

**Project Type**: Documentation/verification addition to the existing multi-service backend
(`Ecommerce.slnx`). No new project, no new service.

**Performance Goals**: N/A — no runtime code path is touched.

**Constraints**: MUST NOT rewrite or rebase existing commit history to simulate a red/green order
that did not occur — that would fabricate authorship history, which this repository's operating
rules already exclude without explicit, scoped user authorization, and which the constitution's
Governance section would treat as an undocumented, unauditable change in its own right. The retrofit
therefore applies to future commits, not past ones.

**Scale/Scope**: Two services already covered (`baskets`, `orders`); zero new services, zero new
endpoints; one new short practice-note document; `data-model.md` and `quickstart.md` as this
feature's design artifacts; no `contracts/` (no new interface).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| III. Test-First Development (NON-NEGOTIABLE) | This feature exists to close the auditability gap in this principle for basket-pricing and order-creation commits specifically. The behavioral rules it covers are confirmed already red-green-tested at both unit and integration level (Summary); what this feature adds is the going-forward commit-discipline practice and the audit procedure Principle III's compliance review needs to check it. **PASS**. |
| I. Service Autonomy and Bounded Context | Not implicated: no service boundary, database ownership, or internal architecture changes. Both services keep their existing vertical-slice structure. **N/A**. |
| II. Contract-First Integration | Not implicated: no HTTP or event contract is added or changed. **N/A**. |
| IV. Event-Driven by Default | Not implicated: no publisher, consumer, or broker interaction is added. **N/A**. |
| V. Tenant Isolation | Not implicated: existing tenant-attribution rules on `Order` are unchanged and already covered by `OrderTenantTests.cs` / `TenantEnforcementTests.cs`, outside this feature's scope. **N/A**. |
| VI. Secure by Default | Not implicated: no authorization policy, input-validation surface, or secret handling changes. **N/A**. |
| VII. Observable by Default | Not implicated: no telemetry surface changes. **N/A**. |
| VIII. Performance and Resilience Budgets | Not implicated: no runtime call path changes; nothing to budget. **N/A**. |
| IX. Frontend Discipline | Not implicated: no frontend change. **N/A**. |
| X. Toggle-Gated, Reversible Delivery | Not implicated: no runtime behavior changes, so there is nothing to gate behind a toggle — the practice note and audit guide are documentation, not shipped code. **N/A**. |

No violations. Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/009-retrofit-tdd-basket-order/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

No `contracts/` directory: this feature introduces no new externally reachable interface.

### Source Code (repository root)

```text
docs/
└── engineering/
    └── test-first-commits.md   # New: the going-forward commit-discipline practice note (Summary #1)

services/baskets/
├── src/Baskets.Api/Data/Basket.cs             # Unchanged — audited, already correct (FR-001–003)
├── src/Baskets.Api/Data/BasketLineItem.cs     # Unchanged — audited
└── tests/Baskets.Api.UnitTests/
    ├── BasketLineMergeTests.cs                # Unchanged — already proves FR-001–003
    └── BasketTotalTests.cs                    # Unchanged — already proves total computation

services/orders/
├── src/Orders.Api/Data/Order.cs               # Unchanged — audited, already correct (FR-004–006)
└── tests/Orders.Api.UnitTests/
    ├── OrderTotalTests.cs                     # Unchanged — already proves FR-004–006
    └── OrderTenantTests.cs                    # Unchanged — out of this feature's scope
```

**Structure Decision**: No new project and no production code paths change. The one net-new file is
`docs/engineering/test-first-commits.md`, placed alongside the pattern this repository already uses
for durable engineering documentation (`docs/adr/`), but as a practice note rather than an ADR since
it operationalizes an existing constitutional principle (III) for one code area instead of recording
a new architectural decision. Everything else under `services/baskets` and `services/orders` is
referenced for audit purposes only — listed so `/speckit-tasks` can point each verification task at
the exact file and test method it is confirming, not because any of it is modified.

## Complexity Tracking

*No violations — table intentionally omitted.*
