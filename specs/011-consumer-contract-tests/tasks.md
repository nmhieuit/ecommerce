---

description: "Task list for Consumer-Driven Contract Tests Across BFF/Service Boundaries"

---

# Tasks: Consumer-Driven Contract Tests Across BFF/Service Boundaries

**Input**: Design documents from `/specs/011-consumer-contract-tests/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: This feature's deliverable *is* tests (constitution Principle III). Every boundary task
below follows red-green: the consumer-side pact and the provider-side verification test land first
(and pass against today's correct implementation), then a revert-and-confirm-red step deliberately
breaks the producer, confirms the producer's own build fails, and restores it — proving Jira Test
Scenario 1 for that boundary.

**Organization**: Tasks are grouped by user story, matching spec.md's three stories (US1 at P1, US2
at P2, US3 at P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

Four new `*.Api.ContractTests` projects beside each service's existing `*.Api.IntegrationTests`
project (`services/{bff,products,baskets,orders}/tests/`), a new repo-root `pacts/` directory of
committed Pact JSON documents, and a new repo-root `tests/ContractCoverageTests` convention-test
project (matching the existing `tests/StructureConventionTests` pattern). One new production file,
`services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs`, is added for the
event pilot (research.md Decision 3) — no other production file is permanently modified; every
revert-and-confirm-red step restores the file it touched.

---

## Phase 1: Setup

**Purpose**: Stand up the four new contract-test projects and the `pacts/` directory before any
pact is written.

- [X] T001 Add a `PactNet` `5.0.1` `PackageVersion` entry to `Directory.Packages.props`, next to the
      `xunit`/testing entries, with a comment referencing ADR-0006 and research.md Decision 1
- [X] T002 [P] Create `services/bff/tests/Bff.Api.ContractTests/Bff.Api.ContractTests.csproj`
      (net10.0, `Nullable` enabled, `PackageReference` to `PactNet`, `xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`; `ProjectReference`
      to `services/bff/src/Bff.Api/Bff.Api.csproj` for the `ProductsApiClient`/`BasketsApiClient`/
      `OrdersApiClient` resource records the consumer pacts describe)
- [X] T003 [P] Create
      `services/products/tests/Products.Api.ContractTests/Products.Api.ContractTests.csproj`
      (net10.0, `PactNet`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
      `coverlet.collector`, `Microsoft.AspNetCore.Mvc.Testing`; `ProjectReference` to
      `services/products/src/Products.Api/Products.Api.csproj`)
- [X] T004 [P] Create
      `services/baskets/tests/Baskets.Api.ContractTests/Baskets.Api.ContractTests.csproj`
      (same package set as T003 plus `Microsoft.AspNetCore.Mvc.Testing`; `ProjectReference` to
      `services/baskets/src/Baskets.Api/Baskets.Api.csproj` **and** to
      `shared/EventContracts/EventContracts.csproj` for `BasketCheckedOutV1`, needed by the event
      pilot's provider test in Phase 4)
- [X] T005 [P] Create `services/orders/tests/Orders.Api.ContractTests/Orders.Api.ContractTests.csproj`
      (same package set as T003 plus `Microsoft.AspNetCore.Mvc.Testing`; `ProjectReference` to
      `services/orders/src/Orders.Api/Orders.Api.csproj` **and** to
      `shared/EventContracts/EventContracts.csproj` for `BasketCheckedOutV1`, needed by the event
      pilot's consumer test in Phase 4)
- [X] T006 [P] Create `pacts/README.md` documenting the four-boundary table from
      [data-model.md](./data-model.md#boundary) (boundary, consumer, producer, kind, file name) so
      the directory's purpose is self-explanatory before any pact file exists in it
- [X] T007 Add the four new projects (T002–T005) to `Ecommerce.slnx`
- [X] T008 Run `dotnet build Ecommerce.slnx` and confirm the solution — including the four new,
      currently-empty contract-test projects — builds cleanly before any pact test is written

**Checkpoint**: New projects and the `pacts/` directory exist and the solution builds. Safe to
proceed to user story work.

---

## Phase 2: Foundational

**Purpose**: Blocking prerequisites shared by all three user stories.

None. Each of the three HTTP boundaries in User Story 1 is independently self-contained once
Phase 1's projects exist; User Story 2's event pilot needs only Phase 1 plus its own new production
file; User Story 3's coverage check reads the artifacts the other two stories produce but is its own
project. Proceed directly to User Story 1.

---

## Phase 3: User Story 1 - A breaking HTTP response change fails the producer's own build (Priority: P1) 🎯 MVP

**Goal**: Prove FR-001–FR-003, FR-005, FR-006 — each of `products`, `baskets`, and `orders` verifies
its real HTTP responses against the BFF's documented expectations as part of its own build, and a
breaking change to any of them fails that service's own build (spec SC-002).

**Independent Test**: For each of the three services, rename a field the BFF's downstream client
reads, run that service's own `*.Api.ContractTests` project, confirm it fails naming the mismatch,
then revert and confirm green again.

### Implementation for User Story 1

- [X] T009 [P] [US1] Write `ProductsConsumerPactTests.cs` in
      `services/bff/tests/Bff.Api.ContractTests/`: define a Pact consumer test for `GET /products`
      expecting a 200 with a JSON array of objects shaped `{ id: uuid, name: string, price: decimal }`
      — matching exactly what `ProductsApiClient.GetProductsAsync`
      (`services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs`) reads into
      `ProductResource`. Run it and confirm it passes against Pact's mock provider and writes
      `pacts/bff-products.json`
- [X] T010 [P] [US1] Write `BasketsConsumerPactTests.cs` in
      `services/bff/tests/Bff.Api.ContractTests/`: define Pact consumer tests for the three
      interactions `BasketsApiClient` makes (`services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs`)
      — `GET /baskets/current` and `POST /baskets/current/items` both returning
      `{ id, customerRef, items: [{ productId, quantity, unitPrice, lineTotal }], total }`, and
      `POST /baskets/current/clear` returning 204 (or 409 with an `error` body for the
      already-empty case). Run it and confirm it writes `pacts/bff-baskets.json`
- [X] T011 [P] [US1] Write `OrdersConsumerPactTests.cs` in
      `services/bff/tests/Bff.Api.ContractTests/`: define Pact consumer tests for the two
      interactions `OrdersApiClient` makes (`services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs`)
      — `GET /orders/{orderId}` and `POST /orders`, both returning
      `{ id: uuid, placedAtUtc: date-time, total: decimal }` (the BFF's `OrderResource` does not read
      `tenantId`, so the pact must not require it). Run it and confirm it writes
      `pacts/bff-orders.json`
- [X] T012 [US1] Write `ProductsProviderPactTests.cs` in
      `services/products/tests/Products.Api.ContractTests/`: host `Products.Api` in-process via
      `WebApplicationFactory` (research.md Decision 5), seed at least one product, and run
      `PactVerifier` against `pacts/bff-products.json` (depends on T009). Confirm it passes against
      the real `CatalogEndpoints.MapCatalogEndpoints` handler
      (`services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`)
- [X] T013 [US1] Revert-and-confirm-red for the products boundary: temporarily rename `Price` to
      `UnitPrice` on `ProductResponse` in
      `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`, run
      `dotnet test services/products/tests/Products.Api.ContractTests`, confirm it now fails naming
      the missing `price` field, then `git checkout --
      services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs` and re-run to confirm
      green again (Jira Test Scenario 1)
- [X] T014 [US1] Write `BasketsProviderPactTests.cs` in
      `services/baskets/tests/Baskets.Api.ContractTests/`: host `Baskets.Api` in-process, resolve a
      caller the same way the existing `Baskets.Api.IntegrationTests` do, and run `PactVerifier`
      against `pacts/bff-baskets.json` (depends on T010). Confirm it passes against the real
      `BasketEndpoints.MapBasketEndpoints` handlers
      (`services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs`)
- [X] T015 [US1] Revert-and-confirm-red for the baskets boundary: temporarily rename `Total` to
      `GrandTotal` on `BasketResponse` in
      `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs`, run
      `dotnet test services/baskets/tests/Baskets.Api.ContractTests`, confirm it now fails naming the
      missing `total` field, then `git checkout --
      services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` and re-run to confirm
      green again
- [X] T016 [US1] Write `OrdersProviderPactTests.cs` in
      `services/orders/tests/Orders.Api.ContractTests/`: host `Orders.Api` in-process and run
      `PactVerifier` against `pacts/bff-orders.json` (depends on T011). Confirm it passes against the
      real `OrderEndpoints.MapOrderEndpoints` handlers
      (`services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`)
- [X] T017 [US1] Revert-and-confirm-red for the orders boundary: temporarily rename `PlacedAtUtc` to
      `CreatedAtUtc` on `OrderResponse` in
      `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`, run
      `dotnet test services/orders/tests/Orders.Api.ContractTests`, confirm it now fails naming the
      missing `placedAtUtc` field, then `git checkout --
      services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs` and re-run to confirm green
      again

**Checkpoint**: FR-001–FR-003, FR-005, FR-006 are provably true across all three HTTP boundaries;
SC-002's HTTP half is closed. User Story 1 is independently complete and is the MVP.

---

## Phase 4: User Story 2 - A breaking event payload change fails the publishing service's own build (Priority: P2)

**Goal**: Prove FR-004–FR-006 for the piloted event boundary — `baskets` (as the eventual publisher)
verifies a constructed `BasketCheckedOutV1` payload against `orders`' (the eventual consumer's)
documented expectation, with no MassTransit or live broker involved (research.md Decisions 3–4).

**Independent Test**: Break the payload construction, run `Baskets.Api.ContractTests`, confirm it
fails naming the mismatch, then revert and confirm green again.

### Implementation for User Story 2

- [X] T018 [US2] Write `BasketCheckedOutConsumerPactTests.cs` in
      `services/orders/tests/Orders.Api.ContractTests/`: using `PactNet`'s message-pact API, define
      `orders`' expectation of a `BasketCheckedOut` message shaped like
      `EventContracts.BasketCheckedOutV1` (`eventId`, `occurredAtUtc`, `basketId`, `customerRef`,
      `tenantId`, `correlationId`, `items: [{ productId, quantity, unitPrice, lineTotal }]`, `total`).
      Run it and confirm it writes `pacts/orders-basketcheckedout.json`
- [X] T019 [US2] Implement
      `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs`: a pure,
      uncalled function `ToEvent(Basket basket, string tenantId, string correlationId, Guid eventId,
      DateTime occurredAtUtc)` returning a `BasketCheckedOutV1` built from the basket's `Id`,
      `CustomerRef`, `LineItems` (mapped to `BasketLineItemV1`), and `Total`
      (`services/baskets/src/Baskets.Api/Data/Basket.cs`), plus the tenant/correlation/event
      identifiers passed in. No caller is wired yet — this is payload construction only, matching
      research.md Decision 3 (no MassTransit, no publish call; SCRUM-31's job)
- [X] T020 [US2] Write `BasketCheckedOutProviderPactTests.cs` in
      `services/baskets/tests/Baskets.Api.ContractTests/`: build a `Basket` via
      `Basket.ForCustomer`/`AddItem` (the same domain API `Baskets.Api.UnitTests` already exercises),
      call `BasketCheckedOutMapper.ToEvent`, and verify the resulting payload against
      `pacts/orders-basketcheckedout.json` using `PactNet`'s message verification (depends on T018,
      T019). Confirm it passes
- [X] T021 [US2] Revert-and-confirm-red for the event boundary: temporarily swap the `Total` argument
      for a hardcoded `0m` inside `BasketCheckedOutMapper.ToEvent` in
      `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs`, run
      `dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter BasketCheckedOutProviderPactTests`,
      confirm it now fails naming the `total` mismatch, then `git checkout --
      services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs` and re-run to
            confirm green again (Jira Test Scenario 1, applied to the event boundary)
      - **Executed differently, deliberately.** The hardcoded `0m` does *not* fail: `orders`
        records `total` with a type matcher, so a wrong-but-numeric total is a data bug rather than
        a contract break, and pinning the value would contradict FR-007's tolerant-reader rule.
        Verified by running it. The red proof was therefore made with a change that *is* a contract
        break — emitting no `items` — which failed `Baskets.Api.ContractTests` naming
        `$.items -> Expected [] (size 0) to have minimum size of 1`, and passed again once the
        mapper was restored.

**Checkpoint**: FR-004–FR-006 are satisfied for the piloted event boundary; SC-002's event half is
closed. User Stories 1 and 2 are both independently complete.

---

## Phase 5: User Story 3 - Boundary contract-test coverage is auditable (Priority: P3)

**Goal**: Prove FR-007–FR-009 — the four thin-slice boundaries are enumerable and cross-checkable
against existing contract tests, and removing a required one is caught (spec SC-001, SC-003, SC-004).

**Independent Test**: Run the new coverage test suite; then temporarily delete one required pact
file or provider test file, re-run, and confirm it fails naming the missing boundary.

### Implementation for User Story 3

- [X] T022 [US3] Create `tests/ContractCoverageTests/ContractCoverageTests.csproj` (net10.0,
      `Nullable` enabled, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
      `coverlet.collector` — no `ProjectReference` to any service, matching
      `tests/StructureConventionTests/StructureConventionTests.csproj`'s filesystem-only pattern),
      and add it to `Ecommerce.slnx`
- [X] T023 [US3] Implement `tests/ContractCoverageTests/ContractCoverageScanner.cs`, modeled on
      `tests/StructureConventionTests/VerticalSliceStructureScanner.cs`: locate the repo root, define
      the four expected boundaries from [data-model.md](./data-model.md#boundary) (name → pact file
      path under `pacts/`, expected provider/consumer test source file path), and return a result
      listing any boundary missing its pact file or its test source file
- [X] T024 [US3] Implement `tests/ContractCoverageTests/ContractCoverageTests.cs`, modeled on
      `tests/StructureConventionTests/VerticalSliceStructureTests.cs`'s two-test pattern: 
      `AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest` asserts
      `ContractCoverageScanner.Scan(...).Violations` is empty, and
      `Scan_ActuallyExaminesAllFourExpectedBoundaries` asserts the scan's `ScannedBoundaries` count
      is exactly 4 (guards against a relocated `pacts/` directory silently reporting zero
      violations). Run `dotnet test tests/ContractCoverageTests` and confirm both pass now that
      Phases 3–4 have produced all four pact files and test source files (depends on T023 and on
      T009–T021)
- [X] T025 [US3] Revert-and-confirm-red for coverage: temporarily rename
      `services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs` to a
      `.cs.bak` extension, run `dotnet test tests/ContractCoverageTests`, confirm
      `AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest` now fails naming the BFF↔products
      boundary, then rename the file back and re-run to confirm green again (Jira Test Scenario 3)

**Checkpoint**: FR-007–FR-009 are satisfied; SC-001, SC-003, and SC-004 are closed. All three user
stories are now independently complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that the whole suite is green, nothing stray was left behind, and
every success criterion holds end to end.

- [X] T026 [P] Run `dotnet build Ecommerce.slnx` and confirm the whole solution — the five new test
      projects (`Bff.Api.ContractTests`, `Products.Api.ContractTests`, `Baskets.Api.ContractTests`,
      `Orders.Api.ContractTests`, `ContractCoverageTests`) plus every existing service — builds
      cleanly
- [X] T027 [P] Run `git status` and `git diff` at the repo root and confirm the only changes are
      `Directory.Packages.props`, `Ecommerce.slnx`, the five new test projects, `pacts/`, the new
      `BasketCheckedOutMapper.cs`, and this feature's own `specs/011-consumer-contract-tests/`
      artifacts — no stray edits survive any revert-and-confirm-red step
- [X] T028 Walk through [quickstart.md](./quickstart.md) SC-001 through SC-004 top to bottom exactly
      as written and confirm each produces its documented expected result

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Empty — no additional blocking work beyond Setup
- **User Story 1 (Phase 3)**: Depends on Setup (T001–T008) — needs the four contract-test projects
  to exist and build
- **User Story 2 (Phase 4)**: Depends on Setup only, same as User Story 1; independent of User
  Story 1 (different files: the event pilot touches `baskets`/`orders`' event-pilot test classes
  and a new `BasketCheckedOutMapper.cs`, never the HTTP response DTOs User Story 1 touches)
- **User Story 3 (Phase 5)**: Depends on Setup for its own project (T022), but its tests only pass
  meaningfully once Phases 3–4 have produced all four pact files and test source files — build it
  after, even though its own scaffolding (T022–T023) could start earlier
- **Polish (Phase 6)**: Depends on all three user stories being complete — T027/T028 need every
  revert-and-restore cycle finished so the working tree is clean and every boundary is covered

### Within Each User Story

- User Story 1: T009/T010/T011 (consumer pacts, one per boundary, different files) are
  parallelizable; each is followed by its own sequential provider-verification task
  (T012/T014/T016) that depends only on its own consumer pact, then its own
  revert-and-confirm-red task (T013/T015/T017)
- User Story 2: T018 (consumer pact) and T019 (mapper) can run in parallel (different files, no
  dependency on each other); T020 (provider verification) depends on both; T021 depends on T020
- User Story 3: T022 (project) → T023 (scanner) → T024 (tests) → T025 (revert-and-confirm-red) are
  strictly sequential

### Parallel Opportunities

- T002–T006 (Setup) in parallel — different projects/files
- T009, T010, T011 (User Story 1 consumer pacts) in parallel with each other
- T009–T017 (User Story 1) can run in parallel with T018–T021 (User Story 2) once Setup is complete
  — entirely different files
- T026 and T027 (Polish) in parallel — one builds, the other only reads `git status`/`git diff`

---

## Parallel Example: Setup Complete, Two Stories in Parallel

```bash
# Once T001-T008 (Setup) are green, hand each story to a different track:
Task: "US1 — HTTP boundaries across bff/products/baskets/orders (T009-T017)"
Task: "US2 — BasketCheckedOut event pilot (T018-T021)"
# US3 (T022-T025) follows once US1/US2's artifacts exist for it to audit.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1
3. **STOP and VALIDATE**: FR-001–FR-003, FR-005, FR-006 provably true across all three HTTP
   boundaries — this alone closes the HTTP two-thirds of the ticket's acceptance criteria

### Incremental Delivery

1. Setup (Phase 1) → four contract-test projects and `pacts/` exist and build
2. User Story 1 (Phase 3) → HTTP boundaries provably covered (MVP)
3. User Story 2 (Phase 4) → event pilot provably covered, in parallel with Phase 3
4. User Story 3 (Phase 5) → coverage audit proven, once Phases 3–4 have produced their artifacts
5. Polish (Phase 6) → full solution build, clean working tree, quickstart walkthrough passes

### Parallel Team Strategy

With up to two people, once Setup is done:

- Person A: User Story 1 (three HTTP boundaries)
- Person B: User Story 2 (event pilot)
- Either, once both land: User Story 3 (coverage audit)

---

## Notes

- No production runtime behavior changes except the new, uncalled
  `BasketCheckedOutMapper.ToEvent` (T019) — every HTTP revert in Phase 3 and the mapper revert in
  Phase 4 is explicitly reverted within the same task.
- [P] tasks touch different files (or, for the User Story 1 consumer pacts, different boundary test
  classes within the same project) with no dependencies on each other.
- Commit after each task or logical group, consistent with
  `docs/engineering/test-first-commits.md` from `009-retrofit-tdd-basket-order`: a failing (red)
  proof lands alongside the code/config that makes it pass, never as a same-or-later "add tests"
  follow-up commit.
