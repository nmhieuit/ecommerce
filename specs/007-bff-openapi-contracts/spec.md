# Feature Specification: OpenAPI Specs for BFF Routes + Generated Clients

**Feature Branch**: `007-bff-openapi-contracts`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-17 — [CONTRACT-2] OpenAPI specs for BFF routes + generated clients. As the Developer, I want OpenAPI specs written for the products/baskets/orders BFF routes, with client code generated from them, so that the contract is the source of truth and hand-written client drift is impossible (Principle II)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Contract precedes implementation for BFF routes (Priority: P1)

As a developer building or changing a products, baskets, or orders BFF route, I need an OpenAPI
contract for that route to exist and be reviewed before the route's implementation is considered
finished, so that the contract — not the code — is the source of truth for what the BFF exposes.

**Why this priority**: This is the foundational behavior the whole feature exists for. Without a
contract-first workflow, every other capability (generated clients, drift prevention) has nothing
authoritative to generate from.

**Independent Test**: Pick any existing products, baskets, or orders BFF route and confirm an
OpenAPI spec document exists that describes its path, methods, request/response shapes, and status
codes, matching the route's actual behavior.

**Acceptance Scenarios**:

1. **Given** a BFF route for products, baskets, or orders exists, **When** a developer looks for
   its contract, **Then** an OpenAPI spec for that route exists and describes its request and
   response shapes accurately.
2. **Given** a developer is adding a new BFF route, **When** they finalize the implementation,
   **Then** the corresponding OpenAPI spec was authored/updated as part of that work, not added
   afterward as an unreviewed formality.

---

### User Story 2 - SPA API client is fully generated from the contract (Priority: P1)

As a developer working on the SPA, I need the API client code that talks to the BFF's products,
baskets, and orders routes to be generated from the OpenAPI specs, so that no hand-written HTTP
call can drift out of sync with the actual contract.

**Why this priority**: This is the mechanism that makes the contract enforceable day to day — it
is equally critical to the "contract as source of truth" goal, since a contract nobody consumes
provides no protection against drift.

**Independent Test**: Change a field in an OpenAPI spec, regenerate the client, and confirm the
SPA's generated client reflects the change without any manual edits to generated files.

**Acceptance Scenarios**:

1. **Given** the OpenAPI spec for a products, baskets, or orders route changes, **When** the
   client is regenerated, **Then** the SPA's API client for that route is fully generated, with no
   hand-written HTTP calls remaining for it.
2. **Given** the generated client files, **When** they are compared against the OpenAPI spec they
   were generated from, **Then** they match exactly, with no manual edits layered on top.
3. **Given** the SPA codebase, **When** searched for raw `fetch`/`axios` (or equivalent) calls to
   BFF endpoints, **Then** none are found outside the generated client.

---

### User Story 3 - Generated client tolerates unknown response fields (Priority: P2)

As a developer, I need the generated client to keep working when a BFF response contains a field
the client doesn't know about, so that additive, backward-compatible contract changes don't break
the SPA.

**Why this priority**: This protects the versioning/evolution story for the contract once it
exists and is consumed — important for long-term stability, but it only matters once Stories 1 and
2 are in place.

**Independent Test**: Add an unused/unexpected field to a mocked BFF response for a products,
baskets, or orders route and confirm the SPA continues to function without errors or crashes.

**Acceptance Scenarios**:

1. **Given** a consumer (the SPA) receives a BFF response containing a field not present in the
   OpenAPI spec at the time the client was generated, **When** the response is parsed, **Then**
   the generated client does not break, error, or drop the rest of the response.

---

### Edge Cases

- What happens when an OpenAPI spec for a route is missing entirely — should client generation
  fail loudly, or silently skip that route?
- How does the system handle a BFF route whose actual behavior has drifted from its OpenAPI spec
  (e.g., an undocumented field the BFF actually returns)?
- What happens when a required field is removed or renamed in the spec — does regeneration fail
  the build, or does it produce a silently broken client?
- How are the parties/checkout/health-check BFF routes (outside products/baskets/orders) treated —
  excluded from this effort, or expected to follow once this pattern is established?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: An OpenAPI specification MUST exist for every products, baskets, and orders BFF
  route, describing its path, HTTP method, request parameters/body, response shapes, and status
  codes.
- **FR-002**: OpenAPI specs MUST be authored (or updated) as part of finishing a route's
  implementation, not retrofitted after the fact — a route MUST NOT be considered complete without
  a matching, accurate spec.
- **FR-003**: The SPA's API client code for products, baskets, and orders routes MUST be generated
  entirely from the OpenAPI specs; no part of that client code may be hand-written.
- **FR-004**: Regenerating the client from an updated OpenAPI spec MUST produce output that exactly
  matches what the spec describes, with no manual edits required or permitted on generated files.
- **FR-005**: The SPA codebase MUST NOT contain raw HTTP calls (e.g., direct `fetch`/`axios` usage)
  to BFF products, baskets, or orders endpoints outside of the generated client.
- **FR-006**: The generated client MUST tolerate unknown/additional fields in BFF responses without
  raising errors or discarding the rest of the response (tolerant reader behavior).
- **FR-007**: Generated client files MUST be clearly distinguishable from hand-written code (e.g.,
  kept in a dedicated location or clearly marked) so contributors don't mistake them for editable
  source.
- **FR-008**: The process for regenerating the client from specs MUST be a single reproducible
  step that any developer can run locally.

### Key Entities

- **OpenAPI Spec**: A contract document describing one or more BFF routes' request/response shapes
  for a domain area (products, baskets, orders). Authored/reviewed before or alongside the route it
  describes.
- **Generated API Client**: SPA-side code produced mechanically from an OpenAPI spec, used by the
  SPA to call the corresponding BFF routes. Not manually editable.
- **BFF Route**: An HTTP endpoint exposed by the BFF for a domain area, consumed only through its
  generated client.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of products, baskets, and orders BFF routes have a corresponding OpenAPI spec
  that accurately describes their current behavior.
- **SC-002**: A diff between the generated client and its source OpenAPI spec shows zero manual
  modifications to generated files.
- **SC-003**: A codebase search for raw HTTP calls to BFF products/baskets/orders endpoints outside
  the generated client returns zero results.
- **SC-004**: Introducing an unrecognized field into a mocked BFF response causes zero SPA errors
  or crashes in the affected flow.
- **SC-005**: A developer can regenerate the full SPA API client from updated specs in a single
  command, in under one minute.

## Assumptions

- "BFF routes" in scope for this feature are limited to the products, baskets, and orders domain
  areas, matching the Jira issue's explicit scope; parties, checkout, and health-check routes are
  out of scope for this feature.
- The OpenAPI spec format/tooling and the client-generation tool are implementation decisions to be
  made during planning, not fixed by this specification.
- "Tolerant reader" behavior means unknown fields are ignored rather than causing parse failures;
  it does not imply the client exposes those unknown fields to calling code.
- This feature covers the SPA web client only; no other consumers of the BFF are assumed to exist
  at this time.
