---

description: "Task list for 005-one-command-local-run"
---

# Tasks: One-Command Local Run with Real Containers

**Input**: Design documents from `/specs/005-one-command-local-run/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, and not optional — Principle III supersedes any other document. Note the shape they take here, though: this feature has **one genuine unit test** (the Dockerfile convention scanner, written before the Dockerfiles are fixed) and one **reused end-to-end suite** (004's walkthrough, pointed at containers). The rest of the verification is scenario-based against [quickstart.md](./quickstart.md), because "the stack comes up on a clean machine" is not a thing a unit test can assert. Where a task is a scenario check rather than an automated test, it says so.

**Organization**: Tasks are grouped by user story so each can be implemented and demonstrated on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: Which user story the task serves (US1, US2, US3)
- Every task names the exact file or directory it touches

## Path Conventions

- Stack topology: `docker-compose.yml` at the repository root (the default filename — see [plan.md](./plan.md) Structure Decision)
- Commands: `scripts/` at the repository root
- Service images: `services/{name}/src/{Name}.Api/Dockerfile`
- Storefront image: `frontend/apps/web/`

---

## ⚠️ Before starting: one decision to confirm

[plan.md](./plan.md) Complexity Tracking flags that the OpenTelemetry Collector (T013) is slightly more than the spec asked for. Without it every service logs OTLP export failures continuously, because `ServiceDefaults` targets `localhost:4317` and inside a container that is the container itself. The alternative is setting `OTEL_SDK_DISABLED=true` on each service instead — one container lighter, and no telemetry locally. **If that is preferred, T013 is dropped and T009 gains the environment variable.**

---

## Phase 1: Setup (Buildable Images)

**Purpose**: Nothing in this feature can run until the images build. Five of six currently cannot ([research.md](./research.md), Finding), and the storefront has no image at all.

- [ ] T001 Create the `tests/ContainerConventionTests` xUnit project and register it in `Ecommerce.slnx`, alongside the existing `tests/CrossServiceIsolation.Tests` and `tests/StructureConventionTests`
- [ ] T002 Write the failing convention test in `tests/ContainerConventionTests/DockerfileSharedProjectTests.cs` — for every `services/*/src/*.Api/*.csproj`, each `shared/*` project it references must be copied by the matching Dockerfile. Expect five failures today (baskets, bff, orders, parties, products); the gateway passes
- [ ] T003 Write the scanner it uses in `tests/ContainerConventionTests/DockerfileReferenceScanner.cs`, reporting **what it examined** as well as what it objected to — a scan that resolved the wrong directory must not look identical to compliance, the same guard `ConnectionStringScanner` already applies
- [ ] T004 Fix the five broken Dockerfiles to copy `shared/Tenancy` — `services/{baskets,bff,orders,parties,products}/src/*.Api/Dockerfile`, both the `.csproj` line and the source line, matching how `ServiceDefaults` is already copied
- [ ] T005 [P] Add an HTTP probe utility to the final stage of all six `services/*/src/*.Api/Dockerfile`, installed before `USER $APP_UID` so the images still run as non-root ([research.md](./research.md) Decision 9)
- [ ] T006 [P] Add a `migrator` stage producing a self-contained EF Core migration bundle to the four Dockerfiles whose service owns a `DbContext` — `services/{products,baskets,orders,parties}/src/*.Api/Dockerfile` ([research.md](./research.md) Decision 4)
- [ ] T007 [P] Create the storefront image in `frontend/apps/web/Dockerfile` (build the production bundle, then serve it) with `frontend/apps/web/nginx.conf` providing SPA history fallback — without it a reload on `/basket` answers 404 ([research.md](./research.md) Decision 5)
- [ ] T008 [P] Create `docker/otel-collector-config.yaml` — an OTLP receiver and a debug exporter, so traces and logs are readable locally

**Checkpoint**: every image builds from a clean checkout, and the convention test passes.

---

## Phase 2: Foundational (Compose Skeleton and Dependencies)

**Purpose**: The topology every story mounts into — the network, the volumes, and the four components nothing else depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T009 Create `docker-compose.yml` at the repository root with the project name, the network, the two named volumes (SQL Server data, RabbitMQ state), and `MSSQL_SA_PASSWORD` read from `.env` ([data-model.md](./data-model.md) — Stack Data)
- [ ] T010 Add the `sqlserver` service to `docker-compose.yml` with a health gate that passes only when it accepts a query, reusing the healthcheck already proven in `docker-compose.deps.yml`
- [ ] T011 [P] Add `redis` to `docker-compose.yml` with a ping health gate. Nothing connects to it — see spec FR-017; the gate means "available", never "working"
- [ ] T012 [P] Add `rabbitmq` to `docker-compose.yml` with a running-check health gate and its named volume. Also unused by any service today
- [ ] T013 [P] Add `otel-collector` to `docker-compose.yml`, wired to `docker/otel-collector-config.yaml` — **subject to the decision noted above**

**Checkpoint**: `docker compose up sqlserver redis rabbitmq otel-collector --wait` returns success.

---

## Phase 3: User Story 1 - One command brings the whole platform up (Priority: P1) 🎯 MVP

**Goal**: A contributor runs one documented command on a machine with only Docker, and every component starts and reports healthy — with the command refusing to report success until they have.

**Independent Test**: On a clean checkout, copy the template and run the command; confirm `docker compose ps` shows fifteen components, eleven healthy and four migrators exited `0`, with no further input.

### Tests for User Story 1

> Scenario-based, per the note at the top of this file. Write down the expected outcome before running.

- [ ] T014 [US1] Record the expected component inventory and gates from [data-model.md](./data-model.md) as the acceptance checklist for quickstart Scenario 1, in `specs/005-one-command-local-run/quickstart.md`

### Implementation for User Story 1

- [ ] T015 [US1] Add the four migrator services to `docker-compose.yml`, each depending on `sqlserver` being healthy, each given only its own database's connection string — no service's configuration may name another's database (spec FR-018)
- [ ] T016 [US1] Add the four domain services (`products-api`, `baskets-api`, `orders-api`, `parties-api`) to `docker-compose.yml`, each depending on its own migrator completing successfully, each health-gated on `/health/ready`
- [ ] T017 [US1] Add `bff-api` to `docker-compose.yml`, depending on the four domain services being healthy, health-gated on `/health/ready`
- [ ] T018 [US1] Add `gateway-api` to `docker-compose.yml`, depending on `bff-api`, health-gated on `/health/live` and publishing host port 5300 — liveness not readiness, because its readiness is deliberately empty ([data-model.md](./data-model.md))
- [ ] T019 [US1] Add `storefront` to `docker-compose.yml` publishing host port 4173, health-gated on serving its index page. It waits for nothing: it is static files, and the browser reaches the gateway afterwards
- [ ] T020 [US1] Write `scripts/up.ps1` — check Docker is installed, the daemon responds, `.env` exists, and the daemon meets the documented memory floor, each failing with one sentence naming the missing thing (spec FR-011), then delegate to `docker compose up --build --wait`
- [ ] T021 [P] [US1] Write `scripts/up.sh` with the same checks and delegation, for macOS and Linux
- [ ] T022 [US1] Verify quickstart Scenarios 1 and 7 in `specs/005-one-command-local-run/quickstart.md` — first run under 10 minutes, fifteen components in the expected states, and each prerequisite failure naming its own cause before any container starts

**Checkpoint**: the platform starts with one command. This is the MVP.

---

## Phase 4: User Story 2 - The storefront works end to end against the stack (Priority: P2)

**Goal**: A contributor opens the documented URL and completes a real purchase — browse, basket, checkout, confirmation — with nothing else to configure.

**Independent Test**: With the stack up, open `http://localhost:4173` and complete the full walkthrough without editing a file or running another command.

### Tests for User Story 2

- [ ] T023 [P] [US2] Extend `services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs` to cover the containerized storefront's origin — the suite that exists because 004's walkthrough found the storefront blocked in a real browser
- [ ] T024 [P] [US2] Confirm 004's walkthrough needs no code change to target containers: `frontend/apps/web/e2e/walkthrough.spec.ts` already reads `STOREFRONT_URL` and `GATEWAY_ORIGIN` from the environment ([research.md](./research.md) Decision 12)

### Implementation for User Story 2

- [ ] T025 [US2] Pass the backend origin to the storefront image as a build argument in `docker-compose.yml` — it must be the host-reachable `http://localhost:5300`, **not** the compose hostname, because the browser runs on the host ([research.md](./research.md) Decision 6)
- [ ] T026 [US2] Add the storefront's published origin to `Cors:AllowedOrigins` in `services/gateway/src/Gateway.Api/appsettings.Development.json`, alongside the existing dev-server origin
- [ ] T027 [US2] Verify quickstart Scenario 2 in `specs/005-one-command-local-run/quickstart.md` — three seeded products with no seeding step, the basket totalling $59.25, **a reload on `/basket` that loads rather than 404s**, and checkout producing a confirmation
- [ ] T028 [US2] Run 004's walkthrough against the stack from `frontend/apps/web` with `STOREFRONT_URL=http://localhost:4173` and `GATEWAY_ORIGIN=http://localhost:5300` — the acceptance test for this story
- [ ] T029 [US2] Verify quickstart Scenario 8 — the gateway answers from the host and the BFF is refused, making spec 004's single-entry-point rule a property of the environment rather than an assertion

**Checkpoint**: the flow works through containers, verified by the same suite that verifies it outside them.

---

## Phase 5: User Story 3 - The stack stops and restarts cleanly (Priority: P3)

**Goal**: Stopping leaves nothing behind; starting again returns the platform with its data; resetting returns it to a first run.

**Independent Test**: Bring the stack up, stop it, start it again, and confirm the platform is usable with no manual cleanup and previously placed orders still present.

### Tests for User Story 3

- [ ] T030 [US3] Record the expected post-stop state — zero containers, ports 4173 and 5300 free — as the acceptance checklist for quickstart Scenario 3, in `specs/005-one-command-local-run/quickstart.md`

### Implementation for User Story 3

- [ ] T031 [P] [US3] Write `scripts/down.ps1` and `scripts/down.sh` — stop and remove every container, keep the volumes (spec FR-006, FR-007)
- [ ] T032 [P] [US3] Write `scripts/reset.ps1` and `scripts/reset.sh` — stop, remove, and discard the volumes, so the next start behaves like a first run (spec FR-008). A separate command from `down` because losing an afternoon's test orders should take a deliberate act
- [ ] T033 [US3] Verify quickstart Scenario 3 — stop leaves no containers and frees both ports; restart preserves the order and basket from before
- [ ] T034 [US3] Verify quickstart Scenario 4 — after reset, the seeded catalog is present and prior orders are gone
- [ ] T035 [US3] Run the stop/start cycle ten times, confirming no orphaned containers and no port conflicts on any cycle (spec SC-004)
- [ ] T036 [US3] Verify quickstart Scenario 6 — a changed string in `frontend/apps/web/src/app/routes.tsx` appears after re-running the up command, proving no stale image was reused (spec FR-009)

**Checkpoint**: the daily loop works, not just the first run.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T037 [P] Add the `debug` profile to `docker-compose.yml`, publishing the internal service ports and the broker's management interface — needed when regenerating the API client from the BFF's document while the stack is what is running ([contracts/stack-interface.md](./contracts/stack-interface.md))
- [ ] T038 [P] Write `docs/local-development.md` — the one command, the prerequisites, the measured resource floor, both addresses, the stop and reset commands, and the `debug` profile (spec FR-012)
- [ ] T039 [P] Document in `docs/local-development.md` and `services/README.md` that the stack's single database server is a local convenience and **not** the deployed topology, so consolidation is not read as permission to share a database between services (spec FR-019)
- [ ] T040 [P] Cross-reference the new guide from `services/README.md` and `frontend/README.md`, both of which currently send readers to the per-service workflow
- [ ] T041 Measure and record the actual first-run and subsequent-run durations and the peak memory the stack uses, updating the figures in `docs/local-development.md` and confirming spec SC-001 and SC-009
- [ ] T042 Verify quickstart Scenario 5 — stopping the database and restarting fails within 2 minutes naming the component, rather than reporting success and failing at first use (spec SC-005)
- [ ] T043 Run every scenario in [quickstart.md](./quickstart.md) end to end and record the results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately. **Blocks everything**: nothing runs until the images build
- **Foundational (Phase 2)**: depends on Setup — blocks every user story
- **User Story 1 (Phase 3)**: depends on Foundational only
- **User Story 2 (Phase 4)**: depends on US1 — the storefront cannot be exercised until the backend it calls is up
- **User Story 3 (Phase 5)**: depends on US1. Independent of US2, though the restart check is more convincing once there is an order to preserve
- **Polish (Phase 6)**: depends on the stories you intend to ship

### Within Phase 1

T002 states the intent, T003 makes it compile and fail for the right reason, T004 makes it pass. T005 – T008 touch different files and run alongside once T004 has landed.

### Parallel Opportunities

- Setup: T005, T006, T007, T008 all run in parallel after T004
- Foundational: T011, T012, T013 are independent of each other; T010 is not (everything database-shaped waits on it)
- US1: T021 pairs with T020; T015 – T019 are sequential edits to one file
- US2: T023 and T024 run together before the implementation tasks
- US3: T031 and T032 are separate files
- Polish: T037 – T040 are all parallel

**Note on `docker-compose.yml`**: T009 – T019 and T037 all edit the same file, so they are sequential by nature even where the components are independent. The parallelism in this feature is mostly in Phase 1.

---

## Parallel Example: Phase 1

```bash
# After T004 has fixed the broken Dockerfiles, four independent pieces of work:
Task: "Add an HTTP probe utility to all six services/*/src/*.Api/Dockerfile"
Task: "Add a migrator stage to the four Dockerfiles with a DbContext"
Task: "Create frontend/apps/web/Dockerfile and nginx.conf with SPA history fallback"
Task: "Create docker/otel-collector-config.yaml"
```

---

## Implementation Strategy

### MVP first (Setup + Foundational + User Story 1)

1. Phase 1 — the images build, and a test keeps them building
2. Phase 2 — the dependencies come up healthy
3. Phase 3 — one command brings the platform up
4. **Stop and validate**: quickstart Scenarios 1 and 7
5. Demoable: a contributor with only Docker gets the whole platform from one line

### Incremental delivery

1. Setup + Foundational → images build, dependencies start
2. + US1 → the platform starts with one command → demo (MVP)
3. + US2 → the storefront works end to end, verified by 004's own walkthrough → demo
4. + US3 → the daily stop/start loop is clean
5. + Polish → documented, measured, and reachable for debugging

---

## Not in scope for these tasks

Carried from [plan.md](./plan.md), so their absence is a decision rather than an oversight:

- **Schema-per-tenant separation.** Now unresolved across two features: 003's plan specified it and marked its tasks complete, the code has never contained it, and 004 recorded the same failure. This feature neither introduces nor worsens it — consolidating onto one database server only makes it easier to notice. **Still awaiting a maintainer decision.**
- **Event-driven checkout.** The broker runs here with nothing publishing to it, exactly as spec FR-017 chose. ADR-0011 records the interim design; SCRUM-18 and SCRUM-31 own the replacement.
- **A deployable storefront image.** The backend origin is inlined at build time and points at a host address, so the image suits this stack only. Runtime configuration is the fix when deployment matters ([research.md](./research.md) Decision 6).
- **Observability backends, CI pipelines, and Kubernetes.** Out of scope per the spec. The collector in T013 is the endpoint `ServiceDefaults` already targets, not a backend.

---

## Notes

- [P] marks tasks touching different files with no incomplete dependency
- The one automated test that would have prevented this feature's largest precondition is T002/T003 — five service images have been unbuildable since 003 and nothing noticed
- Scenario-verification tasks name the quickstart scenario they check, so a reviewer can follow the same steps
- Commit after each task or logical group; stop at any checkpoint to validate a story on its own
