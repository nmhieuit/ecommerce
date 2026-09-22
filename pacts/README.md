# Pacts

Committed, file-based Pact documents — one per boundary in the
[011-consumer-contract-tests](../specs/011-consumer-contract-tests/spec.md) thin slice.

Each file is written by that boundary's **consumer-side** test and read by its **provider-side**
verification test, which runs inside the producer's own build. A producer whose real response (or
constructed event payload) stops matching the file here fails its own build, which is the guarantee
constitution Principle III and ADR-0006 ask for.

There is deliberately no Pact Broker: this directory *is* the exchange mechanism, and listing it is
how boundary coverage is audited without reading any service's source (spec SC-003, and see
[research.md Decision 2](../specs/011-consumer-contract-tests/research.md)). Standing up a broker
remains ADR-0006 Action Item 1 — separate infrastructure work, not a prerequisite of this feature.

## Boundaries

| Boundary | Consumer | Producer | Kind | Pact file |
|---|---|---|---|---|
| BFF↔products | `bff` | `products` | HTTP | `bff-products.json` |
| BFF↔baskets | `bff` | `baskets` | HTTP | `bff-baskets.json` |
| BFF↔orders | `bff` | `orders` | HTTP | `bff-orders.json` |
| BasketCheckedOut | `orders` | `baskets` | Event (message) | `orders-basketcheckedout.json` |

This table is the source `tests/ContractCoverageTests` checks the directory against: a boundary
listed here with no pact file, or no verification test, fails that suite by name.

## Regenerating

```bash
dotnet test services/bff/tests/Bff.Api.ContractTests
dotnet test services/orders/tests/Orders.Api.ContractTests --filter FullyQualifiedName~BasketCheckedOutConsumerPactTests
```

Regenerate deliberately, and review the diff: these files record what a consumer *relies on*, so a
field disappearing from one is a consumer dropping a dependency, not housekeeping. A producer-side
change must never be "fixed" by rewriting the pact.

## Authorization

Every provider host in `*.Api.ContractTests` accepts a token via `IntegrationTestSupport`'s
`TestJwtBearer` (the same symmetric-key bypass every `*.Api.IntegrationTests` project uses) instead
of a real identity server, and every `*ProviderPactTests` attaches a fresh one at verification time
via `PactVerifierSource.WithCustomHeader` — see each provider host's `ConfigureWebHost` remarks.
**If you add or change `.RequireAuthorization(...)` on an endpoint one of these boundaries covers,
run that service's `*.Api.ContractTests` locally before merging.** This directory's own coverage
check (`tests/ContractCoverageTests`) confirms a pact file and a verification test both exist; it
does not — and structurally cannot, since it never sends an HTTP request — confirm the provider
actually accepts what the verifier sends. `Jenkinsfile`'s `contract tests` stage does exercise every
`*.Api.ContractTests` project for real and fails the build on a mismatch (011-consumer-contract-tests
QA_Debt entry: this exact gap — spec 015 changing how every endpoint authorizes a caller, `pacts/`
not updated to match — went undetected for about three weeks despite that gate existing).
