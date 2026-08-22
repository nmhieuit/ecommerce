# Quickstart: Validating Testcontainers Integration Test Infrastructure

## Prerequisites

- Repository checked out.
- .NET 10 SDK installed.
- Docker available and running — every scenario below starts real containers.

## Scenario 1 — Containers are visible for the whole run (Jira Test Scenario 1; SC-001)

In one terminal, start a longer-running slice of the suite and watch containers appear:

```bash
dotnet test services/baskets/tests/Baskets.Api.IntegrationTests &
dotnet test shared/IntegrationTestSupport.Tests
```

In a second terminal, while the above is running:

```bash
docker ps
```

**Expected**: a `mcr.microsoft.com/mssql/server` container (from `Baskets.Api.IntegrationTests`'
existing `SqlServerFixture`) and, once `IntegrationTestSupport.Tests` starts, a `redis` and a
`rabbitmq` container are all visible simultaneously.

## Scenario 2 — A real SQL constraint violation is caught (Jira Test Scenario 2; FR-001, FR-002, SC-002)

Maps to User Story 1. Baskets carries a real unique index
(`services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`: `basket.HasIndex(entity =>
entity.CustomerRef).IsUnique();`) enforced by SQL Server itself, not application code.

1. Temporarily remove `.IsUnique()` from the `CustomerRef` index in `BasketsDbContext.cs`.
2. Run the (new, this-feature) integration test that inserts two baskets for the same
   `CustomerRef` and expects the second insert to throw:
   `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket`
3. **Expected**: the test fails — the real SQL Server no longer rejects the duplicate, because the
   constraint is gone.
4. Revert: `git checkout -- services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`, then
   re-run the same filter and confirm it passes again.

Repeat the same revert-and-confirm-red technique for at least one constraint in each other audited
service (`orders`, `parties`, `products`) — e.g. a `HasMaxLength` column that SQL Server truncates
against — per the task list.

## Scenario 3 — Unhealthy containers fail loudly, never skip silently (Jira Test Scenario 3 (partial); FR-007, SC-003)

```bash
docker network disconnect bridge $(docker ps -q --filter ancestor=mcr.microsoft.com/mssql/server:2022-latest) 2>/dev/null || true
dotnet test services/baskets/tests/Baskets.Api.IntegrationTests
```

**Expected**: the run fails with an error identifying the SQL Server container/fixture — it does
not report 0 tests run or a green/skipped result. (Restart Docker or remove the disconnected
container afterward to restore normal state.)

## Scenario 4 — Redis fixture is reachable (FR-003, FR-005)

```bash
dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue
```

**Expected**: passes — the test writes a key through `StackExchange.Redis` against the fixture's
real container and reads the same value back.

## Scenario 5 — RabbitMQ fixture is reachable, and a mid-test kill fails fast (Jira Test Scenario 3; FR-004, FR-006, FR-008, SC-004)

```bash
dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_Connects_ToARealBroker
dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest
```

**Expected**: both pass. Time the second one —
`time dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest`
— and confirm it completes in well under 30 seconds (SC-004), not by hanging until an external
timeout.

## Outcome

All five scenarios passing closes SC-001 through SC-005: containers are visible during a run
(SC-001), a real constraint violation is caught (SC-002), unhealthy containers fail the run loudly
(SC-003), a killed broker fails a test within 30s (SC-004), and the Redis/RabbitMQ fixtures used
above are the exact, unmodified fixtures any future feature would reuse (SC-005).
