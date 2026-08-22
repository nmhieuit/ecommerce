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
