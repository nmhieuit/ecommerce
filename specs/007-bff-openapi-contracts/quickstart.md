# Quickstart: Verify OpenAPI Contracts + Generated Clients

This feature adds test coverage and a verification record on top of an already-shipped pipeline
(see [plan.md](./plan.md) Summary). This guide walks through the spec's success criteria and
confirms each one, including the two new test cases this feature adds.

## Prerequisites

- .NET 10 SDK, Node 22+, pnpm (or run everything through `./scripts/up.ps1` — see
  [docs/local-development.md](../../docs/local-development.md))
- Dependencies installed: `pnpm install` from `frontend/`

## SC-001 — every products/baskets/orders route has an accurate OpenAPI spec

Run the BFF locally and inspect its published document:

```bash
dotnet run --project services/bff/src/Bff.Api
# in another shell, once it's listening on http://localhost:5301
curl http://localhost:5301/openapi/v1.json
```

Confirm the document lists every route defined in
`services/bff/src/Bff.Api/Features/{Products,Baskets,Orders}/*Endpoints.cs`, with response shapes
matching each route's `.Produces<T>()` declaration. This holds by construction — see
[research.md](./research.md) Decision 2.

## SC-002 / SC-003 — generated client has zero manual edits, zero raw HTTP calls outside it

```bash
cd frontend
pnpm --filter @ecommerce/api-client verify-generated
```

Expect this to exit 0. It regenerates the client against the running BFF and fails if the
committed output under `packages/api-client/src/generated/` differs from what regeneration
produces.

Then confirm no hand-written calls exist outside the generated client:

```bash
grep -rE "fetch\(|axios\(" frontend/apps/web/src
```

Expect zero matches against BFF endpoints (any hits should be unrelated, e.g. none expected at
all today).

## SC-004 — unrecognized response fields don't break the SPA (new in this feature)

Run the three new tolerant-reader test cases added by this feature:

```bash
cd frontend
pnpm --filter @ecommerce/web test -- ProductList BasketView DoubleSubmit
```

Each of `tests/catalog/ProductList.test.tsx`, `tests/basket/BasketView.test.tsx`, and
`tests/checkout/DoubleSubmit.test.tsx` includes a case that mocks a BFF response containing an
extra, unrecognized field and asserts the component still renders its expected content. Expect all
to pass.

The orders case lives in `DoubleSubmit.test.tsx` rather than `Confirmation.test.tsx` because that
is the file exercising the real checkout round trip that parses the response; `Confirmation` is a
presentational component handed a hardcoded prop, so it never parses a BFF response at all.

## SC-005 — single-command client regeneration, under one minute

```bash
cd frontend
time pnpm --filter @ecommerce/api-client generate
```

Expect one command, no manual follow-up steps, completing in well under a minute against a locally
running BFF.

## Full local run (optional, exercises everything end to end)

```bash
cp .env.example .env
./scripts/up.ps1   # or ./scripts/up.sh
```

Then open <http://localhost:4173> and confirm the storefront (catalog → basket → checkout →
confirmation) works — each screen is served entirely by the generated client.
