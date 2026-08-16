# Phase 1 Data Model: Minimal Shopping SPA

**Feature**: [spec.md](./spec.md) · **Research**: [research.md](./research.md) · **Date**: 2026-08-16

Entities are grouped by the service that owns them. No service reads another's tables; every
cross-service value below arrives over HTTP (constitution Principle I).

---

## Products service — `services/products`

### Product *(existing, unchanged shape)*

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Required, max 200 |
| `Price` | `decimal` | Precision (18, 2). A bare amount in the single Phase 1 currency (spec FR-024) — no currency is stored |

**Change in this feature**: no schema change. A new migration adds three seeded rows via `HasData`
with fixed identifiers (research Decision 10), satisfying FR-018.

Seeded catalog (identifiers fixed so tests and the walkthrough can name them):

| Name | Price |
|------|-------|
| Field Notes Notebook | 12.50 |
| Ceramic Pour-Over Set | 48.00 |
| Linen Apron | 34.25 |

---

## Baskets service — `services/baskets`

### Basket *(existing, materially extended)*

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key |
| `CustomerRef` | `string` | Required, max 200. The caller's subject identifier, taken from the `X-Subject-Id` header (research Decision 6). **Unique** — at most one basket per caller (spec FR-006) |
| `LineItems` | `BasketLineItem[]` | Owned collection; may be empty |

**Change from today**: `CustomerId` (`Guid`) is replaced by `CustomerRef` (`string`). The Phase 1
stub subject is `phase1-stub-user`, which is not a `Guid`, and inventing a mapping to one would add
a translation layer whose only purpose is to preserve a field type nothing depends on. This alters
the existing `GET /baskets/{basketId}` and `GET /bff/baskets/{basketId}` response shapes; spec 002's
contract marks both as "to be finalized", there is no released consumer, and the SPA this feature
builds is the first one. Recorded rather than versioned for that reason.

**Uniqueness rule**: a unique index on `CustomerRef` is what makes "at most one open basket per
shopper" (FR-006) a database guarantee rather than a convention two concurrent requests could break.

### BasketLineItem *(new)*

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key |
| `BasketId` | `Guid` | Required, FK to `Basket`; cascade delete |
| `ProductId` | `Guid` | Required. An identifier only — this service holds no product record and performs no lookup against the products service |
| `Quantity` | `int` | Required, **≥ 1**. A line with quantity 0 must not exist; removing an item removes the line |
| `UnitPrice` | `decimal` | Precision (18, 2). Captured when the product was added, never recomputed (research Decision 7) |

**Uniqueness rule**: unique on (`BasketId`, `ProductId`) — a product occupies at most one line per
basket (spec FR-005, FR-021). Adding a product already present increments `Quantity` on the existing
line.

**Derived value**: the basket total is `Σ (Quantity × UnitPrice)`, computed by the baskets service
from its own rows and never stored. Storing it would create a second source of truth that can
disagree with the lines.

### Basket lifecycle

```text
(no basket)
    │  first add-to-basket for this caller
    ▼
Empty ──── add item ────► Holding items ──── add same product ────► Holding items (quantity + 1)
  ▲                             │
  └──────── checkout ───────────┘   (line items deleted; the Basket row survives)
```

- Checkout empties the basket rather than deleting it (spec FR-010). The row survives so the
  caller's basket identity is stable across purchases.
- A checkout against an empty basket is rejected (spec FR-008's backend half; research Decision 9).
- There is no expiry. A basket persists until its owner checks out (spec Assumptions).

---

## Orders service — `services/orders`

### Order *(existing, unchanged shape)*

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid` | Primary key. This is the reference shown verbatim on the confirmation screen (spec FR-009) |
| `PlacedAtUtc` | `DateTime` | UTC instant |
| `Total` | `decimal` | Precision (18, 2). **Computed by this service** from the lines it is sent, never accepted from the caller (research Decision 8) |

**Change in this feature**: no schema change. A new write endpoint accepts the basket's lines,
computes the total, and persists the order. Order line items are deliberately not persisted — no
acceptance criterion needs them, and they arrive with the story that owns orders (spec Assumptions).

**Validation**: a place-order request carrying no lines is rejected. An order with a zero total from
an empty basket is not a thing this feature should be able to create.

---

## Request-scoped context — `shared/Tenancy`

### TenantContext *(existing, unchanged)*

Holds the tenant resolved at the gateway, read from `X-Tenant-Id`. Two states only: Unresolved and
Resolved. `RequireTenantId()` throws `MissingTenantContextException` when unresolved — there is no
default tenant.

### CallerContext *(new)*

| Field | Type | Rules |
|-------|------|-------|
| `SubjectId` | `string?` | The caller's subject identifier, read from `X-Subject-Id`. `null` while unresolved |

`RequireSubjectId()` throws when unresolved, mirroring `TenantContext.RequireTenantId()`. Registered
and wired by the same `AddTenancy()` / `UseTenancy()` calls every service already makes, so no
service's `Program.cs` gains a new concept — only the library does (research Decision 6).

Like the tenant, the subject is resolved **once, at the gateway**, from the authenticated principal's
`ClaimTypes.NameIdentifier` claim, and any inbound value is overwritten rather than trusted.

---

## Client-side state — `frontend/apps/web`

No entity is duplicated into a client store. Per Principle IX, server data lives in TanStack Query's
cache and nowhere else:

| State | Where it lives | Why |
|-------|----------------|-----|
| Product list | TanStack Query cache, keyed by the products query | Server data |
| Current basket (lines, quantities, total) | TanStack Query cache, keyed by the basket query; invalidated after add-to-basket and after checkout | Server data — this is what makes FR-011 work without browser storage |
| Created order, for the confirmation screen | Router navigation state for the confirmation route | Transient result of the checkout mutation, not a cached resource |
| Checkout in flight | The mutation's own pending state | Drives the in-flight guard for FR-016 |

Nothing is written to `localStorage` or `sessionStorage`. The basket survives refresh because it is
the server's basket for the authenticated caller, not because the browser remembered anything
(spec FR-006, Clarifications 2026-08-16).

---

## Cross-service data flow

```text
add to basket        BFF ──GET /products──► products      (resolve unit price)
                     BFF ──POST /baskets/current/items──► baskets   (product, quantity, unit price)

view basket          BFF ──GET /baskets/current──► baskets          (lines, quantities, total)

checkout             BFF ──GET  /baskets/current──► baskets         (lines)
                     BFF ──POST /orders──────────► orders           (lines → total computed there)
                     BFF ──POST /baskets/current/clear──► baskets   (empty the basket)
                     BFF returns the created order to the storefront
```

Every hop carries `X-Tenant-Id` and `X-Subject-Id`. The BFF performs no arithmetic anywhere in this
flow — it forwards values that domain services produced.
