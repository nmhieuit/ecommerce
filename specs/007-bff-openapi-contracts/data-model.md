# Data Model: OpenAPI Specs for BFF Routes + Generated Clients

No new entities, fields, or state transitions are introduced by this feature. The spec's Key
Entities are conceptual/tooling artifacts that already exist concretely in the codebase; this
feature verifies and adds test coverage around them rather than modeling new data.

| Spec entity | Concrete artifact | Location |
|---|---|---|
| OpenAPI Spec | Document generated at runtime by `AddOpenApi()`/`MapOpenApi()` from the BFF's typed route definitions | `services/bff/src/Bff.Api/Program.cs`; served at `/openapi/v1.json` in Development |
| Generated API Client | Orval-generated TanStack Query hooks and models, committed and CI-verified against drift | `frontend/packages/api-client/src/generated/**` |
| BFF Route | Minimal-API endpoint for products, baskets, or orders, each declaring its response contract inline | `services/bff/src/Bff.Api/Features/{Products,Baskets,Orders}/*Endpoints.cs` |

No changes to any of the above are planned. See [research.md](./research.md) for why each is
already structurally correct, and [quickstart.md](./quickstart.md) for how to verify that in a
local environment.
