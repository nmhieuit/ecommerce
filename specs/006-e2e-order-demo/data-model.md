# Phase 1 Data Model: End-to-End Order Demo — Phase 1 Exit Proof

**Feature**: 006-e2e-order-demo · **Date**: 2026-08-19

One persisted field changes in this feature. Everything else described here is run output — files the
demo produces — which is modelled because the walkthrough depends on their shape and location.

---

## Persisted: `Order` (orders service)

`services/orders/src/Orders.Api/Data/Order.cs`

| Field | Type | Nullability | Source | Notes |
|---|---|---|---|---|
| `Id` | `Guid` | required | generated | Unchanged. The reference shown on the confirmation |
| `PlacedAtUtc` | `DateTime` | required | `DateTime.UtcNow` | Unchanged. UTC instant, never local time |
| `Total` | `decimal(18,2)` | required | computed from lines | Unchanged. Computed in `PlaceFrom`, never accepted from the caller |
| **`TenantId`** | **`string`** | **nullable in schema, always written by code** | **`TenantContext.RequireTenantId()`** | **NEW.** Max length 128. Never read from the request body (research Decision 2) |

### Validation rules

- `Order.PlaceFrom` gains a `tenantId` parameter and rejects null, empty, or whitespace with
  `ArgumentException` — a blank tenant is not a tenant (mirrors `TenantContext.RequireTenantId`).
- Existing rules are untouched: at least one line, quantity ≥ 1, unit price ≥ 0.
- The column is nullable at the schema level on purpose (research Decision 3): the expand half of
  expand/contract. No code path writes null.

### Migration

`AddOrderTenantId` — additive, nullable, no default, no backfill of existing rows.

Existing local rows keep `NULL`. That is correct rather than sloppy: those orders were placed before
anything recorded who they belonged to, and inventing `contoso` for them would fabricate the exact
attribution this feature exists to make trustworthy. The demo places new orders, so nothing it shows
depends on old rows. A cold start (research Decision 11) has no old rows at all.

### State transitions

None. An order is created and never changes — unchanged by this feature.

---

## In transit: the orders service read response

`GET /orders/{orderId}` → `OrderResponse`

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | Unchanged |
| `placedAtUtc` | date-time | Unchanged |
| `total` | number | Unchanged |
| **`tenantId`** | **string** | **NEW.** Additive; consumers tolerating unknown fields are unaffected (Principle II) |

The BFF's `/bff/orders/{orderId}` response is **not** changed (research Decision 4). The BFF's
internal `OrderResource` record gains the field only if deserialization requires it — it does not,
since unknown JSON members are ignored by default — so the BFF is expected to need no change at all.

---

## Produced: demo run output

Written by one demo run. Paths are repository-relative.

| Artifact | Path | Committed | Shape |
|---|---|---|---|
| Per-step stills | `docs/demo/01-catalog.png` … `04-confirmation.png` | **Yes** | PNG, viewport-sized, one per key step |
| Written walkthrough | `docs/demo-phase-1.md` | **Yes** | The procedure, the hops, the exit-criteria mapping, where the video lives |
| Video recording | `artifacts/demo/video/*.webm` | No — git-ignored | Full flow, produced by Playwright |
| Verification output | `artifacts/demo/verification.txt` | No | The two direct order queries and their responses |
| Hop evidence | `artifacts/demo/hops.txt` | No | Collector span lines, one section per component |

`artifacts/` is added to `.gitignore`. `test-results/` and `playwright-report/` are already ignored,
which is why neither can hold the committed stills (research Decision 8).

### Verification output shape

The demo prints, and writes, one labelled block that someone who did not build the system can read:

```text
ORDER PLACED
  reference : 7c9e6679-7425-40de-944b-e07fc1f90ae7
  total     : $59.25
  tenant    : contoso

TENANT ATTRIBUTION
  resolved tenant for the placing request : contoso
  tenant stored on the order              : contoso
  match                                   : YES

WITHOUT A TENANT
  GET /orders/7c9e...  (no X-Tenant-Id)  ->  500, no order returned
  the orders service refuses to answer when no tenant was resolved

HOPS THAT SERVED THIS RUN
  Gateway.Api   spans: 14
  Bff.Api       spans: 12
  Products.Api  spans:  3
  Baskets.Api   spans:  6
  Orders.Api    spans:  2
```

Span counts are reported, not asserted against fixed numbers — the assertion is that each component
is present with at least one span (FR-011a). Fixed counts would break on any harmless change to how
many calls the storefront makes.

---

## Entities named in the spec that have no data of their own

- **Tenant context** — request-scoped, already modelled by `Tenancy.TenantContext`. Not persisted by
  this feature; its value is what gets copied onto the order.
- **Demo procedure** — documentation plus the demo script. No stored representation.
- **Reference artifact** — the walkthrough, the stills, and the externally held video, listed above.
