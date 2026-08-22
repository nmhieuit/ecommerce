# Feature Specification: Testcontainers Integration Test Infrastructure

**Feature Branch**: `010-testcontainers-integration-tests`

**Created**: 2026-08-22

**Status**: Draft

**Input**: Jira SCRUM-20 — "[CONTRACT-2] Integration tests via Testcontainers (SQL Server, Redis, RabbitMQ)": As QA, I want integration tests running against real SQL Server, Redis, and RabbitMQ via Testcontainers so that the suite catches what in-memory fakes would hide (constitution Principle III).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SQL Server integration tests are audited and proven to catch real defects (Priority: P1)

QA needs confidence that the SQL Server Testcontainers pattern already used across `baskets`,
`orders`, `parties`, and `products` genuinely exercises the real database engine — not an
in-memory substitute that would hide a real constraint violation.

**Why this priority**: SQL Server is the only one of the three dependencies already wired into
every service's integration suite. Confirming it holds is the fastest, lowest-risk win and
establishes the baseline the Redis and RabbitMQ fixtures (User Stories 2 and 3) must match.

**Independent Test**: Introduce a real SQL constraint violation (e.g. remove a guard that a unique
constraint or foreign key would otherwise catch) in one audited service, run its integration suite,
and confirm the test fails specifically because the real database rejected the operation — then
revert and confirm green again.

**Acceptance Scenarios**:

1. **Given** the integration suite for an audited service runs, **When** it starts, **Then** a real
   SQL Server container is visible in `docker ps` for the duration of the run.
2. **Given** a real SQL constraint violation is deliberately introduced, **When** the integration
   suite runs, **Then** the relevant test fails because the real engine rejected the operation, and
   passes again once the violation is reverted.
3. **Given** the SQL Server container fails to become healthy at startup, **When** the suite runs,
   **Then** the run fails with a clear error identifying the container, rather than skipping the
   affected tests silently.

---

### User Story 2 - A reusable Redis Testcontainers fixture exists and proves itself reachable (Priority: P1)

QA needs a Redis container fixture, following the same shape as the existing SQL Server fixture,
that any service's integration test project can use once that service adopts Redis-backed caching.

**Why this priority**: The constitution names Redis as the basket store and distributed-caching
backend, but no service currently uses it in production. Standing up the fixture now — proven
reachable and provably failing loudly when unhealthy — removes the integration-test blocker for
whichever future feature wires Redis into a service, without that feature having to build test
infrastructure from scratch.

**Independent Test**: Add a smoke test that starts the Redis fixture, connects to it, performs a
basic read/write round-trip against the real container, and confirms the container is visible in
`docker ps` throughout.

**Acceptance Scenarios**:

1. **Given** a test project references the Redis fixture, **When** the suite starts, **Then** a
   real Redis container starts and is reachable over the connection details the fixture exposes.
2. **Given** the Redis container fails to become healthy at startup, **When** the suite runs,
   **Then** the run fails with a clear error identifying the container, rather than skipping the
   affected tests silently.

---

### User Story 3 - A reusable RabbitMQ Testcontainers fixture exists and survives a mid-test broker failure (Priority: P2)

QA needs a RabbitMQ container fixture, following the same shape as the existing SQL Server fixture,
that any service's integration test project can use once that service adopts RabbitMQ-based
publishing or consumption.

**Why this priority**: Like Redis, RabbitMQ is named in the constitution (event-driven messaging
via MassTransit) but nothing in the codebase publishes or consumes through it yet. This is P2
rather than P1 because the broker-outage scenario is explicitly tied to the not-yet-built Phase 4
timeout/resilience work (SCRUM-30) — only the test's own hang-safety is in scope here, not full
resilience policy.

**Independent Test**: Add a smoke test that starts the RabbitMQ fixture, connects to it, and
confirms the container is visible in `docker ps`. Separately, add a test that kills the RabbitMQ
container mid-test and confirms the affected test fails within a bounded time instead of hanging
indefinitely.

**Acceptance Scenarios**:

1. **Given** a test project references the RabbitMQ fixture, **When** the suite starts, **Then** a
   real RabbitMQ container starts and is reachable over the connection details the fixture exposes.
2. **Given** the RabbitMQ container fails to become healthy at startup, **When** the suite runs,
   **Then** the run fails with a clear error identifying the container, rather than skipping the
   affected tests silently.
3. **Given** a test is connected to a running RabbitMQ container, **When** the container is killed
   mid-test, **Then** the test fails within a bounded time rather than hanging indefinitely.

---

### Edge Cases

- What happens when Docker itself is unavailable on the machine running the suite (no daemon)? The
  run must fail with a clear, actionable error rather than hanging or silently skipping the
  affected tests.
- What happens when two container fixtures (e.g. SQL Server and Redis) start concurrently and one
  is slow to become healthy? The suite must wait per-fixture and still fail loudly if any one of
  them times out, not just the slowest.
- What happens when a test project needs more than one of these fixtures at once (e.g. a future
  feature test needing both SQL Server and RabbitMQ)? The fixtures must be composable without one
  fixture's failure masking another's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The integration suites for `baskets`, `orders`, `parties`, and `products` MUST
  continue to run their tests against a real SQL Server container via Testcontainers, not an
  in-memory or fake provider.
- **FR-002**: At least one integration test per audited service MUST be shown to fail when a real
  SQL constraint it depends on is removed, and pass again once restored, proving the test exercises
  the real engine.
- **FR-003**: A reusable Redis Testcontainers fixture MUST exist that any test project can use to
  start a real Redis container and obtain connection details to it.
- **FR-004**: A reusable RabbitMQ Testcontainers fixture MUST exist that any test project can use
  to start a real RabbitMQ container and obtain connection details to it.
- **FR-005**: At least one test MUST prove the Redis fixture is reachable (a real read/write against
  the started container).
- **FR-006**: At least one test MUST prove the RabbitMQ fixture is reachable (a real connection to
  the started container).
- **FR-007**: When any of the three containers (SQL Server, Redis, RabbitMQ) fails to become
  healthy during fixture startup, the test run MUST fail with an error that identifies which
  container failed, rather than skipping the dependent tests silently.
- **FR-008**: At least one test MUST prove that killing the RabbitMQ container mid-test causes the
  affected test to fail within a bounded time rather than hang indefinitely.
- **FR-009**: This feature MUST NOT introduce new production caching or messaging behavior — no
  service's runtime code starts depending on Redis or RabbitMQ as a result of this feature. Only
  test infrastructure is added.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: QA can run the full integration suite locally and observe SQL Server, Redis, and
  RabbitMQ containers all present in `docker ps` during the run.
- **SC-002**: A deliberately introduced SQL constraint violation is caught by an integration test
  100% of the time it is introduced, across every audited service.
- **SC-003**: If any of the three containers fails to become healthy, the suite run fails and
  reports the failing container by name — zero silent skips.
- **SC-004**: A RabbitMQ container killed mid-test causes the affected test to fail within 30
  seconds rather than hang indefinitely.
- **SC-005**: A future feature that adds Redis caching or RabbitMQ publishing to a service can
  reuse the Redis or RabbitMQ fixture from this feature without modifying it.

## Assumptions

- Redis and RabbitMQ are not yet used by any service's production code — no service caches in
  Redis or publishes/consumes via RabbitMQ today. This feature adds the Testcontainers fixtures and
  proves them reachable and fail-loud; it does not add caching or messaging behavior. That is
  future, separately scoped work (the constitution's Technology and Infrastructure Constraints
  already name Redis and RabbitMQ as the platform's caching and messaging backends).
- Docker (or an equivalent container runtime reachable by Testcontainers) is available wherever the
  integration suites run, locally and in CI, consistent with the existing SQL Server fixture.
- "Fails loudly" means the test run exits non-zero and the failure output identifies which
  container did not become healthy — not merely a generic timeout with no diagnostic.
- The existing per-service `SqlServerFixture` pattern (one fixture class per service's integration
  test project) is the baseline User Story 1 audits against; no new SQL Server behavior is
  introduced by this feature.
- The full broker-outage resilience story (timeouts, retries, circuit breakers on outbound calls)
  is out of scope — that is SCRUM-30 (Phase 4). This feature only proves the test itself does not
  hang when the broker disappears mid-test.
