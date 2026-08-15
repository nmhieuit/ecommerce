# Quickstart: Validate Gateway → BFF Routing

Validates the feature end-to-end against spec.md's acceptance scenarios and test scenarios. See [data-model.md](data-model.md) for the response shapes referenced below and [contracts/bff-openapi.yaml](contracts/bff-openapi.yaml) for the full route contract.

## Prerequisites

- .NET 10 SDK installed.
- Docker available (products/baskets/orders/parties each need their own SQL Server dependency container — see `docker-compose.deps.yml`).
- This feature's services built: `services/gateway/src/Gateway.Api`, `services/bff/src/Bff.Api`.

## Setup

1. Start each domain service's own database dependency (repeat per service, per `docker-compose.deps.yml`'s existing convention):

```bash
docker compose -f docker-compose.deps.yml up --wait products-db-init baskets-db-init orders-db-init parties-db-init
```

2. Create each service's schema. `docker-compose.deps.yml` creates empty databases only — tables are EF Core migrations' job, and a service started against an empty database connects successfully and then fails on its first query. The tooling is pinned in `.config/dotnet-tools.json`, so restore it first:

```bash
dotnet tool restore
dotnet dotnet-ef database update --project services/products/src/Products.Api
dotnet dotnet-ef database update --project services/baskets/src/Baskets.Api
dotnet dotnet-ef database update --project services/orders/src/Orders.Api
dotnet dotnet-ef database update --project services/parties/src/Parties.Api
```

3. Run each domain service locally (separate terminals, or via the solution's multi-startup profile):

```bash
dotnet run --project services/products/src/Products.Api
dotnet run --project services/baskets/src/Baskets.Api
dotnet run --project services/orders/src/Orders.Api
dotnet run --project services/parties/src/Parties.Api
```

4. Run the BFF, pointed at the four running services via its `Services:*:BaseUrl` configuration:

```bash
dotnet run --project services/bff/src/Bff.Api
```

5. Run the gateway, pointed at the running BFF via its `ReverseProxy` configuration:

```bash
dotnet run --project services/gateway/src/Gateway.Api
```

6. **Wait for readiness, not liveness, before issuing the first request.** `/health/live` answers as soon as the process is up; `/health/ready` additionally opens a real database connection, which is what warms the EF model and connection pool. A request issued before then pays that cost inside the BFF's 3-second per-downstream budget and can time out — measured at just over 3 s cold versus ~1 s once ready, so this is the difference between the first `curl` below returning data and returning a 504.

```bash
curl --fail --silent --retry 30 --retry-all-errors --retry-delay 1 http://localhost:5088/health/ready
```

Note the local ports: products `5088`, baskets `5041`, orders `5188`, parties `5204`, BFF `5301`, gateway `5300`.

## Validation Scenarios

### Scenario 1 — BFF proxies the product-listing route (spec Test Scenario 1, US1 Acceptance Scenario 1)

```bash
curl -i http://localhost:5300/bff/products
```

**Expected**: `200 OK` with a JSON body matching `ProductListResponse` in the contract — an `items` array of shaped product summaries sourced from the products service, not a raw pass-through of its internal response shape.

### Scenario 2 — Gateway reaches the correct destination without the caller specifying topology (US2 Acceptance Scenario 1)

```bash
curl -i http://localhost:5300/bff/products
```

**Expected**: The request only ever names the gateway's own host/port — no products/baskets/orders/parties host or port appears anywhere in the request the caller issues. Confirm this by checking that this single call, with zero other configuration, reaches products-backed data.

### Scenario 3 — SPA never calls a domain service directly (US2 Acceptance Scenario 2)

No SPA exists yet in this repo (SCRUM-14 builds it next); this scenario becomes fully executable once that lands. For this feature, validate the enabling condition: confirm the gateway is the only externally reachable entry point by checking that `Gateway.Api`'s configuration is the sole service exposed outside the local/cluster network in `docker-compose`/manifests, while `Products.Api`, `Baskets.Api`, `Orders.Api`, and `Parties.Api` remain internal-only.

### Scenario 4 — Clear error when a downstream service is down (spec Test Scenario 3, US3 Acceptance Scenario 1)

```bash
# Stop the products service, then:
curl -i http://localhost:5300/bff/products
```

**Expected**: A response within 5 seconds (SC-003) with `502` or `504` status and a `ProblemDetails` JSON body including `correlationId` — not a hang, not a raw exception stack trace, and not an indefinite wait.

### Scenario 5 — BFF contains no business logic beyond aggregation/shaping (US1 Acceptance Scenario 3, SC-004)

Code-review check, not a runtime call: inspect `services/bff/src/Bff.Api/Features/**` and confirm each route handler only calls a downstream typed client and maps/shapes its response — no domain rules, no persistence, no validation beyond request shape.

## Automated Coverage

These scenarios are the manual/exploratory complement to the automated integration tests in `services/bff/tests/Bff.Api.IntegrationTests` and `services/gateway/tests/Gateway.Api.IntegrationTests`, which exercise the same paths against real in-process downstream instances (see `research.md` Decision 5) as part of the PR gate.
