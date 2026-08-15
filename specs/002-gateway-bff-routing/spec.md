# Feature Specification: Gateway → BFF Routing for Products, Baskets, Orders, and Parties

**Feature Branch**: `002-gateway-bff-routing`

**Created**: 2026-08-15

**Status**: Draft — **User Story 1 blocked pending a scope decision.** Implementation reached Phase 3 and found that the four underlying services hold and expose no data (see Assumptions, first entry, corrected 2026-08-15). Phases 1 and 2 are built and verified; US1 needs the open scope question resolved before it can proceed. US2's gateway routing (Phase 4) is unaffected and remains implementable.

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-13 — [WALK-1] Wire gateway → BFF routing for the three services. As the Developer, I want the API gateway and BFF wired to route to products, baskets, and orders so that the frontend has a single backend surface to call, per the platform's edge architecture. Acceptance Criteria: (1) the BFF aggregates from the underlying services rather than the SPA calling them directly; (2) a request hitting the gateway reaches the correct downstream service without the client knowing service topology; (3) the BFF contains no business logic beyond aggregation/shaping. Test Scenarios: call the BFF's product-listing route and confirm it proxies to the products service and returns shaped data; confirm the SPA never calls a microservice directly from its network tab; kill the products service and confirm the gateway/BFF returns a clear error rather than hanging."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SPA gets product, basket, order, and party data through one backend surface (Priority: P1)

As a frontend developer building the SPA, I need a single BFF surface that returns product, basket, order, and party (customer) data so that I never have to know the address, protocol, or shape of the underlying products, baskets, orders, or parties services.

**Why this priority**: This is the core value of the feature — without it, the SPA has no single backend surface to call and the rest of the edge architecture has no purpose. Every other story depends on this aggregation existing.

**Independent Test**: Can be fully tested by calling a BFF endpoint (e.g. product listing) with the products, baskets, orders, and parties services running behind it, and confirming the BFF returns correctly shaped data sourced from those services rather than passing the request through unchanged or requiring the caller to know their individual addresses.

**Acceptance Scenarios**:

1. **Given** the products, baskets, orders, and parties services are running and reachable, **When** the SPA calls the BFF's product-listing route, **Then** the BFF proxies the call to the products service and returns shaped data to the caller.
2. **Given** a page needs data from more than one underlying service, **When** the SPA calls the relevant BFF endpoint, **Then** the BFF aggregates the responses from the underlying services into a single response rather than requiring the SPA to make multiple calls itself.
3. **Given** the BFF's code, **When** it is inspected, **Then** it contains only request aggregation and response shaping — no domain or business rules.

---

### User Story 2 - Gateway routes requests without exposing service topology (Priority: P2)

As a frontend developer, I need the gateway to route every request to the correct downstream destination so that the client only ever needs to know one entry point and never the internal layout of services.

**Why this priority**: This makes the single-entry-point promise real at the network level. It matters once User Story 1's aggregation exists, since the gateway is what gets requests to the BFF (and to any service the BFF is not fronting) in the first place.

**Independent Test**: Can be fully tested by sending a request to the gateway and confirming it reaches the correct downstream destination — and by inspecting the SPA's network traffic to confirm it only ever addresses the gateway/BFF, never a products, baskets, orders, or parties service directly.

**Acceptance Scenarios**:

1. **Given** a request arrives at the gateway, **When** it is routed, **Then** it reaches the correct downstream destination without the caller having specified anything about internal service topology.
2. **Given** the SPA is running against the deployed edge, **When** its network traffic is inspected, **Then** no request targets a products, baskets, orders, or parties service directly — every request goes through the gateway/BFF.

---

### User Story 3 - Clear failure when a downstream service is unavailable (Priority: P3)

As a frontend developer, I need the gateway/BFF to fail clearly and quickly when a downstream service is down so that the SPA can show a meaningful error instead of hanging indefinitely.

**Why this priority**: This is a precursor check for later resilience work (circuit breakers, retries) rather than a full resilience implementation. It's lower priority than getting routing and aggregation working, but still needed so the feature doesn't ship with unbounded waits.

**Independent Test**: Can be fully tested by stopping the products service and calling the affected BFF route, confirming a clear, bounded error response is returned instead of the request hanging.

**Acceptance Scenarios**:

1. **Given** the products service is stopped, **When** the SPA calls a BFF route that depends on it, **Then** the gateway/BFF returns a clear error response within a bounded time rather than hanging.

---

### Edge Cases

- What happens when a BFF request needs data from two services and one of the two calls fails? The BFF MUST NOT return a silently incomplete or malformed response; it MUST return a clear error or a well-defined partial result, consistent with User Story 3.
- What happens when the gateway receives a request path that doesn't match any known route? The gateway MUST return a clear not-found response rather than hanging or leaking internal routing details.
- What happens when a downstream service responds slowly rather than being fully down? The gateway/BFF MUST NOT wait indefinitely; the request MUST fail clearly once a bounded time is exceeded.
- What happens when the SPA (or any other caller) attempts to call the products, baskets, orders, or parties service directly instead of going through the gateway/BFF? That path is out of scope for this feature to block at the network layer, but User Story 2's acceptance scenario 2 confirms the SPA itself never does this.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The gateway MUST route every incoming client request to the correct downstream destination (BFF, or a service the BFF fronts) based on the request path, without requiring the caller to know internal service topology.
- **FR-002**: The BFF MUST expose the endpoints the SPA uses to read product, basket, order, and party data; the SPA MUST call these BFF endpoints instead of calling the products, baskets, orders, or parties services directly.
- **FR-003**: The BFF MUST aggregate data from the products, baskets, orders, and parties services when fulfilling a request that spans more than one of them, returning a single combined response to the caller.
- **FR-004**: The BFF's product-listing route MUST proxy to the products service and return shaped data.
- **FR-005**: The BFF MUST contain no business logic beyond aggregating and shaping responses from the underlying services — no domain rules, validation beyond request shape, or persistence.
- **FR-006**: When a downstream service the gateway/BFF depends on is unavailable, the gateway/BFF MUST return a clear, well-formed error response within a bounded time instead of hanging indefinitely.
- **FR-007**: The gateway MUST reject or clearly error on requests to paths that don't match a known route, rather than hanging.

### Key Entities

- **Route Mapping**: The association between an inbound request path at the gateway and the downstream destination (BFF, or a fronted service) it resolves to.
- **Aggregated Response**: The single, shaped response the BFF returns to a caller after combining data retrieved from one or more of the products, baskets, orders, and parties services.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the SPA's product, basket, order, and party data requests, as observed on its network tab, go through the gateway/BFF — zero direct calls to a products, baskets, orders, or parties service.
- **SC-002**: The BFF's product-listing route returns correctly shaped data sourced from the products service in every test run, with no manual intervention required.
- **SC-003**: When a depended-on downstream service is unavailable, callers receive a clear error response in under 5 seconds, in 100% of observed cases, instead of an indefinite wait.
- **SC-004**: A code review of the BFF finds zero instances of business logic beyond aggregation and response shaping.

## Assumptions

- The gateway and BFF technology choices are already settled by existing architecture decision records; this feature wires routing and aggregation on top of those choices rather than re-selecting technology.
- Scope covers all four Phase-1 domain services — products, baskets, orders, and parties — rather than only the three named in the source ticket's text; parties was scaffolded alongside the other three (same shell convention, same "scaffold only" state) and stakeholder direction during planning confirmed it belongs in this feature's routing surface too.
- ~~The products, baskets, orders, and parties services already expose the HTTP APIs needed for the BFF to call them; building or changing those underlying service APIs is out of scope here.~~ **Corrected 2026-08-15 during implementation — this assumption was false.** None of the four services exposes any data-bearing endpoint: each maps only its two health probes, and each service's database context holds no records of any kind (they were scaffolded deliberately empty, with the first domain story left to add them). There is therefore no product listing for the BFF's product-listing route to draw on, and nothing behind the basket, order, or party routes either. Whether those underlying services gain data-bearing endpoints — and which of them do — is now an **open scope decision for this feature**, not a settled out-of-scope boundary. Until it is decided, FR-002, FR-003, and FR-004 cannot be satisfied end-to-end, since each of them describes returning real product, basket, order, or party data to the caller.
- Authentication/token validation at the gateway and tenant-context propagation are assumed to be handled by existing or separately tracked work; this feature covers request routing and response aggregation, not auth or tenant resolution.
- "Clear error" means a structured, bounded-time error response distinct from an unhandled exception or an indefinite wait; the specific retry/circuit-breaker mechanics are deferred to the Phase 4 resilience work this feature is a precursor check for.
