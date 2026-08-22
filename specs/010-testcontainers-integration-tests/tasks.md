---

description: "Task list for Testcontainers Integration Test Infrastructure"

---

# Tasks: Testcontainers Integration Test Infrastructure

**Input**: Design documents from `/specs/010-testcontainers-integration-tests/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [quickstart.md](./quickstart.md)

**Tests**: This feature's deliverable *is* tests and test infrastructure (constitution Principle
III). Every implementation task below follows red-green: a failing test lands first, then the
minimal code that makes it pass.

**Organization**: Tasks are grouped by user story, matching spec.md's three stories (US1, US2 at
P1; US3 at P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

New shared library at `shared/IntegrationTestSupport/` and `shared/IntegrationTestSupport.Tests/`.
New constraint-audit test files under each audited service's existing
`services/<service>/tests/<Service>.Api.IntegrationTests/` directory. No existing production files
are modified permanently — every revert-and-confirm-red step restores the file it touched.

---

## Phase 1: Setup

**Purpose**: Stand up the new shared test-infrastructure projects before any fixture code is
written.

- [X] T001 Add `Testcontainers.Redis` (4.14.0), `Testcontainers.RabbitMq` (4.14.0),
      `StackExchange.Redis` (3.1.31), and `RabbitMQ.Client` (7.2.2) `PackageVersion` entries to
      `Directory.Packages.props`, next to the existing `Testcontainers.MsSql` entry, with a comment
      explaining the version-lockstep rationale (research.md Decision 1)
- [X] T002 [P] Create `shared/IntegrationTestSupport/IntegrationTestSupport.csproj` (net10.0,
      `Nullable` enabled, `PackageReference` to `Testcontainers.Redis` and `Testcontainers.RabbitMq`,
      no version attributes per central package management)
- [X] T003 [P] Create `shared/IntegrationTestSupport.Tests/IntegrationTestSupport.Tests.csproj`
      (net10.0, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
      `coverlet.collector`, `StackExchange.Redis`, `RabbitMQ.Client`, and a `ProjectReference` to
      `shared/IntegrationTestSupport/IntegrationTestSupport.csproj`)
- [X] T004 Add both new projects to `Ecommerce.slnx`
- [X] T005 Run `dotnet build Ecommerce.slnx` and confirm the solution — including the two new,
      currently-empty projects — builds cleanly before any fixture or audit code is written

**Checkpoint**: New projects exist and build. Safe to proceed to user story work.

---

## Phase 2: Foundational

**Purpose**: Blocking prerequisites shared by all three user stories.

None. User Story 1 audits existing, already-working SQL Server fixtures; User Stories 2 and 3 each
build their own fixture inside their own phase. All three are independently runnable once Phase 1's
projects exist and build. Proceed directly to User Story 1.

---

## Phase 3: User Story 1 - SQL Server integration tests are audited and proven to catch real defects (Priority: P1) 🎯 MVP

**Goal**: Prove FR-001 and FR-002 — the existing SQL Server Testcontainers pattern in `baskets`,
`orders`, `parties`, and `products` still catches a real database-level constraint violation, not
just an application-level guard (spec SC-002).

**Independent Test**: For each audited service, revert the real SQL-level constraint its new test
depends on, confirm that test fails, then restore the constraint and confirm green again.

### Implementation for User Story 1

- [X] T006 [P] [US1] Write failing test `CustomerRef_Is_UniquePerBasket` in
      `services/baskets/tests/Baskets.Api.IntegrationTests/BasketConstraintsTests.cs`: insert two
      baskets with the same `CustomerRef` directly via `BasketsDbContext` and assert the second
      `SaveChangesAsync` throws `DbUpdateException` — exercises the real unique index on
      `CustomerRef` in `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`
- [X] T007 [US1] Revert-and-confirm-red for the `CustomerRef` unique index: comment out
      `.IsUnique()` on that index in `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`,
      run `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket`,
      confirm it fails, then `git checkout -- services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`
      and re-run to confirm green again (quickstart.md Scenario 2)
- [X] T008 [P] [US1] Write failing test `TenantId_ExceedingMaxLength_IsRejectedByTheDatabase` in
      `services/orders/tests/Orders.Api.IntegrationTests/OrderConstraintsTests.cs`: insert an
      `Order` with a `TenantId` longer than 128 characters directly via `OrdersDbContext` and assert
      `SaveChangesAsync` throws — exercises the real `nvarchar(128)` column bound from
      `.HasMaxLength(128)` in `services/orders/src/Orders.Api/Data/OrdersDbContext.cs`
- [X] T009 [US1] Revert-and-confirm-red for the `TenantId` length bound: temporarily change
      `.HasMaxLength(128)` to `.HasMaxLength(500)` in
      `services/orders/src/Orders.Api/Data/OrdersDbContext.cs`, run
      `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter TenantId_ExceedingMaxLength_IsRejectedByTheDatabase`,
      confirm it now fails (SQL Server no longer rejects the over-length value), then
      `git checkout -- services/orders/src/Orders.Api/Data/OrdersDbContext.cs` and re-run to confirm
      green again
- [X] T010 [P] [US1] Write failing test `DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase` in
      `services/parties/tests/Parties.Api.IntegrationTests/PartyConstraintsTests.cs`: insert a
      `Party` with a `DisplayName` longer than 200 characters directly via `PartiesDbContext` and
      assert `SaveChangesAsync` throws — exercises the real `nvarchar(200)` column bound from
      `.HasMaxLength(200)` in `services/parties/src/Parties.Api/Data/PartiesDbContext.cs`
- [X] T011 [US1] Revert-and-confirm-red for the `DisplayName` length bound: temporarily change
      `.HasMaxLength(200)` to `.HasMaxLength(500)` in
      `services/parties/src/Parties.Api/Data/PartiesDbContext.cs`, run
      `dotnet test services/parties/tests/Parties.Api.IntegrationTests --filter DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase`,
      confirm it now fails, then `git checkout -- services/parties/src/Parties.Api/Data/PartiesDbContext.cs`
      and re-run to confirm green again
- [X] T012 [P] [US1] Write failing test `Name_ExceedingMaxLength_IsRejectedByTheDatabase` in
      `services/products/tests/Products.Api.IntegrationTests/ProductConstraintsTests.cs`: insert a
      `Product` with a `Name` longer than 200 characters directly via `ProductsDbContext` and assert
      `SaveChangesAsync` throws — exercises the real `nvarchar(200)` column bound from
      `.HasMaxLength(200)` in `services/products/src/Products.Api/Data/ProductsDbContext.cs`
- [X] T013 [US1] Revert-and-confirm-red for the `Name` length bound: temporarily change
      `.HasMaxLength(200)` to `.HasMaxLength(500)` in
      `services/products/src/Products.Api/Data/ProductsDbContext.cs`, run
      `dotnet test services/products/tests/Products.Api.IntegrationTests --filter Name_ExceedingMaxLength_IsRejectedByTheDatabase`,
      confirm it now fails, then `git checkout -- services/products/src/Products.Api/Data/ProductsDbContext.cs`
      and re-run to confirm green again
- [X] T014 [US1] Run quickstart.md Scenario 1: start
      `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests` and confirm a real SQL
      Server container is visible in `docker ps` for the duration of the run (Jira Test Scenario 1,
      partial — full multi-container confirmation happens in T024 once Phases 4–5 land)

**Checkpoint**: FR-001 and FR-002 are provably true across all four audited services; SC-002 is
closed. User Story 1 is independently complete.

---

## Phase 4: User Story 2 - A reusable Redis Testcontainers fixture exists and proves itself reachable (Priority: P1)

**Goal**: Prove FR-003 and FR-005 — a reusable `RedisFixture` starts a real Redis container and a
real client can read/write against it.

**Independent Test**: Run `shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue`.

### Implementation for User Story 2

- [X] T015 [US2] Write failing test `RedisFixture_Roundtrips_ARealValue` in
      `shared/IntegrationTestSupport.Tests/RedisFixtureTests.cs`, referencing a not-yet-existing
      `RedisFixture` type — connects via `StackExchange.Redis` using `RedisFixture.ConnectionString`,
      sets a key, reads it back, asserts equality (build fails — red, per data-model.md)
- [X] T016 [US2] Implement `shared/IntegrationTestSupport/RedisFixture.cs`: `IAsyncLifetime`
      wrapping `Testcontainers.Redis`'s `RedisBuilder().Build()`, exposing `ConnectionString` after
      `InitializeAsync`, matching the existing `SqlServerFixture` shape (research.md Decision 4);
      confirm T015 now passes
- [X] T017 [US2] Run `dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue`
      and confirm it's green with a real Redis container visible in `docker ps` during the run

**Checkpoint**: FR-003 and FR-005 are satisfied. The Redis fixture is ready for reuse by any future
service (partial SC-005). User Stories 1 and 2 are both independently complete.

---

## Phase 5: User Story 3 - A reusable RabbitMQ Testcontainers fixture exists and survives a mid-test broker failure (Priority: P2)

**Goal**: Prove FR-004, FR-006, and FR-008 — a reusable `RabbitMqFixture` starts a real RabbitMQ
container, a real client can connect to it, and killing the container mid-test fails the affected
test within a bounded time instead of hanging (spec SC-004).

**Independent Test**: Run
`shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_Connects_ToARealBroker` and
`--filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest`, timing the second to confirm it
completes well under 30 seconds.

### Implementation for User Story 3

- [X] T018 [US3] Write failing test `RabbitMqFixture_Connects_ToARealBroker` in
      `shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs`, referencing a not-yet-existing
      `RabbitMqFixture` type — opens a `RabbitMQ.Client` connection using
      `RabbitMqFixture.ConnectionString` and asserts it succeeds (build fails — red)
- [X] T019 [US3] Implement `shared/IntegrationTestSupport/RabbitMqFixture.cs`: `IAsyncLifetime`
      wrapping `Testcontainers.RabbitMq`'s `RabbitMqBuilder().Build()`, exposing `ConnectionString`
      after `InitializeAsync` plus an internal-only container handle for this feature's own tests to
      call `StopAsync()` on (data-model.md), matching the existing `SqlServerFixture` shape; confirm
      T018 now passes
- [X] T020 [US3] Write failing test `RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` in
      `shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs` per research.md Decision 5: open
      a `RabbitMQ.Client` connection with a short (few-second) `ContinuationTimeout`, start an
      in-flight operation, stop the fixture's container mid-operation via its internal handle, and
      assert the client call throws — wrapped in a bounded `Task.WhenAny` against a 30-second timer
      so the test itself cannot hang even if the client-side timeout is misconfigured; confirm this
      fails first against an unbounded/naive assertion before wiring the bound (red)
- [X] T021 [US3] Wire the bounded timeout and assertion so T020 passes reliably; run
      `dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest`
      and time it (`time dotnet test ...`) to confirm it completes well under 30 seconds (SC-004)

**Checkpoint**: FR-004, FR-006, and FR-008 are satisfied; SC-004 is closed. Both new fixtures are
ready for reuse (SC-005 complete). All three user stories are now independently complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that the whole suite is green, nothing stray was left behind, and
every success criterion holds end to end.

- [X] T022 Run `dotnet build Ecommerce.slnx` and confirm the whole solution — the two new shared
      projects plus all four audited services — builds cleanly
- [X] T023 [P] Run `git status` and `git diff` at the repo root and confirm the only changes are
      `Directory.Packages.props`, `Ecommerce.slnx`, `shared/IntegrationTestSupport/`,
      `shared/IntegrationTestSupport.Tests/`, the four new `*ConstraintsTests.cs` files, and this
      feature's own `specs/010-testcontainers-integration-tests/` artifacts — no stray production
      edits survive any revert-and-confirm-red step (FR-009)
- [X] T024 Walk through [quickstart.md](./quickstart.md) Scenarios 1–5 top to bottom exactly as
      written, including the full Scenario 1 (SQL Server, Redis, and RabbitMQ containers all
      visible in `docker ps` simultaneously) and Scenario 3 (unhealthy-container fail-loud check),
      and confirm each produces its documented expected result, closing SC-001 through SC-005

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Empty — no additional blocking work beyond Setup
- **User Story 1 (Phase 3)**: Depends on Setup only for the general baseline; does not depend on
  the new shared projects at all — can start as soon as T001 lands (or even before, since it
  touches only existing service projects)
- **User Story 2 (Phase 4)**: Depends on Setup (Phase 1) — needs
  `shared/IntegrationTestSupport(.Tests)` to exist and build (T001–T005); independent of User
  Story 1
- **User Story 3 (Phase 5)**: Depends on Setup (Phase 1) only, same as User Story 2; independent of
  User Story 1 and User Story 2 (different files: `RabbitMqFixture.cs` vs `RedisFixture.cs`)
- **Polish (Phase 6)**: Depends on all three user stories being complete — T022–T024 need every
  revert-and-restore cycle finished so the working tree is clean and every fixture exists

### Within Each User Story

- User Story 1: T006/T008/T010/T012 (one per service, different files) are parallelizable; each
  is followed by its own sequential revert-and-confirm-red task (T007/T009/T011/T013) that depends
  only on its own preceding test task, not on the other services' pairs; T014 depends on all of
  Phase 3's tests existing and passing
- User Story 2: T015 (test) → T016 (fixture) → T017 (confirmation run) are strictly sequential —
  each depends on the previous
- User Story 3: T018 (connectivity test) → T019 (fixture) are sequential; T020 (kill-mid-test test)
  depends on T019's internal container handle; T021 depends on T020

### Parallel Opportunities

- T002 and T003 (Setup) in parallel — different projects
- T006, T008, T010, T012 (User Story 1 baseline tests, one per service) in parallel with each other
- T006–T014 (User Story 1) can run in parallel with T015–T017 (User Story 2) and T018–T021 (User
  Story 3) once Setup (Phase 1) is complete — entirely different files
- T023 (Polish) can run in parallel with T022/T024 — it only reads `git status`/`git diff`

---

## Parallel Example: Setup Complete, Three Stories in Parallel

```bash
# Once T001-T005 (Setup) are green, hand each story to a different track:
Task: "US1 — SQL constraint audit across baskets/orders/parties/products (T006-T014)"
Task: "US2 — Redis fixture (T015-T017)"
Task: "US3 — RabbitMQ fixture (T018-T021)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1
3. **STOP and VALIDATE**: FR-001 and FR-002 provably true across every audited service — this
   alone closes the SQL Server third of the ticket's acceptance criteria, with zero new production
   dependencies

### Incremental Delivery

1. Setup (Phase 1) → new shared projects exist and build
2. User Story 1 (Phase 3) → SQL Server audit provably passes (MVP)
3. User Story 2 (Phase 4) → Redis fixture built and proven reachable, in parallel with Phase 3
4. User Story 3 (Phase 5) → RabbitMQ fixture built, proven reachable, and proven fail-fast on a
   mid-test broker kill, in parallel with Phases 3–4
5. Polish (Phase 6) → full solution build, clean working tree, quickstart walkthrough passes

### Parallel Team Strategy

With up to three people, once Setup is done:

- Person A: User Story 1 (SQL constraint audit across four services)
- Person B: User Story 2 (Redis fixture)
- Person C: User Story 3 (RabbitMQ fixture)

All three integrate independently — different files, no shared state, and every User Story 1
revert is restored before that task completes.

---

## Notes

- No production runtime behavior changes: every revert in Phase 3 is explicitly reverted within
  the same task; the new fixtures in Phases 4–5 are referenced by no service's runtime code
  (FR-009).
- [P] tasks touch different files (or, for the User Story 1 baseline tests, different services'
  test projects) with no dependencies on each other.
- Commit after each task or logical group, consistent with `docs/engineering/test-first-commits.md`
  from `009-retrofit-tdd-basket-order`: a failing test lands before or alongside the code that makes
  it pass, never as a same-or-later "add tests" follow-up commit.
