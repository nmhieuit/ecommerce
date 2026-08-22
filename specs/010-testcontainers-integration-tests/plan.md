# Implementation Plan: Testcontainers Integration Test Infrastructure

**Branch**: `010-testcontainers-integration-tests` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-testcontainers-integration-tests/spec.md`

## Summary

Audit the existing SQL Server Testcontainers pattern (already live in `baskets`, `orders`,
`parties`, `products`) to prove it still catches a real constraint violation, and add two new
reusable Testcontainers fixtures — Redis and RabbitMQ — that any service's integration test
project can reference once that service adopts Redis caching or RabbitMQ messaging. No production
caching or messaging code is added; this is test infrastructure only (per user scope decision:
"test-infra only, no new features").

## Technical Context

**Language/Version**: C# / .NET 10 (matches every existing service and test project)

**Primary Dependencies**: xUnit (existing), `Testcontainers.MsSql` (existing, no version change),
new `Testcontainers.Redis` and `Testcontainers.RabbitMq` packages added to
`Directory.Packages.props`, `StackExchange.Redis` (Redis smoke-test client) and `RabbitMQ.Client`
(RabbitMQ smoke-test client) added as test-only dependencies

**Storage**: N/A — SQL Server, Redis, and RabbitMQ are the systems under test via ephemeral
containers, not application storage owned by this feature

**Testing**: xUnit integration test projects per service (`*.Api.IntegrationTests`), following the
existing `IAsyncLifetime`-based fixture pattern in `SqlServerFixture.cs`

**Target Platform**: Local developer machines and Jenkins CI, both requiring a reachable Docker
daemon — identical assumption to the existing SQL Server fixture

**Project Type**: Backend test infrastructure inside the existing multi-project .NET solution
(`Ecommerce.slnx`) — no new runnable service, no frontend impact

**Performance Goals**: N/A (test infrastructure, not a user-facing runtime path); the only timing
requirement is SC-004's 30-second bound on detecting a killed RabbitMQ container

**Constraints**: Zero production code changes (FR-009); fixtures must be usable by future features
without modification (SC-005); container failures must fail the run loudly, never skip silently
(FR-007)

**Scale/Scope**: Audit 4 existing SQL Server integration test projects (`baskets`, `orders`,
`parties`, `products`); add 1 new shared Redis fixture, 1 new shared RabbitMQ fixture, and smoke
tests proving each is reachable and fails loudly when unhealthy

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I (Service Autonomy)**: The new Redis and RabbitMQ fixtures are test infrastructure,
  not runtime data access — they don't read or write another service's database. Placing them in a
  new shared library (`shared/`, alongside the existing `shared/EventContracts`) so every service's
  integration test project can reference the same fixture without duplicating it is consistent with
  precedent and does not compromise per-service data ownership. **PASS**.
- **Principle II (Contract-First Integration)**: Not applicable — this feature adds no HTTP or
  event contract. **N/A**.
- **Principle III (Test-First, NON-NEGOTIABLE)**: This feature *is* the Testcontainers requirement
  the constitution names explicitly ("Integration tests exercising real dependencies via
  Testcontainers (SQL Server, Redis, RabbitMQ)"). Every task is itself a test or a test fixture; the
  audit tasks (User Story 1) follow revert-and-confirm-red discipline matching feature
  `009-retrofit-tdd-basket-order`. **PASS**.
- **Principle IV (Event-Driven by Default)**: Not applicable — no publisher or consumer is added.
  The RabbitMQ fixture only proves connectivity and failure behavior; it carries no message
  contract. **N/A**.
- **Principle V (Tenant Isolation)**: Not applicable — no tenant-scoped data path is touched.
  **N/A**.
- **Technology constraint** ("Redis backs the basket store", "Messaging: RabbitMQ via MassTransit"):
  This feature does not yet wire either into production, which is a real gap between the
  constitution's standing technology decision and current implementation. It is explicitly out of
  scope here (spec.md Assumptions) and remains open as future work — flagged, not silently ignored.

No violations requiring the Complexity Tracking table.

**Post-Design Re-check** (after Phase 0/1 artifacts below): The chosen design — a shared
`shared/IntegrationTestSupport` library plus smoke tests, zero production dependency changes
(Decisions 2, 3, 6 in research.md) — introduces no new gate concerns. Still **PASS** on Principles
I and III, still **N/A** on II/IV/V, and the technology-constraint gap (Redis/RabbitMQ named in the
constitution but not yet wired into any service) remains explicitly out of scope rather than
silently resolved or silently ignored.

## Project Structure

### Documentation (this feature)

```text
specs/010-testcontainers-integration-tests/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
└── IntegrationTestSupport/
    ├── IntegrationTestSupport.csproj
    ├── RedisFixture.cs         # New: IAsyncLifetime wrapper over Testcontainers.Redis
    └── RabbitMqFixture.cs      # New: IAsyncLifetime wrapper over Testcontainers.RabbitMq

shared/IntegrationTestSupport.Tests/
├── IntegrationTestSupport.Tests.csproj
├── RedisFixtureTests.cs        # New: smoke test — real read/write round trip (FR-005)
└── RabbitMqFixtureTests.cs     # New: smoke tests — connectivity (FR-006) + mid-test kill (FR-008)

services/baskets/tests/Baskets.Api.IntegrationTests/    # Audited only (User Story 1), no new files
services/orders/tests/Orders.Api.IntegrationTests/      # Audited only (User Story 1), no new files
services/parties/tests/Parties.Api.IntegrationTests/    # Audited only (User Story 1), no new files
services/products/tests/Products.Api.IntegrationTests/  # Audited only (User Story 1), no new files
```

**Structure Decision**: A new shared library, `shared/IntegrationTestSupport`, holds the reusable
Redis and RabbitMQ fixtures — mirroring how `shared/EventContracts` already holds the reusable
event schemas referenced by multiple services. Its own `shared/IntegrationTestSupport.Tests`
project hosts the smoke tests that prove the fixtures work (FR-005, FR-006, FR-008), independent of
any single service. User Story 1's SQL Server audit touches no new files — it exercises the four
existing per-service `SqlServerFixture`-based integration test projects via temporary,
reverted-in-place constraint removals (same technique as `009-retrofit-tdd-basket-order`).

## Complexity Tracking

*No violations — table not needed.*
