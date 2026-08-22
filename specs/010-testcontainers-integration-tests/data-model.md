# Data Model: Testcontainers Integration Test Infrastructure

This feature has no domain/business data model — it adds test infrastructure. The "entities" below
are the test fixtures themselves, described for implementers rather than as persisted data.

## RedisFixture

**Represents**: An `IAsyncLifetime` xUnit fixture that owns one real Redis container for the
lifetime of a test collection.

**Fields**:

- `ConnectionString` (`string`, read-only, populated after `InitializeAsync`): the connection
  string a `StackExchange.Redis` `ConnectionMultiplexer` (or a future service's Redis client) uses
  to reach the container.

**Lifecycle**:

1. `InitializeAsync()` builds and starts a `RedisBuilder().Build()` container. If the container
   does not become healthy within its default wait strategy's timeout, `StartAsync()` throws and
   fixture initialization fails (Decision 4) — the owning test collection fails, none of its tests
   run or skip silently.
2. Tests in the collection read `ConnectionString` to connect.
3. `DisposeAsync()` stops and removes the container.

**Relationships**: None to other entities — it is referenced (via `ICollectionFixture<RedisFixture>`
or constructor injection, per the existing `SqlServerFixture` pattern) by any test class that needs
a real Redis instance, starting with `RedisFixtureTests` in this feature and, in the future, by
whichever service adopts Redis caching.

## RabbitMqFixture

**Represents**: An `IAsyncLifetime` xUnit fixture that owns one real RabbitMQ container for the
lifetime of a test collection, plus the ability for a test to stop that container mid-test.

**Fields**:

- `ConnectionString` (`string`, read-only, populated after `InitializeAsync`): the AMQP connection
  string a `RabbitMQ.Client` `ConnectionFactory` (or a future service's publisher/consumer) uses to
  reach the container.
- `Container` (internal handle, not exposed beyond this library): the underlying
  `RabbitMqContainer`, exposed only to this feature's own smoke tests so `RabbitMqFixtureTests` can
  call `StopAsync()` to simulate a mid-test broker failure (Decision 5). Not part of the fixture's
  public contract that future service consumers depend on.

**Lifecycle**:

1. `InitializeAsync()` builds and starts a `RabbitMqBuilder().Build()` container. Same fail-loud
   behavior as `RedisFixture` on startup failure.
2. Tests in the collection read `ConnectionString` to connect.
3. `DisposeAsync()` stops and removes the container (idempotent even if a test already stopped it
   early via the mid-test-kill scenario).

**Relationships**: None to other entities — referenced the same way as `RedisFixture`.

## No changes to existing entities

`SqlServerFixture` (per service) and the domain entities in `Basket`, `Order`, etc. are audited by
User Story 1 but not modified — no fields, lifecycle, or relationships change for any of them.
