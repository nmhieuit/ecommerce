# Quickstart: validating the end-to-end order demo

**Feature**: 006-e2e-order-demo · **Date**: 2026-08-19

Nine scenarios. Together they cover every functional requirement and every success criterion in
[spec.md](./spec.md). Run them against a stack you started yourself — none of them assume state left
by a previous scenario except where it says so.

## Prerequisites

```bash
cp .env.example .env          # if you have not already
./scripts/up.ps1              # or up.sh — the default stack
pnpm --filter @ecommerce/web exec playwright install chromium
```

Scenarios 1–7 assume demo mode, which `./scripts/demo.ps1` enters for you.

---

## Scenario 1 — One order, end to end (US1, FR-001…FR-004)

```bash
./scripts/demo.ps1            # or demo.sh
```

**Expected**: exit code 0. The printed summary shows an order reference, a total of `$59.25`, and a
tenant. `docs/demo/` holds four fresh stills and `artifacts/demo/video/` holds a `.webm`.

**Fails if**: any step needs a keystroke, a restart, or a data fix. That is FR-001, and a demo that
needs nursing has not met it.

---

## Scenario 2 — The persisted order matches the confirmation (US1 scenario 2, FR-003, FR-004)

From the summary Scenario 1 printed, take the reference and:

```bash
curl -s -H "X-Tenant-Id: contoso" http://localhost:5041/orders/<reference> | jq
```

**Expected**: `200`, with `total` exactly equal to the figure the confirmation screen showed, and
exactly one order for that reference.

---

## Scenario 3 — Tenant attribution is visible and correct (US2, FR-005, FR-005a, SC-004)

The response from Scenario 2 carries `tenantId`.

**Expected**: `"tenantId": "contoso"` — non-empty, and the same tenant the gateway resolved for the
placing request. The demo asserts this too; doing it by hand is the point, because US2 scenario 3
requires it to be legible to someone who did not build the system.

---

## Scenario 4 — No tenant, no order (US2 scenario 2, FR-006)

```bash
curl -i http://localhost:5041/orders/<reference>          # deliberately no X-Tenant-Id
```

**Expected**: a 500-class response and no order body. The orders service refuses rather than
answering from a default — the enforcement this feature's evidence rests on.

Then confirm the write path refuses the same way:

```bash
curl -i -X POST http://localhost:5041/orders \
  -H "Content-Type: application/json" \
  -d '{"items":[{"productId":"9f8d6b1e-0001-4000-8000-000000000001","quantity":1,"unitPrice":12.50}]}'
```

**Expected**: failure, and no new order record. Count the orders before and after if you want it
airtight.

---

## Scenario 5 — Repeatable (FR-007, FR-017, SC-003)

```bash
./scripts/demo.ps1 -SkipStart
```

**Expected**: exit code 0 again, and a **different** order reference from Scenario 1. Both orders
exist and are readable. Nothing about the second run depends on the first having happened.

**Fails if**: the second run needs a manual basket clear, or produces the same reference, or trips
over the order the first run left behind.

---

## Scenario 6 — Every named hop actually served traffic (FR-011a, SC-006a)

```bash
cat artifacts/demo/hops.txt
```

**Expected**: a section for each of `Gateway.Api`, `Bff.Api`, `Products.Api`, `Baskets.Api`, and
`Orders.Api`, each with at least one span from the run window. The demo fails if any is missing —
that is the assertion, not the printout.

**Not expected**: one request followed across all five by a shared id. That is Phase 3
(SCRUM-25/26), deliberately (research Decision 6).

---

## Scenario 7 — The committed evidence stands on its own (US3, FR-013a, FR-014, FR-015, SC-005)

```bash
git status --porcelain docs/
```

**Expected**: `docs/demo/` stills and `docs/demo-phase-1.md` are tracked files. Open the walkthrough
and check that it:

- names each hop on the checkout path (FR-011),
- embeds the stills in flow order,
- says where the video is (SCRUM-16), and
- maps every Phase 1 exit criterion to *evidenced by this run* or *deferred to Phase N* (FR-015).

**Also expected**: no `.webm` anywhere under `docs/`, and `artifacts/` reported as ignored by
`git check-ignore artifacts` (FR-013, Decision 8).

---

## Scenario 8 — Cold start (FR-007a, FR-008, SC-003a)

Run this once. It rebuilds from nothing and takes minutes.

```bash
./scripts/reset.ps1           # discards all data
./scripts/up.ps1
./scripts/demo.ps1
```

**Expected**: the demo reaches the confirmation screen with no manual seeding and no repair step. The
catalogue is present because it rides the migration history, not because someone loaded it.

**Fails if**: the first run after a reset needs anything the walkthrough does not list.

---

## Scenario 9 — Failure paths are comprehensible (FR-016, SC-008)

Two checks, each undone afterwards.

**Empty basket:** open <http://localhost:4173/basket> on a cleared basket. The checkout control is
disabled and no request is sent. Already covered by the 004 walkthrough; re-checked here because
SC-008 names it.

**Downstream unavailable:**

```bash
docker compose stop orders-api
./scripts/demo.ps1 -SkipStart
docker compose start orders-api
```

**Expected**: the demo fails with a message naming what was unreachable, exits non-zero, and leaves
no partial order behind. A stack trace with no attribution is a failure of this scenario.

---

## Coverage

| Requirement | Scenario |
|---|---|
| FR-001…FR-004 | 1, 2 |
| FR-005, FR-005a, FR-005b | 2, 3 |
| FR-006 | 4 |
| FR-007, FR-017 | 5 |
| FR-007a | 8 |
| FR-007b, FR-010 | 7 (the walkthrough is what is being read) |
| FR-007c | 1 (one command) |
| FR-007d | — no pipeline change exists to test; asserted by absence |
| FR-008 | 8 |
| FR-009 | 1 (`up` returns only when healthy) |
| FR-011, FR-011a, FR-011b | 6, 7 |
| FR-012 | 2, 3, 4 |
| FR-013, FR-013a, FR-014 | 7 |
| FR-015 | 7 |
| FR-016 | 9 |
| SC-001, SC-002 | 1 |
| SC-003, SC-003a | 5, 8 |
| SC-004 | 3 |
| SC-005 | 7 |
| SC-006, SC-006a | 6, 7 |
| SC-007 | Covered by the existing 004 walkthrough's double-checkout test |
| SC-008 | 9 |
