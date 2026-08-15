# Phase 1 Data Model: Gateway → BFF Routing for Products, Baskets, Orders, and Parties

This feature owns no database (see `plan.md` Technical Context — Storage: N/A). The "entities" below are configuration and in-flight message shapes, not persisted records; they exist for the lifetime of a single request.

## Route Mapping (gateway, configuration)

The gateway's YARP route/cluster configuration (Decision 2, `research.md`). One row per configured route.

| Field | Description | Notes |
|---|---|---|
| RouteId | Unique identifier for the route entry | e.g. `bff-route` |
| Match.Path | Inbound path pattern the gateway matches | `{**catch-all}` — all API traffic forwards to the BFF |
| ClusterId | The destination cluster this route forwards to | Always the single `bff-cluster` for this feature (Decision 1) |
| ClusterId → Destination.Address | The BFF's base address | Environment-supplied, not baked into the image |
| Timeout | Per-request timeout the gateway enforces before failing the proxied call | Bounded per constitution Principle VIII; must be ≥ the BFF's own downstream timeout budget so the gateway doesn't cut a request off that the BFF was still legitimately waiting on |

**Validation rules**: Exactly one cluster (`bff-cluster`) exists for this feature's scope; a route with no matching cluster is a configuration error and MUST fail startup, not route silently to nothing.

## Downstream Service Client (BFF, configuration)

One per downstream service the BFF calls (products, baskets, orders, parties). Not persisted — resolved from configuration at startup into a typed `HttpClient` (Decision 3).

| Field | Description | Notes |
|---|---|---|
| ServiceName | Logical name of the downstream service | `ProductsApi`, `BasketsApi`, `OrdersApi`, `PartiesApi` |
| BaseUrl | Base address the typed client calls | `Services:{ServiceName}:BaseUrl`, environment-supplied |
| Timeout | Per-call timeout ceiling | Internal-service-API budget — p95 ≤ 150 ms / p99 ≤ 500 ms (constitution Principle VIII default) |
| RetryPolicy | Retry attempts + backoff before giving up | Standard resilience handler default, applied uniformly across all four clients for this feature |
| CircuitBreaker | Failure-ratio threshold that opens the circuit | Standard resilience handler default |

**Validation rules**: A downstream client with no configured `BaseUrl` is a startup configuration error, not a runtime null-reference — fail fast rather than fail per-request.

## Aggregated Response (BFF, in-flight only)

The shaped payload the BFF returns to a caller (spec Key Entity: "Aggregated Response"). Shape varies per route; the two concrete shapes needed for this feature's minimum scope (spec US1 Acceptance Scenario 1, Test Scenario 1) are:

### Product listing response (`GET /bff/products`)

| Field | Description | Source |
|---|---|---|
| items[] | List of shaped product summaries | Proxied 1:1 from the products service's listing response |
| items[].id | Product identifier | Products service |
| items[].name | Product display name | Products service |
| items[].price | Current price | Products service |

This route is a proxy-and-shape, not a multi-service aggregation — it establishes the pattern Test Scenario 1 verifies before basket/order routes (which do combine more than one service) are added.

### Error response (any BFF route, downstream failure)

Follows RFC 7807 `ProblemDetails` (Decision 4).

| Field | Description |
|---|---|
| type | A URI identifying the error category (e.g. `https://ecommerce.internal/errors/downstream-unavailable`) |
| title | Short human-readable summary, e.g. "Downstream service unavailable" |
| status | HTTP status code — 502 (downstream error) or 504 (downstream timeout) |
| detail | Which downstream dependency failed, without leaking internal addressing beyond the service's logical name |
| correlationId | The request's `X-Correlation-Id`, so the error is traceable in the shared observability stack (Principle VII) |

**Validation rules**: An error response MUST always include `correlationId` and MUST NOT include the downstream service's internal URL/address — only its logical name — so the error is diagnosable without leaking topology to the client (consistent with spec FR-001's "without the client knowing service topology").

## State Transitions

None — every entity here is either static configuration resolved once at startup or a value that exists only for the duration of a single request/response cycle. No entity in this feature has a persisted lifecycle.
