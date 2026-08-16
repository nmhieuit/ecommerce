# Feature Specification: Stub Identity with Resolved Tenant Context

**Feature Branch**: `003-stub-identity-tenant-context`

**Created**: 2026-08-15

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-12 — [WALK-1] Stub identity with a resolved tenant context. As the Developer, I want a single fake authenticated user whose request always carries a resolved tenant context so that no code path can reach persistence without one, even before the real identity server exists (Principle V). Acceptance Criteria: (1) a request entering at the gateway carries an explicit tenant identifier through gateway → BFF → services; (2) a service handler touching persistence requires the tenant identifier to resolve the connection — no default/fallback tenant; (3) the tenant source is hardcoded for Phase 1, and when Phase 3 replaces it with real token claims, only the resolution source changes, not the propagation mechanism. Test Scenarios: trace a single request end to end and confirm the tenant ID is visible in logs at every hop; attempt to call a persistence method with no tenant context and confirm it throws/fails rather than silently defaulting; grep the codebase for any repository/DB call that does not require a tenant parameter and expect zero results."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tenant identity is resolved once and propagated explicitly end to end (Priority: P1)

As the Developer, I need every request's tenant identifier resolved once at the edge and carried explicitly through the gateway, the BFF, and every service it reaches, so that I can trust the tenant context at any hop without re-deriving or guessing it.

**Why this priority**: This is the foundation the rest of the feature depends on — enforcement (User Story 2) is meaningless if the tenant identifier never reliably reaches the services that need it.

**Independent Test**: Can be fully tested by sending one request through the gateway and inspecting the logs the gateway, the BFF, and the service that ultimately handles it each produce, confirming the same tenant identifier appears at every hop.

**Acceptance Scenarios**:

1. **Given** a request enters at the gateway, **When** it is forwarded to the BFF and then to a downstream service, **Then** the same explicit tenant identifier is attached at every hop.
2. **Given** a single request's logs at the gateway, the BFF, and the service that handled it, **When** they are compared, **Then** the tenant identifier recorded is identical and visible in all of them.

---

### User Story 2 - No persistence access without a resolved tenant (Priority: P2)

As the Developer, I need every service's persistence access to require a resolved tenant identifier so that no code path can ever read or write data without knowing which tenant it belongs to.

**Why this priority**: This is the actual security property being protected — propagation alone (User Story 1) only carries the identifier around; this makes the requirement enforced rather than advisory, closing the gap a forgotten check could otherwise leave open.

**Independent Test**: Can be fully tested by invoking a persistence-touching operation with no tenant context resolved and confirming it fails rather than defaulting, and separately by scanning the codebase's repository/persistence call sites and confirming every one of them requires an explicit tenant parameter.

**Acceptance Scenarios**:

1. **Given** a service handler is invoked without a resolved tenant context, **When** it attempts to touch persistence, **Then** the attempt fails/throws rather than silently proceeding with some default tenant.
2. **Given** the codebase, **When** every repository/persistence call site is inspected, **Then** each one requires an explicit tenant parameter — there are zero call sites that don't.

---

### Edge Cases

- What happens when a service receives a request with no tenant identifier attached at all (e.g., the gateway was bypassed)? Persistence access MUST still fail per User Story 2 — there is no default tenant to fall back to.
- What happens if the tenant identifier a service observes doesn't match what the gateway originally resolved (propagation corrupted or overwritten mid-flight)? This MUST be treated as no tenant being resolved, not as a valid-but-different tenant — persistence MUST NOT proceed.
- What happens when the hardcoded Phase 1 tenant source is later replaced with real identity-server-issued token claims? Only the resolution step changes; the propagation mechanism (how the identifier moves gateway → BFF → services) and the persistence-side enforcement MUST remain unaffected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST resolve a single tenant identifier once, at the gateway, for every incoming request. For Phase 1, this resolution source is a hardcoded stub identity rather than a real identity server.
- **FR-002**: The system MUST propagate the resolved tenant identifier explicitly through gateway → BFF → services on every request, via a mechanism that is not user-editable request content (never inferred from a query parameter or body field).
- **FR-003**: Every service MUST make the resolved tenant identifier available to its own handlers before any persistence access occurs.
- **FR-004**: Persistence access MUST require a resolved tenant identifier to proceed; there MUST NOT be a default or fallback tenant used when none is resolved.
- **FR-005**: When persistence is attempted without a resolved tenant identifier, the system MUST fail/throw rather than silently proceeding.
- **FR-006**: The tenant identifier MUST be visible in each hop's logs for a given request, so a single request is traceable end to end by its tenant identifier.
- **FR-007**: The tenant-resolution source MUST be replaceable (e.g., by real token claims in a later phase) without requiring changes to how the tenant identifier is propagated or enforced downstream.
- **FR-008**: For Phase 1, exactly one tenant identity MUST be resolvable; the resolution and propagation mechanism MUST NOT assume only one tenant will ever exist.

### Key Entities

- **Tenant Context**: The resolved tenant identifier attached to a request, present at every hop from the gateway through to whichever service ultimately touches persistence on that request's behalf.
- **Stub Identity**: The single hardcoded fake authenticated user Phase 1 uses in place of a real identity server; resolving it is what yields the tenant context for a request.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of traced requests show an identical tenant identifier present in the logs at the gateway, the BFF, and the service that handled them.
- **SC-002**: 100% of attempts to access persistence without a resolved tenant identifier fail, with zero silent successes, across every domain service.
- **SC-003**: A repository-wide scan finds zero persistence call sites that do not require an explicit tenant parameter.
- **SC-004**: Swapping the tenant-resolution source for a real identity-server-issued claim touches only the resolution step — zero changes are required to gateway routing, header propagation, or any service's persistence guard.

## Assumptions

- Phase 1 needs exactly one resolvable tenant; multi-tenant switching or any tenant-selection UI is out of scope for this feature (the platform still resolves a real tenant context end to end, just from a hardcoded source, per the roadmap).
- No real identity server exists yet — the "fake authenticated user" is a Phase 1 stand-in for a resolved identity, not a login flow; credential entry, session management, and sign-out are out of scope here.
- Propagating tenant context onto asynchronous integration events is out of scope for this feature, since no messaging infrastructure exists yet in the repository; that propagation hop applies once a future feature introduces it.
- This feature adds tenant resolution, propagation, and persistence enforcement on top of the gateway, BFF, and four domain services (parties, products, baskets, orders) that already exist; it does not add new domain functionality to any of them.
- "Persistence access" means any repository-level read or write of stored data in a domain service. The gateway and BFF do not persist data themselves and are affected only by the propagation requirement, not the enforcement requirement.
