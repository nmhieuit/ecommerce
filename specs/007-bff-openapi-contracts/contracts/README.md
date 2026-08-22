# Contracts

This feature introduces no new or changed API contract.

The authoritative contract for the products, baskets, and orders BFF routes already exists and is
published live by the BFF itself:

- **Source**: `services/bff/src/Bff.Api/Features/{Products,Baskets,Orders}/*Endpoints.cs` — each
  route declares its response shape inline via `.Produces<T>()` / `.ProducesProblem(...)`.
- **Published document**: `GET /openapi/v1.json` on the BFF, in Development environments only
  (see `Program.cs`).
- **Consumed by**: `frontend/packages/api-client/orval.config.ts`, which generates the SPA's
  TanStack Query client from that document (`pnpm --filter @ecommerce/api-client generate`).

See [research.md](../research.md) Decision 2 for why this document cannot drift from route
behavior, and [quickstart.md](../quickstart.md) for how to inspect it locally.
