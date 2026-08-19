# Contract: The demo's interface

**Feature**: 006-e2e-order-demo · **Status**: Design-time contract · **Date**: 2026-08-19

The demo exposes no HTTP API. Its interface is what a person types, what they see, and what they are
left holding afterwards — so that is what this contract fixes. Changing anything here changes the
procedure someone has to relearn, which is the cost the walkthrough exists to remove.

Written in the shape of [005's stack-interface.md](../../005-one-command-local-run/contracts/stack-interface.md),
because the demo command joins the same script family.

## Commands

| Command | Does | Guarantees |
|---|---|---|
| `./scripts/demo.ps1` · `./scripts/demo.sh` | Runs the whole demo end to end | Returns non-zero if any step or assertion fails (FR-007c) |
| `./scripts/demo.ps1 -SkipStart` · `--skip-start` | Same, against a stack already up in demo mode | For a repeat run when the stack is already warm |

The demo command is additive to the existing trio and changes none of them. `up`, `down`, and `reset`
behave exactly as 005 documents.

### What the command does, in order

1. **Ensure demo mode.** Brings the stack up with `docker-compose.demo.yml` layered, unless
   `-SkipStart`. Returns only when every component is healthy — the same `--wait` gate `up` uses.
2. **Clean basket.** `POST http://localhost:5188/baskets/current/clear` with `X-Tenant-Id: contoso`
   and `X-Subject-Id: phase1-stub-user` (research Decision 7). A 409 means it was already empty,
   which is success.
3. **Mark the evidence window.** Records the timestamp the hop evidence is collected from.
4. **Run the flow.** The Playwright demo project drives `http://localhost:4173`: browse → add to
   basket → check out → confirmation, capturing video and one still per step.
5. **Verify the order.** Two direct calls to the orders service (below).
6. **Collect hop evidence.** Reads collector span lines from the window opened in step 3.
7. **Report.** Prints the labelled summary and writes the artifacts.

### Prerequisite failures

The command fails before doing anything, naming exactly one missing thing — the rule 005 set:

| Condition | Message names |
|---|---|
| Docker daemon not responding | The daemon, and that it needs starting |
| `.env` absent | The file, and the template to copy |
| Playwright browsers not installed | The install command, and that it is a one-time step |
| Stack not in demo mode, with `-SkipStart` given | That demo mode is required, and to drop the flag |

## Addresses the demo uses

| What | Address | Published by |
|---|---|---|
| Storefront | `http://localhost:4173` | The default stack |
| Platform entry point | `http://localhost:5300` | The default stack |
| Orders service | `http://localhost:5041` | **Demo mode only** |
| Baskets service | `http://localhost:5188` | **Demo mode only** |

The default stack still publishes nothing but the first two. Demo mode does not change what the
storefront can reach — it only gives the demo's own verification step an address to call
(research Decision 5).

## The verification calls

Both are made directly against the orders service, by the demo, after the flow completes.

| Call | Headers | Expected |
|---|---|---|
| `GET /orders/{id}` | none | Failure. No order returned — no tenant was resolved (FR-006, US2 scenario 2) |
| `GET /orders/{id}` | `X-Tenant-Id: contoso` | `200` with `id`, `placedAtUtc`, `total`, and `tenantId: contoso` |

The demo asserts that the returned `tenantId` is non-empty and equals the tenant the placing request
resolved. The first call is not error handling — it is the enforcement gate demonstrated.

## Outputs

| Artifact | Path | Committed |
|---|---|---|
| Per-step stills | `docs/demo/*.png` | Yes |
| Written walkthrough | `docs/demo-phase-1.md` | Yes |
| Video | `artifacts/demo/video/*.webm` | No — attached to SCRUM-16 |
| Verification output | `artifacts/demo/verification.txt` | No |
| Hop evidence | `artifacts/demo/hops.txt` | No |

The exact printed shape is fixed in [data-model.md](../data-model.md) under "Verification output
shape". It is a contract because US2 acceptance scenario 3 requires someone who did not build the
system to read it.

## What this command is not

- **Not a build gate.** It is never invoked by a pipeline, blocking or scheduled. Gating belongs to
  SCRUM-22 (spec Clarifications, research Decision 12).
- **Not a replacement for the 004 walkthrough.** `frontend/apps/web/e2e/walkthrough.spec.ts` keeps
  running against the dev server and keeps owning its own success criteria. The demo project is a
  separate Playwright project with a different target and different output.
