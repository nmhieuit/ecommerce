---

description: "Task list template for feature implementation"
---

# Tasks: Retrofit TDD for Basket Pricing and Order Creation

**Input**: Design documents from `/specs/009-retrofit-tdd-basket-order/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [quickstart.md](./quickstart.md)

**Tests**: [research.md](./research.md) Decision 1 found FR-001–FR-006 already implemented and already
unit- and integration-tested — there is no new behavior to write tests for. Every task below is
either a verification task (running and reverting existing tests to prove they hold, per
[quickstart.md](./quickstart.md)) or the one net-new artifact this feature adds
(`docs/engineering/test-first-commits.md`). No production code changes survive any task.

**Organization**: Tasks are grouped by user story, matching spec.md's three stories (US1, US2 at P1;
US3 at P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

No new project. All paths are under the existing `services/baskets/`, `services/orders/`
directories (audited, not modified beyond a task's own temporary revert-and-restore step) plus one
new file at `docs/engineering/test-first-commits.md`.

---

## Phase 1: Setup

**Purpose**: Confirm the starting state is clean before any revert-and-confirm-red step touches
production code.

- [ ] T001 Run `dotnet build Ecommerce.slnx` from the repo root and confirm the solution — including
      `services/baskets` and `services/orders` — builds cleanly before any audit work begins

**Checkpoint**: Baseline build is green. Safe to proceed to user story verification.

---

## Phase 2: Foundational

**Purpose**: Blocking prerequisites shared by all three user stories.

None. Unlike a typical feature, there is no shared infrastructure to stand up — the code every
story audits already exists (per [research.md](./research.md) Decision 1), so each story's tasks
are independently runnable once Phase 1's baseline build is confirmed. Proceed directly to User
Story 1.

---

## Phase 3: User Story 1 - Basket pricing edge cases are proven by failing-first tests (Priority: P1) 🎯 MVP

**Goal**: Prove FR-001 (quantity floor), FR-002 (price-at-add-time stability), and FR-003 (line
dedup) each have a unit test that fails when the rule is reverted (spec SC-001, SC-004).

**Independent Test**: Run `Baskets.Api.UnitTests`, then revert each of the three guards in
`Basket.AddItem` in turn — each reversion must fail exactly one named test.

### Implementation for User Story 1

- [ ] T002 [P] [US1] Run `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter FullyQualifiedName~BasketLineMergeTests` and confirm every case passes — baseline evidence for FR-001–003
- [ ] T003 [US1] Revert-and-confirm-red for FR-001 (quantity floor) in `services/baskets/src/Baskets.Api/Data/Basket.cs`: comment out `ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);` in `AddItem`, run `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_AQuantityBelowOne`, confirm it fails, then `git checkout -- services/baskets/src/Baskets.Api/Data/Basket.cs` and re-run to confirm green again (quickstart.md Scenario 2)
- [ ] T004 [US1] Revert-and-confirm-red for FR-002 (price stability) in `services/baskets/src/Baskets.Api/Data/Basket.cs`: in the merge branch of `AddItem`, temporarily add `existing.UnitPrice = unitPrice;`, run `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged`, confirm it fails, revert with `git checkout -- services/baskets/src/Baskets.Api/Data/Basket.cs`, confirm green again
- [ ] T005 [US1] Revert-and-confirm-red for FR-003 (line dedup) in `services/baskets/src/Baskets.Api/Data/Basket.cs`: remove the `existing is not null` branch in `AddItem` so every call appends a new line, run `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket`, confirm it fails, revert with `git checkout -- services/baskets/src/Baskets.Api/Data/Basket.cs`, confirm green again

**Checkpoint**: FR-001–003 are provably tested and provably regression-catching. User Story 1 is
independently complete.

---

## Phase 4: User Story 2 - Order-creation rules are proven by failing-first tests (Priority: P1)

**Goal**: Prove FR-004 (empty-order rejection), FR-005 (invalid-line rejection), and FR-006
(computed total) each have a unit test that fails when the rule is reverted, and that the
empty-basket rejection holds at the HTTP layer too (spec SC-001, SC-003, SC-004; Jira Test
Scenario 3).

**Independent Test**: Run `Orders.Api.UnitTests` and the empty-basket case of
`Orders.Api.IntegrationTests`, then revert each of the three guards in `Order.PlaceFrom` in turn —
each reversion must fail exactly one named test.

### Implementation for User Story 2

- [ ] T006 [P] [US2] Run `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~OrderTotalTests` and confirm every case passes — baseline evidence for FR-004–006
- [ ] T007 [P] [US2] Run `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrder_Rejects_ARequestWithNoLines` (requires Docker for the SQL Server Testcontainer) and confirm it passes — the HTTP-layer half of the empty-basket rejection (Jira Test Scenario 3)
- [ ] T008 [US2] Revert-and-confirm-red for FR-004 (empty-order rejection) in `services/orders/src/Orders.Api/Data/Order.cs`: comment out the `if (lines.Count == 0)` block in `PlaceFrom`, run `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet`, confirm it fails, revert with `git checkout -- services/orders/src/Orders.Api/Data/Order.cs`, confirm green again (quickstart.md Scenario 2 and Scenario 3)
- [ ] T009 [US2] Revert-and-confirm-red for FR-005 (invalid-line rejection) in `services/orders/src/Orders.Api/Data/Order.cs`: comment out the per-line `ArgumentOutOfRangeException.ThrowIfLessThan`/`ThrowIfNegative` checks in `PlaceFrom`, run `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_ALineWithANonPositiveQuantity`, confirm it fails, revert with `git checkout -- services/orders/src/Orders.Api/Data/Order.cs`, confirm green again
- [ ] T010 [US2] Revert-and-confirm-red for FR-006 (computed total) in `services/orders/src/Orders.Api/Data/Order.cs`: replace `Total = lines.Sum(line => line.Quantity * line.UnitPrice),` with a hardcoded value in `PlaceFrom`, run `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_SumsEveryLine`, confirm it fails, revert with `git checkout -- services/orders/src/Orders.Api/Data/Order.cs`, confirm green again

**Checkpoint**: FR-004–006 are provably tested and provably regression-catching; the empty-basket
rejection is confirmed at both the domain and HTTP layers. User Stories 1 and 2 are both
independently complete.

---

## Phase 5: User Story 3 - TDD discipline is verifiable from commit history (Priority: P2)

**Goal**: Make the commit-history gap [research.md](./research.md) Decision 2 found (tests bundled
with implementation in single commits, not shown as failing-first) auditable and closed going
forward (spec SC-002).

**Independent Test**: Run the `git log --follow` audit from `quickstart.md` Scenario 1 and confirm
it matches the documented baseline; confirm the going-forward practice note exists and states the
rule plus the audit procedure.

### Implementation for User Story 3

- [ ] T011 [US3] Run the commit-history audit from `quickstart.md` Scenario 1 — `git log --oneline --follow` against `services/baskets/src/Baskets.Api/Data/Basket.cs`, `services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs`, `services/orders/src/Orders.Api/Data/Order.cs`, and `services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs` — and confirm the result matches [research.md](./research.md) Decision 2 (implementation and tests bundled together in commits `1bc77a6`, `c99783c`, `b3873b5`, not separated by days)
- [ ] T012 [US3] Write `docs/engineering/test-first-commits.md`: state the going-forward rule (a commit touching basket-pricing or order-creation logic must be preceded or accompanied by a failing test that the change makes pass, and must never arrive as a same-day-or-later "add tests" follow-up commit), include the `git log --follow` audit commands from T011 as the verification procedure, and link to this feature's [quickstart.md](./quickstart.md) for the full validation walkthrough (depends on T011; per [research.md](./research.md) Decision 3)

**Checkpoint**: The commit-discipline rule and its audit procedure are written down and
discoverable. All three user stories are now independently complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that nothing was left modified and every success criterion holds
end to end.

- [ ] T013 Run `dotnet test services/baskets/tests/Baskets.Api.UnitTests`, `services/orders/tests/Orders.Api.UnitTests`, `services/baskets/tests/Baskets.Api.IntegrationTests`, and `services/orders/tests/Orders.Api.IntegrationTests` together and confirm all green — proves every revert-and-restore step in Phases 3–4 left production code exactly as it was
- [ ] T014 [P] Run `git status` and `git diff` at the repo root and confirm the only changes in the working tree are `docs/engineering/test-first-commits.md` and this feature's own `specs/009-retrofit-tdd-basket-order/` artifacts — no stray edits survive from the revert-and-confirm-red steps
- [ ] T015 Walk through [quickstart.md](./quickstart.md) top to bottom exactly as written and confirm Scenarios 1–3 all produce their documented expected results, closing SC-001 through SC-004

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Empty — no additional blocking work beyond Setup
- **User Story 1 (Phase 3)**: Depends on Setup (Phase 1) — needs the confirmed-green baseline build
- **User Story 2 (Phase 4)**: Depends on Setup (Phase 1); independent of User Story 1 (touches
  `Order.cs`, not `Basket.cs`) — can run in parallel with Phase 3
- **User Story 3 (Phase 5)**: Depends on Setup (Phase 1) only — the commit-history audit reads
  existing history and is independent of the revert-and-confirm-red work in Phases 3–4; can run in
  parallel with both
- **Polish (Phase 6)**: Depends on all three user stories being complete — T013/T014 specifically
  need Phases 3–4's revert-and-restore cycles finished so the working tree is clean

### Within Each User Story

- User Story 1: T002 (baseline) is independent/parallelizable; T003 → T004 → T005 are sequential —
  all three edit and restore the same file (`Basket.cs`), so a second revert must not start until
  the previous one has been reverted
- User Story 2: T006 and T007 (baselines, different files) are parallelizable; T008 → T009 → T010
  are sequential — all three edit and restore the same file (`Order.cs`)
- User Story 3: T011 (audit) before T012 (practice note cites T011's commands)

### Parallel Opportunities

- T006 and T007 (User Story 2 baselines) in parallel
- User Story 1 (Phase 3), User Story 2 (Phase 4), and User Story 3 (Phase 5) can run in parallel
  with each other once Setup (Phase 1) is complete — they touch entirely different files
  (`Basket.cs` vs. `Order.cs` vs. commit history/documentation)
- T014 (Polish) can run in parallel with T013/T015 — it only reads `git status`/`git diff`

---

## Parallel Example: Setup Complete, Three Stories in Parallel

```bash
# Once T001 (baseline build) is green, hand each story to a different track:
Task: "US1 — revert-and-confirm-red across Basket.cs (T002-T005)"
Task: "US2 — revert-and-confirm-red across Order.cs (T006-T010)"
Task: "US3 — commit-history audit and practice note (T011-T012)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1
3. **STOP and VALIDATE**: FR-001–003 provably tested and regression-catching — this alone closes
   the basket-pricing half of the ticket's acceptance criteria

### Incremental Delivery

1. Setup (Phase 1) → baseline build confirmed green
2. User Story 1 (Phase 3) → basket-pricing rules provably tested (MVP)
3. User Story 2 (Phase 4) → order-creation rules provably tested, in parallel with Phase 3
4. User Story 3 (Phase 5) → commit-discipline practice note written and auditable, in parallel with
   Phases 3–4
5. Polish (Phase 6) → full suite run, clean working tree confirmed, quickstart walkthrough passes

### Parallel Team Strategy

With up to three people, once Setup is done:

- Person A: User Story 1 (`Basket.cs` reverts)
- Person B: User Story 2 (`Order.cs` reverts)
- Person C: User Story 3 (commit audit + practice note)

All three integrate independently — different files, no shared state, and every revert is restored
before the task completes.

---

## Notes

- No production code changes survive this feature — every revert in Phases 3–4 is explicitly
  reverted within the same task, confirmed by T013/T014 in Polish.
- The only net-new file is `docs/engineering/test-first-commits.md` (T012).
- [P] tasks touch different files (or, for T002/T006/T007, only read/run existing tests) with no
  dependencies on each other.
- Commit after each task or logical group — and, in the spirit of this feature, do not bundle
  `docs/engineering/test-first-commits.md` (T012) into the same commit as an unrelated change.
