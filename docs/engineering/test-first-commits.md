# Test-First Commits: Basket Pricing and Order Creation

**Status**: Active practice note
**Applies to**: `services/baskets/src/Baskets.Api/Data/` and `services/orders/src/Orders.Api/Data/`
**Operationalizes**: Constitution [Principle III — Test-First Development (NON-NEGOTIABLE)](../../.specify/memory/constitution.md)
**Origin**: [009-retrofit-tdd-basket-order](../../specs/009-retrofit-tdd-basket-order/spec.md) (Jira SCRUM-19)

This is a practice note, not an ADR. It records no architectural decision — `docs/adr/` stays for
those. What it does is make one existing constitutional principle checkable for one code area, so a
reviewer can confirm compliance from the repository itself instead of from memory or a verbal norm.

## Why this note exists

Principle III already mandates Red-Green-Refactor platform-wide. The basket-pricing and
order-creation rules it covers are correct and are genuinely tested — but a `git log --follow` audit
during 009 found their tests landing *inside* the same large feature commits as the implementation
(`1bc77a6`, `c99783c`, `b3873b5`), never as a preceding failing-test commit.

That is a working style, not a broken one, and it clears the bar the source ticket actually sets
(tests must not "follow days later" — these follow by zero days). But it leaves nothing in the
repository that tells a future contributor what commit shape is expected, so the same bundling
would likely repeat. Existing history was deliberately **not** rewritten to insert synthetic red
commits: fabricating an order of events that did not happen would misrepresent authorship and fix
nothing. The gap is closed going forward instead — by this note.

## The rule

A commit that touches basket-pricing or order-creation logic **must** be preceded by, or arrive
together with, a failing test that the change makes pass.

It **must not** arrive as a same-day-or-later "add tests" follow-up commit.

Concretely, for any change to `Basket.AddItem`, `Basket.Total`, `Order.PlaceFrom`, or the entities
they own:

- **Preferred** — two commits: the failing test first, then the change that makes it green. This is
  what makes Red-Green-Refactor visible commit-by-commit.
- **Acceptable** — one commit containing both the test and the implementation, where the test would
  demonstrably fail without the implementation half.
- **Not acceptable** — implementation in one commit and its test in any later commit. A test written
  after the code it covers has never been observed to fail, so nothing proves it can catch the
  regression it claims to guard.

The last point is the whole rule. A test that has only ever been green is an assertion about the
code's *current* shape, not a guard against its future one.

## Verifying compliance

Run the same audit 009 used. Each file's history should show its test file changing in the same
commit as, or an earlier commit than, its implementation — never later:

```bash
git log --oneline --follow -- services/baskets/src/Baskets.Api/Data/Basket.cs
git log --oneline --follow -- services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs
git log --oneline --follow -- services/orders/src/Orders.Api/Data/Order.cs
git log --oneline --follow -- services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs
```

To confirm a specific commit bundled its test rather than deferring it:

```bash
git show --stat --format="" <sha>
```

## Confirming the tests actually catch regressions

Commit shape proves intent; it does not prove the tests bite. To prove that, temporarily weaken a
guard, watch the matching test go red, then restore it.
[`specs/009-retrofit-tdd-basket-order/quickstart.md`](../../specs/009-retrofit-tdd-basket-order/quickstart.md)
walks all six rules through this end to end and is the full validation procedure for this area.
The short version:

| Rule | Weaken this | Expect red |
|---|---|---|
| Quantity floor | `ThrowIfLessThan(quantity, 1)` in `Basket.AddItem` | `AddItem_Rejects_AQuantityBelowOne` |
| Price stability | Overwrite `existing.UnitPrice` in the merge branch | `AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| Line dedup | Drop the `existing is not null` branch | `AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket` |
| Empty-order rejection | Drop the `lines.Count == 0` check in `Order.PlaceFrom` | `PlaceFrom_Rejects_AnEmptyLineSet` |
| Invalid-line rejection | Drop the per-line `ArgumentOutOfRangeException` checks | `PlaceFrom_Rejects_ALineWithANonPositiveQuantity` |
| Computed total | Hardcode `Total` instead of summing lines | `PlaceFrom_SumsEveryLine` |

Always restore with `git checkout -- <file>` and re-run the suite green before committing. No
weakened guard may ever survive into a commit.

## Scope

This note binds basket pricing and order creation, the two areas 009 audited. Principle III binds
everything else already; extending this note's commit-shape rule platform-wide would be a
constitution amendment, which the Governance section reserves for platform maintainers.
