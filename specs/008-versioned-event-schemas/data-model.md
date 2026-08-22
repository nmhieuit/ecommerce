# Data Model: Versioned Event Schemas — OrderPlaced, BasketCheckedOut

## Versioning convention

Each event has an explicit, unambiguous version identifier in its type name:
`{EventName}V{N}` (e.g. `OrderPlacedV1`), per [ADR-0005](../../docs/adr/0005-event-contract-format.md).
Its JSON Schema file follows the matching name: `{EventName}.v{N}.schema.json`.

- **N starts at 1** for an event's first published shape.
- **A breaking change** (new required field, removed field, changed field type/semantics, renamed
  field) MUST ship as `V{N+1}` — a new type, a new schema file, the old ones left untouched
  (FR-003).
- **A schema file, once committed, is immutable** (research.md Decision 3) — even a non-breaking
  edit requires a new version, since this feature does not attempt to classify breaking vs.
  non-breaking changes.
- **Deprecation window**: once `V{N+1}` exists, `V{N}` remains defined, compiled, and covered by its
  own tests for a documented window before removal. This feature ships only `V1` of each event, so
  no removal has happened yet; the policy — recorded in `shared/EventContracts/README.md` — is that
  a superseded version may only be deleted after (a) a new version has shipped, and (b) no known
  consumer still depends on the old version, confirmed via the consumer-driven contract testing
  effort tracked separately ([ADR-0006](../../docs/adr/0006-contract-testing-tool.md), SCRUM-21).
  This feature does not invent a fixed day-count, since no consumer exists yet to measure against.

## Entities

### OrderPlacedV1

Represents an order having been placed, derived from the `Order` entity and `OrderResponse` shape
already defined in `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`.

| Field | Type | Required | Notes |
|---|---|---|---|
| `eventId` | UUID | Yes | Unique identifier of this event instance, distinct from `orderId` — lets a consumer deduplicate a redelivered message (constitution Principle IV: idempotent consumers). |
| `occurredAtUtc` | date-time (UTC) | Yes | When the order was placed; maps to `Order.PlacedAtUtc`. |
| `orderId` | UUID | Yes | Maps to `Order.Id`. |
| `tenantId` | string | Yes | The tenant the order belongs to. Required at the schema level (constitution Principle V), even though today's `OrderResponse.TenantId` is nullable in the read model — the event contract is intentionally stricter, since it is authored ahead of the publisher that will satisfy it. |
| `correlationId` | string | Yes | The correlation id generated at the edge and threaded through the request that created the order (constitution Principle VII). |
| `total` | decimal | Yes | Maps to `Order.Total` — computed by Orders, never by a caller. |
| `lines` | array of `OrderLineV1` | Yes | At least one line (an order cannot exist with zero lines, per existing `OrderEndpoints` validation). |

**OrderLineV1** (nested object, not independently versioned — it changes only when `OrderPlacedV1`
does):

| Field | Type | Required | Notes |
|---|---|---|---|
| `productId` | UUID | Yes | Maps to `OrderLine`'s product reference. |
| `quantity` | integer (≥ 1) | Yes | |
| `unitPrice` | decimal (≥ 0) | Yes | |

### BasketCheckedOutV1

Represents a basket having been checked out (cleared as part of a successful order placement),
derived from the `Basket` entity and `BasketResponse` shape in
`services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs`.

| Field | Type | Required | Notes |
|---|---|---|---|
| `eventId` | UUID | Yes | Unique identifier of this event instance (idempotency, as above). |
| `occurredAtUtc` | date-time (UTC) | Yes | When the basket was cleared as part of checkout. |
| `basketId` | UUID | Yes | Maps to `Basket.Id`. |
| `customerRef` | string | Yes | Maps to `Basket.CustomerRef` — the caller's resolved subject id. |
| `tenantId` | string | Yes | Required at the schema level (constitution Principle V) — not currently surfaced by `BasketResponse`, same rationale as `OrderPlacedV1.tenantId`. |
| `correlationId` | string | Yes | As above (constitution Principle VII). |
| `items` | array of `BasketLineItemV1` | Yes | The lines that were checked out. May be empty only in the sense that checkout of an already-empty basket is rejected upstream (`BasketEndpoints` returns `409` for that case) — in practice always ≥ 1 item. |
| `total` | decimal | Yes | Maps to `Basket.Total`. |

**BasketLineItemV1** (nested object, not independently versioned):

| Field | Type | Required | Notes |
|---|---|---|---|
| `productId` | UUID | Yes | |
| `quantity` | integer (≥ 1) | Yes | |
| `unitPrice` | decimal (≥ 0) | Yes | |
| `lineTotal` | decimal (≥ 0) | Yes | Maps to `BasketLineItemResponse.LineTotal` — money arithmetic stays in the owning service, never recomputed by a consumer. |

## Validation rules (encoded in the JSON Schema, not just this table)

- All fields listed as Required are `required` in the JSON Schema and use `additionalProperties:
  false` **at the top level only** — nested/unknown top-level properties are rejected by the
  schema itself (this is what a producer validates against, per FR-009), while *consumers*
  deserializing with `System.Text.Json` still ignore unknown properties by default regardless of
  the schema (FR-007) — the schema's strictness governs what a producer is allowed to publish, not
  what a lenient consumer must reject.
- `quantity` and `unitPrice`/`lineTotal`/`total` carry the `minimum` constraints noted above,
  mirroring the validation already enforced in `OrderEndpoints`/`BasketEndpoints` (e.g. quantity
  ≥ 1, non-negative prices).

## State transitions

Not applicable — these are point-in-time integration events (something that already happened), not
entities with a lifecycle of their own.
