---

description: "Task list template for feature implementation"
---

# Tasks: OpenAPI Specs for BFF Routes + Generated Clients

**Input**: Design documents from `/specs/007-bff-openapi-contracts/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: This feature's deliverable for User Story 3 *is* test coverage (tolerant-reader
behavior currently has none), so those tasks are listed directly as implementation. User Stories 1
and 2 require no code changes — the underlying pipeline already ships from spec 004 — so their
tasks are verification steps confirming the spec's success criteria, per [plan.md](./plan.md)
Summary and [research.md](./research.md).

**Organization**: Tasks are grouped by user story, matching spec.md's three stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

Existing web-application monorepo (no new structure): `frontend/apps/web/`,
`frontend/packages/api-client/`, `services/bff/src/Bff.Api/`.

---

## Phase 1: Setup

**Purpose**: Confirm the local toolchain is ready. No new dependencies or scaffolding — everything
needed already exists in the repo.

- [X] T001 [P] Install/verify frontend workspace dependencies: run `pnpm install` from `frontend/`
- [X] T002 [P] Verify the BFF builds cleanly: run `dotnet build services/bff/src/Bff.Api`

---

## Phase 2: Foundational

**Purpose**: Get the BFF's live OpenAPI document servable, since both User Story 1 and User Story 2
verification steps depend on it.

**⚠️ CRITICAL**: Complete before starting the User Story 1 or User Story 2 phases below.

- [X] T003 Start the BFF locally in Development mode: `dotnet run --project services/bff/src/Bff.Api` (listens on `http://localhost:5301`, publishing its OpenAPI document at `http://localhost:5301/openapi/v1.json`)

**Checkpoint**: BFF running locally with its OpenAPI document reachable — User Stories 1 and 2 can
now be verified. User Story 3 does not depend on this (it uses MSW mocks) and can proceed in
parallel.

---

## Phase 3: User Story 1 - Contract precedes implementation for BFF routes (Priority: P1)

**Goal**: Confirm every products, baskets, and orders BFF route has an OpenAPI spec that
accurately describes its current behavior (spec SC-001) — verified structurally rather than newly
built, per [research.md](./research.md) Decision 2.

**Independent Test**: Fetch `http://localhost:5301/openapi/v1.json` and cross-check its declared
paths/shapes against each route file's `.Produces<T>()` / `.ProducesProblem(...)` declarations.

### Verification for User Story 1

- [X] T004 [P] [US1] Verify OpenAPI accuracy for products routes: compare `http://localhost:5301/openapi/v1.json`'s `/bff/products` entry against `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs`
- [X] T005 [P] [US1] Verify OpenAPI accuracy for baskets routes: compare `http://localhost:5301/openapi/v1.json`'s `/bff/basket*` entries against `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs`
- [X] T006 [P] [US1] Verify OpenAPI accuracy for orders routes: compare `http://localhost:5301/openapi/v1.json`'s `/bff/orders*` entry against `services/bff/src/Bff.Api/Features/Orders/OrdersEndpoints.cs`

**Checkpoint**: SC-001 confirmed independently for all three domain areas. If any discrepancy is
found in T004-T006, it is a real defect (a route missing `.Produces<T>()` or similar) — file it and
fix the route's metadata before proceeding, since that would be the first actual gap found in an
otherwise-shipped pipeline.

---

## Phase 4: User Story 2 - SPA API client is fully generated from the contract (Priority: P1)

**Goal**: Confirm the SPA's API client for products, baskets, and orders is 100% generated, with no
hand-written HTTP calls and no manual edits to generated files (spec SC-002, SC-003, SC-005).

**Independent Test**: Run `verify-generated`; grep the SPA source for raw `fetch`/`axios` calls;
time a full regeneration.

### Verification for User Story 2

- [X] T007 [US2] Run `pnpm --filter @ecommerce/api-client verify-generated` from `frontend/` and confirm it exits 0 (SC-002, SC-003 — fails on any drift or uncommitted generated output)
- [X] T008 [US2] Search the SPA for raw HTTP calls: `grep -rE "fetch\(|axios\(" frontend/apps/web/src` and confirm zero matches call BFF endpoints outside `@ecommerce/api-client` (SC-003)
- [X] T009 [US2] Time a full client regeneration: `time pnpm --filter @ecommerce/api-client generate` from `frontend/` and confirm it is a single command completing in under one minute (SC-005)

**Checkpoint**: SC-002, SC-003, and SC-005 confirmed. If T007 or T008 fails, that is a real defect
(drifted/uncommitted generated code, or a hand-written call slipped in) — fix it before proceeding.

---

## Phase 5: User Story 3 - Generated client tolerates unknown response fields (Priority: P2)

**Goal**: Prove the SPA does not break when a products, baskets, or orders BFF response contains a
field the client doesn't know about (spec FR-006, SC-004) — this is the one genuinely new gap, per
[research.md](./research.md) Decision 3.

**Independent Test**: Run the three new test cases below; all pass.

### Implementation for User Story 3

- [X] T010 [P] [US3] Add a tolerant-reader test case to `frontend/apps/web/tests/catalog/ProductList.test.tsx`: mock `GET /bff/products` with an item containing an extra unrecognized field (e.g. `sku`) and assert the product still renders with its name and price
- [X] T011 [P] [US3] Add a tolerant-reader test case to `frontend/apps/web/tests/basket/BasketView.test.tsx`: mock `GET /bff/basket` with a response containing an extra unrecognized field (e.g. on a line item) and assert the basket still renders its items and total correctly
- [X] T012 [P] [US3] Add a tolerant-reader test case to `frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx`: mock `POST /bff/checkout` with a response containing an extra unrecognized field alongside `id`/`placedAtUtc`/`total`, and assert `CheckoutButton` still completes successfully and hands the expected order fields to `onCheckedOut` (this is the file that already exercises the real checkout round trip that produces the data `Confirmation` renders — `Confirmation.test.tsx` itself only tests the presentational component with a hardcoded prop, so it can't exercise response parsing)
- [X] T013 [US3] Run the three new cases: `pnpm --filter @ecommerce/web test -- ProductList BasketView DoubleSubmit` from `frontend/` and confirm all pass (SC-004)

**Checkpoint**: SC-004 confirmed for all three domain areas. All three user stories are now
independently verified/complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that nothing regressed and the spec's quickstart holds end to end.

- [X] T014 Run the full frontend test suite: `pnpm --filter @ecommerce/web test` from `frontend/` and confirm no regressions from the new test cases
- [X] T015 Walk through [quickstart.md](./quickstart.md) top to bottom and confirm every success criterion (SC-001 through SC-005) passes as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS User Story 1 and User Story 2 (both need the live BFF); does NOT block User Story 3 (MSW-only, no live BFF needed)
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2)
- **User Story 2 (Phase 4)**: Depends on Foundational (Phase 2); independent of User Story 1
- **User Story 3 (Phase 5)**: Depends only on Setup (Phase 1) — can run in parallel with Phases 2-4
- **Polish (Phase 6)**: Depends on all three user stories being complete

### Within Each User Story

- User Story 1: T004-T006 are independent per domain area, all parallelizable
- User Story 2: T007 → T008 → T009 are sequential (each is a quick standalone check, order doesn't
  strictly matter, but T007 establishes a clean generated-output baseline before the grep in T008)
- User Story 3: T010-T012 are independent per domain area (different files), parallelizable; T013
  depends on all three being written

### Parallel Opportunities

- T001 and T002 (Setup) in parallel
- T004, T005, T006 (User Story 1) in parallel
- T010, T011, T012 (User Story 3) in parallel
- User Story 3 (Phase 5) can run in parallel with Foundational/US1/US2 (Phases 2-4), since it has no
  dependency on the live BFF

---

## Parallel Example: User Story 3

```bash
# Launch all three tolerant-reader test additions together (different files, no shared dependency):
Task: "Add tolerant-reader case to frontend/apps/web/tests/catalog/ProductList.test.tsx"
Task: "Add tolerant-reader case to frontend/apps/web/tests/basket/BasketView.test.tsx"
Task: "Add tolerant-reader case to frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 3 Only)

User Story 3 is the only phase with real implementation work — Stories 1 and 2 are verification of
already-shipped behavior. If time is constrained, complete Setup (Phase 1) + User Story 3
(Phase 5) + T014 first: that closes the one genuine gap this feature exists to close. Phases 2-4
can follow as a quick confirmation pass, or be done first if you want early assurance nothing has
silently drifted before investing in new tests.

### Incremental Delivery

1. Setup (Phase 1) → toolchain confirmed
2. User Story 3 (Phase 5) → the real gap closed, independently testable and mergeable on its own
3. Foundational (Phase 2) + User Story 1 (Phase 3) + User Story 2 (Phase 4) → confirms SC-001,
   SC-002, SC-003, SC-005 still hold (can run before or after Story 3 — no ordering dependency)
4. Polish (Phase 6) → final full-suite run and quickstart walkthrough

---

## Notes

- No new production code is expected anywhere in this feature (per plan.md Summary). If any
  verification task (T004-T009) turns up an actual discrepancy, that is a real defect outside this
  feature's assumed scope — pause and re-scope rather than silently patching it inside a "verification"
  task.
- [P] tasks touch different files with no dependencies on each other
- Commit after each task or logical group
