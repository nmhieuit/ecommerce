# Quickstart: Validate the Minimal Shopping SPA

Validates this feature against [spec.md](./spec.md)'s acceptance scenarios and success criteria. See
[data-model.md](./data-model.md) for the shapes referenced below and
[contracts/](./contracts/) for the route contracts.

## Prerequisites

- .NET 10 SDK.
- Node.js 22 LTS and pnpm 9+ (the frontend workspace — see [research.md](./research.md) Decision 1).
- Docker, for the per-service SQL Server dependency containers in `docker-compose.deps.yml`.
- A `.env` at the repository root with `MSSQL_SA_PASSWORD` set (copy `.env.example`).

## Setup

### 1. Fix the swapped downstream base URLs *(one-time, do this first)*

`services/bff/src/Bff.Api/appsettings.Development.json` currently points `BasketsApi` at
`http://localhost:5041` and `OrdersApi` at `http://localhost:5188`. Those are the wrong way round —
baskets listens on **5188** and orders on **5041** (`Properties/launchSettings.json` for each). The
mistake is invisible today because both existing routes are GET-by-id and both answer 404 for an
unknown identifier, but this feature's write paths would post basket items to the orders service.
Swap them before anything below will work.

### 2. Start the databases

```bash
docker compose -f docker-compose.deps.yml up --wait products-db-init baskets-db-init orders-db-init
```

### 3. Apply migrations

`docker-compose.deps.yml` creates empty databases only; tables and the seeded catalog are EF Core's
job. The tooling is pinned in `.config/dotnet-tools.json`.

```bash
dotnet tool restore
dotnet dotnet-ef database update --project services/products/src/Products.Api
dotnet dotnet-ef database update --project services/baskets/src/Baskets.Api
dotnet dotnet-ef database update --project services/orders/src/Orders.Api
```

The products migration seeds three products (data-model.md — Product). Confirm:

```bash
dotnet dotnet-ef migrations list --project services/products/src/Products.Api
```

### 4. Run the backend

Separate terminals, or the solution's multi-startup profile:

```bash
dotnet run --project services/products/src/Products.Api   # :5088
dotnet run --project services/baskets/src/Baskets.Api     # :5188
dotnet run --project services/orders/src/Orders.Api        # :5041
dotnet run --project services/bff/src/Bff.Api              # :5301
dotnet run --project services/gateway/src/Gateway.Api      # :5300
```

### 5. Generate the API client and run the storefront

```bash
cd frontend
pnpm install
pnpm generate        # Orval, against the BFF's /openapi/v1.json (ADR-0004)
pnpm dev             # Vite dev server, :5173, configured against the gateway on :5300
```

---

## Scenario 1 — Browse (spec US1, FR-001, FR-002)

1. Open `http://localhost:5173`.
2. **Expect**: three products listed, each with its name and a price shown as US dollars with two
   decimal places (FR-024) — Field Notes Notebook $12.50, Ceramic Pour-Over Set $48.00, Linen Apron
   $34.25.
3. Open the browser's network tab and confirm every request goes to `localhost:5300` (the gateway).
   **Zero** requests to 5301, 5088, 5188, or 5041 (SC-010).
4. Open the console. **Expect**: no errors (SC-002).

**Empty-catalog check (FR-002)**: delete the seeded rows from the products database, reload, and
expect an explicit "no products available" state — not a blank page, not a spinner that never
resolves, not an error. Re-apply the migration afterwards.

## Scenario 2 — Add to basket (spec US2, FR-003 – FR-006, FR-021)

1. Add "Field Notes Notebook" to the basket, then view the basket.
2. **Expect**: one line, quantity 1, unit price $12.50, basket total $12.50.
3. Add the same product again.
4. **Expect**: still one line, now quantity 2, total $25.00 — **not** two lines (FR-005, SC-003).
5. Add "Linen Apron".
6. **Expect**: two lines, total $59.25.

## Scenario 3 — Basket survives refresh and browser restart (spec FR-011, SC-007)

1. With items in the basket, refresh the page. **Expect**: identical lines and quantities.
2. Close the browser entirely, reopen it, and return to `http://localhost:5173`.
   **Expect**: identical lines and quantities again.
3. Check `localStorage` and `sessionStorage`. **Expect**: nothing basket-related stored — the basket
   came back because it is the server's basket for this caller (data-model.md — Client-side state).

## Scenario 4 — Empty basket blocks checkout (spec FR-008, SC-004)

1. Start from an empty basket (check out once, or clear the database).
2. Attempt checkout.
3. **Expect**: the control is disabled or the attempt is refused in the interface, **and the network
   tab shows no checkout request was sent at all**. The second half is the part that matters — a
   request that the server rejects is a failure of this scenario, not a pass.

## Scenario 5 — Checkout and confirmation (spec US3, FR-009, FR-010, FR-022)

1. With two products in the basket (total $59.25), check out.
2. **Expect**: a confirmation screen showing the created order's identifier verbatim and a total of
   $59.25.
3. Note the identifier, then read the order back through the gateway:

   ```bash
   curl http://localhost:5300/bff/orders/{orderId}
   ```

4. **Expect**: the same identifier and the same total (SC-005).
5. Return to the basket. **Expect**: empty (FR-010).
6. Console still clean (SC-002, FR-013).

## Scenario 6 — Double checkout creates one order (spec FR-016, SC-008)

1. Fill the basket, then trigger checkout twice in rapid succession (double-click the control).
2. **Expect**: exactly one confirmation, and exactly one row in the orders database.
3. **Expect**: the control was disabled while the first checkout was in flight, and the second
   attempt found an empty basket and was refused.

## Scenario 7 — Backend unavailable (spec FR-012, SC-006)

1. Stop the products service.
2. Reload the storefront.
3. **Expect**: a clear, readable error within 5 seconds, the page still usable, a retry available —
   no hang, no blank screen, no unhandled console error.
4. Restart products, retry, and expect the product list to return.

Repeat with the baskets service stopped while adding an item: expect a clear error and a basket that
does **not** show an item that was never actually added (US2 scenario 5).

## Scenario 8 — Keyboard only (spec FR-017, SC-009)

Complete the entire flow — browse, add, view basket, check out, read the confirmation — using only
`Tab`, `Shift+Tab`, `Enter`, and `Space`. **Expect**: a visible focus indicator at every step and
every control reachable and operable. Nothing may require a pointer.

## Scenario 9 — No tenant, no data (constitution Principle V)

Bypass the gateway and call the BFF directly:

```bash
curl -i http://localhost:5301/bff/basket
```

**Expect**: a failure, not a basket. No tenant and no subject were resolved, so persistence is
refused rather than defaulted. This is the same guarantee spec 003 established, extended to the
caller identity this feature adds ([contracts/subject-id-header.md](./contracts/subject-id-header.md)).

---

## Automated checks

```bash
# Backend
dotnet test

# Frontend — unit/component (Vitest + Testing Library), lint, types
cd frontend && pnpm test && pnpm lint && pnpm typecheck

# Frontend — bundle budget gate (FR-025, SC-011); must fail the build when exceeded
cd frontend && pnpm build && pnpm size

# End-to-end walkthrough (SC-002, SC-005, SC-008, SC-009, SC-010)
cd frontend && pnpm e2e
```

**Codegen drift check** (ADR-0004): `pnpm generate` followed by a clean `git status` — a dirty tree
means the checked-in client no longer matches the BFF's document, and CI must fail on it.
