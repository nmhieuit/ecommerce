# Phase 0 Research: Gateway → BFF Routing for Products, Baskets, Orders, and Parties

All Technical Context fields were resolvable from the constitution, existing ADRs (0001–0004), and the existing service scaffolds — no `NEEDS CLARIFICATION` markers remain. This document records the concrete decisions made on top of those already-settled choices.

## Decision 1: Gateway routes to the BFF only, not to the four services directly

**Decision**: The gateway's YARP configuration defines exactly one destination cluster — the BFF. It does not define clusters for products/baskets/orders.

**Rationale**: The constitution's Technology Constraints section fixes the edge chain as "load balancer → API gateway → BFF. The BFF is the only backend surface the clients may call." A client request's path through the gateway always terminates at the BFF; the BFF is what talks to products/baskets/orders. This also keeps spec FR-001 ("gateway routes to the correct downstream destination") and FR-002 ("BFF exposes the endpoints the SPA uses") consistent rather than contradictory.

**Alternatives considered**: Gateway routes directly to products/baskets/orders for read-only endpoints and to the BFF for aggregated ones (a hybrid). Rejected — it reintroduces exactly the "client must know service topology" problem the feature exists to remove, and contradicts the constitution's explicit "BFF is the only backend surface" rule.

## Decision 2: YARP configuration is declarative (`appsettings.json`), not code-first

**Decision**: Define the gateway's `ReverseProxy:Routes` / `ReverseProxy:Clusters` sections in `appsettings.json` (environment-overridable), bound via YARP's standard `LoadFromConfig` extension.

**Rationale**: One route/cluster pair (→ BFF) is the entire routing surface for this feature. Declarative config keeps the route table reviewable as data and swappable per environment without a rebuild, matching how connection strings are already externalized for the other services.

**Alternatives considered**: Code-first route/cluster definitions via `IProxyConfigProvider`. Rejected as unnecessary ceremony for a single static destination; revisit if per-tenant or dynamic routing is ever needed.

## Decision 3: BFF-to-downstream calls use typed `HttpClient`s with a standard resilience handler

**Decision**: Register one typed `HttpClient` per downstream service (`ProductsApiClient`, `BasketsApiClient`, `OrdersApiClient`, `PartiesApiClient`) via `AddHttpClient<T>()`, each with `AddStandardResilienceHandler()` (timeout + retry + circuit breaker) configured to the constitution's internal-service-API budget (p95 ≤ 150 ms / p99 ≤ 500 ms) as the per-call timeout ceiling.

**Rationale**: Constitution Principle VIII requires every outbound call to declare an explicit timeout and be wrapped in retry/circuit-breaker policy via `Microsoft.Extensions.Resilience` — this is a direct, named requirement, not a choice among alternatives. Typed clients keep each downstream dependency's base address and policy colocated and testable in isolation.

**Alternatives considered**: A single generic `HttpClient` shared across all four downstream calls with per-call policy applied manually. Rejected — loses the ability to tune/observe each downstream dependency's resilience behavior independently, and makes it easy to accidentally issue an unbounded call by forgetting the manual wrapper.

## Decision 4: Downstream failures surface as `ProblemDetails`, not raw exceptions or hangs

**Decision**: When a downstream call's resilience pipeline exhausts retries, times out, or trips its circuit breaker, the BFF endpoint catches the resulting exception and returns a structured `ProblemDetails` response (502 Bad Gateway for an unreachable/erroring downstream, 504 Gateway Timeout for an exhausted timeout) rather than letting an unhandled exception produce a generic 500 or leaving the caller waiting.

**Rationale**: Directly satisfies spec FR-006 ("clear, well-formed error response within a bounded time instead of hanging indefinitely") and SC-003 (clear error in under 5 seconds). `ProblemDetails` is the ASP.NET Core built-in structured-error convention, requiring no new dependency.

**Alternatives considered**: Let exceptions propagate to the default developer-exception-page/500 handler. Rejected — an unstructured 500 is not "a clear error" a frontend can branch on, and doesn't distinguish "downstream is down" from "the BFF itself is broken."

## Decision 5: BFF integration tests host the real downstream services in-process, not a mock server

**Decision**: `Bff.Api.IntegrationTests` uses `WebApplicationFactory<Program>` for `Products.Api`, `Baskets.Api`, `Orders.Api`, and `Parties.Api` (their real `Program` classes, already `partial class Program` for exactly this purpose) as the downstream targets the BFF's typed clients are pointed at during the test run, rather than a mocking/stub HTTP library.

**Rationale**: Constitution Principle III requires integration tests to exercise real dependencies and explicitly rejects "hand-rolled fakes for infrastructure." The BFF's dependency in this feature is HTTP calls to our own other services, not a database — the "real" counterpart of that dependency is the real service, which already exists as an in-process-testable `WebApplicationFactory` target in this repo. This also directly implements spec Test Scenario 1 ("confirm it proxies to the products service and returns shaped data") against the actual service rather than a stand-in.

**Alternatives considered**: WireMock.Net or a similar HTTP-mocking library standing in for products/baskets/orders. Rejected under Principle III's fakes prohibition; also, since the real services are already in-repo and already support `WebApplicationFactory`, a mock adds a dependency for no capability the real thing doesn't already provide test-locally.

## Decision 6: OpenAPI contract generation uses the built-in ASP.NET Core OpenAPI document, not Swashbuckle

**Decision**: The BFF enables `Microsoft.AspNetCore.OpenApi`'s native OpenAPI document generation for its Minimal API route groups.

**Rationale**: ADR-0003 already selected Minimal APIs partly because "native, built-in OpenAPI document generation" avoids a third-party dependency for the contract itself, and ADR-0004 (Orval codegen) consumes an OpenAPI document as its input regardless of which generator produced it.

**Alternatives considered**: Swashbuckle. Rejected — redundant with the native generator ADR-0003 already committed to, and would be an unjustified deviation from that ADR.

## Decision 7: Tenant/correlation header propagation is forward-only in this feature

**Decision**: The gateway and BFF forward the correlation-ID header (`X-Correlation-Id`, per `ServiceDefaults`) and any tenant header present end-to-end, but neither resolves, fabricates, nor validates tenant identity themselves.

**Rationale**: Full tenant resolution is SCRUM-12's explicit scope (see roadmap Phase 1) and is not yet implemented anywhere in this repo. This feature stays a pure routing/aggregation concern and avoids building a throwaway tenant stub that SCRUM-12 would then need to replace. Recorded as a documented, time-bounded deviation from Principle V in `plan.md`'s Complexity Tracking.

**Alternatives considered**: Build a minimal hardcoded-tenant stub inline as part of this feature. Rejected — duplicates SCRUM-12's scope and risks two divergent tenant-resolution implementations landing back-to-back.
