---

description: "Task list for 004-minimal-shopping-spa"
---

# Tasks: Minimal Shopping SPA — Browse, Basket, Checkout, Confirmation

**Input**: Design documents from `/specs/004-minimal-shopping-spa/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test tasks are included and are **not optional here**. Constitution Principle III makes Red-Green-Refactor non-negotiable and supersedes any other document, so every implementation task below is preceded by a failing test that it makes pass.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demoed on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: Which user story the task serves (US1, US2, US3)
- Every task names the exact file or directory it touches

## Path Conventions

- Backend services: `services/{name}/src/{Name}.Api/`, tests in `services/{name}/tests/`
- Shared libraries: `shared/Tenancy/`, tests in `shared/Tenancy.UnitTests/`
- Frontend: `frontend/apps/web/` and `frontend/packages/api-client/` (new workspace — see [plan.md](./plan.md) Structure Decision)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the frontend workspace that does not exist yet, and clear one blocking defect in the existing backend configuration.

- [X] T001 Create the frontend workspace root — `frontend/package.json`, `frontend/pnpm-workspace.yaml`, `frontend/turbo.json`, `frontend/tsconfig.base.json` — as a pnpm workspace orchestrated by Turborepo per ADR-0010
- [X] T002 [P] Scaffold the storefront app in `frontend/apps/web/` — Vite + React entry (`index.html`, `src/main.tsx`, `vite.config.ts`) and `tsconfig.json` with `strict` and `noUncheckedIndexedAccess` enabled
- [X] T003 [P] Configure ESLint and Prettier for the workspace in `frontend/eslint.config.js` and `frontend/.prettierrc`, set to fail the build on violation (constitution: Development Workflow — style is machine-enforced)
- [X] T004 [P] Add Tailwind CSS and the Radix primitives dependency to `frontend/apps/web/` — `tailwind.config.ts` and `src/styles.css` per ADR-0009 and [research.md](./research.md) Decision 2
- [X] T005 [P] Add the Vitest + Testing Library + MSW harness in `frontend/apps/web/vitest.config.ts` and `frontend/apps/web/tests/setup.ts`
- [X] T006 [P] Add the Playwright config in `frontend/apps/web/playwright.config.ts`, pointing at the Vite dev server and the gateway on port 5300
- [X] T007 [P] Declare the download-size budget in `frontend/apps/web/.size-limit.json` at 150 kB gzipped for the entry screen, per [research.md](./research.md) Decision 5 (spec FR-025, SC-011)
- [X] T008 [P] Scaffold the generated client package in `frontend/packages/api-client/` with `orval.config.ts` targeting the BFF's `/openapi/v1.json` and emitting TanStack Query hooks into `src/generated/` (ADR-0004)
- [X] T009 Define the task pipelines in `frontend/turbo.json` — `generate` → `build` → `test` / `lint` / `typecheck` / `size` / `e2e`, with `api-client` building before `web`, and `size` failing the build when the budget is exceeded
- [X] T010 Fix the swapped downstream base URLs in `services/bff/src/Bff.Api/appsettings.Development.json` — `BasketsApi` to `http://localhost:5188` and `OrdersApi` to `http://localhost:5041` (they are currently the wrong way round; see [plan.md](./plan.md) Complexity Tracking)

**Phase 1 completed 2026-08-16.** Three departures from the task text as written, each deliberate:

- **T004 has no `tailwind.config.ts`.** Tailwind v4 is configured in CSS rather than a JS config file, so the tokens and the focus-visible rule live in `frontend/apps/web/src/styles.css` behind `@import 'tailwindcss'`, wired through the `@tailwindcss/vite` plugin. Same outcome, current idiom, one fewer config file.
- **Vitest is v3, not v2.** Vitest 2 pins Vite 5 internally, which collides with the app's Vite 6 under `exactOptionalPropertyTypes` — the typecheck failed on mismatched `Plugin` types from two Vite copies. Vitest 3 pairs with Vite 6 and the conflict disappears. The alternative was relaxing `exactOptionalPropertyTypes`, which would have traded a real type-safety guarantee for a tooling inconvenience.
- **`/packages/` in the root `.gitignore` is now anchored.** It was NuGet's legacy restore folder pattern, unanchored, and would have silently ignored `frontend/packages/` — the generated API client's home. Fixed as part of T008 rather than left to be discovered by a missing file in review.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Carry the shopper's identity from the gateway to the services, and build the app shell every screen mounts into.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. US2 and US3 cannot resolve a basket without the subject header; every story needs the app shell.

- [X] T011 [P] Write failing unit tests for the caller context in `shared/Tenancy.UnitTests/CallerContextTests.cs` — Unresolved and Resolved states, and `RequireSubjectId()` throwing when the subject is null or blank
- [X] T012 [P] Write failing unit tests for the caller middleware in `shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs` — reads `X-Subject-Id`, leaves the context unresolved when the header is absent or blank, and opens a logging scope carrying the subject
- [X] T013 Implement `shared/Tenancy/CallerContext.cs` and `shared/Tenancy/MissingCallerContextException.cs` per [data-model.md](./data-model.md) — CallerContext
- [X] T014 Implement `shared/Tenancy/CallerContextMiddleware.cs`, mirroring `TenantContextMiddleware` exactly, per [contracts/subject-id-header.md](./contracts/subject-id-header.md)
- [X] T015 Extend `shared/Tenancy/TenancyExtensions.cs` so `AddTenancy()` registers the caller context and `UseTenancy()` wires its middleware — no service's `Program.cs` may need editing
- [X] T016 [P] Write a failing unit test for gateway subject stamping in `services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs` — stamps from the `NameIdentifier` claim, overwrites any inbound value, and removes the header when nothing is resolved
- [X] T017 Implement `services/gateway/src/Gateway.Api/Identity/SubjectHeaderPropagationMiddleware.cs` and register it after `TenantHeaderPropagationMiddleware` in `services/gateway/src/Gateway.Api/Program.cs`
- [X] T018 [P] Write a failing integration test in `services/bff/tests/Bff.Api.IntegrationTests/SubjectPropagationTests.cs` proving the subject reaches the downstream call, alongside the tenant
- [X] T019 Extend `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs` to relay `X-Subject-Id` on every outbound call, using the same remove-then-set discipline it already applies to the tenant
- [X] T020 [P] Build the app shell in `frontend/apps/web/src/app/` — router, `QueryClientProvider`, and the single backend-origin configuration pointing at the gateway (never the BFF or a service directly, per [research.md](./research.md) Decision 11 and spec FR-014)
- [X] T021 [P] Build the error boundary and shared error surface in `frontend/apps/web/src/shared/ErrorState.tsx` — bounded, readable, retryable, never a blank screen (spec FR-012)
- [X] T022 [P] Implement USD money formatting in `frontend/apps/web/src/shared/money.ts` with unit tests in `frontend/apps/web/tests/shared/money.test.ts` — symbol and two decimal places (spec FR-024)

**Phase 2 completed 2026-08-16.** Verified: `dotnet build` clean (0 warnings, warnings-as-errors on), Tenancy unit tests 28 passed, gateway unit tests 22 passed, gateway integration 17 passed, BFF propagation integration 4 passed (2 new subject + 2 existing tenant, against real containers), frontend typecheck/lint/test/build/size all green.

Three notes on how it was built:

- **T021 is a shared error surface, not a React error boundary.** `ErrorState` is the component every failing screen renders; TanStack Query already turns a failed request into an error state, so a thrown-exception boundary would catch render bugs rather than the backend failures FR-012 is about. A boundary can be added when there is a render path that can throw.
- **T016's unit test is not duplicated at integration level, deliberately.** The "removes the header when nothing resolved" case is unreachable end to end — Phase 1's stub authentication handler always succeeds, so no request through a running gateway arrives unauthenticated. That is precisely the case worth pinning before Phase 3 swaps in an issuer that can fail.
- **`TenantPropagationHandler` keeps its name while now relaying two headers.** Renaming it would leave three planning documents pointing at a file that no longer exists, for a cosmetic gain. Its doc comment states what it actually does. Worth revisiting as a standalone cleanup.

**Checkpoint**: identity flows end to end and the app shell renders. User stories can now begin.

---

## Phase 3: User Story 1 - Shopper browses the product catalog (Priority: P1) 🎯 MVP

**Goal**: A shopper opens the storefront and sees the products available to buy, each with its name and price, sourced from the backend.

**Independent Test**: Open the storefront with the backend running and confirm at least one product appears with its name and price — and that the same screen degrades correctly when the catalog is empty or the backend is down.

### Tests for User Story 1

> **Write these first and confirm they fail before implementing.**

- [X] T023 [P] [US1] Write a failing integration test for the seeded catalog in `services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs` — three known products with the fixed identifiers, names, and prices from [data-model.md](./data-model.md)
- [X] T024 [P] [US1] Write a failing component test for the product list in `frontend/apps/web/tests/catalog/ProductList.test.tsx` — names and prices found by accessible role, from a mocked BFF response
- [X] T025 [P] [US1] Write a failing component test for the empty catalog in `frontend/apps/web/tests/catalog/EmptyCatalog.test.tsx` — an explicit empty state, not a blank page or an endless spinner (spec FR-002)
- [X] T026 [P] [US1] Write a failing component test for the catalog error state in `frontend/apps/web/tests/catalog/CatalogError.test.tsx` — readable message, page still usable, retry available (spec FR-012, US1 scenario 3)

### Implementation for User Story 1

- [X] T027 [US1] Add the catalog seed migration under `services/products/src/Products.Api/Migrations/` using EF Core `HasData` with the three fixed products from [data-model.md](./data-model.md) (spec FR-018, [research.md](./research.md) Decision 10)
- [X] T028 [US1] Regenerate the API client and commit the output — run `pnpm generate` in `frontend/`, committing `frontend/packages/api-client/src/generated/`
- [X] T029 [P] [US1] Implement the product list in `frontend/apps/web/src/features/catalog/ProductList.tsx` using the generated products query hook and the shared money formatter (spec FR-001, FR-024)
- [X] T030 [P] [US1] Implement the empty and error states in `frontend/apps/web/src/features/catalog/CatalogStates.tsx` (spec FR-002, FR-012)
- [X] T031 [US1] Wire the catalog route as the storefront's landing screen in `frontend/apps/web/src/app/routes.tsx`

**Phase 3 completed 2026-08-16.** Verified end to end, not just in tests: with the products database migrated and the products service, BFF, and gateway all running, `GET http://localhost:5300/bff/products` returned the three seeded products with their names and prices, through the real edge. Plus: products integration tests 10 passed (3 new), frontend 22 tests passed across 5 files, typecheck/lint clean, bundle 104.55 kB of the 150 kB budget. Regeneration of the client was confirmed byte-identical, so the drift check T068 adds has something stable to compare against.

Four things worth knowing before the next phase:

- **The generated client types a price as `number | string`.** .NET's OpenAPI generator describes a `decimal` that way so a producer may preserve precision beyond a JSON double — but the design-time contract in [contracts/bff-openapi.yaml](./contracts/bff-openapi.yaml) says `number`/`double`. That is real drift between the two documents. `formatMoney` handles both rather than trusting the implementation, and SCRUM-17 (Phase 2, OpenAPI specs) is where the two should be reconciled.
- **The hand-written `bffFetch` had to return `{ data, status }`, not the bare body.** Orval's fetch client types every result as a union of `{ data, status }` members and reads `.data` off it; returning the parsed body typechecked against that union while being the wrong shape at runtime. Caught by the first component test, which is the argument for having written it first.
- **Screens narrow on `status`, not just `isError`.** The generated result type is a union over every declared response, so `data.data` is "products or problem details" until the status is checked. The check also degrades correctly if the transport ever stopped throwing on a failure status.
- **`[**/Migrations/*.cs]` in `.editorconfig` now disables CA1861.** EF's generated `InsertData` passes column-name arrays inline, which the rule flags; the rule's premise (a repeatedly-called hot path) does not hold for a migration that runs once, and a developer cannot act on it without editing generated output. Scoped to migrations only — hand-written code keeps the rule.

**Checkpoint**: browse works end to end and is demoable on its own. This is the MVP.

---

## Phase 4: User Story 2 - Shopper adds a product to the basket (Priority: P2)

**Goal**: A shopper adds a product to their basket and sees it reflected with the correct quantity, price, and total — surviving a refresh.

**Independent Test**: Add a listed product, open the basket, confirm one line at quantity 1; add the same product again and confirm the quantity becomes 2 rather than a second line appearing.

### Tests for User Story 2

- [X] T032 [P] [US2] Write a failing unit test for the quantity-merge rule in `services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs` — adding a product already present increments its line, never creating a second one (spec FR-005, FR-021)
- [X] T033 [P] [US2] Write a failing unit test for basket-total computation in `services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs` — the sum of quantity × captured unit price, computed and never stored ([data-model.md](./data-model.md))
- [X] T034 [P] [US2] Write failing integration tests for the caller-scoped basket in `services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs` — a first read returns an empty basket rather than 404, the same subject resolves the same basket across separate requests (spec FR-006, FR-011), a second subject gets a different basket, and a request carrying no subject fails rather than defaulting
- [X] T035 [P] [US2] Write a failing integration test for the BFF basket routes in `services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs` — `POST /bff/basket/items` resolves the unit price from the products service and rejects any price supplied by the client ([contracts/bff-openapi.yaml](./contracts/bff-openapi.yaml))
- [X] T036 [P] [US2] Write failing component tests for the basket view in `frontend/apps/web/tests/basket/BasketView.test.tsx` — lines, quantities, unit prices, and total, all asserted through accessible roles (spec FR-004)
- [X] T037 [P] [US2] Write a failing component test for a failed add in `frontend/apps/web/tests/basket/AddItemError.test.tsx` — a clear error appears and the basket never displays an item that was not actually added (US2 scenario 5)

### Implementation for User Story 2

- [X] T038 [P] [US2] Create the line-item entity in `services/baskets/src/Baskets.Api/Data/BasketLineItem.cs` per [data-model.md](./data-model.md)
- [X] T039 [US2] Change the basket entity in `services/baskets/src/Baskets.Api/Data/Basket.cs` — `CustomerRef` (string, required, max 200) replacing `CustomerId`, plus the owned `LineItems` collection
- [X] T040 [US2] Configure the model in `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs` — unique index on `CustomerRef`, unique index on (`BasketId`, `ProductId`), decimal precision (18,2) on `UnitPrice`, cascade delete of line items
- [X] T041 [US2] Add the baskets migration under `services/baskets/src/Baskets.Api/Migrations/` covering the new table and the column change
- [X] T042 [US2] Implement `GET /baskets/current` and `POST /baskets/current/items` in `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` — resolve the basket from the caller's subject, enforce quantity ≥ 1, merge existing lines, compute the total, and update the existing `/baskets/{basketId}` response shape to match ([contracts/downstream-openapi.yaml](./contracts/downstream-openapi.yaml))
- [X] T043 [US2] Extend `services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs` with the current-basket read and add-item calls
- [X] T044 [US2] Implement `GET /bff/basket` and `POST /bff/basket/items` in `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` — resolve the unit price via `ProductsApiClient`, aggregate product names onto the lines, and declare the 200/400/404/502/504 responses explicitly so the generated client knows them
- [X] T045 [US2] Regenerate the API client and commit `frontend/packages/api-client/src/generated/`
- [X] T046 [P] [US2] Implement the add-to-basket control in `frontend/apps/web/src/features/catalog/AddToBasketButton.tsx` using the generated mutation and invalidating the basket query on success (spec FR-003)
- [X] T047 [P] [US2] Implement the basket view in `frontend/apps/web/src/features/basket/BasketView.tsx` — lines, quantities, unit prices, and total through the shared money formatter (spec FR-004, FR-024)
- [X] T048 [US2] Wire the basket route into `frontend/apps/web/src/app/routes.tsx`

**Phase 4 completed 2026-08-16.** Verified through the real edge, not only in tests: adding the notebook twice and the apron once through `POST http://localhost:5300/bff/basket/items` produced two lines, quantity 2 on the first, and a total of **$59.25** — the figure quickstart.md quotes. A request carrying `unitPrice: 0.01` for the $48.00 Pour-Over Set stored it at **$48.00**; an unknown product returned 404 and a zero quantity returned 400. Suites: baskets unit 15, baskets integration 15, BFF unit 12, BFF integration 36, frontend 31 across 7 files. Solution builds clean at 0 warnings; bundle 105.88 kB of 150 kB.

Five things worth knowing:

- **A test caught an arithmetic error in another test.** `Total_MatchesTheWalkthroughFigure` was written asserting $59.25 for one notebook plus one apron, which is $46.75. The walkthrough's figure is two notebooks plus an apron. Pinning a number the documentation quotes is what made the mismatch visible.
- **EF issued an UPDATE instead of an INSERT for new basket lines.** `Basket.AddItem` was assigning `Id = Guid.NewGuid()`, and the change tracker decides "new row" versus "existing row" by whether a navigation-discovered child already has a key — so it tried to update a row that did not exist and failed with a concurrency exception. The identifier is now generated on insert. Found by the integration suite, invisible to the unit tests.
- **`EnsureSuccessStatusCode` was hiding the cause.** It reports "500" and nothing else. The basket test helper now asserts with the response body attached, which is what turned the above from guesswork into a one-line diagnosis.
- **The line total moved into the baskets service.** The BFF was briefly computing `quantity × unitPrice` while the plan claims it performs no arithmetic. The baskets service now returns `lineTotal` per line and the BFF passes it through; `contracts/downstream-openapi.yaml` was updated to match.
- **The add-item request has no price field at all.** Not "a price field that is ignored" — the request record cannot carry one, so there is nothing to accidentally trust. Verified against the running edge above.

**Checkpoint**: browse and basket both work independently. The basket survives a refresh because it is the server's basket for this caller.

---

## Phase 5: User Story 3 - Shopper checks out and receives a confirmation (Priority: P3)

**Goal**: A shopper turns their basket into an order and sees a confirmation naming it. The basket is emptied; checking out twice cannot create two orders.

**Independent Test**: Assemble a basket with at least one item, check out, and confirm the confirmation screen shows an order reference that matches the order actually created in the backend.

### Tests for User Story 3

- [ ] T049 [P] [US3] Write a failing unit test for order-total computation in `services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs` — the total is computed from the lines in the request and is never taken from the caller ([research.md](./research.md) Decision 8)
- [ ] T050 [P] [US3] Write a failing integration test for place-order in `services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs` — creates and returns an order, and rejects a request with no line items ([contracts/downstream-openapi.yaml](./contracts/downstream-openapi.yaml))
- [ ] T051 [P] [US3] Write a failing integration test for basket clearing in `services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs` — removes every line, keeps the basket row, and answers 409 when the basket is already empty
- [ ] T052 [P] [US3] Write a failing integration test for checkout in `services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs` — the order is created before the basket is cleared, an empty basket yields 409, and the response carries the order's identifier and total (spec FR-008, FR-009, [research.md](./research.md) Decision 9)
- [ ] T053 [P] [US3] Write a failing component test for the confirmation screen in `frontend/apps/web/tests/checkout/Confirmation.test.tsx` — the order identifier is shown verbatim alongside the total (spec FR-009)
- [ ] T054 [P] [US3] Write a failing component test for empty-basket blocking in `frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx` — the control is unavailable and **no request is issued at all** (spec FR-008, SC-004)
- [ ] T055 [P] [US3] Write a failing component test for the in-flight guard in `frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx` — triggering checkout twice fires exactly one mutation (spec FR-016)

### Implementation for User Story 3

- [ ] T056 [US3] Implement `POST /orders` in `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs` — compute the total from the request's lines, persist the order, return 201 with a `Location` header, and reject an empty line set
- [ ] T057 [US3] Implement `POST /baskets/current/clear` in `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` — delete the line items, keep the basket row, answer 409 when already empty
- [ ] T058 [P] [US3] Extend `services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs` with the place-order call
- [ ] T059 [P] [US3] Extend `services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs` with the clear-basket call
- [ ] T060 [US3] Implement `POST /bff/checkout` in `services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs` — read the caller's basket, place the order, clear the basket, return the confirmation, performing no arithmetic — and map it in `services/bff/src/Bff.Api/Program.cs`
- [ ] T061 [US3] Regenerate the API client and commit `frontend/packages/api-client/src/generated/`
- [ ] T062 [P] [US3] Implement the checkout control in `frontend/apps/web/src/features/checkout/CheckoutButton.tsx` — unavailable when the basket is empty and while a checkout is in flight (spec FR-008, FR-016)
- [ ] T063 [P] [US3] Implement the confirmation screen in `frontend/apps/web/src/features/checkout/Confirmation.tsx` — the order identifier verbatim, the total, and a "nothing to show" state when the route is reached without having checked out (spec FR-009, Edge Cases)
- [ ] T064 [US3] Wire the confirmation route and post-checkout basket invalidation into `frontend/apps/web/src/app/routes.tsx` so the basket reads as empty afterwards (spec FR-010)

**Checkpoint**: the full walking skeleton is demoable — browse → basket → checkout → confirmation.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T065 [P] Write the end-to-end walkthrough in `frontend/apps/web/e2e/walkthrough.spec.ts` — the happy path with zero console errors, every request addressed to the gateway only, a mid-basket reload preserving contents, rapid double checkout creating one order, and the whole flow completed by keyboard (spec SC-002, SC-005, SC-007, SC-008, SC-009, SC-010)
- [ ] T066 [P] Run an accessibility pass over the catalog, basket, and confirmation screens in `frontend/apps/web/src/features/` — accessible names, correct roles, visible focus on every interactive element (spec FR-017)
- [ ] T067 Measure the built bundle and tighten the budget in `frontend/apps/web/.size-limit.json` to just above the measured figure ([research.md](./research.md) Decision 5)
- [ ] T068 [P] Add the codegen drift check to `frontend/turbo.json` — run `pnpm generate` and fail when the working tree is dirty, so the committed client can never diverge from the BFF's document (ADR-0004)
- [ ] T069 [P] Record the Phase 1 checkout orchestration decision as `docs/adr/0011-checkout-orchestration.md` — the synchronous two-step, the Principle IV deviation, and the SCRUM-18/SCRUM-31 close-out (constitution: architecturally significant decisions MUST be ADRs)
- [ ] T070 [P] Document the frontend workspace commands in a new `frontend/README.md` and cross-reference it from `services/README.md`
- [ ] T071 Run every scenario in [quickstart.md](./quickstart.md) end to end and record the results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: depends on Setup — **blocks every user story**
- **User Story 1 (Phase 3)**: depends on Foundational only
- **User Story 2 (Phase 4)**: depends on Foundational. Shares the catalog with US1 in the interface, but its backend and tests stand alone
- **User Story 3 (Phase 5)**: depends on Foundational. Needs a non-empty basket to demo, so in practice it follows US2 even though its own tests seed baskets directly
- **Polish (Phase 6)**: depends on the stories you intend to ship

### Within each user story

- Tests are written and failing before the implementation that satisfies them
- Entities → persistence configuration → migration → service endpoints → BFF endpoints → client regeneration → screens
- Client regeneration (T028, T045, T061) always follows the BFF route it exposes and precedes the screen that consumes it

### Parallel Opportunities

- Setup: T002 – T008 all run in parallel; T009 and T010 follow
- Foundational: T011/T012 in parallel, then T013 → T014 → T015; T016 and T018 in parallel with those; T020/T021/T022 are frontend-only and run alongside all of it
- Every story's test tasks are marked [P] and run together
- US1's T029/T030, US2's T046/T047, and US3's T058/T059 and T062/T063 are parallel pairs
- With more than one developer, US2's backend (T038 – T044) and US1's frontend (T029 – T031) proceed at the same time

---

## Parallel Example: User Story 2

```bash
# All US2 tests together — they fail, which is the point:
Task: "Quantity-merge unit test in services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs"
Task: "Basket-total unit test in services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs"
Task: "Caller-scoped basket integration tests in services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs"
Task: "BFF basket route integration test in services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs"
Task: "Basket view component tests in frontend/apps/web/tests/basket/BasketView.test.tsx"
Task: "Failed-add component test in frontend/apps/web/tests/basket/AddItemError.test.tsx"

# Then the two frontend screens, once the routes exist and the client is regenerated:
Task: "Add-to-basket control in frontend/apps/web/src/features/catalog/AddToBasketButton.tsx"
Task: "Basket view in frontend/apps/web/src/features/basket/BasketView.tsx"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup
2. Phase 2 Foundational — **do not skip; everything below depends on it**
3. Phase 3 User Story 1
4. **Stop and validate**: quickstart Scenario 1, including the empty-catalog and backend-down checks
5. Demoable: a storefront listing real products through the real edge

### Incremental delivery

1. Setup + Foundational → identity flows, app shell renders
2. + US1 → browse works → demo (MVP)
3. + US2 → basket works, survives refresh → demo
4. + US3 → checkout and confirmation → the walking skeleton is closed
5. + Polish → the walkthrough is automated and the budget is real

---

## Not in scope for these tasks

Two items from [plan.md](./plan.md) are deliberately absent, so their absence is a decision rather than an oversight:

- **Schema-per-tenant separation.** Spec 003 specified it, marked its tasks complete, and did not ship it; `HasDefaultSchema` appears nowhere and every migration targets `dbo`. This feature adds the first tenant-owned business data on top of that gap. Closing it is contained — resolve the schema from the tenant context at each `AddDbContext` call site, plus one migration per service — but it sits outside this spec's clarified scope and was raised for a maintainer decision that has not been made. **If the answer is "fold it in", it belongs in Phase 2 and adds roughly four tasks.**
- **Event-driven checkout.** No messaging infrastructure exists at all. SCRUM-18 and SCRUM-31 own it; T069 records the interim decision so the deviation is written down rather than assumed.

---

## Notes

- [P] marks tasks touching different files with no incomplete dependency
- Every implementation task has a failing test ahead of it (Principle III)
- The BFF performs no arithmetic in any task above — totals are computed by the baskets and orders services, which is what keeps spec 002's FR-005 intact
- Commit after each task or logical group; stop at any checkpoint to validate a story on its own
