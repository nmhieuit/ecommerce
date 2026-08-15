---

description: "Task list template for feature implementation"
---

# Tasks: Scaffold Parties/Products/Baskets/Orders Service Shells

**Input**: Design documents from `/specs/001-scaffold-service-shells/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/health-check.md, quickstart.md

**Tests**: Included and REQUIRED — constitution Principle III (Test-First Development) is non-negotiable; plan.md's Constitution Check binds this feature to writing a failing test before each capability exists.

**Organization**: Tasks are grouped by user story (US1, US2, US3 from spec.md) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- File paths are exact and relative to the repository root

## Path Conventions

Backend microservices layout (see plan.md Project Structure) — no frontend in this feature's scope:

```text
services/<name>/src/<Name>.Api/     # Minimal API project, Features/, Data/
services/<name>/tests/<Name>.Api.UnitTests/
services/<name>/tests/<Name>.Api.IntegrationTests/
shared/ServiceDefaults/             # shared OTel + correlation-ID component
tests/CrossServiceIsolation.Tests/  # US2 solution-level isolation checks
tests/StructureConventionTests/     # US3 solution-level structure check
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository-level scaffolding shared by all four services

- [X] T001 Create solution-level structure: `services/` and `shared/` directories, `Ecommerce.slnx` at repository root (`.slnx` — .NET 10's `dotnet new sln` now defaults to the XML solution format)
- [X] T002 [P] Add root `.editorconfig` and `Directory.Build.props` enabling Roslyn analyzers with nullable reference types and warnings-as-errors (constitution: Backend constraints)
- [X] T003 [P] Create `shared/ServiceDefaults/ServiceDefaults.csproj` project shell (.NET 10 class library)
- [X] T004 [P] Create `Directory.Packages.props` at repository root, central-managing EF Core, `Microsoft.Extensions.Diagnostics.HealthChecks`, xUnit, and Testcontainers package versions for all four services

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared component every service depends on for telemetry (Principle VII) and the isolated local database containers every service needs to run

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Implement `AddServiceDefaults()` extension wiring OpenTelemetry traces/metrics/logs to the Elastic stack in `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` (constitution Principle VII — "not configured per service by hand")
- [X] T006 Implement correlation-ID propagation middleware, registered by `AddServiceDefaults()`, in `shared/ServiceDefaults/CorrelationIdMiddleware.cs` (depends on T005)
- [X] T007 [P] Create `docker-compose.deps.yml` at repository root defining one isolated SQL Server container per service: `parties-db`, `products-db`, `baskets-db`, `orders-db`

**Checkpoint**: `ServiceDefaults` and per-service database containers exist — user story implementation can now begin.

---

## Phase 3: User Story 1 - Run one service without standing up the whole platform (Priority: P1) 🎯 MVP

**Goal**: Any one of the four services can be started alone, with nothing else running, and reports an accurate health status (spec SC-001, SC-002).

**Independent Test**: Start exactly one service with its own database container only; confirm `/health/live` and `/health/ready` both return 200; stop its database and confirm `/health/ready` flips to 503.

### Tests for User Story 1 ⚠️

> Write these tests FIRST, and confirm they FAIL before implementation (Principle III, non-negotiable)

- [X] T008 [P] [US1] Write failing unit test asserting `/health/live` returns 200 in `services/parties/tests/Parties.Api.UnitTests/HealthCheckTests.cs`
- [X] T009 [P] [US1] Write failing unit test asserting `/health/live` returns 200 in `services/products/tests/Products.Api.UnitTests/HealthCheckTests.cs`
- [X] T010 [P] [US1] Write failing unit test asserting `/health/live` returns 200 in `services/baskets/tests/Baskets.Api.UnitTests/HealthCheckTests.cs`
- [X] T011 [P] [US1] Write failing unit test asserting `/health/live` returns 200 in `services/orders/tests/Orders.Api.UnitTests/HealthCheckTests.cs`
- [X] T012 [P] [US1] Write failing Testcontainers integration test asserting `/health/ready` is 200 when the database is reachable and 503 when it is not, in `services/parties/tests/Parties.Api.IntegrationTests/ReadinessTests.cs`
- [X] T013 [P] [US1] Same readiness test shape in `services/products/tests/Products.Api.IntegrationTests/ReadinessTests.cs`
- [X] T014 [P] [US1] Same readiness test shape in `services/baskets/tests/Baskets.Api.IntegrationTests/ReadinessTests.cs` (since independently re-run against a real Testcontainers SQL instance during T026 — 2 passed)
- [X] T015 [P] [US1] Same readiness test shape in `services/orders/tests/Orders.Api.IntegrationTests/ReadinessTests.cs` (same note as T014 — 2 passed)

### Implementation for User Story 1

- [X] T016 [P] [US1] Create `Parties.Api` ASP.NET Core Minimal API project (.NET 10) referencing `shared/ServiceDefaults`, in `services/parties/src/Parties.Api/Parties.Api.csproj`
- [X] T017 [P] [US1] Create `Products.Api` Minimal API project in `services/products/src/Products.Api/Products.Api.csproj`
- [X] T018 [P] [US1] Create `Baskets.Api` Minimal API project in `services/baskets/src/Baskets.Api/Baskets.Api.csproj`
- [X] T019 [P] [US1] Create `Orders.Api` Minimal API project in `services/orders/src/Orders.Api/Orders.Api.csproj`
- [X] T020 [P] [US1] Add `PartiesDbContext` (EF Core, connection string scoped to this service only) in `services/parties/src/Parties.Api/Data/PartiesDbContext.cs` (depends on T016)
- [X] T021 [P] [US1] Add `ProductsDbContext` in `services/products/src/Products.Api/Data/ProductsDbContext.cs` (depends on T017)
- [X] T022 [P] [US1] Add `BasketsDbContext` in `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs` (depends on T018)
- [X] T023 [P] [US1] Add `OrdersDbContext` in `services/orders/src/Orders.Api/Data/OrdersDbContext.cs` (depends on T019)
- [X] T024 [P] [US1] Implement `/health/live` and `/health/ready` (via `AddDbContextCheck<PartiesDbContext>()`) in `services/parties/src/Parties.Api/Features/HealthCheck/HealthCheckEndpoints.cs`; wire `AddServiceDefaults()` in `services/parties/src/Parties.Api/Program.cs` (depends on T020, T005 — makes T008, T012 pass)
- [X] T025 [P] [US1] Same for products in `services/products/src/Products.Api/Features/HealthCheck/HealthCheckEndpoints.cs` + `Program.cs` (depends on T021, T005 — makes T009, T013 pass)
- [X] T026 [P] [US1] Same for baskets in `services/baskets/src/Baskets.Api/Features/HealthCheck/HealthCheckEndpoints.cs` + `Program.cs` (depends on T022, T005 — makes T010, T014 pass)
- [X] T027 [P] [US1] Same for orders in `services/orders/src/Orders.Api/Features/HealthCheck/HealthCheckEndpoints.cs` + `Program.cs` (depends on T023, T005 — makes T011, T015 pass)
- [X] T028 [P] [US1] Add `appsettings.json` with a connection string scoped only to its own database for parties in `services/parties/src/Parties.Api/appsettings.json`
- [X] T029 [P] [US1] Same for products in `services/products/src/Products.Api/appsettings.json`
- [X] T030 [P] [US1] Same for baskets in `services/baskets/src/Baskets.Api/appsettings.json`
- [X] T031 [P] [US1] Same for orders in `services/orders/src/Orders.Api/appsettings.json`
- [X] T032 [P] [US1] Add `Dockerfile` for parties in `services/parties/src/Parties.Api/Dockerfile`
- [X] T033 [P] [US1] Add `Dockerfile` for products in `services/products/src/Products.Api/Dockerfile`
- [X] T034 [P] [US1] Add `Dockerfile` for baskets in `services/baskets/src/Baskets.Api/Dockerfile`
- [X] T035 [P] [US1] Add `Dockerfile` for orders in `services/orders/src/Orders.Api/Dockerfile`

**Implementation notes (US1)**

- Each service's readiness check uses `AddDbContextCheck<T>` with a `customTestQuery` that opens a
  real connection rather than EF's default `CanConnectAsync`, which collapses every failure into a
  bare `false` and so cannot populate the `description` field the health-check contract's 503 body
  specifies.
- `appsettings.json` carries a credential-free connection string naming only that service's own
  host and database; `appsettings.Development.json` carries the local container connection matching
  `.env.example`, and deployed environments override via `ConnectionStrings__<Service>Db`.
- Docker builds take the **repository root** as context (the projects need `Directory.*.props` and
  `shared/ServiceDefaults`): `docker build -f services/parties/src/Parties.Api/Dockerfile -t parties-api .`
- Fixed alongside this phase: `.gitignore`'s `.env.*` rule was swallowing `.env.example`, so a fresh
  clone could not bring up its own database container (SC-001). Added a `!.env.example` negation.

**Checkpoint**: All four services are independently runnable, each reports live/ready accurately, each builds as its own container. This alone is a demonstrable MVP.

---

## Phase 4: User Story 2 - Trust that no service can touch another service's data (Priority: P1)

**Goal**: Prove — not just assume — that no service has any path to another service's data (spec SC-003).

**Independent Test**: Inspect every service's configuration and attempt a cross-service connection; confirm no credential, connection string, or code path allows it to succeed.

### Tests for User Story 2 ⚠️

- [X] T036 [US2] Write failing test asserting no service's `appsettings.json` contains another service's connection string or credential, in `tests/CrossServiceIsolation.Tests/ConnectionStringIsolationTests.cs` (observed RED: 6/6 failed against the not-yet-implemented scanner)
- [X] T037 [P] [US2] Extend `services/parties/tests/Parties.Api.IntegrationTests/ReadinessTests.cs` with an assertion that stopping parties' own database never causes a fallback read from another service's database
- [X] T038 [P] [US2] Same fallback-prevention assertion in `services/products/tests/Products.Api.IntegrationTests/ReadinessTests.cs`
- [X] T039 [P] [US2] Same fallback-prevention assertion in `services/baskets/tests/Baskets.Api.IntegrationTests/ReadinessTests.cs`
- [X] T040 [P] [US2] Same fallback-prevention assertion in `services/orders/tests/Orders.Api.IntegrationTests/ReadinessTests.cs`

### Implementation for User Story 2

- [X] T041 [US2] Implement the connection-string isolation scanner making T036 pass, in `tests/CrossServiceIsolation.Tests/ConnectionStringScanner.cs` (depends on T028-T031, T036)
- [X] T042 [US2] Document the data-isolation guarantee in `services/README.md`, referencing the T041 scanner as its enforcement mechanism (depends on T041)

**Implementation notes (US2)**

- New solution-level project `tests/CrossServiceIsolation.Tests`, registered in `Ecommerce.slnx`.
  It deliberately references no service project, so the check cannot come to depend on the code
  it polices.
- The scanner reads **files, not running services** — possession of another service's credential is
  the violation, not its use — and discovers services from the directory layout, so `logistics` and
  `invoices` are covered the day their folders appear.
- Two guards keep T036 from passing vacuously: `ScanResult` reports what was examined (so a scan
  that resolved the wrong directory fails instead of reporting clean), and fixture-driven theories
  feed the scanner deliberately-broken configuration to prove it still detects a breach.
- T037–T040 are adversarial rather than passive: each hands its service an unreachable connection
  string for its own database *while* offering a genuinely reachable one under every other
  service's key, then requires 503 with `self-database` unhealthy.
- The four readiness suites now share one Testcontainers SQL Server per class (`SqlServerFixture`)
  instead of starting one per test — the new test needs a reachable "foreign" database anyway, and
  sharing keeps the container count per suite at one.
- Open caveat recorded in `services/README.md`: all four local database containers share one
  `MSSQL_SA_PASSWORD`, so local isolation rests on host/port separation rather than distinct
  credentials. Deployed environments are unaffected.

**Checkpoint**: Structural data isolation is proven by a repeatable check, not just asserted by convention.

---

## Phase 5: User Story 3 - Find all the code for a feature in one place (Priority: P2)

**Goal**: Every service's code is organized by feature/capability, verifiably (spec SC-004).

**Independent Test**: Open any one service and confirm there is no top-level technical-layer folder (`Controllers/`, `Services/`, `Repositories/`) — everything for a capability lives together under `Features/<Capability>/`.

### Tests for User Story 3 ⚠️

- [X] T043 [US3] Write failing structure test asserting no top-level `Controllers/`, `Services/`, or `Repositories/` folder exists under any of the four services' `src/*.Api/` directories, in `tests/StructureConventionTests/VerticalSliceStructureTests.cs` (observed RED: 9/9 failed against the not-yet-implemented scanner)

### Implementation for User Story 3

- [X] T044 [US3] Implement the structure check making T043 pass, scanning `services/*/src/*.Api/` (depends on T016-T019, T024-T027, T043) — in `tests/StructureConventionTests/VerticalSliceStructureScanner.cs`
- [X] T045 [P] [US3] Document the vertical-slice convention (each `Features/<Capability>/` folder holds its handler and registration together) and the constitution's escalation-to-layered-architecture exception, in `services/README.md`

**Implementation notes (US3)**

- New solution-level project `tests/StructureConventionTests`, registered in `Ecommerce.slnx`. Like
  the US2 isolation suite it references no service project, so the check cannot come to depend on
  the code it polices.
- Only **direct children** of `services/*/src/*.Api/` are judged. A `Services/` folder beside
  `Features/` splits a capability across the tree, which is what SC-004 forbids; the same name
  nested inside one capability (`Features/Checkout/Services/`) keeps code next to the feature it
  serves and is explicitly allowed — there is a test for each case.
- Vacuity guards, as in US2: the result reports what was scanned (all four API projects asserted),
  and a separate test requires every service to have at least one capability under `Features/` —
  a service with no `Features/` folder has nothing organised by capability and would otherwise pass
  a check that only looks for what must not exist.
- Mutation-verified against the real repository: creating an actual
  `services/parties/src/Parties.Api/Controllers/` folder turned the suite red (1 failed), and
  removing it restored green.
- Deliberately **no per-service opt-out flag**. FR-006's documented exception and the constitution's
  escalation clause are meant to be argued in `plan.md` and reviewed, so taking the exception
  requires a visible edit to the check itself rather than flipping a config switch.

**Checkpoint**: All three user stories are independently implemented and independently verifiable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that apply across all four services and final validation against spec.md

- [ ] T046 [P] Add `service-manifest.yaml` declaring the health-endpoint SLO (constitution Principle VIII) for parties in `services/parties/src/Parties.Api/service-manifest.yaml`
- [ ] T047 [P] Same for products in `services/products/src/Products.Api/service-manifest.yaml`
- [ ] T048 [P] Same for baskets in `services/baskets/src/Baskets.Api/service-manifest.yaml`
- [ ] T049 [P] Same for orders in `services/orders/src/Orders.Api/service-manifest.yaml`
- [ ] T050 Run the `quickstart.md` validation walkthrough end-to-end for all four services and record the outcome
- [ ] T051 Re-verify spec.md Success Criteria (SC-001 through SC-004) are all met and update `specs/001-scaffold-service-shells/checklists/requirements.md` if anything changed during implementation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (every service needs `ServiceDefaults` and its own DB container)
- **User Story 1 (Phase 3)**: Depends on Foundational only — delivers the MVP alone
- **User Story 2 (Phase 4)**: Depends on Foundational; its tests extend files US1 creates (T028-T031 appsettings, T012-T015 readiness tests) — start after US1's implementation tasks (T016-T035) land, not just after Foundational
- **User Story 3 (Phase 5)**: Depends on Foundational; its structure check scans files US1 creates (T016-T019, T024-T027) — start after US1's implementation tasks land
- **Polish (Phase 6)**: Depends on all three user stories being complete

### Within Each User Story

- Tests written and observed failing before implementation (Principle III)
- Project + DbContext before health-check endpoint implementation
- Story complete and checkpoint-validated before moving to the next priority

### Parallel Opportunities

- T002, T003, T004 (Setup) run in parallel
- T007 (Foundational) runs in parallel with T005/T006 (different file)
- Within US1: all 4 services' tests (T008-T015), all 4 projects (T016-T019), all 4 DbContexts (T020-T023), all 4 endpoint implementations (T024-T027), all 4 appsettings (T028-T031), and all 4 Dockerfiles (T032-T035) are parallelizable across services — but within one service, project → DbContext → endpoint is sequential
- Within US2: T037-T040 (the four readiness-test extensions) are parallelizable
- T046-T049 (Polish service manifests) are parallelizable

---

## Parallel Example: User Story 1

```bash
# Tests for all four services, launched together:
Task: "Write failing unit test for /health/live in services/parties/tests/Parties.Api.UnitTests/HealthCheckTests.cs"
Task: "Write failing unit test for /health/live in services/products/tests/Products.Api.UnitTests/HealthCheckTests.cs"
Task: "Write failing unit test for /health/live in services/baskets/tests/Baskets.Api.UnitTests/HealthCheckTests.cs"
Task: "Write failing unit test for /health/live in services/orders/tests/Orders.Api.UnitTests/HealthCheckTests.cs"

# Project scaffolds for all four services, launched together:
Task: "Create Parties.Api Minimal API project in services/parties/src/Parties.Api/"
Task: "Create Products.Api Minimal API project in services/products/src/Products.Api/"
Task: "Create Baskets.Api Minimal API project in services/baskets/src/Baskets.Api/"
Task: "Create Orders.Api Minimal API project in services/orders/src/Orders.Api/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks everything)
3. Complete Phase 3: User Story 1 — all four services independently runnable and health-checked
4. **STOP and VALIDATE**: run quickstart.md's SC-001/SC-002 steps for each service
5. This is a legitimate MVP: the underlying Jira story's core acceptance criteria (independent boot, health endpoint) are met

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. User Story 1 → validate independently → four working, health-checked service shells (MVP)
3. User Story 2 → validate independently → data isolation is now provable, not just assumed
4. User Story 3 → validate independently → feature-slice convention is enforced by a structure check, not just followed by habit
5. Polish → SLO manifests declared, full quickstart re-run, spec success criteria re-verified

### Parallel Team Strategy

With multiple developers, after Foundational completes:

- One developer per service is possible within US1 (T016-T035 are fully partitioned by service)
- US2 and US3 are best done by one person each, since their tests/checks operate across all four services at once (T036, T041, T043, T044)

---

## Notes

- [P] tasks touch different files with no dependency on an incomplete task
- [US1]/[US2]/[US3] labels map every phase-3+ task back to spec.md's user stories for traceability
- Tests MUST be written and observed failing before their corresponding implementation task (Principle III, non-negotiable — no exceptions per constitution's PR gate)
- Commit after each task or logical group per the constitution's trunk-based, small-PR workflow
- Stop at any checkpoint to validate a story independently before starting the next
