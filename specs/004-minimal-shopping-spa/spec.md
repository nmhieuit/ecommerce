# Feature Specification: Minimal Shopping SPA — Browse, Basket, Checkout, Confirmation

**Feature Branch**: `004-minimal-shopping-spa`

**Created**: 2026-08-16

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-14 — [WALK-1] Minimal React SPA: browse, basket, checkout, confirmation. As a shopper, I want to browse a product, add it to my basket, and check out so that I receive an order confirmation, exercising the full thin slice from the UI. Acceptance Criteria: (1) Given I open the SPA, when the page loads, then I see at least one product listed; (2) Given I add the product to my basket, when I view the basket, then it reflects the item and quantity correctly; (3) Given I click checkout, when the order is placed, then I see a confirmation screen referencing the created order; (4) Given the SPA is React + TypeScript + Vite, when I inspect the code, then TypeScript strict mode is on with no unjustified `any`. Test Scenarios: (1) Manual walkthrough: browse → add to basket → checkout → confirmation, no errors in console; (2) Refresh mid-basket — confirm basket state behavior is defined (even if 'resets' for Phase 1, it should be an intentional choice, not undefined behavior); (3) Attempt checkout with an empty basket — confirm the UI blocks it rather than sending a broken request."

## Clarifications

### Session 2026-08-16

- Q: Does this feature build the missing backend capabilities the shopping flow needs — add-to-basket with quantity, basket line items, place-order, and catalog seed data — or is that separate work this feature waits on? (FR-019) → A: This feature includes the minimum backend surface (Option A): catalog seed data, basket line items with quantity, add-to-basket, place-order from basket, and the BFF routes fronting them.
- Q: When a shopper returns to the storefront — after a refresh, or after closing and reopening the browser — how does the backend decide which basket is theirs? (FR-006, FR-011) → A: One standing basket per shopper (Option A), resolved from the shopper's identity; it survives refresh and browser restart, and is emptied by checkout. No basket identifier is kept in the browser.
- Q: What should the confirmation screen show as the order's reference — the order's raw generated identifier, or a short human-friendly order number the shopper can easily quote? (FR-009) → A: The generated identifier, shown as-is (Option A). No order-numbering scheme in this feature.
- Q: What currency should prices and totals be shown in, and does each price need to carry a currency of its own? (FR-001, FR-004, FR-009) → A: One implicit currency, USD (Option A), shown with its symbol and two decimal places. No currency is stored alongside prices or totals.
- Q: Should this feature enforce the constitution's client-side performance budgets — Core Web Vitals targets and a JavaScript bundle size limit per route — from the start, or declare them now and enforce later? (Constitution Principle VIII) → A: Option A — declare the responsiveness targets in the spec and enforce a per-entry-screen download-size budget in the build now; measuring the responsiveness targets is deferred to Phase 4 as a bounded deviation recorded in `plan.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Shopper browses the product catalog (Priority: P1)

As a shopper, I open the storefront and immediately see the products available to buy, each with enough information — its name and its price — to decide whether I want it.

**Why this priority**: This is the entry point of the entire thin slice. Nothing downstream (basket, checkout, confirmation) has anything to operate on until a shopper can see a product. On its own it already demonstrates the full path from the browser through the edge to a service and back, which is the point of the walking skeleton.

**Independent Test**: Can be fully tested by opening the storefront with the backend running and confirming at least one product appears with its name and price, sourced from the backend rather than hardcoded in the page.

**Acceptance Scenarios**:

1. **Given** the storefront and its backend are running and the catalog holds at least one product, **When** a shopper opens the storefront, **Then** at least one product is listed with its name and its price.
2. **Given** the catalog holds no products at all, **When** a shopper opens the storefront, **Then** a clear "no products available" state is shown rather than an empty page, a spinner that never resolves, or an error.
3. **Given** the backend is unavailable or slow to answer, **When** a shopper opens the storefront, **Then** a clear, human-readable error is shown within a bounded time, and the page remains usable (the shopper can retry) rather than hanging or going blank.

---

### User Story 2 - Shopper adds a product to the basket and sees it reflected correctly (Priority: P2)

As a shopper, I add a product I want to my basket and then view the basket, which shows me exactly what I added and how many of it.

**Why this priority**: This is the first point where the shopper changes state rather than only reading it, and it is what makes checkout meaningful. It depends on a product being visible (User Story 1) but delivers standalone value: a shopper can assemble an intended purchase even before checkout exists.

**Independent Test**: Can be fully tested by adding a listed product to the basket, opening the basket view, and confirming the product appears exactly once with quantity 1 — then adding the same product again and confirming the quantity becomes 2 rather than the product appearing twice.

**Acceptance Scenarios**:

1. **Given** a product is listed on the storefront, **When** the shopper adds it to the basket, **Then** the basket reflects that product with quantity 1.
2. **Given** a product is already in the basket with quantity 1, **When** the shopper adds the same product again, **Then** the basket shows that product once with quantity 2 — not two separate entries.
3. **Given** the basket holds one or more items, **When** the shopper views the basket, **Then** each item's name, quantity, and price are shown, along with a basket total.
4. **Given** the basket holds one or more items, **When** the shopper refreshes the page — or closes the browser and returns later — **Then** the basket still shows the same products with the same quantities (FR-011).
5. **Given** the add-to-basket request fails, **When** the shopper attempts to add a product, **Then** a clear error is shown and the basket is not left displaying an item that was never actually added.

---

### User Story 3 - Shopper checks out and receives an order confirmation (Priority: P3)

As a shopper, I check out the basket I have assembled and receive a confirmation that names the order that was created, so I know my purchase was recorded and I have something to refer to.

**Why this priority**: This closes the thin slice — it is the "order" in browse → basket → checkout → order — and it is the story the Phase 1 demo ultimately depends on. It is last because it is only reachable once a shopper can browse (US1) and assemble a basket (US2).

**Independent Test**: Can be fully tested by assembling a basket with at least one item, checking out, and confirming a confirmation screen appears carrying an order reference that matches the order actually created in the backend.

**Acceptance Scenarios**:

1. **Given** the basket holds at least one item, **When** the shopper checks out, **Then** a confirmation screen is shown that references the created order by an identifier the shopper can read and quote.
2. **Given** the basket is empty, **When** the shopper attempts to check out, **Then** the storefront blocks the attempt in the interface and sends no checkout request to the backend at all.
3. **Given** a successful checkout has just completed, **When** the shopper views the basket, **Then** it is empty — the checked-out items are not still sitting in it.
4. **Given** the checkout request fails or times out, **When** the shopper checks out, **Then** a clear error is shown, no confirmation screen is displayed, and the shopper's basket is left intact so they can retry.

---

### Edge Cases

- **Refresh mid-basket, or return after closing the browser**: the shopper finds the same basket, because it is the backend's basket for that shopper rather than anything the browser holds (FR-006, FR-011). Identical on every trial.
- **Empty catalog**: the storefront shows an explicit empty state (US1 scenario 2). Add-to-basket and checkout are simply unreachable, not broken.
- **Empty basket at checkout**: blocked client-side, with no request issued (US3 scenario 2). The backend must not be relied on to reject it.
- **Backend unavailable, or answering slowly**: every screen shows a bounded, readable error and stays usable. The storefront never waits indefinitely and never renders a blank screen or an unhandled error.
- **Double-submit of checkout**: rapidly triggering checkout twice must not create two orders. The action is disabled (or otherwise guarded) while a checkout is in flight.
- **Browser back after confirmation**: returning to the basket after a completed checkout shows the emptied basket, not a stale copy of the checked-out items, and must not silently re-place the order.
- **Add-to-basket while a previous add is still in flight**: the resulting quantity must equal the number of times the shopper added the product — no lost or doubled increments.
- **Direct navigation to the confirmation screen without having checked out**: shows a clear "nothing to show" state rather than a broken screen or a fabricated order reference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The storefront MUST display a list of the products available to buy, each showing at minimum its name and its price, retrieved from the backend rather than embedded in the client.
- **FR-002**: The storefront MUST show an explicit empty state when the catalog contains no products.
- **FR-003**: Shoppers MUST be able to add a listed product to their basket from the product list.
- **FR-004**: The basket MUST show each added product with its quantity, its price, and a basket total.
- **FR-005**: Adding a product already present in the basket MUST increase that product's quantity rather than create a second entry for the same product.
- **FR-006**: The basket's contents MUST be held by the backend, not only in the browser, so the basket a shopper sees is the same basket that is checked out. Each shopper MUST have at most one open basket at a time, resolved from the shopper's identity rather than from an identifier the browser supplies or remembers.
- **FR-007**: Shoppers MUST be able to initiate checkout from the basket.
- **FR-008**: The storefront MUST prevent checkout while the basket is empty, and MUST NOT issue a checkout request in that state.
- **FR-009**: On a successful checkout, the storefront MUST display a confirmation screen showing the created order's generated identifier as-is — a reference the shopper can read off the screen and quote — together with the order's total.
- **FR-010**: After a successful checkout, the shopper's basket MUST be empty.
- **FR-011**: Basket contents MUST survive a page refresh, and MUST also survive closing and reopening the browser — the shopper returns to the same basket they left, until checkout empties it (FR-010). This is the deliberate Phase 1 rule required by the source ticket's second test scenario; the behaviour MUST be identical on every trial.
- **FR-012**: When a backend request fails, times out, or returns an error, the storefront MUST show a clear, human-readable message within a bounded time, MUST leave the shopper able to retry, and MUST NOT hang, blank the screen, or surface an unhandled error to the browser console.
- **FR-013**: A complete browse → add to basket → checkout → confirmation walkthrough MUST produce zero errors in the browser console.
- **FR-014**: The storefront MUST send every backend request to the single backend surface (the gateway/BFF) and MUST NOT call any individual service directly.
- **FR-015**: Every request the storefront makes MUST carry the tenant context resolved at the edge, and no screen may require the shopper to supply, choose, or see a tenant or user identity — Phase 1's identity is the existing stub.
- **FR-016**: Triggering checkout more than once for the same basket MUST NOT create more than one order.
- **FR-017**: All interactive elements — product list entries, add-to-basket, checkout — MUST be operable by keyboard, expose an accessible name, and show a visible focus indicator.
- **FR-018**: The catalog MUST contain at least one purchasable product in every environment where this flow is demonstrated, so that the walkthrough is reachable without manual data setup.
- **FR-019**: This feature MUST deliver the backend capabilities the flow requires and that do not exist today (FR-020 – FR-023, plus the catalog data of FR-018). They are in scope here, not deferred to separate work.
- **FR-020**: A basket MUST hold line items, each identifying one product and the quantity of it wanted, and MUST be readable back with those line items, their quantities, and the basket total.
- **FR-021**: The backend MUST accept a request to add a product to a basket with a quantity, applying FR-005's merge rule so that a product occupies at most one line item per basket.
- **FR-022**: The backend MUST accept a request to place an order from a basket, creating an order that records its total and that is retrievable afterwards by the reference returned to the caller.
- **FR-023**: The client-facing backend surface MUST front the capabilities in FR-020 – FR-022 so the storefront reaches them through the gateway/BFF only, consistent with FR-014.
- **FR-024**: Every monetary amount the shopper sees — product prices, line item prices, the basket total, the order total (FR-001, FR-004, FR-009) — MUST be displayed in US dollars, with the currency symbol and two decimal places. No currency is stored alongside any price or total; Phase 1 has exactly one currency.
- **FR-025**: A download-size budget MUST be declared for each of the storefront's entry screens and MUST be checked automatically on every build, failing the build when an entry screen exceeds its budget.

### Key Entities

- **Product (as shown to the shopper)**: something purchasable, identified uniquely, carrying a display name and a price. The price is a bare amount in the single Phase 1 currency (FR-024) — it carries no currency of its own. Sourced from the catalog; the storefront neither invents nor edits products.
- **Basket**: the shopper's in-progress selection, held by the backend and belonging to the Phase 1 stub shopper. A shopper has at most one open basket, found from their identity rather than from anything the browser holds. Holds zero or more basket line items and a total; checkout empties it.
- **Basket Line Item**: one product within a basket, together with the quantity of it the shopper wants and the price it contributes. A product appears in at most one line item per basket.
- **Order Confirmation**: what the shopper is shown once an order is created — the order's generated identifier shown verbatim, when it was placed, and its total. Read-only; the shopper cannot change it. There is no separate, friendlier order number; introducing one belongs to the story that owns orders.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A shopper who has never seen the storefront before completes the full browse → add to basket → checkout → confirmation walkthrough in under 2 minutes, without instructions or assistance.
- **SC-002**: The full walkthrough completes with zero errors reported in the browser console, in 100% of runs.
- **SC-003**: After any sequence of add-to-basket actions, the basket shows each product exactly once, with a quantity equal to the number of times it was added — correct in 100% of trials.
- **SC-004**: Attempting checkout with an empty basket is blocked in 100% of attempts, with zero checkout requests reaching the backend during those attempts.
- **SC-005**: The order reference shown on the confirmation screen matches the order actually created in the backend in 100% of successful checkouts.
- **SC-006**: When the backend is unavailable, the shopper sees a clear error within 5 seconds in 100% of attempts — never an indefinite wait or a blank screen.
- **SC-007**: Refreshing the page mid-basket, and closing and reopening the browser mid-basket, each return the shopper to a basket holding identical products and quantities, in 100% of trials.
- **SC-008**: Checking out twice in rapid succession creates exactly one order in 100% of trials.
- **SC-009**: The entire flow can be completed using only the keyboard, with the focused element visible at every step.
- **SC-010**: Zero requests observed leaving the storefront address anything other than the single backend surface, across a full walkthrough.
- **SC-011**: Every build checks each entry screen against its declared download-size budget, and a change that pushes a screen over its budget fails the build in 100% of cases.
- **SC-012**: The storefront is held to these responsiveness targets, at the 75th percentile on a mid-range mobile device: the first screen becomes usable within 2.5 seconds, the shopper's first interaction is answered within 200 milliseconds, and visible layout shifts stay under 0.1. These targets are declared here; measuring against them requires a production-like environment and is Phase 4 work, so this feature does not gate on them.

## Assumptions

- **The backend cannot support this flow as it stands, so this feature closes that gap itself (FR-019 – FR-023).** Verified in the repository on 2026-08-16: the BFF exposes three read-only routes only — list products, get one basket by identifier, get one order by identifier. There is no add-to-basket, no checkout, and no place-order capability anywhere; the basket record holds an identifier and a customer identifier with **no line items and no quantities**; the order record holds an identifier, a placed-at time, and a total with **no line items**; and the products catalog has **no seed data**, so the product list currently returns nothing. Acceptance criteria 1, 2, and 3 of the source ticket are therefore all unreachable today. Spec 002 flagged this same gap and left it as an open scope decision; it is resolved here (see Clarifications, 2026-08-16) by bringing the minimum backend surface into this feature, which makes this feature an SPA *plus* a first domain slice across products, baskets, and orders.
- Order line items are **not** part of that minimum surface. No acceptance criterion needs them: the confirmation screen shows the order's reference and total (FR-009), and the basket is what carries per-product detail. Orders gain line items with the story that owns them.
- The gateway → BFF routing (spec 002) and the stub identity with a resolved tenant context (spec 003) are already in place and are consumed as-is; this feature neither re-does nor re-negotiates them.
- Phase 1 has exactly one shopper (the existing stub identity) and one tenant. There is no sign-in, sign-up, account, or tenant selection screen, and none is in scope.
- One shopper-facing storefront application is built. The constitution's second client (mobile-web) and the shared design-system package it implies are deferred until a second application exists to share with — this feature does not create a second copy of anything.
- Checkout is the act of turning the basket into an order. Payment, shipping address, delivery options, taxes, discounts, and stock reservation are all out of scope; the walking skeleton proves the path, not the commerce rules.
- Multiple currencies, per-tenant price lists, and locale-specific number formatting are out of scope. FR-024 fixes one currency for Phase 1; a second tenant with its own currency is what would force that decision open, and no such tenant exists yet.
- Removing items from the basket, editing quantities directly, and clearing the basket are out of scope. The shopper adds and checks out; nothing more. (Checkout emptying the basket, FR-010, is not an exception — it is automatic.)
- Order history, an orders list, and returning to a past confirmation later are out of scope. The confirmation is shown once, immediately after checkout.
- The technology this is built with is already fixed by the constitution and is not a decision this feature makes: a React + TypeScript SPA on Vite, TypeScript in strict mode with `any` requiring written justification, TanStack Query for server state, talking only to the BFF. The source ticket's fourth acceptance criterion (strict mode on, no unjustified `any`) is a code-quality gate verified at review and in the build, and is tracked in the plan rather than as a shopper-facing requirement here.
- "Bounded time" for the error requirements (FR-012, SC-006) means the storefront surfaces the failure to the shopper within 5 seconds, consistent with spec 002's SC-003 for the gateway/BFF error path.
- FR-011 chooses basket survival over the alternative the ticket also permitted ("resets, intentionally"). Survival is chosen because the basket already lives server-side (FR-006), which makes retaining the shopper's basket the cheaper and less surprising of the two; a basket that resets while its data sits intact in the backend would be a defect a reviewer would file. The rejected alternative is recorded here so the choice is visible rather than assumed.
- Because the basket is resolved from the shopper's identity (FR-006) and Phase 1 has exactly one shopper, the basket needs nothing stored in the browser and there are no orphaned baskets to clean up. The consequence is that a shopper who leaves items and returns days later still finds them — intended for Phase 1, not an oversight. Basket expiry is out of scope.
- SC-012's responsiveness targets are declared but not verified in this feature. That is a deliberate, bounded deviation from the constitution's Principle VIII, which requires client applications to meet those targets — Phase 1 has no production-like environment or real traffic to measure at a 75th percentile against. The deviation, its justification, and its time bound belong in `plan.md` per the constitution's governance rules; the enforceable half of the principle (the download-size budget, FR-025 / SC-011) is not deferred.
- Success criteria expressed as user-observable behaviour (console errors, network destinations, keyboard operability) are verified by the manual walkthrough and automated frontend tests; the specific tooling is a planning decision, not a specification one.
