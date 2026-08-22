# Quickstart: Verify Consumer-Driven Contract Tests

This feature adds four new/extended xUnit test projects (`Bff.Api.ContractTests`,
`Products.Api.ContractTests`, `Baskets.Api.ContractTests`, `Orders.Api.ContractTests`) and a
committed `pacts/` directory of Pact documents. This guide walks through generating the pacts,
verifying them, and proving each success criterion in spec.md.

## Prerequisites

- .NET 10 SDK
- A running Docker daemon, for the three HTTP **provider** suites only. They host the service
  in-process via `WebApplicationFactory`, but each service's `DbContext` is registered against SQL
  Server behind a tenant gate, so a real database is needed for a route to answer at all — the same
  `SqlServerFixture`/Testcontainers pattern the `*.Api.IntegrationTests` suites use
  (research.md Decision 5, as amended).
- Nothing beyond the SDK for the consumer-side pacts or for the `BasketCheckedOut` event pilot:
  those read and write local Pact JSON only, and the event pilot calls the payload mapper directly
  with no host, no database, and no broker.

## Generate the consumer-side pacts

```bash
dotnet test services/bff/tests/Bff.Api.ContractTests
dotnet test services/orders/tests/Orders.Api.ContractTests --filter FullyQualifiedName~BasketCheckedOutConsumerPactTests
```

Expect this to (re)write `pacts/bff-products.json`, `pacts/bff-baskets.json`,
`pacts/bff-orders.json`, and `pacts/orders-basketcheckedout.json`.

## SC-001 — every boundary in the thin slice has a contract test

```bash
find pacts -name "*.json"
```

Expect exactly four files, matching the boundary table in
[data-model.md](./data-model.md#boundary): `bff-products.json`, `bff-baskets.json`,
`bff-orders.json`, `orders-basketcheckedout.json`.

## SC-002 — a breaking producer change fails the producer's own build

```bash
dotnet test services/products/tests/Products.Api.ContractTests
dotnet test services/baskets/tests/Baskets.Api.ContractTests
dotnet test services/orders/tests/Orders.Api.ContractTests
```

Expect all to pass against the current, unmodified services. To see a producer's own build catch a
break (Test Scenario 1 in spec.md): rename a field in one of `products`' response DTOs so it no
longer matches `pacts/bff-products.json`, re-run
`dotnet test services/products/tests/Products.Api.ContractTests`, and confirm it now fails with a
Pact mismatch naming the field — then revert the change. Repeat with `baskets`' checkout logic and
`pacts/orders-basketcheckedout.json` to prove the event pilot the same way.

## SC-003 — coverage is auditable without reading service source

```bash
find pacts -name "*.json" | sed 's#pacts/##; s#\.json$##'
```

Expect this list to be cross-checkable against the four-row boundary table above in under 5 minutes,
with no need to open any service's source code.

## SC-004 — removing a required contract test is caught

Delete (or rename) `services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs`
and run the coverage suite:

```bash
dotnet test tests/ContractCoverageTests
```

Expect `AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest` to fail naming the boundary and
the path, like this — then restore the file and re-run to confirm it passes again:

```text
CoverageViolation { Boundary = BFF-products,
  MissingPath = services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs,
  Reason = has a pact but no provider-side test reading it — 'products' would build green while
           the expectation went unchecked }
```

The gate is automated, so FR-009 needs no manual-review fallback. It checks both halves: a boundary
with no committed pact fails the same way, because an expectation nobody recorded and an expectation
nobody verifies are equally uncovered.

## Adding coverage to a boundary outside this thin slice

Out of scope for this feature (spec.md Assumptions) — follow the same
consumer-side/provider-side/`pacts/` pattern established here once a future feature extends
coverage.
