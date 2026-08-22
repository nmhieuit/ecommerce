# Feature Specification: Retrofit TDD for Basket Pricing and Order Creation

**Feature Branch**: `009-retrofit-tdd-basket-order`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-19 — [CONTRACT-2] Retrofit TDD for basket pricing and order creation. As the Developer, I want basket pricing and order-creation logic rebuilt test-first so that Phase 1's untested shortcuts are replaced with Red-Green-Refactor discipline (Principle III, non-negotiable)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basket pricing edge cases are proven by failing-first tests (Priority: P1)

As the Developer, I want every basket-pricing rule (quantity floor, price-at-add-time stability, and line deduplication) backed by a unit test that was written and failing before the rule was implemented or hardened, so that Phase 1's untested shortcuts in basket pricing are closed and any future regression is caught automatically.

**Why this priority**: Basket pricing is money arithmetic a shopper's checkout total depends on directly. Untested pricing logic is the highest-risk gap Phase 1 left behind, and it blocks the constitution's non-negotiable Test-First principle from being satisfied for this service.

**Independent Test**: Can be fully tested by running the basket-pricing unit test suite and reverting any one pricing safeguard (quantity floor, price stability, or deduplication) in turn — each reversion must cause a test to fail. Delivers a verifiably-tested pricing core independent of order-creation work.

**Acceptance Scenarios**:

1. **Given** a basket with no existing line for a product, **When** a line item with quantity zero (or negative) is added, **Then** the addition is rejected and a unit test asserts the rejection.
2. **Given** a basket already containing a line for a product at its original unit price, **When** the same product is added again after its catalog price has changed, **Then** the existing line's unit price is left unchanged and a unit test asserts the original price is retained.
3. **Given** a basket already containing a line for a product, **When** the same product is added again with a different quantity, **Then** the existing line's quantity increases by the new amount rather than a second line being created, and a unit test asserts no duplicate line exists.

---

### User Story 2 - Order-creation rules are proven by failing-first tests (Priority: P1)

As the Developer, I want order-creation logic to reject every invalid attempt to place an order (including from an empty basket) and to compute the order total itself, each backed by a unit test written before the behavior it proves, so that Phase 1's untested shortcuts in order creation are closed.

**Why this priority**: An order that can be created empty, mis-totaled, or unattributed is a data-integrity defect a customer or finance stakeholder would notice immediately. This is equally foundational to Story 1 and must land in the same initiative.

**Independent Test**: Can be fully tested by running the order-creation unit test suite and attempting to place an order from an empty line set at the domain layer — the attempt must be rejected and a test must assert the rejection. Delivers a verifiably-tested order-creation core independent of basket-pricing work.

**Acceptance Scenarios**:

1. **Given** no line items, **When** an order is placed at the domain layer, **Then** the attempt is rejected and a unit test asserts the rejection.
2. **Given** one or more valid line items, **When** an order is placed, **Then** the resulting order's total equals the sum of each line's quantity times its unit price, computed by order-creation logic rather than supplied by the caller, and a unit test asserts this.
3. **Given** a line item with an invalid quantity or a negative unit price, **When** an order is placed, **Then** the attempt is rejected and a unit test asserts the rejection.

---

### User Story 3 - TDD discipline is verifiable from commit history (Priority: P2)

As a reviewer (Developer or QA), I want the commit history for this initiative to show each test change landing with or before the implementation change it proves, so that Red-Green-Refactor compliance can be confirmed without re-deriving it from the final code alone.

**Why this priority**: The other two stories produce the tests; this one makes their existence and ordering auditable, which is what the constitution's compliance-review gate (Governance) actually checks at PR time. It depends on Stories 1 and 2 having produced real commits to inspect.

**Independent Test**: Can be fully tested by reviewing the commit log for pricing/order-creation changes in this initiative and confirming, commit by commit, that a test commit precedes or accompanies its implementation commit rather than following days later.

**Acceptance Scenarios**:

1. **Given** the commit history for this initiative, **When** a reviewer inspects each commit touching basket-pricing or order-creation logic, **Then** every such commit has a preceding or accompanying failing-test commit.

---

### Edge Cases

- What happens when a basket line item is added with quantity zero or a negative quantity? (Story 1, Scenario 1)
- What happens when a product already in the basket is re-added after its catalog price changed mid-session? (Story 1, Scenario 2)
- What happens when a product already in the basket is re-added at a different quantity? (Story 1, Scenario 3)
- What happens when an order is placed from an empty set of line items? (Story 2, Scenario 1)
- What happens when an order is placed with a line item carrying an invalid quantity or a negative unit price? (Story 2, Scenario 3)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Basket-pricing logic MUST reject adding a line item with a quantity below one.
- **FR-002**: Basket-pricing logic MUST retain a line's original unit price when the same product is added again, even if the price supplied on the later addition differs.
- **FR-003**: Basket-pricing logic MUST merge a repeated addition of the same product into the existing line's quantity rather than creating a second line for that product.
- **FR-004**: Order-creation logic MUST reject placing an order with zero line items.
- **FR-005**: Order-creation logic MUST reject placing an order containing a line item with a quantity below one or a negative unit price.
- **FR-006**: Order-creation logic MUST compute the order's total from its line items rather than accept a total supplied by the caller.
- **FR-007**: Each of FR-001 through FR-006 MUST be demonstrated by a unit test that fails when the corresponding rule is removed or weakened.
- **FR-008**: Every commit that introduces or changes basket-pricing or order-creation logic during this initiative MUST be preceded or accompanied, in commit history, by a test commit that fails without the change and passes with it.

### Key Entities *(include if feature involves data)*

- **Basket**: A shopper's shopping basket; owns the pricing rules under test (quantity floor, price-at-add-time stability, line deduplication) and exposes the computed total.
- **Basket Line Item**: One product and quantity within a basket, carrying the unit price captured when it was first added.
- **Order**: The record created when a basket is placed for purchase; owns the rule that it cannot exist without at least one valid line and that its total is computed, not supplied.
- **Order Line**: One product, quantity, and unit price used to compute an order's total at creation time; not persisted beyond that computation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the basket-pricing and order-creation rules named in this specification (FR-001 through FR-006) have a passing automated test that fails when the rule is reverted.
- **SC-002**: 100% of commits touching basket-pricing or order-creation logic within this initiative show a test change landing with or before its implementation change, confirmed by a single pass over commit history.
- **SC-003**: An attempt to place an order from an empty basket is rejected in 100% of cases and is provably covered by an automated test, with zero manual verification steps required.
- **SC-004**: The automated test suite for basket pricing and order creation fails immediately if Phase 1's pre-retrofit behavior (missing quantity floor, missing price-stability rule, or an order accepted from an empty basket) is reintroduced.

## Assumptions

- "Basket pricing logic" and "order-creation logic" refer to the domain-layer rules already implemented in the baskets and orders services (quantity floor, price-at-add-time stability, line deduplication, empty-order rejection, and total computation), not new pricing or ordering behavior — this initiative retrofits test coverage and TDD-compliant history around existing rules rather than changing what they do.
- "Invalid state transitions" for order creation, per the source ticket's own test scenario, means rejecting order placement from an empty basket and from malformed line items; no additional order status/workflow model exists in the current domain to retrofit.
- Coverage required by this initiative is unit-level (domain logic in isolation), matching the ticket's acceptance criteria ("when unit tests run"); integration and contract test coverage for these services is governed separately by the constitution's Test-First principle and is not re-scoped here.
- "Retrofit" applies going forward from this initiative: new or changed commits touching this logic must follow Red-Green-Refactor, and any currently-untested rule identified above gets a test added test-first (failing, then passing) rather than backfilled after the fact.
