# Research: Testcontainers Integration Test Infrastructure

## Decision 1: Package versions for the new Testcontainers modules

**Decision**: Pin `Testcontainers.Redis` and `Testcontainers.RabbitMq` to `4.14.0` in
`Directory.Packages.props`, matching the already-pinned `Testcontainers.MsSql` `4.14.0`.

**Rationale**: All `Testcontainers.*` NuGet packages ship from the same `testcontainers-dotnet`
repository in lockstep — confirmed against the NuGet flat-container index, both packages' latest
stable release is `4.14.0`, identical to the version already in the repo. Matching versions across
the three modules avoids mixed-version skew in the shared `Testcontainers` core dependency they all
pull in transitively.

**Alternatives considered**: Pinning each to an independently "latest for that package" version —
rejected, since they're already aligned and there's no reason to introduce version drift between
sibling packages from the same release train.

## Decision 2: Smoke-test client libraries

**Decision**: Add `StackExchange.Redis` `3.1.31` and `RabbitMQ.Client` `7.2.2` to
`Directory.Packages.props`, referenced **only** by the new `shared/IntegrationTestSupport.Tests`
project — not by any service's runtime (`*.Api`) project.

**Rationale**: FR-005/FR-006 require proving each fixture is reachable with a real client, not just
that the container starts. These are the standard, first-party .NET clients for each broker and are
already implied by the constitution's Technology constraints (Redis, RabbitMQ named as platform
backends) — using anything else would mean re-validating a client no production code will actually
use. Keeping the reference test-only satisfies FR-009 (no production dependency added).

**Alternatives considered**: Using `Testcontainers`' own container `ExecAsync` (e.g. `redis-cli
PING` via container exec) instead of a .NET client — rejected because it doesn't prove the
connection string / exposed port the fixture publishes actually works from outside the container,
which is the thing a future consumer of the fixture needs proven.

## Decision 3: Where the new fixtures live

**Decision**: A new shared library, `shared/IntegrationTestSupport`, holds `RedisFixture.cs` and
`RabbitMqFixture.cs`. A sibling `shared/IntegrationTestSupport.Tests` project holds the smoke tests
that prove them.

**Rationale**: SC-005 requires a future service to reuse a fixture "without modifying it." The
existing `SqlServerFixture.cs` is duplicated verbatim across four service test projects (`baskets`,
`orders`, `parties`, `products`) — copy-paste, not reuse. Repeating that pattern for two more
fixtures compounds the duplication for every future service that adopts Redis or RabbitMQ. A shared
library referenced via `ProjectReference` mirrors the existing `shared/EventContracts` precedent
(one definition, multiple consumers) and keeps Principle I intact: this is test tooling, not a
runtime data-access shortcut between services.

**Alternatives considered**: Duplicating a `RedisFixture.cs`/`RabbitMqFixture.cs` per service like
`SqlServerFixture.cs` — rejected as the SC-005 reuse requirement is explicit and duplication is the
opposite of that. Consolidating the *existing* `SqlServerFixture.cs` copies into the same shared
project — out of scope for this feature (spec.md FR-001/FR-002 only ask to audit the existing
pattern, not refactor it); left as a follow-up, not silently done as a drive-by change.

## Decision 4: Proving "fails loudly, never silently skips" (FR-007, SC-003)

**Decision**: Rely on `Testcontainers`' existing behavior: `IAsyncLifetime.InitializeAsync()` calls
`_container.StartAsync()`, which already throws when the container's default wait strategy (each
builder — `MsSqlBuilder`, `RedisBuilder`, `RabbitMqBuilder` — ships one) doesn't reach the ready
state within its timeout. xUnit surfaces that exception as a fixture-initialization failure naming
the container, failing every test in the collection rather than skipping them. No custom
wait/health-check logic needs to be written — the two new fixtures follow the identical shape as
`SqlServerFixture.cs`, which already relies on this behavior today (confirmed: no silent-skip
handling exists anywhere in the current fixture).

**Rationale**: The existing SQL Server fixture already demonstrates this is the framework default
behavior, not something this feature must build. The audit task for User Story 1 (spec.md FR-007
scenario) simply has to demonstrate it, not add new plumbing.

**Alternatives considered**: A custom retry-with-manual-health-check wrapper — rejected as
unnecessary; it would duplicate what `Testcontainers`' wait strategies already provide and add a
place for a silent-skip bug to hide.

## Decision 5: Proving a mid-test RabbitMQ kill fails fast, not hangs (FR-008, SC-004)

**Decision**: In the RabbitMQ smoke test, open a connection via `RabbitMQ.Client` with an explicit,
short `ContinuationTimeout` (a few seconds) rather than the client's much longer built-in default,
then stop the RabbitMQ container mid-operation using the fixture's own container handle
(`DockerContainer.StopAsync()`), and assert the in-flight client call throws within the 30-second
bound from SC-004 — the test itself wraps the assertion in a bounded `Task.WhenAny` against a
30-second timer so a client-library default that's still too generous cannot make the *test* hang,
even if it doesn't make the *client call* hang.

**Rationale**: SC-004 is explicit about the 30-second bound and explicit that this is about the
test not hanging, not about production resilience (that's SCRUM-30/Phase 4, out of scope per
spec.md Assumptions). A short client-side timeout plus a bounding assertion is the minimum needed
to prove that, without building retry/circuit-breaker policy this feature doesn't own.

**Alternatives considered**: Configuring `Microsoft.Extensions.Http.Resilience`-style policies —
rejected, that's for outbound HTTP calls per the constitution and this isn't an HTTP path; also
explicitly deferred to SCRUM-30. Relying on the RabbitMQ.Client default timeouts unmodified —
rejected, defaults are minutes-scale and would not reliably prove the 30-second bound.

## Decision 6: MassTransit is not introduced

**Decision**: This feature adds no dependency on `MassTransit`, even though the constitution names
it as the platform's messaging abstraction over RabbitMQ.

**Rationale**: MassTransit is a publish/consume abstraction for actual message contracts. Since no
service publishes or consumes anything yet (spec.md Assumptions), there is nothing for MassTransit
to abstract here — the RabbitMQ fixture and its smoke test talk to the broker directly via
`RabbitMQ.Client` to prove connectivity only. Introducing MassTransit now, with no real message
flowing, would be scope creep into the future publishing feature this ticket explicitly excludes.

**Alternatives considered**: Standing up a trivial MassTransit bus in the smoke test purely to
exercise the fixture — rejected as it would test MassTransit's own container-connectivity handling
rather than the fixture, and would need to be redone anyway once a real publisher/consumer exists.
