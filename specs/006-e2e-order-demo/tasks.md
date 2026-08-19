---

description: "Task list for 006-e2e-order-demo"
---

# Tasks: End-to-End Order Demo — Phase 1 Exit Proof

**Input**: Design documents from `/specs/006-e2e-order-demo/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included and non-negotiable. Constitution Principle III mandates a failing test before the
code that makes it pass; this feature claims no exemption. Test tasks precede the implementation
tasks they cover, and are written to fail first.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story the task serves (US1, US2, US3)
- Every task names the exact file it touches

## Path Conventions

Paths are repository-relative, matching the tree in [plan.md](./plan.md) — services under
`services/`, storefront under `frontend/apps/web/`, contributor commands under `scripts/`, committed
evidence under `docs/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Demo mode — the Compose override and telemetry config the run depends on. Nothing here
touches the default stack.

- [X] T001 Verify empirically that the OTel collector's debug exporter prints the `service.name` resource attribute, and record the observed line shape as a finding at the end of `specs/006-e2e-order-demo/research.md`. **DONE — and it changed the design.** The blocker was not verbosity but `service.telemetry.logs.level: warn`, which suppresses the exporter's output entirely at any verbosity. Both `normal` and `detailed` carry `service.name`; `normal` stays. See research.md "Measurement finding (T001)"
- [X] T002 [P] Add `artifacts/` to `.gitignore` under a comment saying the demo's video and raw evidence live there and are deliberately not committed (research Decision 8)
- [X] T003 [P] Create `docker/otel-collector-config.demo.yaml` — a copy of `docker/otel-collector-config.yaml` differing in one line, `service.telemetry.logs.level: info` instead of `warn`, with a header saying that line is what makes span output visible and that verbosity deliberately stays `normal` (revised by T001's finding; the original task said to raise verbosity, which measurement showed buys nothing)
- [X] T004 Create `docker-compose.demo.yml` layering over the default stack: publish the two services the demo actually calls — baskets 5188 and orders 5041 — and select the demo collector config via `command`, mounting it at a distinct target so which config is in force does not depend on Compose's volume-merge semantics. Narrower than the original task's "publish all five": `docker-compose.debug.yml` remains the file for reaching every service, and demo mode changes no service's environment
- [X] T005 Verify demo mode starts clean: `docker compose -f docker-compose.yml -f docker-compose.demo.yml up --build --wait` returns success, `http://localhost:5041/health/ready` answers, and `http://localhost:4173` still serves the storefront. **DONE** — all components healthy, orders 5041 / baskets 5188 / storefront 4173 / gateway 5300 all HTTP 200; the default stack still resolves to only 4173 and 5300

**Checkpoint**: demo mode exists and is reachable. The default stack is unchanged — confirm `./scripts/up.ps1` still publishes only 4173 and 5300.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The demo command shell and the Playwright project that every story's evidence flows
through.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create `frontend/apps/web/playwright.demo.config.ts`: `testDir: './demo'`, `baseURL` from `DEMO_STOREFRONT_URL` defaulting to `http://localhost:4173`, **no `webServer` block**, `video: 'on'`, `outputDir` under `artifacts/demo/`, single chromium project, `retries: 0`. Header comment explaining why this is separate from `playwright.config.ts` (research Decision 1)
- [X] T007 [P] Add a `demo` script to `frontend/apps/web/package.json` running `playwright test --config playwright.demo.config.ts`
- [X] T008 Create `scripts/demo.ps1` with argument parsing (`-SkipStart`) and prerequisite checks that name exactly one missing thing — Docker daemon, `.env`, Playwright browsers, and "stack is not in demo mode" when `-SkipStart` is given. Mirror the `Stop-WithReason` helper and comment style of `scripts/up.ps1`; contract is `specs/006-e2e-order-demo/contracts/demo-interface.md`
- [X] T009 [P] Create `scripts/demo.sh` as the POSIX twin of T008, matching `scripts/up.sh`'s conventions
- [X] T010 Add the demo-mode start step to both scripts: unless `-SkipStart`/`--skip-start`, run Compose with `docker-compose.demo.yml` layered and `--wait`, so the script returns only when every component is healthy (FR-009)

**Checkpoint**: `./scripts/demo.ps1` starts demo mode, checks prerequisites, and exits cleanly with nothing to run yet. **Verified** on 2026-08-19: full run built and started demo mode and exited 0; `--skip-start` succeeded against a demo-mode stack and refused a default-mode one with the message naming the fix.

**Two defects found and fixed while verifying this phase** — both would have surfaced as confusing failures later:

1. `demo.ps1` failed to parse under Windows PowerShell 5.1. No `.ps1` in this repository carries a UTF-8 BOM, so 5.1 reads them as ANSI and mangles non-ASCII characters; an em dash inside a double-quoted string turned the following `-SkipStart` into a parameter token. Fixed by making `demo.ps1` pure ASCII, matching `reset.ps1`. **`up.ps1` (3 non-ASCII characters) and `down.ps1` (1) carry the same latent risk** and have not been touched here — worth a follow-up.
2. `demo.sh` looked for Playwright's browsers under `~/.cache/ms-playwright` on Git Bash, where they actually live under `%LOCALAPPDATA%`. It refused a correctly installed machine. Fixed with a `MINGW*|MSYS*|CYGWIN*` branch using `cygpath`.

**One scope gap closed**: `frontend/apps/web/tsconfig.json` names its included paths explicitly and covered `e2e`/`playwright.config.ts` but not the demo, so T011's spec would never have been typechecked. `demo` and `playwright.demo.config.ts` are now in the include list.

---

## Phase 3: User Story 1 — Watch one order placed end to end, live (Priority: P1) 🎯 MVP

**Goal**: One command drives browse → basket → checkout → confirmation through the container stack,
an order is persisted, and running it again works.

**Independent test**: Run `./scripts/demo.ps1` on a started stack. It reaches the confirmation screen
without intervention, prints an order reference and total, and exits 0. Run it again — a second,
different order. Delivers the full Phase 1 claim without any of US2 or US3.

- [X] T011 [US1] Write `frontend/apps/web/demo/order-demo.spec.ts` — the flow and its assertions, expected to fail until the steps below land: navigate to `/`, add `Field Notes Notebook` ×2 and `Linen Apron`, open the basket, assert the `$59.25` total, check out, assert the confirmation heading and a 36-character reference, then read the order back through `GET {gateway}/bff/orders/{reference}` and assert its total matches the confirmation (US1 scenarios 1–2, FR-002, FR-003, FR-004)
- [X] T012 [US1] Add the clean-basket step to `scripts/demo.ps1` and `scripts/demo.sh`: `POST http://localhost:5188/baskets/current/clear` with `X-Tenant-Id: contoso` and `X-Subject-Id: phase1-stub-user`, treating 409 as success. Comment why this is not a `POST /bff/checkout` reset — that would place a real order (research Decision 7)
- [X] T013 [US1] Add the run-the-flow step to both scripts: invoke the Playwright demo project, propagate its exit code, and on failure print which step failed rather than a raw runner dump (FR-016)
- [X] T014 [US1] Write the order reference and total to `artifacts/demo/verification.txt` and echo them in the script's summary, so the reference is available to the verification step US2 adds (FR-012 groundwork)
- [X] T015 [US1] Assert repeatability in `frontend/apps/web/demo/order-demo.spec.ts` and the scripts: a second run must produce a **different** reference and must not depend on the first run's leftovers (US1 scenario 4, FR-007, FR-017)
- [X] T016 [US1] Run quickstart Scenario 5 (`./scripts/demo.ps1 -SkipStart` twice) and confirm two distinct orders, both readable (SC-003)
- [X] T017 [US1] Run quickstart Scenario 8 once - `reset` -> `up` -> `demo` - and confirm the confirmation screen is reached with no manual seeding or repair (FR-007a, FR-008, SC-003a). **DONE** 2026-08-19: reset 17s, up 84s, demo 67s; **2m48s from wiped volumes to a placed order**, no manual step. Proof the wipe was real: an order placed before the reset now returns 404, the order placed after it reads back with the right total, and all three seeded catalogue products are present again. Elapsed times to be carried into `docs/demo-phase-1.md` by T035

**Checkpoint**: US1 is independently demoable. Phase 1's core claim is evidenced. **Verified** on 2026-08-19: five consecutive runs, five distinct order references, zero failures.

**A race in the demo spec, found by running it rather than reading it.** The second run failed on the basket total while the quantity assertion passed - the signature of a lost write, not a pricing fault. Cause: `AddToBasketButton` disables only its *own* control while a write is in flight, so clicking the apron and immediately following the Basket link could render the basket before that third write landed, showing the two notebooks alone. Fixed by waiting for each control to re-enable, which is the observable signal that the server accepted the item.

Two things worth carrying forward: an intermittent demo would have failed FR-007 in front of an audience rather than in a task, and `e2e/walkthrough.spec.ts` clicks add-to-basket the same way - it has not been touched here, but it is exposed to the same race whenever it runs against containers rather than the dev server.

**One prerequisite revised**: the scripts invoke Playwright through `node node_modules/@playwright/test/cli.js` rather than through pnpm. pnpm installs the dependencies but is not needed to run them, and dropping it as a runtime requirement means the demo runs on any machine where `pnpm install` has ever succeeded. The missing-dependency message still names `pnpm install`.

---

## Phase 4: User Story 2 — Confirm the order belongs to the right tenant (Priority: P2)

**Goal**: The order records the tenant it was placed for, the orders service returns it, and the demo
shows the match — plus the refusal when no tenant is resolved.

**Independent test**: Place one order, then run the two direct calls from
[contracts/demo-interface.md](./contracts/demo-interface.md). The tenanted call returns
`tenantId: contoso`; the untenanted call fails and returns no order. Testable without US3's artifact
work.

**Contract before code** (Principle II): T018–T019 precede everything else in this phase.

- [X] T018 [P] [US2] Add `tenantId` to the `Order` schema in `specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml`, matching `specs/006-e2e-order-demo/contracts/orders-openapi.yaml` — required, `maxLength: 128`, with the note that the column is nullable for one release
- [X] T019 [P] [US2] Make the same addition to `specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml`. All three documents describe the same downstream `Order`; two agreeing and one disagreeing is worse than the original problem (plan.md, Post-Design re-check)

**Tests first** — T020–T024 must fail before T025–T028 exist.

- [X] T020 [P] [US2] Create `services/orders/tests/Orders.Api.UnitTests/OrderTenantTests.cs`: `Order.PlaceFrom` throws `ArgumentException` for null, empty, and whitespace tenant, and carries the tenant through on the happy path
- [X] T021 [US2] Update `services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs` for `PlaceFrom`'s new tenant parameter, leaving every existing total assertion intact
- [X] T022 [US2] Extend `services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs`: an order placed with a resolved tenant persists that tenant on the row, and it equals the tenant the request resolved (FR-005)
- [X] T023 [US2] Extend `services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs`: `GET /orders/{id}` returns `tenantId`, non-empty and matching (FR-005a)
- [X] T024 [US2] Extend `services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs`: a `POST /orders` reaching the service with no resolved tenant creates **no** row — assert the count is unchanged, not merely that the response failed (FR-006, US2 scenario 2)

**Then the implementation.**

- [X] T025 [US2] Add `TenantId` to `services/orders/src/Orders.Api/Data/Order.cs` and a `tenantId` parameter to `PlaceFrom`, rejecting blank with `ArgumentException`. Document in the remarks that the value comes from the resolved context and never from the request body (research Decision 2)
- [X] T026 [US2] Configure the column in `services/orders/src/Orders.Api/Data/OrdersDbContext.cs` — max length 128, nullable — with a comment naming this the expand half of expand/contract (research Decision 3)
- [X] T027 [US2] Generate the EF Core migration `AddOrderTenantId` into `services/orders/src/Orders.Api/Migrations/`. Additive, nullable, no default, no backfill; verify the generated `Down` drops only the new column
- [X] T028 [US2] Update `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`: inject `TenantContext` into the `POST /orders` handler and pass `RequireTenantId()` into `PlaceFrom`; add `TenantId` to `OrderResponse` and the `GET` projection. Do not add tenant to `PlaceOrderRequest`
- [X] T029 [US2] Confirm the BFF needs no change: run `services/bff/tests` and verify `OrderResource` deserializes the new field-bearing response unchanged, and that `/bff/orders/{id}` still returns the three-field client shape (research Decision 4)
- [X] T030 [US2] Add the verification step to `scripts/demo.ps1` and `scripts/demo.sh`: `GET http://localhost:5041/orders/{reference}` once without `X-Tenant-Id` (expect failure, no order) and once with `X-Tenant-Id: contoso` (expect 200 with `tenantId`), asserting the returned tenant is non-empty and equals `contoso` (FR-012, SC-004)
- [X] T031 [US2] Format the summary block exactly as `specs/006-e2e-order-demo/data-model.md` specifies under "Verification output shape", writing it to `artifacts/demo/verification.txt` and stdout. It is a contract because US2 scenario 3 requires a non-author to read it

**Checkpoint**: tenant attribution is provable end to end, and the no-tenant refusal is demonstrated rather than asserted. **Verified** on 2026-08-19.

Red before green, as required: the five test tasks were written first and failed to compile (`'Order' does not contain a definition for 'TenantId'`, `No overload for method 'PlaceFrom' takes 3 arguments`). After the implementation: **15 unit tests, 17 orders integration tests against real SQL Server, all green**. The BFF's 54 tests pass **unchanged**, which is Decision 4 confirmed rather than assumed.

Confirmed on the running stack - the additive field went exactly where it was meant to:

```
orders service : {"id":"...","placedAtUtc":"...","total":59.25,"tenantId":"contoso"}
BFF (client)   : {"id":"...","placedAtUtc":"...","total":59.25}
```

**Two PowerShell defects, both found by running demo.ps1 rather than reading it.**

1. Node wrote a harmless warning to stderr, and under `$ErrorActionPreference = 'Stop'` Windows PowerShell 5.1 turns native-command stderr into a *terminating* error. The demo aborted on a warning while the flow underneath it was passing. Fixed with an `Invoke-Native` helper that drops to `Continue` around native calls and treats the exit code as the signal.
2. The first version of that helper `return`ed `$LASTEXITCODE` - but `& $Command` also writes the command's output to the pipeline, so the caller received the whole transcript *and* the code as one array. Comparing it to `0` reported failure on a passing run, then `exit` on an array exited 0 anyway: wrong twice, in opposite directions. Fixed by handing the code back through a script-scoped variable and letting output stream.

Neither is reachable from `demo.sh`, so testing only the POSIX twin would have shipped both.

---

## Phase 5: User Story 3 — Leave behind a reference artifact (Priority: P3)

**Goal**: Committed walkthrough plus stills that stand on their own, hop evidence proving each named
component served traffic, and a video held outside the repository.

**Independent test**: After a run, `docs/demo-phase-1.md` and `docs/demo/*.png` are tracked files, the
walkthrough maps each Phase 1 exit criterion, and no `.webm` is under `docs/`.

- [ ] T032 [US3] Capture per-step stills in `frontend/apps/web/demo/order-demo.spec.ts` — `docs/demo/01-catalog.png`, `02-basket.png`, `03-checkout.png`, `04-confirmation.png` — writing to `docs/demo/` rather than Playwright's default output, which is git-ignored (FR-013a, research Decision 8)
- [ ] T033 [US3] Add the hop-evidence step to both scripts: mark a timestamp before the flow, then read `docker compose logs --since <timestamp> otel-collector` into `artifacts/demo/hops.txt`. **Parse `ResourceTraces` blocks specifically** — bare `service.name=` also matches `ResourceLog` lines from the logs pipeline — and **drop every span whose `url.path` starts with `/health`**, or Docker's five-second healthchecks make all six services look busy on a stack where nothing ran (research.md, follow-up measurement T005)
- [ ] T034 [US3] Assert in both scripts that `Gateway.Api`, `Bff.Api`, `Products.Api`, `Baskets.Api`, and `Orders.Api` each recorded at least one **non-health** span in the window, failing the run if any is missing. Report counts but do not assert fixed numbers. The filter is what gives this assertion meaning — unfiltered, it passes on a stack where the demo never ran (FR-011a, SC-006a)
- [ ] T035 [US3] Write `docs/demo-phase-1.md`: prerequisites, the ordered procedure with its starting state (FR-007b, FR-010), each hop on the checkout path named in order (FR-011), the embedded stills, where the video lives (SCRUM-16), and a table mapping every Phase 1 exit criterion to *evidenced by this run* or *deferred to Phase N* (FR-015). Fold in T017's cold-start result
- [ ] T036 [US3] Link the demo command and the walkthrough from `docs/local-development.md`'s command table, so both are reachable from the entry point a contributor already uses (FR-014, SC-005)
- [ ] T037 [US3] Confirm the video lands in `artifacts/demo/video/` and nowhere else: run `git status --porcelain docs/` and `git check-ignore artifacts` per quickstart Scenario 7 (FR-013)

**Checkpoint**: a reviewer who was not present can follow the flow from committed files alone.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T038 [P] Run every scenario in `specs/006-e2e-order-demo/quickstart.md` end to end and record which requirement each one cleared
- [ ] T039 [P] Run quickstart Scenario 9's downstream-unavailable check (`docker compose stop orders-api`, demo, restart) and confirm the failure names what was unreachable and leaves no partial order (FR-016, SC-008)
- [ ] T040 [P] Run the full build and test suite — `dotnet build` with warnings-as-errors, the convention tests under `tests/`, `pnpm --filter @ecommerce/web test`, and lint — confirming nothing in this feature broke the structure or container conventions
- [ ] T041 Measure the repeat-run time against the plan's 90-second budget and record the actual figure in `docs/demo-phase-1.md`
- [ ] T042 Attach the video recording to SCRUM-16 and confirm the walkthrough's pointer to it resolves. Mark the story's acceptance criteria against the evidence produced

---

## Dependencies

**Phase order**: Setup → Foundational → US1 → US2 → US3 → Polish.

**Blocking edges that matter**:

- T001 blocks T003, T033, T034 — the collector's output shape decides how hop evidence is parsed
- T004 blocks T005, and T005 blocks all of Phase 2
- T006 blocks T011 and T013; T008/T009 block T010, T012, T013
- T011 blocks T032 (the stills are captured inside the flow spec)
- T014 blocks T030 (the verification step needs the reference the flow produced)
- T018/T019 block T025–T028 — contract before code
- T020–T024 block T025–T028 — tests before implementation, and they must fail first
- T025 blocks T026, which blocks T027, which blocks T028
- T033 blocks T034
- T017 and T041 feed T035

**Story independence**:

- **US1** stands alone once Phase 2 is done. It is the MVP
- **US2** touches only the orders service and the demo's verification step. It does not need US3
- **US3** needs US1's flow to exist (stills come from it) and reads best with US2's verification in
  the summary, but its documentation tasks can be drafted in parallel with US2

---

## Parallel Execution Examples

**Phase 1** — after T001 reports:

```text
T002  .gitignore
T003  docker/otel-collector-config.demo.yaml
```

**Phase 4, contract updates** — different files, no shared state:

```text
T018  specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml
T019  specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml
```

**Phase 4, the unit test that owns its own file**:

```text
T020  Orders.Api.UnitTests/OrderTenantTests.cs   (new file — parallel)
T021  Orders.Api.UnitTests/OrderTotalTests.cs    (existing file — not parallel with T020's runner, but a separate edit)
```

**Phase 6** — independent verification passes:

```text
T038  quickstart scenarios
T039  failure-path scenario
T040  build + test suite
```

T022–T024 all touch the orders integration test project and share the `SqlServerFixture`; run them
sequentially rather than in parallel.

---

## Implementation Strategy

**MVP is US1 alone.** Phases 1–3 give a repeatable, one-command demo of a real order through the
deployed stack. That is the Phase 1 claim, and it is deliverable and demonstrable without the tenant
field or the committed artifact.

**Increment 2 is US2** — the tenant field, its tests, its contract updates, and the two direct
queries. This is the increment that makes the Jira acceptance criterion literally true rather than
approximately true, and it is the only increment that changes a service.

**Increment 3 is US3** — the durable evidence. Valuable, and the reason a later reviewer can settle
what "Phase 1 done" meant without re-running anything, but nothing in Phase 1's claim collapses if it
slips a sprint.

**Sequencing note**: T001 is deliberately first. It is the only task in this list whose outcome could
change the design, and it costs minutes now against a rewrite of T033/T034 later.
