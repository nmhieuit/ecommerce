---

description: "Task list for Stub Identity with Resolved Tenant Context"
---

# Tasks: Stub Identity with Resolved Tenant Context

**Input**: Design documents from `/specs/003-stub-identity-tenant-context/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/tenant-id-header.md](contracts/tenant-id-header.md), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) is NON-NEGOTIABLE for this project — "No implementation code is merged without a preceding failing test that it makes pass." Test tasks below are therefore mandatory, not optional, and MUST be written and confirmed failing before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story (from [spec.md](spec.md)) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2)
- Every task names an exact file path

## Path Conventions

This feature adds one new shared library and touches the gateway, BFF, and four domain services from [002-gateway-bff-routing](../002-gateway-bff-routing/):

- `shared/Tenancy/` (new library) and `shared/Tenancy.UnitTests/` (new test project)
- `services/gateway/src/Gateway.Api/Identity/` (new)
- `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs` (new)
- `services/{products,baskets,orders,parties}/src/{X}.Api/Program.cs` and `Data/{X}DbContext.cs` (modified)
- Existing test-seeding helpers in all four domain services' integration tests, plus `services/bff/tests/Bff.Api.IntegrationTests/BffTestHost.cs` (modified — research.md Decision 7)
- `tests/CrossServiceIsolation.Tests/` (new scanner test)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the new `Tenancy` shared library and wire it into the projects that will consume it.

- [X] T001 Create the `Tenancy` class library project shell at `shared/Tenancy/Tenancy.csproj` (`Microsoft.NET.Sdk`, `net10.0`, `Nullable`/`ImplicitUsings` enabled — flat layout matching `shared/ServiceDefaults/ServiceDefaults.csproj`)
- [X] T002 [P] Create the `Tenancy.UnitTests` project shell at `shared/Tenancy.UnitTests/Tenancy.UnitTests.csproj` (xUnit, referencing `Tenancy.csproj`)
- [X] T003 Add `shared/Tenancy` and `shared/Tenancy.UnitTests` to `Ecommerce.slnx` under the `/shared/` folder (depends on T001, T002)
- [X] T004 Add a `ProjectReference` to `shared/Tenancy/Tenancy.csproj` from `Bff.Api.csproj`, `Products.Api.csproj`, `Baskets.Api.csproj`, `Orders.Api.csproj`, and `Parties.Api.csproj` (depends on T001). The gateway does **not** reference `Tenancy` — it only produces the header (research.md Decision 3), via its own `Identity/` components (US1).

**Checkpoint**: `Tenancy` is scaffolded, part of the solution, and referenced by every project that will consume it.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the shared `Tenancy` library itself — the scoped context, its guard, the inbound-header middleware, and its registration extensions — and wire it into the BFF and all four domain services so each can read a resolved tenant from a request. Nothing produces the header yet (US1) and nothing enforces it against persistence yet (US2); this phase only makes reading one possible.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests for the shared library ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation (constitution Principle III).

- [X] T005 [P] Unit test: `TenantContext.RequireTenantId()` returns the resolved `TenantId` when it has been set, and throws `MissingTenantContextException` when it hasn't, in `shared/Tenancy.UnitTests/TenantContextTests.cs` (spec FR-004/FR-005, Test Scenario 2; data-model.md Tenant Context — Resolved/Unresolved states)
- [X] T006 [P] Unit test: `TenantContextMiddleware` populates `TenantContext` from an inbound `X-Tenant-Id` header when present, leaves it unresolved when the header is absent or empty, and pushes `TenantId` into the logger scope the same way `CorrelationIdMiddleware` already does, in `shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs` (spec FR-003/FR-006)

### Implementation

- [X] T007 Implement `MissingTenantContextException` in `shared/Tenancy/MissingTenantContextException.cs`
- [X] T008 Implement `TenantContext` (scoped, settable `TenantId`, `RequireTenantId()` guard) in `shared/Tenancy/TenantContext.cs` (depends on T007; makes T005 pass)
- [X] T009 Implement `TenantContextMiddleware` (reads `X-Tenant-Id` into `TenantContext`; pushes `TenantId` into a logger scope) in `shared/Tenancy/TenantContextMiddleware.cs` (depends on T008; makes T006 pass)
- [X] T010 Implement `TenancyExtensions` (`AddTenancy()` / `UseTenancy()`, mirroring `ServiceDefaultsExtensions`' `AddServiceDefaults()` / `UseServiceDefaults()` shape) in `shared/Tenancy/TenancyExtensions.cs` (depends on T008, T009)
- [X] T011 [P] Wire `builder.Services.AddTenancy()` / `app.UseTenancy()` into `services/bff/src/Bff.Api/Program.cs`, after `UseServiceDefaults()` (depends on T010, T004)
- [X] T012 [P] Wire `AddTenancy()` / `UseTenancy()` into `services/products/src/Products.Api/Program.cs`, after `UseServiceDefaults()`
- [X] T013 [P] Wire `AddTenancy()` / `UseTenancy()` into `services/baskets/src/Baskets.Api/Program.cs`, after `UseServiceDefaults()`
- [X] T014 [P] Wire `AddTenancy()` / `UseTenancy()` into `services/orders/src/Orders.Api/Program.cs`, after `UseServiceDefaults()`
- [X] T015 [P] Wire `AddTenancy()` / `UseTenancy()` into `services/parties/src/Parties.Api/Program.cs`, after `UseServiceDefaults()`

**Checkpoint**: `Tenancy` is built, unit-tested, and wired into the BFF and all four domain services — each can now read a resolved tenant from a request. Ready for story-specific work.

---

## Phase 3: User Story 1 - Tenant identity is resolved once and propagated explicitly end to end (Priority: P1) 🎯 MVP

**Goal**: The gateway resolves a tenant identifier for every request (from the Phase 1 stub identity) and it reaches the BFF and, through the BFF's downstream calls, every domain service — visibly and identically at each hop.

**Independent Test**: Send one request through the gateway and confirm the same tenant identifier is what the BFF observed, and what the BFF forwards downstream — no persistence enforcement (US2) required to observe this.

### Tests for User Story 1 ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation.

- [X] T016 [P] [US1] Integration test: using `GatewayTestHost.CreateBff()` / `CreateGateway(bff)`, a request through the gateway always carries the resolved Phase 1 tenant id to the BFF, and a caller-supplied `X-Tenant-Id` header is overwritten rather than trusted, in `services/gateway/tests/Gateway.Api.IntegrationTests/TenantPropagationTests.cs` (spec US1 Acceptance Scenario 1, Test Scenario 1; Edge Cases — never client-controlled)
- [X] T017 [P] [US1] Integration test: using `BffTestHost.CreateDownstreamAsync` + `CreateBff<TClient>`, the BFF's outbound call to a downstream service carries the tenant id the BFF itself received, in `services/bff/tests/Bff.Api.IntegrationTests/TenantPropagationTests.cs` (spec US1 Acceptance Scenario 1)

### Implementation for User Story 1

- [X] T018 [US1] Implement `StubIdentityAuthenticationHandler` (an always-succeeding `AuthenticationHandler` issuing a `ClaimsPrincipal` with a fixed tenant claim and a fixed subject/user-id claim) in `services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs` (research.md Decision 1; data-model.md Stub Identity)
- [X] T019 [US1] Implement `TenantHeaderPropagationMiddleware` (reads the authenticated tenant claim and writes `X-Tenant-Id` onto `context.Request.Headers`, always overwriting any inbound value — mirrors `CorrelationIdMiddleware`'s pattern) in `services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs` (research.md Decision 2; depends on T018; makes T016 pass)
- [X] T020 [US1] Wire `builder.Services.AddAuthentication().AddScheme<...>(...)`, `app.UseAuthentication()`, and the new middleware into `services/gateway/src/Gateway.Api/Program.cs`, ordered after `UseServiceDefaults()` and before `MapReverseProxy()` (depends on T018, T019)
- [X] T021 [P] [US1] Configure the Phase 1 hardcoded tenant id in `services/gateway/src/Gateway.Api/appsettings.json` (data-model.md Stub Identity)
- [X] T022 [US1] Implement `TenantPropagationHandler` (a `DelegatingHandler` reading `TenantContext` and setting `X-Tenant-Id` on each outgoing request) in `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs` (research.md Decision 4; makes T017 pass)
- [X] T023 [US1] Attach `TenantPropagationHandler` to each of the four typed `HttpClient`s in `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` (depends on T022, T011)

**Checkpoint**: User Story 1 is fully functional and independently testable — a resolved tenant id reaches every hop (quickstart.md Scenarios 1-2).

---

## Phase 4: User Story 2 - No persistence access without a resolved tenant (Priority: P2)

**Goal**: Every domain service's database connection is gated on a resolved tenant — unresolved means the connection is never opened, not that it opens against some default.

**Independent Test**: Call a domain service directly, bypassing the gateway and BFF so no `X-Tenant-Id` is ever set, and confirm persistence access fails rather than defaulting; separately, confirm exactly one tenant-gated `AddDbContext` call site exists per service.

### Tests for User Story 2 ⚠️

> Write these tests FIRST; confirm they FAIL before starting implementation.

- [X] T024 [P] [US2] Integration test: a request to `Products.Api` with no `X-Tenant-Id` fails when persistence is touched, rather than succeeding against a default schema, in `services/products/tests/Products.Api.IntegrationTests/TenantEnforcementTests.cs` (spec US2 Acceptance Scenario 1, Test Scenario 2)
- [X] T025 [P] [US2] Integration test: same for `Baskets.Api` in `services/baskets/tests/Baskets.Api.IntegrationTests/TenantEnforcementTests.cs`
- [X] T026 [P] [US2] Integration test: same for `Orders.Api` in `services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs`
- [X] T027 [P] [US2] Integration test: same for `Parties.Api` in `services/parties/tests/Parties.Api.IntegrationTests/TenantEnforcementTests.cs`
- [X] T028 [P] [US2] Structural test: each of the four domain services has exactly one `AddDbContext` call site, and that call site is gated by `TenantContext.RequireTenantId()`, in `tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs` (spec Test Scenario 3, SC-003; research.md Decision 6 — extends the existing `ConnectionStringScanner` convention)

### Implementation for User Story 2

> **T029-T032 were rescoped during implementation — decided, not outstanding.** The tenant *gate*
> (the `(serviceProvider, options)` overload calling `RequireTenantId()` before `UseSqlServer`) is
> implemented and green in all four services. The `HasDefaultSchema(tenantId)` half was **dropped
> for Phase 1**: it cannot work against the existing migrations, which create their tables
> unqualified (schema `dbo`), so a runtime-set default schema makes every query fail with
> `Invalid object name 'contoso.Products'` (verified empirically). US2's acceptance criteria are
> met by the gate alone, and physical partitioning buys nothing while exactly one tenant is
> resolvable (FR-008). See the **AMENDED** note on research.md Decision 5 — including its warning
> that a second tenant must not be introduced before that decision is revisited.

- [X] T029 [P] [US2] (gate only — see note above) Switch `ProductsDbContext`'s registration to the `(serviceProvider, options)` overload — resolving `TenantContext.RequireTenantId()` before `UseSqlServer` — and call `modelBuilder.HasDefaultSchema(tenantId)` in `services/products/src/Products.Api/Program.cs` and `services/products/src/Products.Api/Data/ProductsDbContext.cs` (research.md Decision 5; makes T024 and part of T028 pass)
- [X] T030 [P] [US2] (gate only) Same change for `Baskets.Api` in `services/baskets/src/Baskets.Api/Program.cs` and `Data/BasketsDbContext.cs` (makes T025 pass)
- [X] T031 [P] [US2] (gate only) Same change for `Orders.Api` in `services/orders/src/Orders.Api/Program.cs` and `Data/OrdersDbContext.cs` (makes T026 pass)
- [X] T032 [P] [US2] (gate only) Same change for `Parties.Api` in `services/parties/src/Parties.Api/Program.cs` and `Data/PartiesDbContext.cs` (makes T027 pass)
- [X] T033 [P] [US2] Update `CatalogEndpointsTests.CreateFactoryWithCatalogAsync` to set `TenantContext.TenantId` on its manually created DI scope before resolving `ProductsDbContext`, in `services/products/tests/Products.Api.IntegrationTests/CatalogEndpointsTests.cs` (research.md Decision 7 — otherwise this pre-existing seeding helper now throws; depends on T029)
- [X] T034 [P] [US2] Same fix in the basket-seeding helper, `services/baskets/tests/Baskets.Api.IntegrationTests/BasketEndpointsTests.cs` (depends on T030)
- [X] T035 [P] [US2] Same fix in the order-seeding helper, `services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs` (depends on T031)
- [X] T036 [P] [US2] Same fix in the party-seeding helper, `services/parties/tests/Parties.Api.IntegrationTests/PartyEndpointsTests.cs` (depends on T032)
- [X] T037 [US2] Same fix in `BffTestHost.CreateDownstreamAsync`, `services/bff/tests/Bff.Api.IntegrationTests/BffTestHost.cs` (research.md Decision 7 — needed once T029-T032 land, and to keep US1's T017 green; depends on T029, T030, T031, T032)

**Checkpoint**: All two user stories are independently functional — persistence structurally requires a resolved tenant across all four services, and pre-existing tests still pass.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Isolated coverage and final end-to-end validation.

- [ ] T038 [P] Unit test: `StubIdentityAuthenticationHandler` always succeeds and issues the expected tenant claim, in isolation from the full pipeline test in T016, in `services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs`
- [ ] T039 Run [quickstart.md](quickstart.md) validation Scenarios 1-5 end-to-end locally and confirm every expected outcome

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories.
- **User Stories (Phase 3-4)**: Both depend on Foundational completion.
  - US1 has no dependency on US2 — independently testable first (MVP).
  - US2 depends on US1 only insofar as it reuses `BffTestHost` (T037), which US1's T017 also uses; implement after US1 so that fix lands once, not twice.
- **Polish (Phase 5)**: Depends on both user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2). No dependency on US2.
- **User Story 2 (P2)**: Can start after Foundational (Phase 2). Functionally independent of US1 (enforcement doesn't need propagation's gateway/BFF code to exist), but T037's seeding fix is shared with US1's test, so sequencing US2 after US1 avoids touching `BffTestHost.cs` twice.

### Within Each User Story

- Tests MUST be written and confirmed FAILING before implementation (constitution Principle III).
- Story complete and its tests green before moving to the next priority.

### Parallel Opportunities

- T002 can run parallel to T001; T004 waits on T001.
- Foundational tests T005-T006 are parallelizable (different files); of the implementation tasks, T011-T015 (wiring five different `Program.cs` files) are parallelizable once T010 is done.
- US1's two tests (T016, T017) are parallelizable (different projects/files).
- US2's five tests (T024-T028) are parallelizable (different files); its four `DbContext` implementation tasks (T029-T032) are parallelizable; its four existing-seeding-helper fixes (T033-T036) are parallelizable once their respective `DbContext` task lands.

---

## Parallel Example: User Story 2

```bash
# Launch all four domain-service enforcement tests together (write first, confirm failing):
Task: "Integration test: Products.Api fails persistence with no tenant, in services/products/tests/Products.Api.IntegrationTests/TenantEnforcementTests.cs"
Task: "Integration test: Baskets.Api fails persistence with no tenant, in services/baskets/tests/Baskets.Api.IntegrationTests/TenantEnforcementTests.cs"
Task: "Integration test: Orders.Api fails persistence with no tenant, in services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs"
Task: "Integration test: Parties.Api fails persistence with no tenant, in services/parties/tests/Parties.Api.IntegrationTests/TenantEnforcementTests.cs"

# Launch all four DbContext registration changes together:
Task: "Tenant-gate ProductsDbContext's registration in services/products/src/Products.Api/Program.cs"
Task: "Tenant-gate BasketsDbContext's registration in services/baskets/src/Baskets.Api/Program.cs"
Task: "Tenant-gate OrdersDbContext's registration in services/orders/src/Orders.Api/Program.cs"
Task: "Tenant-gate PartiesDbContext's registration in services/parties/src/Parties.Api/Program.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: run quickstart.md Scenarios 1-2 — a resolved tenant reaches every hop.
5. Note: persistence is *not yet* enforced at this checkpoint (that's US2) — this MVP proves propagation only.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. Add User Story 1 → validate independently → tenant propagation proven end to end (MVP!).
3. Add User Story 2 → validate independently → persistence structurally requires a resolved tenant, with zero bypass call sites.
4. Polish → isolated stub-identity coverage, full quickstart pass.

### Solo/Sequential Strategy

Given this is a single-operator exercise (per `docs/roadmap.md`), the realistic path is sequential: Setup → Foundational → US1 → US2 → Polish, validating each story's checkpoint before moving on.

---

## Notes

- [P] tasks = different files, no dependency on an incomplete task.
- [Story] label maps each task to its user story for traceability.
- Tests are mandatory here (constitution Principle III), not optional — write and confirm failing before implementing.
- T033-T037 are not new tests for this feature's own behavior — they are fixes to pre-existing (002-era) seeding helpers that this feature's enforcement (T029-T032) would otherwise break. Treat them as part of US2's definition of done, not as optional cleanup.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently before continuing.
