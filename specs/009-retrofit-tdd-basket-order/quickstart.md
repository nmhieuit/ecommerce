# Quickstart: Verifying the Retrofit TDD Initiative

This is a verification guide, not a build guide — `research.md` Decision 1 found no behavioral code
to write. Run this to produce the evidence Jira SCRUM-19's three test scenarios ask for.

## Prerequisites

- Repository checked out at this feature's branch.
- .NET 10 SDK installed (matches `Directory.Build.props`).
- Docker available and running, for the Testcontainers-backed integration tests.

## Scenario 1 — Commit history shows tests with (not days after) their implementation

Maps to Jira Test Scenario 1 and `research.md` Decision 2.

```bash
git log --oneline --follow -- services/baskets/src/Baskets.Api/Data/Basket.cs
git log --oneline --follow -- services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs
git log --oneline --follow -- services/orders/src/Orders.Api/Data/Order.cs
git log --oneline --follow -- services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs
```

**Expected today**: implementation and test commits coincide (bundled into one commit each), not
separated by days — satisfies the "not follow days later" bar the ticket sets for existing history.
**Expected going forward**: per `docs/engineering/test-first-commits.md`, any *new* commit touching
this logic shows its failing test landing first or in the same reviewable change, never as a
same-or-later "add tests" afterthought commit.

## Scenario 2 — The unit test suite fails if a rule is reverted to Phase 1 behavior

Maps to Jira Test Scenario 2, spec SC-001, SC-004, and `research.md` Decision 4. Run from repo root;
revert each change with `git checkout -- <file>` (or undo manually) once its test has been observed
failing.

| Rule (FR) | Temporarily do this in `Basket.cs` / `Order.cs` | Run | Expect |
|---|---|---|---|
| FR-001 quantity floor | Remove `ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1)` in `Basket.AddItem` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_AQuantityBelowOne` | Test fails |
| FR-002 price stability | Change the merge branch to also overwrite `existing.UnitPrice = unitPrice` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` | Test fails |
| FR-003 line dedup | Remove the `existing is not null` branch so every add appends a new line | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket` | Test fails |
| FR-004 empty-order rejection | Remove the `lines.Count == 0` check in `Order.PlaceFrom` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet` | Test fails |
| FR-005 invalid-line rejection | Remove the per-line `ArgumentOutOfRangeException` checks in `Order.PlaceFrom` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_ALineWithANonPositiveQuantity` | Test fails |
| FR-006 computed total | Change `Total = lines.Sum(...)` to a hardcoded value | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_SumsEveryLine` | Test fails |

Restore each file (`git checkout -- <file>`) after observing the failure, then re-run the full suite
to confirm green:

```bash
dotnet test services/baskets/tests/Baskets.Api.UnitTests
dotnet test services/orders/tests/Orders.Api.UnitTests
```

## Scenario 3 — Creating an order from an empty basket is rejected at the domain layer

Maps to Jira Test Scenario 3 and spec SC-003. Already covered by an existing, passing test — this
step confirms it without any code edit:

```bash
dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet
dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrder_Rejects_ARequestWithNoLines
```

**Expected**: both pass — the domain layer (`Order.PlaceFrom`) and the HTTP layer (`POST /orders`)
both reject an empty basket.

## Outcome

All three scenarios passing is the acceptance bar for this initiative: SC-001 (100% of the named
rules provably tested), SC-002 (commit-history audit passes), SC-003 (empty-basket rejection
provable with zero manual steps beyond running the suite), and SC-004 (suite fails on
Phase-1-behavior reversion) are all satisfied by the tests that already exist in this repository,
run and read as above.
