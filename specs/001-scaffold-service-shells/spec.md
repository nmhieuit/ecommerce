# Feature Specification: Scaffold Parties/Products/Baskets/Orders Service Shells

**Feature Branch**: `001-scaffold-service-shells`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/jira/software/projects/SCRUM/boards/1/backlog?selectedIssue=SCRUM-11" — resolved to Jira issue SCRUM-11: "As the Developer, I want scaffolded service shells for parties, products, baskets, and orders using vertical-slice structure so that each service has its own owned datastore and a place to add the thin-slice logic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run one service without standing up the whole platform (Priority: P1)

A developer working on any single one of the four services (parties, products, baskets, orders) needs to start and verify that one service in isolation, without first having to start the other three, a gateway, or a BFF.

**Why this priority**: This is the foundation everything else in the walking skeleton is built on. If a developer can't run one service alone, no other feature work in this phase can begin, and every later story (routing, frontend, tenant context) depends on these four shells existing and working independently.

**Independent Test**: Start only one of the four services with nothing else running, and confirm it comes up and reports itself healthy.

**Acceptance Scenarios**:

1. **Given** a fresh clone of the repository, **When** a developer starts any single one of the four services on its own, **Then** it starts successfully and reports a healthy status without requiring the other three services to be running.
2. **Given** one service is running, **When** a developer checks its status, **Then** the reported status reflects whether the service can actually reach its own dedicated data store, not just that the process is alive.

---

### User Story 2 - Trust that no service can touch another service's data (Priority: P1)

Anyone reviewing or extending the platform (developer, reviewer, or the technical architect) needs confidence that a service cannot accidentally read or write another service's data, because that boundary is what makes the four services independently deployable and independently reasoned about.

**Why this priority**: This is a structural integrity guarantee, not a nice-to-have. If it's not true from the first commit, every service built on top of these shells inherits a hidden coupling that gets progressively more expensive to unwind.

**Independent Test**: Attempt to reach a second service's data from the first service's code/connection and confirm there is no path by which that succeeds.

**Acceptance Scenarios**:

1. **Given** all four services exist, **When** their data storage is inspected, **Then** each service owns a database/schema that no other service's code has any connection or credential to reach.
2. **Given** a service's own data store is unreachable, **When** the service starts, **Then** it does not silently fall back to another service's data store or a shared default.

---

### User Story 3 - Find all the code for a feature in one place (Priority: P2)

A developer (or a new contributor joining later) opening any of the four services needs to find everything related to one piece of functionality — request handling, logic, and data access — organized together, rather than scattered across generic technical-layer folders.

**Why this priority**: This shapes how fast every subsequent story in this phase can be delivered. It's lower priority than P1 because the platform is still usable (if awkwardly) without it — but every later story pays a productivity tax if this isn't in place first.

**Independent Test**: Open any one service and locate all the code for one capability without needing to search across multiple top-level folders organized by technical role.

**Acceptance Scenarios**:

1. **Given** any one of the four services, **When** a developer looks at its folder structure, **Then** code is organized by feature/capability rather than by generic technical layer.

---

### Edge Cases

- What happens when a service starts but its own dedicated data store is unreachable? The service's reported health status MUST reflect that it is not ready, rather than reporting healthy based on process liveness alone.
- What happens when a developer starts only one of the four services instead of all four? Each MUST boot and operate normally — none of the four may depend on another being up at startup.
- What happens if code inside one service attempts to reference or connect to another service's data store? This MUST NOT be possible through any credential, connection string, or shared library the service has access to.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide four separately deployable service shells: parties, products, baskets, and orders.
- **FR-002**: Each of the four services MUST be startable and fully operational on its own, without requiring any of the other three services to be running.
- **FR-003**: Each of the four services MUST expose a status/health check that reflects whether the service is actually able to serve requests, including reachability of its own data store — not merely that its process is running.
- **FR-004**: Each of the four services MUST persist to a data store (database or schema) that belongs exclusively to it — no other service may read or write it.
- **FR-005**: No service's code MUST have any credential, connection, or shared dependency that allows it to reach another service's data store.
- **FR-006**: Each service's internal code MUST be organized around features/capabilities rather than generic technical layers, unless a documented exception justifies otherwise.

### Key Entities

- **Parties service (scaffold)**: owns identity/party-related data for the platform; no business logic beyond the shell is in scope for this feature.
- **Products service (scaffold)**: owns product-catalog data; no business logic beyond the shell is in scope for this feature.
- **Baskets service (scaffold)**: owns shopping-basket data; no business logic beyond the shell is in scope for this feature.
- **Orders service (scaffold)**: owns order data; no business logic beyond the shell is in scope for this feature.

Each entity above represents a service's owned data boundary at this stage, not a finalized data model — the actual schema for each is defined by the feature work that builds on top of these shells.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can go from a fresh clone of the repository to a running, healthy instance of any single one of the four services, on its own, in under 5 minutes.
- **SC-002**: All four services report a healthy status when started independently, 100% of the time, with no dependency on one another.
- **SC-003**: Zero successful cross-service data accesses are possible — verified by a repeatable check that confirms no service can reach another's data store.
- **SC-004**: A developer can locate all the code for a single feature/capability within one service by looking in one place, without searching across multiple technical-layer folders.

## Assumptions

- This feature covers scaffolding only — the four services expose a health check and their own data store, but no business/domain endpoints. Domain behavior (e.g., tenant context, basket pricing, order creation) is delivered by later backlog items in the same phase.
- "Independently runnable" means each service needs only its own dedicated data store to report healthy — it does not require the API gateway, the BFF, or any of the other three services to be running.
- Each service's health check follows the platform's standing practice of distinguishing basic liveness from readiness (actual ability to serve requests, including data-store connectivity); "healthy" in this spec's success criteria refers to the readiness signal.
- The 5-minute local bootstrap target in SC-001 is a reasonable default for a local developer environment; no explicit target was provided in the source request.
- Code organized "by feature/capability" follows the platform's standing default; none of these four initial shells is expected to need a heavier, more layered internal structure — that escalation is reserved for services that later prove they own genuine business invariants.
