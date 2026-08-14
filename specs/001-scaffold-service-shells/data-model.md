# Phase 1 Data Model: Scaffold Parties/Products/Baskets/Orders Service Shells

## Scope note

Per spec.md's Key Entities section, this feature introduces **no business/domain entities** — Party, Product, Basket, and Order data models are delivered by later feature work built on top of these shells. What this feature *does* establish is the structural data-ownership boundary each later entity will live inside.

## Structural data ownership

| Service | Database/schema | Owned exclusively by | Domain entities (future work) |
|---|---|---|---|
| Parties | `parties` (schema-or-database-per-tenant) | Parties service only | Party, and related identity/account records |
| Products | `products` (schema-or-database-per-tenant) | Products service only | Product, catalog/pricing records |
| Baskets | `baskets` (schema-or-database-per-tenant) | Baskets service only | Basket, basket line items |
| Orders | `orders` (schema-or-database-per-tenant) | Orders service only | Order, order line items, outbox table |

## Validation rules established by this feature

- **No cross-service reference.** No service's `DbContext` or connection configuration may reference another service's database/schema (spec FR-004, FR-005). This is validated structurally (Decision in quickstart.md: attempt a cross-service connection and confirm it has no credential/route to succeed), not by a runtime check.
- **Connectivity is the readiness signal.** Each service's readiness probe (`/health/ready`, see contracts/) executes a lightweight "can I reach my own database" check via `AddDbContextCheck<T>()`. A service with no reachable database MUST report not-ready, never healthy (spec FR-003, Edge Cases).

## State transitions

None — this feature has no business entities, and therefore no entity state machine. State transitions belong to the feature work that introduces Order, Basket, etc.

## Forward references

- Tenant-keyed schema/connection resolution (Principle V) is delivered by SCRUM-12, not this feature — the `DbContext` configuration here uses a single default connection per service, structured so a tenant-aware connection resolver can be substituted later without changing the service's public shape.
- The Orders service's outbox table (constitution Principle IV) is not created in this feature — it's introduced alongside the first event this platform actually publishes.
