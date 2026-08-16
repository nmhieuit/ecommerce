# Quickstart: Validate Stub Identity with Resolved Tenant Context

Validates this feature end-to-end against spec.md's acceptance scenarios and the Jira ticket's test scenarios. See [data-model.md](data-model.md) for the Tenant Context state machine and [contracts/tenant-id-header.md](contracts/tenant-id-header.md) for the header contract.

## Prerequisites

- .NET 10 SDK installed.
- Docker available (products/baskets/orders/parties each need their own SQL Server dependency container — see `docker-compose.deps.yml`; unchanged by this feature).
- [002-gateway-bff-routing](../002-gateway-bff-routing/) already implemented and running: gateway, BFF, and all four domain services.

## Setup

1. Start each domain service's database dependency and run all four domain services, the BFF, and the gateway, exactly as in [002-gateway-bff-routing/quickstart.md](../002-gateway-bff-routing/quickstart.md) Setup steps 1-4.
2. No new configuration is required to exercise the Phase 1 stub — the gateway always resolves the same single hardcoded tenant.

## Validation Scenarios

### Scenario 1 — Tenant identifier is visible in logs at every hop (spec Test Scenario 1, US1)

```bash
curl -i http://localhost:<gateway-port>/bff/products
```

**Expected**: Inspecting the structured logs emitted by the gateway, the BFF, and `Products.Api` for this request (correlated via the existing `X-Correlation-Id`, per `CorrelationIdMiddleware`) shows the identical `TenantId` value in the log scope at all three hops.

### Scenario 2 — A request through the full path succeeds with the resolved tenant (US1 Acceptance Scenario 1)

```bash
curl -i http://localhost:<gateway-port>/bff/products
```

**Expected**: `200 OK`, same as [002-gateway-bff-routing/quickstart.md](../002-gateway-bff-routing/quickstart.md) Scenario 1 — this feature adds tenant resolution underneath an already-working path without changing its outward behavior.

### Scenario 3 — Persistence without a resolved tenant fails, not defaults (spec Test Scenario 2, US2 Acceptance Scenario 1)

Call a domain service directly, bypassing the gateway and BFF (so no `X-Tenant-Id` header is ever set):

```bash
curl -i http://localhost:<products-port>/products
```

**Expected**: The request fails (a 500-class response is acceptable for Phase 1 per the roadmap's "no meaningful hardening yet" — the point under test is that it fails loudly rather than silently returning data from a default tenant/schema) rather than succeeding with any tenant's data. Server-side, this is `MissingTenantContextException` thrown before any SQL connection was opened.

### Scenario 4 — No repository/persistence call site bypasses the tenant gate (spec Test Scenario 3, SC-003)

```bash
dotnet test tests/CrossServiceIsolation.Tests
```

**Expected**: `TenantGatedConnectionScanner`'s tests pass, confirming each of the four domain services has exactly one `AddDbContext` call site and that it is gated by `TenantContext.RequireTenantId()`.

### Scenario 5 — Swapping the resolution source touches only the resolution step (spec FR-007, SC-004)

Code-review check, not a runtime call: confirm that everything downstream of the gateway's `StubIdentityAuthenticationHandler` — `TenantHeaderPropagationMiddleware`, `Tenancy`'s `TenantContextMiddleware`, the BFF's `TenantPropagationHandler`, and every domain service's `RequireTenantId()` gate — reads only the tenant claim/header, with no reference to the stub scheme's implementation. Replacing `AddScheme<StubIdentityAuthenticationSchemeOptions, StubIdentityAuthenticationHandler>(...)` with `AddJwtBearer(...)` should be the only edit Phase 3 needs at the gateway.

## Automated Coverage

These scenarios are the manual/exploratory complement to the automated tests added by this feature: `Tenancy`'s own unit tests, each domain service's integration test proving persistence fails without a resolved tenant, and `tests/CrossServiceIsolation.Tests/TenantGatedConnectionScanner.cs` — all part of the PR gate alongside the existing test suite from [002-gateway-bff-routing](../002-gateway-bff-routing/).
