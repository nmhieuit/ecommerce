# Phase 0 Research: Retrofit TDD for Basket Pricing and Order Creation

## Decision 1: Is there behavioral code to build for FR-001–FR-006?

**Decision**: No. A codebase audit confirms every rule the spec lists is already implemented and
already unit-tested:

| FR | Rule | Implementation | Unit test |
|---|---|---|---|
| FR-001 | Reject quantity < 1 | `Basket.AddItem` (`ArgumentOutOfRangeException.ThrowIfLessThan`) | `BasketLineMergeTests.AddItem_Rejects_AQuantityBelowOne` (`[InlineData(0)]`, `[InlineData(-1)]`) |
| FR-002 | Keep price captured at first add | `Basket.AddItem` (ignores `unitPrice` on an existing line) | `BasketLineMergeTests.AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| FR-003 | Merge repeat add into existing line | `Basket.AddItem` (`existing.Quantity += quantity`) | `BasketLineMergeTests.AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket`, `AddItem_AccumulatesQuantities_AcrossManyAdditions` |
| FR-004 | Reject placing an order with zero lines | `Order.PlaceFrom` (`lines.Count == 0` → `ArgumentException`) | `OrderTotalTests.PlaceFrom_Rejects_AnEmptyLineSet` |
| FR-005 | Reject an invalid line (qty < 1 or negative price) | `Order.PlaceFrom` (`ArgumentOutOfRangeException.ThrowIfLessThan` / `ThrowIfNegative` per line) | `OrderTotalTests.PlaceFrom_Rejects_ALineWithANonPositiveQuantity`, `PlaceFrom_Rejects_ALineWithANegativePrice` |
| FR-006 | Total computed, not accepted | `Order.PlaceFrom` (`Total = lines.Sum(...)`); `PlaceOrderRequest` has no total field at all | `OrderTotalTests.PlaceFrom_MultipliesQuantityByUnitPrice`, `PlaceFrom_SumsEveryLine` |

Integration coverage exists too: `PlaceOrderTests.PlaceOrder_Rejects_ARequestWithNoLines`,
`PlaceOrder_Rejects_ALineWithANonPositiveQuantity`, `PlaceOrder_CreatesTheOrder_AndComputesItsTotal`.

**Rationale**: Re-implementing or restructuring working, already-tested domain code with no
behavioral change would be churn without value and directly contradicts the project's own
anti-over-engineering stance — a bug fix or gap-closer doesn't need surrounding cleanup, and neither
does a retrofit ticket whose code turns out not to need fixing.

**Alternatives considered**: Rewrite `Basket`/`Order` from scratch strictly test-first to produce a
"clean" RGR history for this logic. Rejected — the resulting code would be behaviorally identical to
today's, the rewrite carries real regression risk for no functional gain, and it does not actually
solve the problem (see Decision 2): the issue was never that the logic is wrong, only that its
commit history doesn't show the failing-test-first order.

## Decision 2: How to close the commit-history gap without rewriting history

**Decision**: Confirmed via `git log --follow` that the implementation and its unit tests for both
services landed inside single, large feature commits:

- `1bc77a6` "Implement basket total computation and BFF integration for shopping basket" — touches
  `Basket.cs`/`BasketLineItem.cs`-era changes and `BasketTotalTests.cs`/`BasketLineMergeTests.cs`
  together.
- `c99783c` "feat: Implement checkout workflow in BFF" — introduces `Order` total computation and
  `OrderTotalTests.cs` together.
- `b3873b5` "feat: Add tenant attribution to orders" — extends `Order.PlaceFrom` and
  `OrderTenantTests.cs`/`OrderTotalTests.cs` together.

This is Jira SCRUM-19's "Phase 1 shortcut": tests exist and are correct, but they arrived bundled
with their implementation in one commit rather than as a preceding failing-test commit the way
Principle III's Red-Green-Refactor and this ticket's AC1 describe. Rewriting already-committed
history to insert a synthetic red commit before each green one would fabricate an order of events
that did not happen, which this repository's own operating rules already treat as an action requiring
explicit, narrowly-scoped user authorization it has not been given — and even with authorization it
would misrepresent authorship rather than fix anything. The gap is closed going forward instead: a
written practice note states the rule for future commits, and a quickstart procedure gives a reviewer
a repeatable way to check compliance without needing to trust memory or a verbal norm.

**Rationale**: Matches this ticket's own AC1 wording ("when I commit it" — a forward-looking
per-commit rule) and Test Scenario 1's audit framing ("review commit history... confirm test commits
precede or accompany... not follow days later" — a check applied going forward, not a mandate to
retroactively edit history).

**Alternatives considered**: (a) `git rebase` to split each bundled commit into a synthetic
test-then-implementation pair — rejected as history fabrication, excluded by operating policy, and
brittle against a repo that may already be shared/pushed. (b) Do nothing and consider the ticket
satisfied because the tests already exist — rejected because it leaves User Story 3 (auditability)
unmet: nothing currently tells a future contributor or reviewer what the expected commit shape is,
so the same "bundle everything into one commit" pattern would likely repeat.

## Decision 3: Where the going-forward practice is recorded

**Decision**: `docs/engineering/test-first-commits.md` — a short, linked practice note, not a
constitution amendment and not an ADR.

**Rationale**: Constitution Principle III already mandates Test-First Development platform-wide; this
document operationalizes it for one code area (basket pricing, order creation) with a concrete
commit-shape rule and an audit procedure, which is narrower than a constitutional change and doesn't
require the Governance section's platform-maintainer amendment process. It is also not an
architecturally significant decision in the sense `docs/adr/` records (no technology choice, no
structural trade-off) — it is a workflow convention, so it does not belong in `docs/adr/` alongside
ADR-0001 through ADR-0011.

**Alternatives considered**: (a) Amend the constitution's "Development Workflow and Quality Gates"
section directly — rejected as disproportionate for a single Sprint-1 story and outside this ticket's
authority (Governance requires platform-maintainer approval). (b) Record it as a new ADR — rejected;
it is a process convention, not an architecture decision, and filing it as one would blur that
category for future readers of `docs/adr/`. (c) No written record, rely on review-time reminders —
rejected, directly fails User Story 3's requirement that compliance be auditable rather than assumed.

## Decision 4: How SC-002/SC-004 ("tests fail when the rule is reverted") gets demonstrated

**Decision**: `quickstart.md` documents a manual revert-and-confirm-red pass: for each of FR-001–006,
temporarily weaken the guard (e.g., comment out the quantity-floor check), run the corresponding unit
test, confirm it fails, then restore the guard and confirm it passes again. This directly executes
Jira Test Scenario 2.

**Rationale**: This is a one-time verification step appropriate to a single retrofit story with an
already-small, already-covered rule set — it doesn't justify standing infrastructure.

**Alternatives considered**: Mutation testing (e.g., Stryker.NET) to automate the revert-and-confirm
check on every build — rejected as disproportionate for six already-covered rules in a repository
with no existing mutation-testing convention; worth reconsidering platform-wide in a future story if
the pattern needs to scale beyond this one.
