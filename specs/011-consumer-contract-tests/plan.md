# Implementation Plan: Consumer-Driven Contract Tests Across BFF/Service Boundaries

**Branch**: `011-consumer-contract-tests` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-consumer-contract-tests/spec.md`

## Summary

Add consumer-driven contract tests for the four boundaries named in the thin slice: the three
BFF→service HTTP boundaries (BFF↔products, BFF↔baskets, BFF↔orders) and one piloted event boundary
(`baskets`, publisher of `BasketCheckedOut` ↔ `orders`, its intended consumer). Each producer
(`products`, `baskets`, `orders`) gets a Pact provider-verification test in its own test project so
a breaking response/event-shape change fails that service's own build, per Principle III and
ADR-0006. Pact files are exchanged as committed, file-based artifacts (no Pact Broker stood up yet
— ADR-0006 Action Item 1 remains separate follow-up work) so the boundary inventory is auditable by
listing files, satisfying User Story 3. The event pilot verifies the `BasketCheckedOutV1` payload
directly against a consumer-defined pact, with no MassTransit dependency, since no service publishes
or consumes it yet (matches the `010-testcontainers-integration-tests` precedent of not introducing
MassTransit ahead of a real publisher).

## Technical Context

**Language/Version**: C# / .NET 10 (matches every existing service and test project)

**Primary Dependencies**: xUnit (existing), new `PactNet` `5.0.1` package (HTTP pact + message pact
APIs) added to `Directory.Packages.props`, referenced only by new test projects; existing
`EventContracts` record types (`BasketCheckedOutV1`) referenced directly by the event pilot's tests

**Storage**: N/A — no persistence added; Pact files are JSON documents committed under a new
repo-root `pacts/` directory, not a database

**Testing**: xUnit contract-test projects per service (`*.Api.ContractTests`), following the
existing `*.Api.IntegrationTests` project shape (`Directory.Packages.props`-managed versions,
`ProjectReference` to the service's own `*.Api` project)

**Target Platform**: Local developer machines and Jenkins CI — contract tests run in-process
(`WebApplicationFactory`-hosted provider, or a directly-invoked payload builder for the event pilot).
The three HTTP provider suites do need a Docker daemon for their SQL Server fixture: each service's
`DbContext` is gated on a resolved tenant and registered against SQL Server, so an in-process host
still needs a real database to answer a route (research.md Decision 5, as amended during
implementation). The event pilot needs neither.

**Project Type**: Backend test infrastructure inside the existing multi-project .NET solution
(`Ecommerce.slnx`) — no new runnable service, no frontend impact

**Performance Goals**: N/A (build-time test verification, not a runtime request path; the
constitution's performance budgets in Principle VIII govern runtime behavior, not contract tests)

**Constraints**: Contract tests MUST exercise real producer behavior, not a hand-maintained double
(FR-006); an added, consumer-unreferenced field MUST NOT fail verification (FR-007, tolerant-reader
rule); no MassTransit or live broker dependency introduced (matches Assumptions and the
`010-testcontainers-integration-tests` precedent); no Pact Broker stood up as part of this feature
(ADR-0006 Action Item 1 is separate infrastructure work, not gated on here)

**Scale/Scope**: 4 boundaries — 3 HTTP consumer/provider pairs (BFF as consumer; products, baskets,
orders each as provider) and 1 event consumer/provider pair (orders as consumer, baskets as
provider, for `BasketCheckedOut`); one new consumer-side test project (`Bff.Api.ContractTests`),
three new/extended provider-side test projects (`Products.Api.ContractTests`,
`Baskets.Api.ContractTests`, `Orders.Api.ContractTests`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I (Service Autonomy)**: Contract tests read each service's own real HTTP/event
  behavior in-process; no test reads or writes another service's database. New test projects live
  under each owning service's own `tests/` directory (consistent with existing `*.IntegrationTests`
  placement) — no shared runtime coupling introduced. **PASS**.
- **Principle II (Contract-First Integration)**: This feature directly implements the "consumer
  tolerates unknown fields" and contract-verification requirements of this principle for the HTTP
  and event boundaries in scope. It does not touch the BFF's downstream HTTP clients
  (`ProductsApiClient`, etc.) or whether they are generated vs. hand-written — that is a pre-existing
  condition outside this feature's scope, not modified here. **PASS** (for the scope this feature
  owns).
- **Principle III (Test-First, NON-NEGOTIABLE)**: This feature *is* the "consumer-driven contract
  tests for every HTTP and event boundary" requirement named explicitly in this principle. Each new
  contract test is written test-first: authored to fail against a deliberately broken shape before
  the real code is confirmed to satisfy it (Test Scenario 1). **PASS**.
- **Principle IV (Event-Driven by Default)**: The event pilot tests a payload shape, not a live
  publish/consume flow — no outbox, no MassTransit, no RabbitMQ dependency is added, matching the
  documented reality that no service publishes or consumes `BasketCheckedOut` yet (spec.md
  Assumptions; ADR-0011). This is consistent with, not a violation of, the constitution: there is no
  live event flow to break by omission. **N/A** for live delivery; **PASS** for the payload-contract
  requirement this principle also implies.
- **Principle V (Tenant Isolation)**: Not applicable — no tenant-scoped data path is touched; sample
  payloads used in contract tests carry no real tenant data. **N/A**.
- **Principle VI (Secure by Default)**: Not applicable — no new externally reachable endpoint or
  authorization policy is added. **N/A**.
- **Principle VII (Observable by Default)**: Not applicable — contract tests are build-time, not a
  running service; no telemetry surface is added. **N/A**.
- **Principle VIII (Performance and Resilience Budgets)**: Not applicable — no runtime request path
  is added. **N/A**.
- **Principle X (Toggle-Gated, Reversible Delivery)**: Not applicable — this feature adds tests and
  CI-time verification only; it changes no runtime, user-facing behavior for a feature toggle to
  gate. **N/A**.
- **PR gate** ("build → unit → integration → contract tests → SonarQube → vulnerability scan"): This
  feature is what makes the "contract tests" stage of that gate non-empty for the first time.
  Wiring the new `*.Api.ContractTests` projects into the existing test-run step of the pipeline is
  in scope; standing up a Pact Broker for `can-i-deploy`-style gating (ADR-0006 Action Item 1) is
  explicitly deferred — documented here, not silently dropped.

No violations requiring the Complexity Tracking table.

**Post-Design Re-check** (after Phase 0/1 artifacts below): The chosen design — file-based Pact
exchange under `pacts/`, one consumer-side project (BFF) and three provider-side projects
(products, baskets, orders), the event pilot invoking the payload-construction path directly rather
than through a broker (research.md Decisions 1–4) — introduces no new gate concerns. Still **PASS**
on Principles I, II, III; still **N/A** on IV (live delivery), V, VI, VII, VIII, X. The Pact Broker
gap remains explicitly deferred rather than silently resolved.

## Project Structure

### Documentation (this feature)

```text
specs/011-consumer-contract-tests/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/            # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
pacts/                                    # New: committed, file-based Pact documents (the audit
├── bff-products.json                     # trail for User Story 3) — one file per boundary
├── bff-baskets.json
├── bff-orders.json
└── orders-basketcheckedout.json          # Event pilot: orders (consumer) ↔ baskets (provider)

services/bff/tests/Bff.Api.ContractTests/          # New: consumer-side HTTP pact tests
├── Bff.Api.ContractTests.csproj
├── ProductsConsumerPactTests.cs                    # Defines BFF's expectations of products
├── BasketsConsumerPactTests.cs                     # Defines BFF's expectations of baskets
└── OrdersConsumerPactTests.cs                      # Defines BFF's expectations of orders

services/products/tests/Products.Api.ContractTests/  # New: provider-side verification
├── Products.Api.ContractTests.csproj
└── ProductsProviderPactTests.cs                      # Verifies pacts/bff-products.json

services/baskets/tests/Baskets.Api.ContractTests/     # New: provider-side verification
├── Baskets.Api.ContractTests.csproj
├── BasketsProviderPactTests.cs                       # Verifies pacts/bff-baskets.json (HTTP)
└── BasketCheckedOutProviderPactTests.cs              # Verifies pacts/orders-basketcheckedout.json
                                                       # (event pilot, payload-only)

services/orders/tests/Orders.Api.ContractTests/       # New: provider-side + event consumer-side
├── Orders.Api.ContractTests.csproj
├── OrdersProviderPactTests.cs                        # Verifies pacts/bff-orders.json (HTTP)
└── BasketCheckedOutConsumerPactTests.cs              # Defines orders' expectation of
                                                       # BasketCheckedOut (event pilot)
```

**Structure Decision**: Each new `*.Api.ContractTests` project lives beside its service's existing
`*.Api.IntegrationTests` project, following the same `Directory.Packages.props`-managed,
`ProjectReference`-to-the-service pattern already established (see
`services/products/tests/Products.Api.IntegrationTests/Products.Api.IntegrationTests.csproj` for the
template). Splitting consumer-side (BFF, orders for the event pilot) from provider-side
(products, baskets, orders) tests mirrors Pact's own consumer/provider vocabulary and keeps each
producer's "own build" (FR-001–FR-005) limited to the provider-side project it owns. A single
top-level `pacts/` directory — rather than one per service — is the artifact User Story 3's coverage
audit lists directly (data-model.md `Boundary`), and keeps the file-based exchange (research.md
Decision 2) in one discoverable place instead of scattered per-project output folders.

## Complexity Tracking

*No violations — table not needed.*
