# Feature Specification: Versioned Event Schemas — OrderPlaced, BasketCheckedOut

**Feature Branch**: `008-versioned-event-schemas`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-18 — [CONTRACT-2] Versioned event schemas: OrderPlaced, BasketCheckedOut. As the Developer, I want versioned event schemas for OrderPlaced and BasketCheckedOut in a shared contracts location so that future consumers can integrate against a stable, evolvable contract (Principle II)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Event schemas live in one shared, authoritative location (Priority: P1)

As a developer publishing or consuming the `OrderPlaced` or `BasketCheckedOut` event, I need its
schema to be defined once in a shared contracts location, so that the publishing service isn't the
de facto source of truth and every consumer integrates against the same definition.

**Why this priority**: Every other capability in this feature — versioning, deprecation windows,
tolerant reading — presupposes a single authoritative schema location. Without it, there is nothing
consistent to version.

**Independent Test**: For each of `OrderPlaced` and `BasketCheckedOut`, confirm the schema is
defined in the shared contracts location and that the publishing service (Orders for `OrderPlaced`,
the checkout flow for `BasketCheckedOut`) references that definition rather than declaring its own
inline copy.

**Acceptance Scenarios**:

1. **Given** the `OrderPlaced` or `BasketCheckedOut` event schema, **When** a developer looks for
   where it is defined, **Then** it lives in the shared contracts location, not inline in the
   publishing service.
2. **Given** a publishing service emits `OrderPlaced` or `BasketCheckedOut`, **When** its code is
   inspected, **Then** it references the shared schema definition rather than duplicating the
   field/type definitions locally.

---

### User Story 2 - Breaking schema changes are versioned, not silently shipped (Priority: P1)

As a developer evolving the `OrderPlaced` or `BasketCheckedOut` schema, I need a breaking change to
require a new explicit version while the previous version keeps working for a documented
deprecation window, so that existing consumers are never broken out from under them by a schema
edit.

**Why this priority**: This is the core promise of "stable, evolvable contract" from the Jira story
and Principle II. Without enforcement, versioning is a convention that erodes the first time someone
is in a hurry.

**Independent Test**: Add a new required field to a published event schema without bumping its
version and confirm this is caught by a compatibility check before the change can ship. Separately,
ship a properly versioned breaking change and confirm the previous version's schema is still present
and documented as supported through its deprecation window.

**Acceptance Scenarios**:

1. **Given** a schema change to `OrderPlaced` or `BasketCheckedOut` is breaking (e.g., a new
   required field, a removed field, or a changed field type), **When** it is shipped, **Then** it
   carries a new explicit version and the previous version keeps working for a documented
   deprecation window.
2. **Given** a developer adds a new required field to a schema without introducing a new version,
   **When** the change is validated, **Then** the compatibility check fails and blocks the change.
3. **Given** a schema version has been superseded and is inside its deprecation window, **When** a
   consumer still built against that version processes an event, **Then** it continues to work as
   documented.

---

### User Story 3 - Consumers tolerate unknown fields (Priority: P1)

As a developer consuming `OrderPlaced` or `BasketCheckedOut`, I need my consumer to keep working
when an event contains fields my code doesn't know about, so that an older consumer never crashes
just because a newer, backward-compatible producer added a field.

**Why this priority**: This is what makes additive, non-breaking evolution possible at all — without
it, every field addition would effectively be a breaking change in practice, defeating the point of
versioning.

**Independent Test**: Simulate an older consumer (built against an earlier schema version) reading
an event published under a newer version that includes extra fields, and confirm deserialization
succeeds with no crash and no loss of the fields the consumer does recognize.

**Acceptance Scenarios**:

1. **Given** a consumer receives an `OrderPlaced` or `BasketCheckedOut` event containing fields
   unknown to that consumer, **When** the event is deserialized, **Then** deserialization does not
   fail and the fields the consumer does recognize are still correctly available.
2. **Given** an `OrderPlaced` event is published and validated, **When** it is checked against its
   published schema, **Then** it validates successfully.

---

### Edge Cases

- What happens when a schema change is purely additive (a new optional field) — must it still bump
  the version, or can it ship under the current version?
- How is the end of a deprecation window enforced — does the previous schema version simply stop
  being documented as supported, or is there an active removal/rejection step?
- What happens when a consumer encounters an event version newer than any version it has schema
  knowledge of at all (not just unknown fields, but an unrecognized version identifier)?
- What happens when a publishing service attempts to emit an event that doesn't match any version of
  the shared schema (a malformed or drifted payload)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Versioned schemas for `OrderPlaced` and `BasketCheckedOut` MUST be defined in a shared
  contracts location, not inline in the service that publishes them.
- **FR-002**: Each event schema version MUST carry an explicit, unambiguous version identifier that
  producers and consumers can use to distinguish it from other versions of the same event.
- **FR-003**: A breaking change to a published event schema (new required field, removed field,
  changed field type/semantics) MUST ship as a new explicit version rather than an edit to the
  existing version.
- **FR-004**: When a new version of an event schema is introduced to replace a breaking change, the
  previous version MUST remain defined and functional for a documented deprecation window.
- **FR-005**: The deprecation window and the conditions under which a superseded schema version may
  be removed MUST be documented alongside the schema itself.
- **FR-006**: A compatibility check MUST validate schema changes to `OrderPlaced` and
  `BasketCheckedOut` and MUST fail when a breaking change is made without a corresponding new
  version.
- **FR-007**: Consumers of `OrderPlaced` and `BasketCheckedOut` MUST NOT fail deserialization when an
  event instance contains fields the consumer's schema knowledge doesn't include.
- **FR-008**: Each event schema MUST document its fields, which are required versus optional, and
  its version history, in a form discoverable from the shared contracts location.
- **FR-009**: An `OrderPlaced` event produced by the publishing service MUST validate successfully
  against its published schema.

### Key Entities

- **Event Schema**: A versioned contract document (for `OrderPlaced` or `BasketCheckedOut`) defining
  the event's fields, required/optional status, and version history. Lives in the shared contracts
  location, independent of any single publishing service.
- **Event Version**: One specific, explicitly identified iteration of an event schema (e.g. an
  initial version and any subsequent versions introduced for breaking changes), with its own field
  set and a defined support/deprecation status.
- **Contract Compatibility Check**: The validation step that compares a proposed schema change
  against the currently published version and flags breaking changes that were not accompanied by a
  new version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of `OrderPlaced` and `BasketCheckedOut` schema definitions live in the shared
  contracts location, with zero inline/duplicated schema definitions remaining in the publishing
  services.
- **SC-002**: A breaking change introduced to `OrderPlaced` or `BasketCheckedOut` without a new
  version is caught by the compatibility check before it can merge, in 100% of tested cases.
- **SC-003**: A consumer built against an older schema version successfully processes a newer,
  additive-only version of the same event with zero deserialization failures, across all tested
  scenarios.
- **SC-004**: A developer can find the current version, any still-supported prior versions, and the
  deprecation window for `OrderPlaced` or `BasketCheckedOut` entirely from the shared contracts
  location, without consulting another service's source code.

## Assumptions

- The exact schema format and shared-location mechanics (e.g., a shared package versus a shared
  folder) are implementation decisions for planning; Principle II only requires "a shared contracts
  location," and prior platform decisions (ADR-0005) already settled on JSON Schema in a shared
  contracts package rather than a separate registry service.
- The precise duration of the deprecation window (e.g., a fixed number of days or release cycles) is
  a documentation/policy detail to be settled during planning; this specification only requires that
  a window exists, is documented, and is honored.
- The compatibility check required by FR-006 is scoped to this feature as a schema-level check (e.g.
  comparing a proposed schema against the previously published one). Full cross-consumer,
  producer-build-breaking contract testing is the separate, not-yet-delivered consumer-driven
  contract testing effort (tracked outside this feature); this feature's check does not depend on
  that infrastructure existing first.
- Consumers of `OrderPlaced` and `BasketCheckedOut` are other services within this platform (e.g.
  Logistics and Invoices consuming `OrderPlaced`; Orders consuming `BasketCheckedOut` to create an
  order); this feature does not assume any external, third-party consumer.
- Only the `OrderPlaced` and `BasketCheckedOut` event schemas are in scope; other integration events
  in the system are out of scope for this feature.
