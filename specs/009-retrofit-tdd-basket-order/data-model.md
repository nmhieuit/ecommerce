# Phase 1 Data Model: Retrofit TDD for Basket Pricing and Order Creation

No schema or entity changes. This document is an audit trail mapping each entity's already-enforced
rule (per `research.md` Decision 1) to the requirement it satisfies and the test that proves it —
the evidence `/speckit-tasks` and a reviewer will point to, not a design for new state.

## Basket

Source: `services/baskets/src/Baskets.Api/Data/Basket.cs`

| Field | Type | Rule | Status |
|---|---|---|---|
| `Id` | `Guid` | Assigned on creation via `ForCustomer` | Implemented, untouched by this feature |
| `CustomerRef` | `string` | Required, non-blank (`ArgumentException.ThrowIfNullOrWhiteSpace`) | Implemented, untouched by this feature |
| `LineItems` | `ICollection<BasketLineItem>` | See Basket Line Item below | — |
| `Total` | `decimal` (computed) | Sum of `LineItems[].LineTotal`, never stored | Implemented; proved by `BasketTotalTests` |

**Rule → FR-001 through FR-003** (all on `Basket.AddItem`):

| Rule | FR | Test |
|---|---|---|
| Reject quantity < 1 | FR-001 | `BasketLineMergeTests.AddItem_Rejects_AQuantityBelowOne` |
| Reject negative unit price | (implementation detail, not separately spec'd) | `BasketLineMergeTests.AddItem_Rejects_ANegativeUnitPrice` |
| Merge repeat add into existing line's quantity | FR-003 | `BasketLineMergeTests.AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket`, `AddItem_AccumulatesQuantities_AcrossManyAdditions` |
| Keep price captured at first add, ignore later price on the same product | FR-002 | `BasketLineMergeTests.AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| Distinct products stay on distinct lines | (supports FR-003) | `BasketLineMergeTests.AddItem_KeepsProductsApart_WhenDifferentProductsAreAdded` |

## Basket Line Item

Source: `services/baskets/src/Baskets.Api/Data/BasketLineItem.cs`

| Field | Type | Rule | Status |
|---|---|---|---|
| `ProductId` | `Guid` | Identifier only; no cross-service lookup (Constitution Principle I) | Implemented, untouched |
| `Quantity` | `int` | Never below 1 — enforced by `Basket.AddItem`, not on the entity itself | Implemented; proved above |
| `UnitPrice` | `decimal` | Price at moment of first add, never recomputed from catalog | Implemented; proved above |
| `LineTotal` | `decimal` (computed) | `Quantity * UnitPrice` | Implemented; proved by `BasketTotalTests.Total_MultipliesQuantityByUnitPrice` |

## Order

Source: `services/orders/src/Orders.Api/Data/Order.cs`

| Field | Type | Rule | Status |
|---|---|---|---|
| `Id` | `Guid` | Assigned in `PlaceFrom` | Implemented, untouched |
| `PlacedAtUtc` | `DateTime` | Supplied by caller (`DateTime.UtcNow` at the endpoint), stored as UTC | Implemented, untouched |
| `Total` | `decimal` | Computed from lines, never accepted as input | Implemented; proved below (FR-006) |
| `TenantId` | `string?` | Required, non-blank; out of this feature's scope (see `OrderTenantTests`) | Implemented, untouched — not re-verified here |

**Rule → FR-004 through FR-006** (all on `Order.PlaceFrom`):

| Rule | FR | Test |
|---|---|---|
| Reject zero line items | FR-004 | `OrderTotalTests.PlaceFrom_Rejects_AnEmptyLineSet`; integration: `PlaceOrderTests.PlaceOrder_Rejects_ARequestWithNoLines` |
| Reject a line with quantity < 1 | FR-005 | `OrderTotalTests.PlaceFrom_Rejects_ALineWithANonPositiveQuantity`; integration: `PlaceOrderTests.PlaceOrder_Rejects_ALineWithANonPositiveQuantity` |
| Reject a line with negative unit price | FR-005 | `OrderTotalTests.PlaceFrom_Rejects_ALineWithANegativePrice` |
| Total computed from lines, not accepted (no total field exists on the request at all) | FR-006 | `OrderTotalTests.PlaceFrom_MultipliesQuantityByUnitPrice`, `PlaceFrom_SumsEveryLine`; integration: `PlaceOrderTests.PlaceOrder_CreatesTheOrder_AndComputesItsTotal` |

## Order Line

Source: `services/orders/src/Orders.Api/Data/Order.cs` (`OrderLine` record)

Used only to compute `Order.Total` at creation time; not persisted (documented design decision,
unchanged by this feature). Its per-line validation is covered under FR-005 above.

## No new entities

This feature introduces no new entity, field, migration, or state transition. "Invalid state
transitions," per this specification's Assumptions, resolves entirely to the empty-basket and
malformed-line rejections already tabulated under Order — there is no order-status field or workflow
model in the current domain for this feature to retrofit.
