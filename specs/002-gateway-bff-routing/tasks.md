---

description: "Task list for Gateway → BFF Routing for Products, Baskets, Orders, and Parties"
---

# Tasks: Gateway → BFF Routing for Products, Baskets, Orders, and Parties

**Input**: Design documents from `/specs/002-gateway-bff-routing/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/bff-openapi.yaml](contracts/bff-openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) is NON-NEGOTIABLE for this project — "No implementation code is merged without a preceding failing test that it makes pass." Test tasks below are therefore mandatory, not optional, and MUST be written and confirmed failing before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story (from [spec.md](spec.md)) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Every task names an exact file path

## Path Conventions

This feature adds two new peer service shells to the existing `services/<name>/{src,tests}` convention (see [plan.md](plan.md) Project Structure):

- `services/gateway/src/Gateway.Api/` and `services/gateway/tests/Gateway.Api.{IntegrationTests,UnitTests}/`
- `services/bff/src/Bff.Api/` and `services/bff/tests/Bff.Api.{IntegrationTests,UnitTests}/`

Phase 3 additionally touches the four existing domain services under `services/{products,baskets,orders,parties}/`.

---

## ⚠️ Regeneration Note (2026-08-15) — Read Before Starting Phase 3

This list was regenerated after Phase 3 implementation halted on a blocker: **spec.md's assumption that the four domain services "already expose the HTTP APIs needed for the BFF to call them" is false.** Verified against the repo — all four map only `/health/live` and `/health/ready`, and no `*DbContext` declares a single `DbSet`. There was nothing for the BFF to proxy.

**The scope decision this list assumes**: all four domain services gain a minimal read surface (Phase 3 below), because that is the only reading under which the spec's existing requirements hold as written:

- **FR-002** requires the BFF to expose endpoints serving **product, basket, order, and party** data — not products alone.
- **FR-003** requires aggregation across more than one of them.
- **FR-004 / SC-002** name the product-listing route specifically as the proven proxy path, which is why products gets the fullest shape below.

The alternative — real data for products only, with the basket/order/party routes deferred to a later feature — remains viable, but it requires **amending FR-002 and FR-003 first**, since it cannot satisfy them. If that is the call, delete T016-T018, T020-T022, T024-T026, T029-T031, and the corresponding US1 tasks T034-T036, T039-T041, T043-T045; nothing else in the list changes.

**Phases 1 and 2 (T001-T013) are complete, verified, and committed** (`fa39a80`, `106ed89`). Their task IDs and text are preserved unchanged so the completed work stays traceable; renumbering starts after T013.

---

## Phase 1: Setup (Shared Infrastructure) — ✅ COMPLETE

**Purpose**: Scaffold the two new service shells and their test projects, matching the existing products/baskets/orders/parties convention.

- [X] T001 Create the Gateway.Api project shell at `services/gateway/src/Gateway.Api/Gateway.Api.csproj` (`Microsoft.NET.Sdk.Web`, `net10.0`, `Nullable`/`ImplicitUsings` enabled — matches `services/products/src/Products.Api/Products.Api.csproj`)
- [X] T002 [P] Create the Bff.Api project shell at `services/bff/src/Bff.Api/Bff.Api.csproj` (same convention as T001)
- [X] T003 [P] Create `Gateway.Api.IntegrationTests` and `Gateway.Api.UnitTests` project shells under `services/gateway/tests/` (xUnit, `Microsoft.AspNetCore.Mvc.Testing`, referencing Gateway.Api — matches `services/baskets/tests/Baskets.Api.IntegrationTests`)
- [X] T004 [P] Create `Bff.Api.IntegrationTests` and `Bff.Api.UnitTests` project shells under `services/bff/tests/` (same convention as T003, referencing Bff.Api)
- [X] T005 Add the gateway and bff src/test projects to `Ecommerce.slnx` under new `/services/gateway/` and `/services/bff/` folders (depends on T001-T004)
- [X] T006 [P] Add `Yarp.ReverseProxy` and `Microsoft.Extensions.Http.Resilience` package versions to `Directory.Packages.props` (ADR-0002; constitution Principle VIII)

**Checkpoint**: ✅ Both service shells and their test projects exist and are part of the solution.

> **Phase 1 implementation notes**
>
> - T001/T002 each include a minimal placeholder `Program.cs`. A `Microsoft.NET.Sdk.Web` project with no entry point fails to compile (CS5001), which would have left the solution unbuildable at this checkpoint and blocked the T003/T004 test projects that reference it. T007/T008 replaced these bodies with the ServiceDefaults wiring.
> - T003/T004 deliberately omit `Testcontainers.MsSql`: the gateway and BFF own no database (plan.md Technical Context: Storage N/A).

---

## Phase 2: Foundational (Blocking Prerequisites) — ✅ COMPLETE

**Purpose**: Wire the platform-mandated cross-cutting concerns (observability, health, contract generation) that every route in every user story depends on.

- [X] T007 [P] Reference `shared/ServiceDefaults` from `Gateway.Api.csproj` and wire `builder.AddServiceDefaults()` / `app.UseServiceDefaults()` plus health-check endpoints in `services/gateway/src/Gateway.Api/Program.cs` (constitution Principle VII; matches `services/products/src/Products.Api/Program.cs`)
- [X] T008 [P] Reference `shared/ServiceDefaults` from `Bff.Api.csproj` and wire `builder.AddServiceDefaults()` / `app.UseServiceDefaults()` plus health-check endpoints in `services/bff/src/Bff.Api/Program.cs` (same convention as T007)
- [X] T009 [P] Create `services/gateway/src/Gateway.Api/service-manifest.yaml` declaring SLOs (internal-service-API defaults, constitution Principle VIII) matching `services/products/src/Products.Api/service-manifest.yaml`
- [X] T010 [P] Create `services/bff/src/Bff.Api/service-manifest.yaml` declaring SLOs (client-facing BFF read budget: p95 ≤ 300 ms / p99 ≤ 800 ms, constitution Principle VIII)
- [X] T011 [P] Create `services/gateway/src/Gateway.Api/Dockerfile` matching `services/products/src/Products.Api/Dockerfile`
- [X] T012 [P] Create `services/bff/src/Bff.Api/Dockerfile` matching the same convention
- [X] T013 Enable native `Microsoft.AspNetCore.OpenApi` document generation in `services/bff/src/Bff.Api/Program.cs` (research.md Decision 6; feeds ADR-0004's codegen pipeline) (depends on T008)

**Checkpoint**: ✅ Verified by running both services, not only building them: `/health/live` and `/health/ready` return `200` on each, responses carry `X-Correlation-Id` from `ServiceDefaults`, and the BFF serves a valid OpenAPI 3.1.1 document at `/openapi/v1.json`.

> **Phase 2 implementation notes**
>
> - **Readiness registers no checks, deliberately.** Neither service owns a database, and neither probes its downstream. Readiness gates whether an instance receives traffic; depending on a downstream would pull instances out of rotation during an outage — for the BFF, even on routes that never touch the failing service — when the required behaviour is to stay up and return a bounded error per request (FR-006, US3).
> - **`Microsoft.AspNetCore.OpenApi` is pinned to 10.0.11, not 10.0.0** like the file's other ASP.NET Core/EF entries: 10.0.0 resolves a transitive `Microsoft.OpenApi` 2.0.0 carrying high-severity advisory GHSA-v5pm-xwqc-g5wc, failing the repo's NuGet audit. 10.0.11 resolves 2.7.5.
> - **`MapOpenApi()` is Development-only** — the document maps the BFF's whole client-facing surface and no authorization sits in front of it yet (plan.md Principle VI deviation).
> - Both shells also gained `appsettings.json`, `appsettings.Development.json`, and `Properties/launchSettings.json` (gateway `5300`/`7300`, BFF `5301`/`7301`).

---

## Phase 3: Downstream Data Surface (Blocking Prerequisite for US1)

**Purpose**: Give the four domain services the minimal read endpoints the BFF proxies to. Without this, US1 has nothing to proxy — this phase exists because the original plan's assumption that these endpoints already existed proved false (see Regeneration Note).

**⚠️ CRITICAL**: US1 (Phase 4) cannot begin until this phase is complete. US2 (Phase 5) does not depend on it.

**Scope discipline**: each service gets the *smallest* read surface that makes the BFF's route real — one entity, one read endpoint. This is not the services' first domain story; no business rules, no write endpoints, no cross-service references (constitution Principle I).

### Contract first ⚠️

> Constitution Principle II: "API and event contracts MUST be written and reviewed before implementation."

- [X] T014 Author the four downstream read contracts (products listing; single basket, order, party by id) in `specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml`, with the products listing's `id`/`name`/`price` shape matching what `ProductSummary` in `contracts/bff-openapi.yaml` requires the BFF to be able to produce

### Tests for the downstream surface ⚠️

> Write these FIRST; confirm they FAIL before the entity/endpoint tasks. Each uses the service's existing `SqlServerFixture` (Testcontainers — constitution Principle III forbids in-memory providers and hand-rolled fakes) and applies migrations via `dbContext.Database.MigrateAsync()` before seeding.

- [X] T015 [P] Integration test: seed two products, `GET /products` returns `200` and both, shaped `id`/`name`/`price`, in `services/products/tests/Products.Api.IntegrationTests/CatalogEndpointsTests.cs`
- [X] T016 [P] Integration test: seed one basket, `GET /baskets/{basketId}` returns `200` and it, plus `404` for an unknown id, in `services/baskets/tests/Baskets.Api.IntegrationTests/BasketEndpointsTests.cs`
- [X] T017 [P] Integration test: seed one order, `GET /orders/{orderId}` returns `200` and it, plus `404` for an unknown id, in `services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs`
- [X] T018 [P] Integration test: seed one party, `GET /parties/{partyId}` returns `200` and it, plus `404` for an unknown id, in `services/parties/tests/Parties.Api.IntegrationTests/PartyEndpointsTests.cs`

### Entities

- [X] T019 [P] Add `Product` (Id, Name, Price) in `services/products/src/Products.Api/Data/Product.cs` and a `DbSet<Product>` plus its `decimal` precision configuration on `services/products/src/Products.Api/Data/ProductsDbContext.cs` (replaces that file's "deliberately has no entity sets" remark)
- [X] T020 [P] Add `Basket` (Id, CustomerId) in `services/baskets/src/Baskets.Api/Data/Basket.cs` and a `DbSet<Basket>` on `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`
- [X] T021 [P] Add `Order` (Id, PlacedAtUtc, Total) in `services/orders/src/Orders.Api/Data/Order.cs` and a `DbSet<Order>` plus `decimal` precision configuration on `services/orders/src/Orders.Api/Data/OrdersDbContext.cs`
- [X] T022 [P] Add `Party` (Id, DisplayName) in `services/parties/src/Parties.Api/Data/Party.cs` and a `DbSet<Party>` on `services/parties/src/Parties.Api/Data/PartiesDbContext.cs`

### Migrations

> `docker-compose.deps.yml` states the rule this implements: "Schema inside the database is EF Core migrations' job once entities exist; creating the empty database is infrastructure's." No migration exists anywhere in the repo yet, so each of these is that service's *initial* migration.

- [X] T023 [P] Add `Microsoft.EntityFrameworkCore.Design` to each of the four `*.Api.csproj` files (already versioned in `Directory.Packages.props`; required for `dotnet ef`)
- [X] T024 [P] Generate the initial EF migration into `services/products/src/Products.Api/Migrations/` (`dotnet ef migrations add InitialCatalog --project services/products/src/Products.Api`) (depends on T019, T023)
- [X] T025 [P] Generate the initial EF migration into `services/baskets/src/Baskets.Api/Migrations/` (depends on T020, T023)
- [X] T026 [P] Generate the initial EF migration into `services/orders/src/Orders.Api/Migrations/` (depends on T021, T023)
- [X] T027 [P] Generate the initial EF migration into `services/parties/src/Parties.Api/Migrations/` (depends on T022, T023)

### Read endpoints

- [X] T028 [P] Implement `GET /products` (list) in `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs` and map it from `services/products/src/Products.Api/Program.cs` — vertical-slice convention, capability folder beside `Features/HealthCheck/` (makes T015 pass)
- [X] T029 [P] Implement `GET /baskets/{basketId}` in `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` and map it from that service's `Program.cs` (makes T016 pass)
- [X] T030 [P] Implement `GET /orders/{orderId}` in `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs` and map it from that service's `Program.cs` (makes T017 pass)
- [X] T031 [P] Implement `GET /parties/{partyId}` in `services/parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs` and map it from that service's `Program.cs` (makes T018 pass)

### Declare the new surface

- [X] T032 Update the `endpoints:` and `data:` sections of all four `services/*/src/*.Api/service-manifest.yaml` files to list the new read route and drop the "Scaffold only — no domain endpoints yet" description (constitution Principle VIII)

**Checkpoint**: ✅ Each domain service serves real data over HTTP from its own database, with a contract and a passing Testcontainers-backed test. US1 now has something to proxy. Verified: **20/20 integration tests pass** across the four services (2 new + 3 pre-existing readiness tests each); full solution builds with 0 warnings, 0 errors.

> **Phase 3 implementation notes**
>
> - **Red-green was staged, and the red was real.** The four test files were written first and failed to compile (no `Product`/`Basket`/`Order`/`Party` type) — a compile error is a weak red, so after the entities and migrations landed the suites were re-run to get a *meaningful* failure: `Expected: OK / Actual: NotFound` against a real containerised SQL Server with rows already seeded. Only then were the endpoints written.
> - **The repo's first EF migrations.** `dotnet ef` needs the `Design` package and a version-matched tool. The globally-installed `dotnet-ef` (10.0.11) could not load `Microsoft.CodeAnalysis.Workspaces` against the repo's EF Core 10.0.0 pins, so tooling is now pinned repo-side in `.config/dotnet-tools.json` at 10.0.0 (`dotnet tool restore` before `dotnet dotnet-ef`). This keeps `Directory.Packages.props` untouched at 10.0.0 across every EF package, rather than bumping the runtime graph to satisfy a design-time tool.
> - `Microsoft.EntityFrameworkCore.Design` is referenced with `PrivateAssets="all"` so migration tooling never reaches the published image.
> - **Wire shapes are separate records from entities** in all four services (`ProductResponse`, `BasketResponse`, …). The stored model can grow with each service's real domain story without silently widening what the BFF — and the SPA behind it — receives. This matters most for `Party`, the type most likely to gain personal data.
> - `GET /products` returns an empty array for an empty catalog; the three by-id routes return `404` for an unknown id, so the BFF can distinguish "no such record" from "a record that is empty". Both behaviours are asserted.
> - **Correction to the Phase 2 report**: `CrossServiceIsolation.Tests` also has a stale hardcoded `ExpectedServices` list and has been red since T001, for the same reason as the structure test. Earlier phases ran only `StructureConventionTests`, so the claim that it was "the only red test in the suite" was based on an incomplete run. T059 now covers both files. The substantive isolation assertion (`NoServiceConfiguration_NamesAnotherServicesDatabase`) passes — no service names another's database, and Phase 3 added no connection string.

---

## Phase 4: User Story 1 - SPA gets product, basket, order, and party data through one backend surface (Priority: P1) 🎯 MVP

**Goal**: The BFF proxies and shapes data from all four domain services behind endpoints the SPA will call, containing no business logic beyond aggregation/shaping.

**Independent Test**: Call each BFF endpoint (e.g. `GET /bff/products`) with products/baskets/orders/parties running behind it and confirm correctly shaped data comes back — no gateway required to validate this story.

### Tests for User Story 1 ⚠️

> Write these FIRST; confirm they FAIL before implementation. Each hosts the real downstream service in-process via `WebApplicationFactory<Program>` (research.md Decision 5 — the real service, not a mock), which in turn needs that service's Testcontainers database from Phase 3.

- [X] T033 [P] [US1] Integration test: `GET /bff/products` proxies to an in-process `Products.Api` and returns a shaped `ProductListResponse` (`items[].id/name/price`) in `services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs` (spec Test Scenario 1; contracts/bff-openapi.yaml)
- [X] T034 [P] [US1] Integration test: `GET /bff/baskets/{basketId}` proxies to an in-process `Baskets.Api` and returns basket data in `services/bff/tests/Bff.Api.IntegrationTests/BasketsRouteTests.cs`
- [X] T035 [P] [US1] Integration test: `GET /bff/orders/{orderId}` proxies to an in-process `Orders.Api` and returns order data in `services/bff/tests/Bff.Api.IntegrationTests/OrdersRouteTests.cs`
- [X] T036 [P] [US1] Integration test: `GET /bff/parties/{partyId}` proxies to an in-process `Parties.Api` and returns party data in `services/bff/tests/Bff.Api.IntegrationTests/PartiesRouteTests.cs`
- [X] T037 [P] [US1] Unit test: a downstream client configured with no `BaseUrl` fails at startup rather than per request, in `services/bff/tests/Bff.Api.UnitTests/DownstreamServiceClientOptionsTests.cs` (data-model.md validation rule)

### Implementation for User Story 1

- [X] T038 [US1] Add `DownstreamServiceClientOptions` (ServiceName, BaseUrl) with options validation that fails fast on a missing `BaseUrl` in `services/bff/src/Bff.Api/DownstreamClients/DownstreamServiceClientOptions.cs` (data-model.md Downstream Service Client; makes T037 pass)
- [X] T039 [P] [US1] Add typed `ProductsApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs` (depends on T038)
- [X] T040 [P] [US1] Add typed `BasketsApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs` (depends on T038)
- [X] T041 [P] [US1] Add typed `OrdersApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs` (depends on T038)
- [X] T042 [P] [US1] Add typed `PartiesApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/PartiesApiClient.cs` (depends on T038)
- [X] T043 [US1] Implement the `GET /bff/products` route group, mapping `ProductsApiClient`'s response to `ProductListResponse`/`ProductSummary`, in `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs` (depends on T039; makes T033 pass)
- [X] T044 [US1] Implement the `GET /bff/baskets/{basketId}` route group in `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` (depends on T040; makes T034 pass)
- [X] T045 [US1] Implement the `GET /bff/orders/{orderId}` route group in `services/bff/src/Bff.Api/Features/Orders/OrdersEndpoints.cs` (depends on T041; makes T035 pass)
- [X] T046 [US1] Implement the `GET /bff/parties/{partyId}` route group in `services/bff/src/Bff.Api/Features/Parties/PartiesEndpoints.cs` (depends on T042; makes T036 pass)
- [X] T047 [US1] Register all four typed clients (`AddHttpClient<T>()` + `AddStandardResilienceHandler()` at the internal-service-API timeout budget) and map all four route groups in `services/bff/src/Bff.Api/Program.cs` (depends on T038-T046)
- [X] T048 [P] [US1] Add `Services:ProductsApi:BaseUrl` / `BasketsApi` / `OrdersApi` / `PartiesApi` configuration keys to `services/bff/src/Bff.Api/appsettings.json` and `appsettings.Development.json`, pointing at the domain services' dev ports (`5088`, `5041`, `5188`, `5204`)

**Checkpoint**: ✅ User Story 1 is fully functional and independently testable — the BFF proxies/shapes all four services (quickstart.md Scenario 1). Verified: **8 BFF integration tests + 3 unit tests pass**, each route asserted against the real downstream service running in-process over a real database; solution builds 0 warnings / 0 errors; no regressions across the rest of the suite.

> **Phase 4 implementation notes**
>
> - **All four domain services declare `public partial class Program` in the global namespace**, so a single test project referencing them together hits CS0433. The four `ProjectReference`s in `Bff.Api.IntegrationTests.csproj` therefore carry `Aliases` (`ProductsApi`, `BasketsApi`, …), and each test file opens with `extern alias ProductsApi;` and uses `ProductsApi::Program`. The BFF's own unqualified `Program` stays unambiguous. These are test-only references — `Bff.Api` itself references no domain service and reaches them only over HTTP (Principle I).
> - **How a typed client is pointed at an in-process downstream**: each client is registered *by its logical service name* (`AddHttpClient<TClient>(serviceName, …)`), so a test can call `AddHttpClient(serviceName).ConfigurePrimaryHttpMessageHandler(downstream.Server.CreateHandler)` and reroute it without the BFF knowing it is under test. A base URL is still configured because the request needs an absolute URI, but the handler decides where it lands. `BffTestHost` holds this arrangement.
> - **Each downstream gets its own database on the shared container** (`bff-products`, `bff-baskets`, …) rather than sharing one catalog, so the test topology mirrors the production isolation rule (Principle I) instead of proving the BFF works in a world where all four services share a schema.
> - **A downstream 404 stays a 404.** The typed clients map `NotFound` to a `null` result instead of letting `EnsureSuccessStatusCode` throw. "No such basket" is the downstream answering correctly; routing it through the failure path would turn it into US3's 502 and lose a distinction the SPA needs. Asserted in three tests.
> - **Resilience budgets** (`DownstreamClientRegistrationExtensions`): 1 s per attempt, 3 s total per downstream call including retries, 10 s circuit-breaker sampling window. Chosen to sit above the downstream services' declared p99 of 500 ms and below SC-003's 5-second ceiling. Verified live: with the products service down, `GET /bff/products` returned in **3.16 s** rather than hanging.
> - **Contract generation confirmed working end-to-end**: the BFF's `/openapi/v1.json`, which listed `paths: {}` at the Phase 2 checkpoint, now advertises all four routes automatically. T062 still owns reconciling it against the hand-authored `bff-openapi.yaml`.
> - **Known gap, owned by US3 (T053-T056)**: with a downstream unreachable the BFF currently returns a bare `500`, not the `502`/`504` `ProblemDetails` with `correlationId` the contract specifies. The *timing* half of FR-006 is already satisfied (bounded at ~3 s); the *shape* half is Phase 6's work.

---

## Phase 5: User Story 2 - Gateway routes requests without exposing service topology (Priority: P2)

**Goal**: The gateway forwards every client request to the BFF so the caller only ever needs to know the gateway's own host/port.

**Independent Test**: Send a request to the gateway and confirm it reaches the BFF without the caller specifying anything about internal topology; confirm an unmatched path returns a clear error.

**Note**: This story does **not** depend on Phase 3 or US1. It can be built and validated today against the BFF's Phase 2 health endpoints — useful if the Phase 3 scope decision is still settling.

### Tests for User Story 2 ⚠️

- [X] T049 [P] [US2] Integration test: a request to the gateway is forwarded to the BFF and returns the BFF's response, using nothing but the gateway's own host/port, in `services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs` (US2 Acceptance Scenario 1)
- [X] T050 [P] [US2] Integration test: a request to a path with no matching gateway route returns a clear 404, not a hang, in `services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs` (FR-007; Edge Case)

### Implementation for User Story 2

- [X] T051 [US2] Define the gateway's `ReverseProxy:Routes`/`Clusters` config — a single `bff-route` catch-all matched to a single `bff-cluster`, with the BFF's `BaseUrl` and a forwarding timeout ≥ the BFF's downstream timeout budget — in `services/gateway/src/Gateway.Api/appsettings.json` (data-model.md Route Mapping; research.md Decision 1/2)
- [X] T052 [US2] Wire YARP into `services/gateway/src/Gateway.Api/Program.cs`: `builder.Services.AddReverseProxy().LoadFromConfig(...)`, `app.MapReverseProxy()`, and reference `Yarp.ReverseProxy` from `Gateway.Api.csproj` (version already in `Directory.Packages.props` from T006) (depends on T051; makes T049/T050 pass)

**Checkpoint**: ✅ User Stories 1 and 2 both work independently and together — gateway → BFF → services end-to-end (quickstart.md Scenarios 1-3). Verified twice over: **8 gateway integration tests pass**, and a real three-process run (Products.Api + Bff.Api + Gateway.Api against a containerised SQL Server) returned seeded product data from `curl http://localhost:5300/bff/products` — the caller naming only the gateway's port, exactly quickstart Scenario 2.

> **Phase 5 implementation notes**
>
> - **Route table is a single catch-all**, per data-model.md: `{**catch-all}` → `bff-cluster`. A gateway that enumerated the BFF's paths would need editing every time the BFF gained one — the topology coupling US2 exists to remove. Cluster timeout is `ActivityTimeout: 00:00:10`, comfortably above the BFF's 3 s per-downstream budget as data-model.md requires (T058 re-verifies this under US3).
> - **Where the FR-007 404 comes from, precisely**: because the gateway route is a catch-all, *no* path is unmatched at the gateway — an unknown path is forwarded and the BFF, which has a finite route table, answers 404. The requirement is about what the caller observes, and the caller observes a prompt, clear 404 either way (measured at **5.7 ms** live). The alternative design — scoping the route to `/bff/{**catch-all}` so YARP itself 404s anything outside — would satisfy FR-007 one layer earlier and is recorded in `UnmatchedRouteTests`' remarks as the road not taken.
> - **The gateway's own health probes are guarded by test**, not left to routing precedence. ASP.NET Core prefers the more specific `/health/live` over the catch-all, so they work today; `TheGatewaysOwnHealthProbes_AreServedLocally_NotForwarded` exists so a later route-table change cannot silently make a BFF outage restart every gateway pod.
> - **How YARP is pointed at an in-process BFF**: by replacing `IForwarderHttpClientFactory`, so route matching, cluster selection, header handling, and timeout all still run as configured and only the socket is swapped — the same substitution the BFF's tests make one layer down. Deliberately not by binding a real Kestrel port, which makes suites flaky under parallel CI.
>
> **⚠️ Finding for US3 — cold start exceeds the BFF's downstream budget.** In the live run, the *very first* request through the chain returned `500` after 3.0 s (`Polly.Timeout.TimeoutRejectedException`); every subsequent request returned `200` in **11-40 ms**. The first call pays JIT, EF model build, and connection-pool warm-up, which does not fit the 3 s `TotalRequestTimeout` set in T047. This is a real availability concern — it would burn error budget on the first request after every deploy or scale-out, against an error-rate SLO of 0.1 % 5xx — and it is not visible to the integration suite, which warms each host before asserting. **The timeout was deliberately not widened here**: it is US3's territory (T056 owns downstream failure mapping), and changing a Principle VIII budget should be a recorded decision rather than a quiet edit made to make one manual observation look better. Options for T056: raise `TotalRequestTimeout`, warm the pipeline at startup, or gate readiness on a first successful downstream call.

---

## Phase 6: User Story 3 - Clear failure when a downstream service is unavailable (Priority: P3)

**Goal**: When a downstream service the gateway/BFF depends on is unavailable, callers get a clear, bounded-time error instead of a hang.

**Independent Test**: Stop the products service and call the affected BFF route through the gateway; confirm a clear, bounded error comes back instead of a hang.

### Tests for User Story 3 ⚠️

- [X] T053 [P] [US3] Integration test: `GET /bff/products` returns a `502` or `504` `ProblemDetails` response carrying `correlationId` in under 5 seconds when `Products.Api` is unreachable, in `services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs` (spec Test Scenario 3; SC-003)
- [X] T054 [P] [US3] Integration test: the error body names only the downstream's logical name and contains no internal host/port, in `services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs` (data-model.md Error response validation rule; FR-001)
- [X] T055 [P] [US3] Integration test: a gateway request returns a clear error rather than hanging when the BFF itself is unreachable, in `services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs`

### Implementation for User Story 3

- [X] T056 [US3] Add `ProblemDetails` middleware (`AddProblemDetails()`, `UseExceptionHandler()`) and a `DownstreamExceptionHandler` mapping resilience-pipeline exceptions to `ProblemDetails` (timeout → 504, circuit-open/error → 502, naming only the downstream's logical name plus `correlationId`, never its internal address) in `services/bff/src/Bff.Api/ErrorHandling/DownstreamExceptionHandler.cs` and `services/bff/src/Bff.Api/Program.cs` (research.md Decision 4; FR-006; makes T053/T054 pass)
- [X] T057 [US3] Wire `DownstreamExceptionHandler` into all four route groups' downstream-call sites in `services/bff/src/Bff.Api/Features/**/*.cs` (depends on T043-T046, T056)
- [X] T058 [US3] Verify the gateway's forwarding timeout (set in T051) stays ≥ the BFF's now-concrete per-downstream-call timeout budget (T056); adjust `services/gateway/src/Gateway.Api/appsettings.json` if needed (makes T055 pass)

**Checkpoint**: ✅ All three user stories are independently functional — the feature is complete per spec.md. Verified: **70 tests pass, 2 fail** (both the known T059 guard tests). quickstart Scenario 4 confirmed live through the gateway — with the products service stopped, `GET /bff/products` returned **504 `application/problem+json` in 3.06 s** naming only `'ProductsApi'`, well inside SC-003's 5 s.

> **Phase 6 implementation notes**
>
> - **Failure handling lives with the typed clients, not in each route group.** `DownstreamCall.ExecuteAsync` wraps every downstream call, so a route group physically cannot issue one that bypasses it — a per-route try/catch is something the next route added can forget. It also keeps route groups pure proxy-and-shape (FR-005). Only dependency failures are caught; a bug inside the BFF still surfaces as a 500 rather than being reported as somebody else's outage.
> - **Retry backoff had to be tuned, or 502 would have been dead code.** The standard resilience handler's default retry delay is 2 s, which does not fit inside the 3 s total budget: the first retry's delay alone exhausted it, so *every* failure — including an instantly-refused connection — surfaced as `TimeoutRejectedException` and mapped to 504. Retry delay is now 200 ms with 2 retries, so all three attempts finish well inside the budget and the 502/504 distinction the contract draws is real.
> - **Test unreachability is a DNS failure, not a closed port.** `http://127.0.0.1:1` was measured taking **2.0 s** to be refused on Windows loopback — longer than the 1 s attempt timeout, so it registered as a timeout. That is an OS behaviour, not ours, and would have made the test observe 504 on one machine and 502 on another. `products-service.invalid` (RFC 2606) fails in ~13 ms everywhere.
> - **T058 is enforced by test, not by inspection.** `Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests` reads the gateway's `ActivityTimeout` from `appsettings.json` and asserts it is ≥ the BFF's 3 s total budget and bounded. The invariant spans two services, so neither alone can enforce it, and a note would have held only until somebody tuned one of the two numbers.
>
> **Defect found and fixed outside this phase's tasks — correlation ID did not survive the gateway hop.** Running quickstart Scenario 4 end to end showed the `X-Correlation-Id` response header and the `correlationId` in the error body carrying **different values**. `CorrelationIdMiddleware` wrote the resolved ID to `HttpContext.Items` and the *response* headers but never to the *request* headers, and YARP forwards inbound request headers — so a gateway-generated ID reached only the gateway's own response, and the BFF minted a second, unrelated one. The ID quoted in a US3 error therefore matched nothing in the BFF's logs, which is exactly when a caller needs it. That breaks constitution Principle VII ("generated at the edge and propagated across every synchronous call") and research.md Decision 7. Fixed by one line in `shared/ServiceDefaults/CorrelationIdMiddleware.cs`, covered by the new `Gateway.Api.IntegrationTests/CorrelationIdPropagationTests` (generated *and* caller-supplied cases), and regression-checked across all 14 test projects since the middleware is shared by every service. A caller-supplied ID already propagated correctly; only the generated case was broken.
>
> **Phase 5's cold-start finding is resolved — no timeout change needed.** The earlier 3 s failure on first request was an artefact of the test harness waiting on `/health/live`. Kubernetes gates traffic on `/health/ready`, which for the domain services opens a real database connection and so warms the EF model and connection pool before any traffic arrives. Re-measured waiting on readiness instead: the very first request through gateway → BFF → products returned **200 in 1.07 s**, inside the 3 s budget. The platform's existing readiness gate is the mitigation; quickstart.md should tell readers to wait on readiness, which T065 now covers.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Repo-wide consistency and final validation across all three stories.

- [X] T059 [P] Add `"bff"` and `"gateway"` to the hardcoded `ExpectedServices` array in **both** `tests/StructureConventionTests/VerticalSliceStructureTests.cs` (line 13) and `tests/CrossServiceIsolation.Tests/ConnectionStringIsolationTests.cs` (line 13) — **these are the only two red tests in the suite**, both red since T001 created the two new service directories, and both are guard assertions ("did the scan actually look at every service?") rather than the substantive checks, which pass. Note `ConnectionStringIsolationTests` additionally asserts `ScannedConnectionStringCount >= ExpectedServices.Length`, which will not hold once `bff`/`gateway` join the list, because neither owns a database and neither has a connection string — that assertion needs to count only the services that are supposed to have one, not every scanned service
- [X] T060 [P] Add `Gateway.Api.UnitTests` coverage for YARP route-config loading (config parses to the expected route/cluster) in `services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs`
- [X] T061 [P] Add `Bff.Api.UnitTests` coverage for the Products/Baskets/Orders/Parties response-mapping functions in isolation (no HTTP) in `services/bff/tests/Bff.Api.UnitTests/ResponseMappingTests.cs`
- [X] T062 [P] Verify the BFF's generated `/openapi/v1.json` matches the hand-authored `specs/002-gateway-bff-routing/contracts/bff-openapi.yaml` for all four routes, and reconcile any drift in favour of the generated document (constitution Principle II; ADR-0004)
- [X] T063 Update `services/gateway/src/Gateway.Api/service-manifest.yaml` and `services/bff/src/Bff.Api/service-manifest.yaml` `endpoints:` sections to list the routes actually implemented
- [X] T064 Run [quickstart.md](quickstart.md) validation Scenarios 1-5 end-to-end locally and confirm every expected outcome
- [X] T065 Update [quickstart.md](quickstart.md) Setup steps to (a) restore the pinned tooling (`dotnet tool restore`) and apply the Phase 3 migrations (`dotnet dotnet-ef database update` per service) before running the services, which the current steps do not mention because no migrations existed when it was written, and (b) tell readers to wait for each service's `/health/ready` — not `/health/live` — before issuing the first request, since readiness is what warms the EF model and connection pool and a request issued before it can exceed the BFF's 3 s downstream budget (measured; see Phase 6 notes)

**Checkpoint**: ✅ Feature complete. **All 65 tasks done; the whole suite is green for the first time — 96 tests, 0 failures**, across 14 test projects, with the solution building 0 warnings / 0 errors. quickstart Scenarios 1-5 all confirmed end-to-end against six running services and four containerised databases.

> **Phase 7 implementation notes**
>
> - **T059 fixed the two stale guard lists and corrected one of them rather than just extending it.** `ConnectionStringIsolationTests` asserted `ScannedConnectionStringCount >= ExpectedServices.Length`; with `bff` and `gateway` added that compares connection strings against services two of which own no database. It now counts against `DatabaseOwningServices`, and a new test asserts the converse — that neither stateless service declares a `ConnectionStrings` section at all, which is the Principle I rule the original scan could not see (it looks for connections naming *another* service's database, and a stateless service has none of its own to compare against).
> - **T060 loads the route table through YARP's own `LoadFromConfig` binding**, not as raw JSON. A test asserting the presence of JSON keys would pass on configuration YARP rejects — a misspelled `ClusterId`, a malformed match — which is exactly the failure worth catching before a gateway boots on it. It also pins research.md Decision 1 by asserting no cluster names a domain service.
> - **T062 found real drift and fixed it in the code, not the reference.** The generated document declared only `200` per route, while the implementation genuinely returns `404`, `502`, and `504`. Minimal APIs infer the success response only. Since ADR-0004 makes the *generated* document authoritative and generates the frontend's client from it, reconciling "in favour of the generated document" meant making the code declare what it does — `.Produces` / `.ProducesProblem` on all four routes — rather than deleting real responses from `bff-openapi.yaml`. Otherwise SCRUM-14's client would have had no typed notion of "basket not found" or "products service unavailable". `GeneratedContractTests` now pins this so the drift cannot silently return.
> - **T064 ran all five scenarios for real** — four containerised SQL Servers, migrations applied, one seeded record per service, six services running. All four `/bff/*` routes returned correctly shaped data through the gateway alone; unknown id → 404; unmatched path → 404 in 8 ms; products stopped → 504 ProblemDetails in 3.14 s naming only `'ProductsApi'`. The Phase 6 correlation-ID fix was confirmed end-to-end: response header and body `correlationId` now carry the same value through the gateway hop.
> - **An intermittent test failure was found and fixed while verifying this phase.** The final full-suite run showed one `Bff.Api.IntegrationTests` failure that passed on isolated re-run. Rather than dismiss it, it was reproduced by running the four Testcontainers-backed suites concurrently — the contention the full run creates — which failed two `DownstreamUnavailableTests` cases consistently. Cause: those tests asserted **502** against a genuinely unresolvable host, but 502 versus 504 turns on whether the transport fails inside the 1 s attempt timeout, and under Docker load DNS resolution slowed past it, so the failure was reclassified as a timeout. This is the same environment-dependence that earlier moved the tests off `127.0.0.1:1`; switching to a different real address only moved the threshold rather than removing it. Fixed properly: tests asserting a *specific* status now inject an immediately-failing transport handler, so the mapping under test is asserted rather than the host's socket timing, while a new test keeps the real unreachable address and asserts only what holds at any speed (bounded, `problem+json`, names the dependency, carries a correlation ID). Re-verified green under the same concurrent load.
> - **A false alarm worth recording**: the first Scenario 1 run showed baskets and orders returning 404 with rows verifiably present in their databases. The cause was the harness, not the product — a race in the backgrounded start loop bound baskets to orders' port and vice versa, so each request reached the wrong service. Diagnosed by noticing the baskets service logged *no* `DbCommand` at all (the request never reached a handler) while parties logged one. Re-run with `--no-launch-profile --urls` and explicit staggering: all four routes returned 200. No product change was made, and none was warranted.

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: ✅ complete.
- **Foundational (Phase 2)**: ✅ complete.
- **Downstream Data Surface (Phase 3)**: depends on Phase 2 — BLOCKS US1 only.
- **User Stories (Phases 4-6)**:
  - US1 depends on Phase 3. Independently testable once it lands (MVP).
  - US2 depends only on Phase 2 — it can be built **now**, in parallel with or before Phase 3.
  - US3 builds on US1's route groups (T043-T046) and US2's gateway timeout (T051) — implement after both.
- **Polish (Phase 7)**: depends on all three user stories, except T059, which can be done at any time to get the suite green.

### User Story Dependencies

- **User Story 1 (P1)**: needs Phase 3's downstream endpoints. No dependency on US2/US3.
- **User Story 2 (P2)**: needs nothing beyond Phase 2. Validating the full chain end-to-end is richer once US1 exists, but its own routing config and wiring stand alone.
- **User Story 3 (P3)**: needs US1's route groups and US2's gateway timeout — implement last.

### Within Each User Story

- Tests MUST be written and confirmed FAILING before implementation (constitution Principle III).
- Contracts before tests; entities before migrations; migrations before endpoints.
- Downstream typed clients before route groups that use them.
- Route groups before `Program.cs` registration.
- Story complete and its tests green before moving to the next priority.

### Parallel Opportunities

- Phase 3: the four test tasks (T015-T018), the four entities (T019-T022), the four migrations (T024-T027), and the four endpoints (T028-T031) each parallelize across services — they touch four disjoint service trees.
- Phase 4: all US1 tests (T033-T037) parallelize; all four typed clients (T039-T042) parallelize once T038 lands.
- **Phase 5 (US2) parallelizes with the whole of Phases 3-4** — different projects, no shared files.
- Phase 6: the three US3 tests (T053-T055) parallelize.
- Phase 7: T059-T062 parallelize.

---

## Parallel Example: Phase 3 Downstream Surface

```bash
# After T014's contract, launch all four downstream tests together (write first, confirm failing):
Task: "Integration test GET /products in services/products/tests/Products.Api.IntegrationTests/CatalogEndpointsTests.cs"
Task: "Integration test GET /baskets/{basketId} in services/baskets/tests/Baskets.Api.IntegrationTests/BasketEndpointsTests.cs"
Task: "Integration test GET /orders/{orderId} in services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs"
Task: "Integration test GET /parties/{partyId} in services/parties/tests/Parties.Api.IntegrationTests/PartyEndpointsTests.cs"

# Then all four entities together:
Task: "Product entity + DbSet in services/products/src/Products.Api/Data/"
Task: "Basket entity + DbSet in services/baskets/src/Baskets.Api/Data/"
Task: "Order entity + DbSet in services/orders/src/Orders.Api/Data/"
Task: "Party entity + DbSet in services/parties/src/Parties.Api/Data/"
```

---

## Implementation Strategy

### Recommended next step: US2 first, then Phase 3 → US1

Phases 1-2 are done. US2 (Phase 5, T049-T052) is the only remaining work that depends on **nothing** unresolved — it needs just the BFF responding, which it already does. Taking it next delivers the gateway half of the feature while the Phase 3 scope decision settles, and it is only four tasks.

### MVP path (User Story 1)

1. ✅ Phase 1: Setup.
2. ✅ Phase 2: Foundational.
3. Phase 3: Downstream Data Surface — the prerequisite the original plan missed.
4. Phase 4: User Story 1.
5. **STOP and VALIDATE**: run quickstart.md Scenario 1 against the BFF directly (no gateway needed).

### Incremental Delivery

1. ✅ Setup + Foundational → foundation ready.
2. Add US2 → gateway forwards to the BFF, topology hidden from callers (available now).
3. Add Phase 3 + US1 → BFF proxies all four services (MVP).
4. Add US3 → downstream failures surface as clear, bounded errors.
5. Polish → structure-convention check green, unit coverage, contract reconciliation, manifest sync, full quickstart pass.

### Solo/Sequential Strategy

Given this is a single-operator exercise (per `docs/roadmap.md`), the realistic path is sequential: US2 → Phase 3 → US1 → US3 → Polish, validating each checkpoint before continuing.

---

## Notes

- [P] tasks = different files, no dependency on an incomplete task.
- [Story] label maps each task to its user story for traceability.
- Tests are mandatory here (constitution Principle III), not optional — write and confirm failing before implementing.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently before continuing.
- Phase 3 is the correction of a planning error, not new scope creep: the feature always required the BFF to return real product/basket/order/party data (FR-002, FR-003, FR-004); the plan simply assumed that data was already reachable and it was not.
- Basket/order/party BFF response shapes remain intentionally minimal — `contracts/bff-openapi.yaml` marks them "shape to be finalized", and only the product-listing shape is fully specified, per spec Test Scenario 1's minimum scope.
