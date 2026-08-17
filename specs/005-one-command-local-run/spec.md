# Feature Specification: One-Command Local Run with Real Containers

**Feature Branch**: `005-one-command-local-run`

**Created**: 2026-08-17

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-15 — [WALK-1] One-command local run with real containers. As the DevOps-hat-wearer, I want the entire skeleton runnable locally with one command so that local development matches the platform's 'runnable locally with real dependencies via containers' constraint from day one. Acceptance Criteria: (1) Given a clean machine with Docker installed, when I run the single documented command, then all services, SQL Server, Redis, and RabbitMQ start and become healthy; (2) Given the stack is up, when I open the SPA URL, then the app is fully functional without any additional manual steps; (3) Given the command is documented, when a new contributor (future me) follows it, then no undocumented manual steps are required. Test Scenarios: (1) From a clean checkout, run the one command and time how long until the SPA is usable; (2) Stop and restart the stack — confirm it comes back up cleanly (no orphaned containers/ports); (3) Intentionally omit a dependency container — confirm the failure is obvious, not silent."

## Clarifications

### Session 2026-08-17

- Q: The ticket requires Redis and RabbitMQ to start and become healthy, but no code uses either. What should that mean here? (FR-017) → A: Start both as health-gated infrastructure with nothing connecting to them yet (Option A). Wiring a real use of each is left to the stories that own it.
- Q: Should the stack keep a database server per service, or consolidate? (FR-018) → A: One database server hosting a database per service (Option B). Each service keeps its own database and its own connection string; the per-service-server topology remains available in the existing single-service dependency setup.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A contributor brings the whole platform up with one command (Priority: P1)

As someone setting up this repository for the first time, I run one documented command and end up with a working platform — every service, every dependency it needs, and its data — without hunting through documents for the next step.

**Why this priority**: This is the feature. Everything else here is a property of this command working. Today the same outcome takes a database container per service, three separate migration commands, five service processes, and a dev server — nine steps, in an order that is not written down in one place.

**Independent Test**: Can be fully tested by taking a clean checkout on a machine that has only Docker installed, running the single documented command, and confirming the platform comes up with no further input.

**Acceptance Scenarios**:

1. **Given** a clean checkout and a machine with Docker installed and running, **When** the documented command is run, **Then** every component of the stack starts and reports healthy, and the command does not report success until they have.
2. **Given** the command has completed, **When** the contributor consults the documentation for what to do next, **Then** the only remaining instruction is a URL to open — no migrations to apply, no seed script to run, no configuration to edit.
3. **Given** a machine where a required prerequisite is missing (Docker not installed, not running, or below the documented resource floor), **When** the command is run, **Then** it fails with a message naming the missing prerequisite rather than a partial start or an unrelated error.

---

### User Story 2 - The storefront works end to end against the containerized stack (Priority: P2)

As a contributor with the stack running, I open the storefront's URL and complete a real purchase — browse, add to basket, check out, see the confirmation — with nothing else to configure.

**Why this priority**: A stack that starts but cannot serve the flow has not proved anything. This is what turns "the containers are up" into "the platform works", and it is the acceptance criterion the ticket words most strongly ("fully functional without any additional manual steps").

**Independent Test**: Can be fully tested by bringing the stack up, opening the documented URL, and completing the full browse → basket → checkout → confirmation walkthrough without editing a file or running another command.

**Acceptance Scenarios**:

1. **Given** the stack is up, **When** the contributor opens the documented storefront URL, **Then** the storefront loads and lists purchasable products.
2. **Given** the storefront is open, **When** the contributor completes browse → add to basket → checkout, **Then** a confirmation appears referencing the created order, exactly as it does when the parts are run by hand today.
3. **Given** the stack is up, **When** the storefront's network traffic is inspected, **Then** every request goes to the platform's single entry point, as it does outside containers.
4. **Given** a freshly started stack that has never been used, **When** the storefront is opened, **Then** products are already present — the catalog does not require a separate seeding step.

---

### User Story 3 - The stack stops and restarts cleanly (Priority: P3)

As a contributor who works on this repository over days, I stop the stack when I am done and start it again later, and it comes back the same way — without stale containers, held ports, or leftover state that makes the second run behave unlike the first.

**Why this priority**: The first run is a one-off; the tenth run is the daily experience. A stack that needs manual cleanup between runs quietly reintroduces the undocumented steps this feature exists to remove.

**Independent Test**: Can be fully tested by bringing the stack up, stopping it with the documented command, starting it again, and confirming the platform is usable with no manual cleanup in between.

**Acceptance Scenarios**:

1. **Given** a running stack, **When** the documented stop command is run, **Then** every container the stack started is stopped and removed, and every port it held is released.
2. **Given** a stack that has been stopped, **When** it is started again, **Then** it becomes usable without any manual cleanup, and previously placed orders and basket contents are still there.
3. **Given** a contributor who wants to start over completely, **When** they run the documented reset command, **Then** all stored data is discarded and the next start behaves like a first run, seed data included.
4. **Given** a source file has changed since the last run, **When** the stack is started again, **Then** the change is present in the running platform rather than a stale build being reused silently.

---

### Edge Cases

- **A port the stack needs is already in use**: the failure names the port and the component that wanted it, rather than a container exiting silently or a service starting in a half-working state.
- **One component fails to become healthy**: the command reports which one and stops, rather than reporting overall success while a service is down.
- **A dependency container is deliberately removed** (test scenario 3): the service that needs it fails visibly and names what it could not reach. It must not start "successfully" and fail later at the first request.
- **First run versus subsequent runs**: the first is slower because images are pulled and built. Both must succeed; only the expected duration differs.
- **Stale data from an earlier version of the schema**: starting against data left by an older version either upgrades it or fails clearly. It must not start with a schema the running code does not match.
- **The machine is below the documented resource floor**: the shortfall is stated up front rather than surfacing as an unexplained container death partway through.
- **Two stacks started at once** (for example, a second checkout): the second either runs alongside the first or refuses with a clear reason — it does not silently attach to the first one's data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A single documented command MUST bring up the entire platform — every service, every dependency, and the storefront — from a clean checkout on a machine with only the documented prerequisites installed.
- **FR-002**: The command MUST NOT report success until every component has reported healthy; a component that is starting, unhealthy, or failed MUST prevent the command from reporting a working stack.
- **FR-003**: Database schemas and the seed catalog MUST be applied automatically as part of that command, with no separate migration or seeding step.
- **FR-004**: The storefront MUST be reachable at a documented URL once the command completes, and MUST support the complete browse → basket → checkout → confirmation flow with no further configuration.
- **FR-005**: The storefront MUST reach the backend through the platform's single entry point when running under this command, exactly as it does when run outside containers.
- **FR-006**: A single documented command MUST stop the stack, removing every container it started and releasing every port it held.
- **FR-007**: A stopped stack MUST restart cleanly with no manual cleanup, preserving data written during previous runs.
- **FR-008**: A separate documented command MUST discard all stored data, so the next start behaves like a first run.
- **FR-009**: When a source file has changed, starting the stack MUST run the changed code rather than silently reusing a stale build.
- **FR-010**: When any component fails to start or become healthy, the command MUST fail visibly and name the component that failed.
- **FR-011**: When a required prerequisite is missing, the command MUST say which one rather than failing partway through for an unrelated-looking reason.
- **FR-012**: The documentation MUST state every prerequisite, the command to start, the command to stop, the command to reset, the URL to open, and the expected first-run and subsequent-run durations — with no step left implicit.
- **FR-013**: Configuration and credentials used by the stack MUST come from environment configuration rather than being written into container images, and the repository MUST carry a template a contributor copies without editing to get a working local run.
- **FR-014**: Every service image MUST build from a clean checkout. Today none of them does: each service's image copies only one of the two shared libraries it now depends on, so every image build fails (see Assumptions).
- **FR-015**: The storefront MUST have a container image; it has none today, and the flow cannot be served from the stack without one.
- **FR-016**: The stack MUST include every service the platform has, including those the shopping flow does not currently exercise, so that "the whole platform runs" is true rather than "the parts this feature happened to need".
- **FR-017**: The stack MUST run the platform's declared message broker and cache as health-gated components, and MUST NOT report success until both are healthy. No service connects to either in this feature — they are present so that every dependency the platform declares is runnable locally, and so the story that first uses one finds it already there.
- **FR-018**: All service databases MUST be hosted on one database server within the stack, with a separate database and a separate connection string per service. No service's configuration may name another service's database.
- **FR-019**: The documentation MUST state that the stack's single database server is a local convenience and not the deployed topology, so that a contributor does not read local consolidation as permission to share a database between services.

### Key Entities

- **Stack Component**: one part of the platform the command starts — a service, a dependency, the storefront, or a one-off setup step. Each has a name a contributor can recognise, a health state, and the components it must wait for.
- **Health Gate**: the condition a component must satisfy before things that depend on it are started, and before the command reports success. This is what makes FR-002 more than a hopeful pause.
- **Stack Data**: everything the platform stores between runs — placed orders, basket contents, the seeded catalog. Survives a stop and restart (FR-007); discarded deliberately by the reset command (FR-008).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a clean checkout, one command takes the platform from nothing to a usable storefront in under 10 minutes on first run and under 3 minutes on subsequent runs.
- **SC-002**: The number of manual steps between cloning the repository and using the storefront is exactly two — copy the configuration template, and run the command — in 100% of first-time setups.
- **SC-003**: A full browse → basket → checkout → confirmation walkthrough completes against the containerized stack in 100% of runs, with no configuration edited after the command.
- **SC-004**: Stopping and restarting the stack leaves zero orphaned containers and zero held ports, across 10 consecutive cycles.
- **SC-005**: Removing any single dependency produces a failure that names the missing component within 2 minutes, in 100% of trials — never a stack that reports success and fails at first use.
- **SC-006**: A contributor following only the written documentation reaches a working storefront without needing any step that is not written down, in 100% of attempts.
- **SC-007**: Every component of the stack reports healthy after a successful start, in 100% of runs.
- **SC-008**: After a reset, the storefront shows the seeded catalog and no prior orders, in 100% of trials.
- **SC-009**: The documented resource floor (memory, disk, CPU) is stated, and the stack runs within it in 100% of runs on a machine meeting it.

## Assumptions

- **The message broker and cache run, but nothing connects to them yet (FR-017).** Verified on 2026-08-17: there is no Redis client, no RabbitMQ client, and no MassTransit reference anywhere in the solution. The constitution assigns Redis the basket store and distributed caching, and RabbitMQ the messaging, but the baskets service is relational today and ADR-0011 records that checkout is synchronous precisely because no messaging exists. Running them anyway is a deliberate, eyes-open choice (see Clarifications): it makes the stack match the platform's declared dependencies and removes a setup step from the story that first needs one. **What it does not do is prove anything about the platform** — a healthy broker with no publisher is a running container, not a working integration. The honest reading of these two health checks is "the dependency is available", never "the dependency works". They become meaningful when SCRUM-18 publishes the first event and when the basket moves to the cache.
- **One database server, one database per service (FR-018).** Chosen for the resource floor: four database servers plus six services plus a broker, a cache, and the storefront is a heavy ask of a laptop, and SC-001 and SC-009 both depend on it. Each service keeps its own database and its own connection string, so the configuration-level isolation the platform enforces is untouched — `tests/CrossServiceIsolation.Tests` scans configuration, not container topology, and passes either way.
- **What consolidation costs, stated plainly.** With one server, isolation between services rests on database names and the configuration scan, not on separate hosts and ports. A contributor who deliberately edited a connection string could reach another service's data locally. This is a narrowing of an existing caveat rather than a new one — `services/README.md` already records that all four local database containers share one password, so credentials never separated them. The per-service-server topology stays available in the existing single-service dependency setup for anyone who wants to demonstrate the deployed shape, and FR-019 requires the documentation to say which is which.
- **Every service image fails to build today (FR-014).** Verified: each service's image copies `shared/ServiceDefaults` only, while every service has referenced `shared/Tenancy` since spec 003 added it. The images have not been built since that dependency appeared. Fixing them is a precondition of this feature, not an optional tidy-up.
- **The storefront has no image (FR-015).** It runs from a development server today. Serving it from the stack means giving it an image and deciding how it learns the backend's address — which will also require the backend's cross-origin allow-list to name whatever origin the containerized storefront is served from, since it currently names the development server's.
- Database schemas and seed data are applied by a dedicated setup step per service that runs before that service starts, mirroring the existing per-service database-creation convention. Making each service migrate its own database at startup was rejected: it races when more than one instance starts, and the platform's services are meant to be horizontally scalable.
- The existing single-service dependency setup is kept alongside this one rather than replaced. It exists to demonstrate that a service runs without its neighbours — and, now, to demonstrate the per-service-server topology this stack consolidates away. The whole-stack command can show neither.
- The stack runs the Phase 1 stub identity and its single tenant, unchanged. Standing up a real identity server is Phase 3 work and is not part of running what exists locally.
- "Fully functional" means the shopping flow the platform currently has. This feature changes how the platform is run, not what it does — no shopper-facing behaviour is added, removed, or altered.
- Observability backends, the CI pipeline, and any production or Kubernetes deployment concern are out of scope. This is the local development experience only.
- The command is expected to be run on the platforms contributors actually use — Windows, macOS, and Linux with a current Docker installation. Anything platform-specific about the command belongs in the documentation FR-012 requires.
