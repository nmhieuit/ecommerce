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

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the two new service shells and their test projects, matching the existing products/baskets/orders/parties convention.

- [X] T001 Create the Gateway.Api project shell at `services/gateway/src/Gateway.Api/Gateway.Api.csproj` (`Microsoft.NET.Sdk.Web`, `net10.0`, `Nullable`/`ImplicitUsings` enabled — matches `services/products/src/Products.Api/Products.Api.csproj`)
- [X] T002 [P] Create the Bff.Api project shell at `services/bff/src/Bff.Api/Bff.Api.csproj` (same convention as T001)
- [X] T003 [P] Create `Gateway.Api.IntegrationTests` and `Gateway.Api.UnitTests` project shells under `services/gateway/tests/` (xUnit, `Microsoft.AspNetCore.Mvc.Testing`, referencing Gateway.Api — matches `services/baskets/tests/Baskets.Api.IntegrationTests`)
- [X] T004 [P] Create `Bff.Api.IntegrationTests` and `Bff.Api.UnitTests` project shells under `services/bff/tests/` (same convention as T003, referencing Bff.Api)
- [X] T005 Add the gateway and bff src/test projects to `Ecommerce.slnx` under new `/services/gateway/` and `/services/bff/` folders (depends on T001-T004)
- [X] T006 [P] Add `Yarp.ReverseProxy` and `Microsoft.Extensions.Http.Resilience` package versions to `Directory.Packages.props` (ADR-0002; constitution Principle VIII)

**Checkpoint**: Both service shells and their test projects exist and are part of the solution.

> **Phase 1 implementation notes**
>
> - T001/T002 each include a minimal placeholder `Program.cs` (`CreateBuilder`/`Build`/`Run` plus `public partial class Program;`). A `Microsoft.NET.Sdk.Web` project with no entry point fails to compile (CS5001), which would have left the solution unbuildable at this checkpoint and blocked the T003/T004 test projects that reference it. T007/T008 replace these bodies with the ServiceDefaults wiring.
> - T003/T004 deliberately omit `Testcontainers.MsSql` (present in the domain services' integration tests): the gateway and BFF own no database (plan.md Technical Context: Storage N/A).
> - **Known failing test until T037**: `StructureConventionTests.VerticalSliceStructureTests.Scan_ActuallyExaminesEveryServicesApiProject` asserts against a hardcoded `ExpectedServices = ["baskets", "orders", "parties", "products"]` and now sees `bff`/`gateway` too. It cannot be fixed by simply adding the two names — `EveryService_OrganisesAtLeastOneCapabilityUnderFeatures` would then fail as well, since neither new shell has a `Features/<Capability>/` folder until Phase 2 (T007/T008 health checks). T037 owns reconciling the scanner with the two new shells.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire the platform-mandated cross-cutting concerns (observability, health, contract generation) that every route in every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T007 [P] Reference `shared/ServiceDefaults` from `Gateway.Api.csproj` and wire `builder.AddServiceDefaults()` / `app.UseServiceDefaults()` plus health-check endpoints in `services/gateway/src/Gateway.Api/Program.cs` (constitution Principle VII; matches `services/products/src/Products.Api/Program.cs`)
- [ ] T008 [P] Reference `shared/ServiceDefaults` from `Bff.Api.csproj` and wire `builder.AddServiceDefaults()` / `app.UseServiceDefaults()` plus health-check endpoints in `services/bff/src/Bff.Api/Program.cs` (same convention as T007)
- [ ] T009 [P] Create `services/gateway/src/Gateway.Api/service-manifest.yaml` declaring SLOs (internal-service-API defaults, constitution Principle VIII) matching `services/products/src/Products.Api/service-manifest.yaml`
- [ ] T010 [P] Create `services/bff/src/Bff.Api/service-manifest.yaml` declaring SLOs (client-facing BFF read budget: p95 ≤ 300 ms / p99 ≤ 800 ms, constitution Principle VIII)
- [ ] T011 [P] Create `services/gateway/src/Gateway.Api/Dockerfile` matching `services/products/src/Products.Api/Dockerfile`
- [ ] T012 [P] Create `services/bff/src/Bff.Api/Dockerfile` matching the same convention
- [ ] T013 Enable native `Microsoft.AspNetCore.OpenApi` document generation in `services/bff/src/Bff.Api/Program.cs` (research.md Decision 6; feeds ADR-0004's codegen pipeline) (depends on T008)

**Checkpoint**: Both services build, boot, expose health checks, are wired into the solution, and the BFF publishes an OpenAPI document. Ready for story-specific routes.

---

## Phase 3: User Story 1 - SPA gets product, basket, order, and party data through one backend surface (Priority: P1) 🎯 MVP

**Goal**: The BFF proxies and shapes data from all four domain services (products, baskets, orders, parties) behind endpoints the SPA will call, containing no business logic beyond aggregation/shaping.

**Independent Test**: Call each BFF endpoint (e.g. `GET /bff/products`) with products/baskets/orders/parties running behind it and confirm correctly shaped data comes back — no gateway required to validate this story.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation (constitution Principle III).

- [ ] T014 [P] [US1] Integration test: `GET /bff/products` proxies to an in-process `Products.Api` instance (`WebApplicationFactory<Products.Api.Program>`, research.md Decision 5) and returns a shaped `ProductListResponse` (`items[].id/name/price`) in `services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs` (spec Test Scenario 1; contracts/bff-openapi.yaml)
- [ ] T015 [P] [US1] Integration test: `GET /bff/baskets/{basketId}` proxies to an in-process `Baskets.Api` instance and returns basket data in `services/bff/tests/Bff.Api.IntegrationTests/BasketsRouteTests.cs`
- [ ] T016 [P] [US1] Integration test: `GET /bff/orders/{orderId}` proxies to an in-process `Orders.Api` instance and returns order data in `services/bff/tests/Bff.Api.IntegrationTests/OrdersRouteTests.cs`
- [ ] T017 [P] [US1] Integration test: `GET /bff/parties/{partyId}` proxies to an in-process `Parties.Api` instance and returns party data in `services/bff/tests/Bff.Api.IntegrationTests/PartiesRouteTests.cs`

### Implementation for User Story 1

- [ ] T018 [P] [US1] Add `DownstreamServiceClientOptions` (ServiceName, BaseUrl) binding and typed `ProductsApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs` (data-model.md Downstream Service Client; research.md Decision 3)
- [ ] T019 [P] [US1] Add typed `BasketsApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs`
- [ ] T020 [P] [US1] Add typed `OrdersApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs`
- [ ] T021 [P] [US1] Add typed `PartiesApiClient` `HttpClient` in `services/bff/src/Bff.Api/DownstreamClients/PartiesApiClient.cs`
- [ ] T022 [US1] Implement the `GET /bff/products` route group, mapping `ProductsApiClient`'s response to `ProductListResponse`/`ProductSummary`, in `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs` (depends on T018; makes T014 pass)
- [ ] T023 [US1] Implement the `GET /bff/baskets/{basketId}` route group in `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` (depends on T019; makes T015 pass)
- [ ] T024 [US1] Implement the `GET /bff/orders/{orderId}` route group in `services/bff/src/Bff.Api/Features/Orders/OrdersEndpoints.cs` (depends on T020; makes T016 pass)
- [ ] T025 [US1] Implement the `GET /bff/parties/{partyId}` route group in `services/bff/src/Bff.Api/Features/Parties/PartiesEndpoints.cs` (depends on T021; makes T017 pass)
- [ ] T026 [US1] Register all four typed clients (`AddHttpClient<T>()` + `AddStandardResilienceHandler()` at the internal-service-API timeout budget) and map all four route groups in `services/bff/src/Bff.Api/Program.cs` (depends on T018-T025)
- [ ] T027 [P] [US1] Add `Services:ProductsApi:BaseUrl` / `BasketsApi` / `OrdersApi` / `PartiesApi` configuration keys to `services/bff/src/Bff.Api/appsettings.json` and `appsettings.Development.json` (data-model.md Downstream Service Client)

**Checkpoint**: User Story 1 is fully functional and independently testable — the BFF proxies/aggregates all four services (quickstart.md Scenario 1).

---

## Phase 4: User Story 2 - Gateway routes requests without exposing service topology (Priority: P2)

**Goal**: The gateway forwards every client request to the BFF so the caller only ever needs to know the gateway's own host/port.

**Independent Test**: Send a request to the gateway and confirm it reaches the BFF without the caller specifying anything about internal topology; confirm an unmatched path returns a clear error.

### Tests for User Story 2 ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation.

- [ ] T028 [P] [US2] Integration test: a request to the gateway is forwarded to the BFF and returns the BFF's proxied product-listing response, using nothing but the gateway's own host/port, in `services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs` (US2 Acceptance Scenario 1)
- [ ] T029 [P] [US2] Integration test: a request to a path with no matching gateway route returns a clear 404, not a hang, in `services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs` (FR-007; Edge Case)

### Implementation for User Story 2

- [ ] T030 [US2] Define the gateway's `ReverseProxy:Routes`/`Clusters` config — a single `bff-route` catch-all matched to a single `bff-cluster`, with the BFF's `BaseUrl` and a forwarding timeout ≥ the BFF's downstream timeout budget — in `services/gateway/src/Gateway.Api/appsettings.json` (data-model.md Route Mapping; research.md Decision 1/2)
- [ ] T031 [US2] Wire YARP into `services/gateway/src/Gateway.Api/Program.cs`: `builder.Services.AddReverseProxy().LoadFromConfig(...)`, `app.MapReverseProxy()` (depends on T030; makes T028/T029 pass)

**Checkpoint**: User Stories 1 and 2 both work independently and together — gateway → BFF → services end-to-end (quickstart.md Scenarios 1-3).

---

## Phase 5: User Story 3 - Clear failure when a downstream service is unavailable (Priority: P3)

**Goal**: When a downstream service the gateway/BFF depends on is unavailable, callers get a clear, bounded-time error instead of a hang.

**Independent Test**: Stop the products service and call the affected BFF route through the gateway; confirm a clear, bounded error comes back instead of a hang.

### Tests for User Story 3 ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation.

- [ ] T032 [P] [US3] Integration test: `GET /bff/products` returns a `502` or `504` `ProblemDetails` response with `correlationId` in under 5 seconds when `Products.Api` is unreachable, in `services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs` (spec Test Scenario 3; SC-003)
- [ ] T033 [P] [US3] Integration test: a gateway request returns a clear error rather than hanging when the BFF itself is unreachable, in `services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs`

### Implementation for User Story 3

- [ ] T034 [US3] Add `ProblemDetails` middleware scaffolding (`AddProblemDetails()`, `UseExceptionHandler()`) and a `DownstreamExceptionHandler` mapping resilience-pipeline exceptions to `ProblemDetails` (timeout → 504, circuit-open/error → 502, naming only the downstream service's logical name plus `correlationId`, never its internal address) in `services/bff/src/Bff.Api/ErrorHandling/DownstreamExceptionHandler.cs` and `services/bff/src/Bff.Api/Program.cs` (research.md Decision 4; data-model.md Error response; FR-006)
- [ ] T035 [US3] Wire `DownstreamExceptionHandler` into all four route groups' downstream-call sites in `services/bff/src/Bff.Api/Features/**/*.cs` (depends on T022-T025, T034; makes T032 pass)
- [ ] T036 [US3] Verify the gateway's forwarding timeout (set in T030) stays ≥ the BFF's now-concrete per-downstream-call timeout budget (T034); adjust in `services/gateway/src/Gateway.Api/appsettings.json` if needed (makes T033 pass)

**Checkpoint**: All three user stories are independently functional — the feature is complete per spec.md.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Repo-wide consistency and final validation across all three stories.

- [ ] T037 [P] Verify `services/gateway` and `services/bff` satisfy `tests/StructureConventionTests`' vertical-slice scan; extend `tests/StructureConventionTests/VerticalSliceStructureScanner.cs` if it doesn't already recognize the two new shells
- [ ] T038 [P] Add `Gateway.Api.UnitTests` coverage for YARP route-config loading (config parses to the expected route/cluster) in `services/gateway/tests/Gateway.Api.UnitTests/`
- [ ] T039 [P] Add `Bff.Api.UnitTests` coverage for the Products/Baskets/Orders/Parties response-mapping functions in isolation (no HTTP) in `services/bff/tests/Bff.Api.UnitTests/`
- [ ] T040 Update `services/gateway/src/Gateway.Api/service-manifest.yaml` and `services/bff/src/Bff.Api/service-manifest.yaml` `endpoints:` sections to list the routes actually implemented (mirroring `services/products/src/Products.Api/service-manifest.yaml`'s convention)
- [ ] T041 Run [quickstart.md](quickstart.md) validation Scenarios 1-5 end-to-end locally and confirm every expected outcome

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phase 3-5)**: All depend on Foundational completion.
  - US1 has no dependency on US2 or US3 — independently testable first (MVP).
  - US2 depends on US1 existing to validate true end-to-end forwarding (quickstart Scenario 1/2), but its own routing config/wiring (T030-T031) can be authored in parallel with US1.
  - US3 builds directly on US1's route groups (T022-T025) and US2's gateway timeout (T030) — implement after both.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2). No dependency on US2/US3.
- **User Story 2 (P2)**: Can start after Foundational (Phase 2). Its routing config is independent of US1's code, but validating it end-to-end needs a running BFF (US1).
- **User Story 3 (P3)**: Needs US1's route groups (T022-T025) and US2's gateway timeout (T030) to wire failure handling into and to verify against — implement last.

### Within Each User Story

- Tests MUST be written and confirmed FAILING before implementation (constitution Principle III).
- Downstream typed clients before route groups that use them.
- Route groups before Program.cs registration.
- Story complete and its tests green before moving to the next priority.

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002-T004, T006).
- All Foundational tasks marked [P] can run in parallel (T007-T012); T013 depends on T008.
- Once Foundational completes, US1 and US2 can both start (different files/projects); US3 must wait on both.
- All four US1 tests (T014-T017) can run in parallel; all four US1 typed clients (T018-T021) can run in parallel.
- The two US2 tests (T028-T029) can run in parallel; the two US3 tests (T032-T033) can run in parallel.
- Polish tasks T037-T039 can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (write first, confirm failing):
Task: "Integration test GET /bff/products in services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs"
Task: "Integration test GET /bff/baskets/{basketId} in services/bff/tests/Bff.Api.IntegrationTests/BasketsRouteTests.cs"
Task: "Integration test GET /bff/orders/{orderId} in services/bff/tests/Bff.Api.IntegrationTests/OrdersRouteTests.cs"
Task: "Integration test GET /bff/parties/{partyId} in services/bff/tests/Bff.Api.IntegrationTests/PartiesRouteTests.cs"

# Launch all four typed downstream clients together:
Task: "ProductsApiClient in services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs"
Task: "BasketsApiClient in services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs"
Task: "OrdersApiClient in services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs"
Task: "PartiesApiClient in services/bff/src/Bff.Api/DownstreamClients/PartiesApiClient.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 against the BFF directly (no gateway needed yet).
5. Demo the BFF's proxy/aggregation behavior.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add User Story 1 → validate independently → BFF proxies all four services (MVP!).
3. Add User Story 2 → validate independently → gateway forwards to the BFF, topology hidden from callers.
4. Add User Story 3 → validate independently → downstream failures surface as clear, bounded errors.
5. Polish → structure-convention check, unit coverage, manifest sync, full quickstart pass.

### Solo/Sequential Strategy

Given this is a single-operator exercise (per `docs/roadmap.md`), the realistic path is sequential: Setup → Foundational → US1 → US2 → US3 → Polish, validating each story's checkpoint before moving on, rather than parallelizing across stories.

---

## Notes

- [P] tasks = different files, no dependency on an incomplete task.
- [Story] label maps each task to its user story for traceability.
- Tests are mandatory here (constitution Principle III), not optional — write and confirm failing before implementing.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently before continuing.
- Basket/order/party response shapes are intentionally minimal/generic for this feature (contracts/bff-openapi.yaml marks them "shape to be finalized") — only the product-listing shape is fully specified, per spec Test Scenario 1's minimum scope.
