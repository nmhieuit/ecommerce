# Feature Specification: Consumer-Driven Contract Tests Across BFF/Service Boundaries

**Feature Branch**: `011-consumer-contract-tests`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "Jira SCRUM-21: [CONTRACT-2] Consumer-driven contract tests across BFF/service boundaries. As QA, I want consumer-driven contract tests on every HTTP and event boundary so that a producer breaking a published contract fails its own build, not a downstream consumer's (Principle III). Boundaries: BFF↔products, BFF↔baskets, BFF↔orders (HTTP), and event producer↔consumer. https://nmhieuit.atlassian.net/browse/SCRUM-21"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A breaking HTTP response change fails the producer's own build (Priority: P1)

As QA, I need each of the products, baskets, and orders services to verify its real HTTP responses
against the BFF's documented expectations as part of its own build, so that a service which breaks
what the BFF relies on discovers that immediately — in its own pipeline — instead of a downstream
consumer discovering it later in production.

**Why this priority**: This is the foundational guarantee the whole feature exists for on the HTTP
side. Without it, a breaking change to any of the three BFF-facing services can reach production
undetected.

**Independent Test**: Change a field name in one service's (products, baskets, or orders) real HTTP
response so it no longer matches what the BFF expects, run that service's own test suite, and
confirm the contract test fails before the change can merge.

**Acceptance Scenarios**:

1. **Given** the BFF's documented expectations of the products service's responses, **When** the
   products service's own build runs, **Then** it verifies its real responses against those
   expectations and fails the build if they diverge.
2. **Given** the BFF's documented expectations of the baskets service's responses, **When** the
   baskets service's own build runs, **Then** it verifies its real responses against those
   expectations and fails the build if they diverge.
3. **Given** the BFF's documented expectations of the orders service's responses, **When** the
   orders service's own build runs, **Then** it verifies its real responses against those
   expectations and fails the build if they diverge.
4. **Given** a producer changes a response shape in a breaking way, **When** its build runs,
   **Then** the contract test fails before the change can merge.

---

### User Story 2 - A breaking event payload change fails the publishing service's own build (Priority: P2)

As QA, I need the service that publishes an integration event to verify its published event payload
against the consuming service's documented expectations as part of its own build, so that a breaking
change to an event contract is caught at the source rather than surfacing as a silent failure in a
downstream consumer.

**Why this priority**: Event-driven communication is the platform's default integration style
(Principle IV), so leaving the event boundary uncovered would leave the highest-volume integration
path unprotected. It is P2 rather than P1 because, unlike the three HTTP boundaries, no service yet
publishes or consumes these events in production (see Assumptions) — this story establishes the
pattern on one representative event pair as a pilot, ahead of the broker wiring that lands
separately.

**Independent Test**: Change a field in the published event payload so it no longer matches the
consuming service's documented expectation, run the publishing service's own test suite, and
confirm the contract test fails before the change can merge.

**Acceptance Scenarios**:

1. **Given** a consuming service's documented expectations of a published integration event,
   **When** the publishing service's own build runs, **Then** it verifies the event payload it
   would emit against those expectations and fails the build if they diverge.
2. **Given** a publishing service changes an event's shape in a breaking way, **When** its build
   runs, **Then** the contract test fails before the change can merge.

---

### User Story 3 - Boundary contract-test coverage is auditable (Priority: P3)

As QA, I need to be able to list every HTTP and event boundary in the thin slice and confirm each
one has a corresponding contract test, so that coverage gaps are visible rather than assumed.

**Why this priority**: Establishing coverage once is only useful if it stays true. This story makes
the coverage state checkable on demand, and gives the team a way to notice — rather than discover
in an incident — when a boundary silently loses its contract test.

**Independent Test**: List all HTTP/event boundaries in the thin slice and cross-check each one has
a corresponding contract test; then remove one contract test intentionally and confirm the gap is
caught.

**Acceptance Scenarios**:

1. **Given** the four boundaries in the thin slice (BFF↔products, BFF↔baskets, BFF↔orders, and the
   piloted event producer↔consumer pair), **When** someone lists them, **Then** every one has a
   corresponding contract test that can be located without reading service internals.
2. **Given** a required contract test is removed, **When** the coverage check runs (automated gate)
   or a reviewer performs the equivalent manual check (if no automated gate exists yet), **Then**
   the gap is caught before merge.

---

### Edge Cases

- What happens when a service has zero documented consumer expectations for one of its responses
  (e.g., a brand-new BFF route not yet covered)? The boundary is missing required coverage and
  User Story 3's audit MUST surface it as a gap, not silently pass.
- How does the system verify the event boundary when no broker is deployed and no service actually
  publishes or consumes the event in a running environment yet? Verification MUST work against the
  producer's constructed payload and the consumer's documented expectation directly, without
  requiring a live message broker or an end-to-end delivery.
- What happens when a consumer's documented expectation references a field the producer's real
  response never returns? The producer's own build MUST fail; it must not be possible for this gap
  to only surface in the consumer's build or in production.
- What happens when a producer adds a new field the consumer doesn't know about? Per Principle II,
  consumers MUST tolerate unknown fields, so an added field alone MUST NOT fail the contract test.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The products service MUST verify its real HTTP responses to the BFF against the BFF's
  documented consumer expectations as part of the products service's own build.
- **FR-002**: The baskets service MUST verify its real HTTP responses to the BFF against the BFF's
  documented consumer expectations as part of the baskets service's own build.
- **FR-003**: The orders service MUST verify its real HTTP responses to the BFF against the BFF's
  documented consumer expectations as part of the orders service's own build.
- **FR-004**: The service publishing the piloted integration event MUST verify its constructed event
  payload against the consuming service's documented expectations as part of the publishing
  service's own build.
- **FR-005**: When a producer's real response (or a publisher's event payload) diverges from a
  consumer's documented expectation, the producer's (or publisher's) own build MUST fail — the
  failure MUST NOT surface only in a downstream consumer's build or later.
- **FR-006**: Contract verification MUST exercise the producer's real behavior (an actual HTTP
  response, or an actual constructed event payload), not a hand-maintained substitute for the
  producer.
- **FR-007**: An added field a consumer does not reference MUST NOT cause a contract test to fail,
  consistent with the platform's tolerant-reader rule.
- **FR-008**: The full set of HTTP and event boundaries in the thin slice (BFF↔products,
  BFF↔baskets, BFF↔orders, and the piloted event producer↔consumer pair) MUST be enumerable and
  cross-checkable against the set of existing contract tests.
- **FR-009**: A boundary in the thin slice missing its required contract test MUST be detectable —
  by an automated coverage check where one exists, or documented as a manual review item where it
  does not yet.

### Key Entities

- **Boundary**: A named pairing of one consumer and one producer for a single interaction — either
  an HTTP request/response (BFF↔products, BFF↔baskets, BFF↔orders) or a published/consumed
  integration event. Boundaries are what User Story 3's coverage audit enumerates.
- **Consumer Expectation**: The documented shape a consumer relies on from a specific producer
  interaction — the basis a contract test verifies the producer's real behavior against.
- **Verification Result**: The pass/fail outcome of running a producer's (or publisher's) contract
  test against its own real behavior, produced as part of that service's own build.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the four boundaries in the thin slice (BFF↔products, BFF↔baskets,
  BFF↔orders, and the piloted event producer↔consumer pair) have an associated contract test.
- **SC-002**: When a producer's or publisher's shape breaks a documented consumer expectation, that
  producer's or publisher's own build fails in the same build run, in 100% of cases tested across
  all four boundaries — never only in a downstream consumer's build.
- **SC-003**: A reviewer can determine which of the four thin-slice boundaries have contract-test
  coverage in under 5 minutes, using only the contract test inventory, without reading service
  source code.
- **SC-004**: Removing a required contract test for a covered boundary is caught before merge —
  either by an automated gate failing, or by a documented manual-review step — in 100% of cases.

## Assumptions

- The "thin slice" for this feature is the three HTTP boundaries between the BFF and the products,
  baskets, and orders services, plus one representative event producer↔consumer pair (drawn from
  `BasketCheckedOut` and/or `OrderPlaced`), matching ADR-0006's phased rollout (pilot one event
  boundary before platform-wide adoption).
- No service currently publishes or consumes `BasketCheckedOut` or `OrderPlaced` in a running
  environment — the message broker wiring and outbox-backed publishing are tracked separately
  (SCRUM-31) and are not a prerequisite for this feature. The event-boundary contract test verifies
  the producer's constructed payload against the consumer's documented expectation directly, without
  depending on a live broker or end-to-end delivery.
- The BFF's documented request/response expectations for products, baskets, and orders routes are
  the OpenAPI contracts already established for those routes (delivered under
  `007-bff-openapi-contracts`).
- "The producer's own build" means that producer service's own CI pipeline/test run — not a shared
  or downstream pipeline.
- Extending contract-test coverage to any HTTP or event boundary outside this thin slice is future
  work and out of scope here.
