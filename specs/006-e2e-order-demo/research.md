# Phase 0 Research: End-to-End Order Demo — Phase 1 Exit Proof

**Feature**: 006-e2e-order-demo · **Date**: 2026-08-19 · **Input**: [spec.md](./spec.md)

Every decision below was taken against the code as it stands, not against how the earlier specs
described it. Where the two disagree, the finding at the end of this file says so.

---

## Decision 1 — The demo runs against the container stack, not the dev server

**Decision**: The demo drives the storefront at `http://localhost:4173` (the `storefront` container)
and reads through the gateway at `http://localhost:5300`. It never starts Vite.

**Rationale**: The story says "on the deployed skeleton". The container stack is what 005 delivers as
the deployed article; the Vite dev server is a developer convenience with different bundling,
different origin, and different CORS treatment. Demonstrating the dev server would prove something
adjacent to what the story asks about.

**Consequence**: The existing [playwright.config.ts](frontend/apps/web/playwright.config.ts) cannot
be reused as-is — its `webServer` block starts `pnpm dev` and its `baseURL` is `:5173`. The demo gets
its own Playwright project with no `webServer`, pointed at `:4173`.

**Alternatives considered**: Reuse the existing config and override with env vars. Rejected —
`webServer` would still launch a dev server nobody watches, and the demo's video/screenshot settings
differ enough that overloading one config makes both harder to read.

---

## Decision 2 — The order stores the tenant it was placed for, taken from the resolved context

**Decision**: `Order` gains a `TenantId` string. The value comes from
`TenantContext.RequireTenantId()` inside the `POST /orders` handler and is passed into
`Order.PlaceFrom`. It is never read from the request body.

**Rationale**: Constitution Principle V forbids inferring tenant from user-supplied request data. The
orders service already has the resolved context injected in the same request scope
([Program.cs:24](services/orders/src/Orders.Api/Program.cs#L24) gates the `DbContext` on it), so the
value is already present and authoritative at the point the record is built.

**Alternatives considered**: Adding `tenantId` to `PlaceOrderRequest` so the BFF supplies it.
Rejected outright — that is the smuggling route Principle V exists to close, and the gateway already
strips caller-supplied tenant headers for the same reason
([TenantHeaderPropagationMiddleware.cs](services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs)).

---

## Decision 3 — The migration expands only; the NOT NULL tightening is a separate step

**Decision**: One migration adds `TenantId` as a **nullable** column. The service always writes it;
`Order.PlaceFrom` rejects a blank tenant. Making the column `NOT NULL` is a follow-up contract
migration, not part of this feature.

**Rationale**: Constitution Principle X requires migrations to be backward compatible with the
previously deployed version — expand/contract, never destructive in one step. A `NOT NULL` column
with no default would break the previous version's inserts the moment both versions run together.

**Consequence**: The guarantee that every order carries a tenant is enforced in the domain
(`PlaceFrom` throws on a blank tenant) and asserted by tests, not by the schema, until the contract
step lands. SC-004 is satisfied either way — it asks that what is read back is non-empty and correct.

**Alternatives considered**: `NOT NULL` with a `''` default. Rejected — it keeps old inserts working
by allowing exactly the blank-tenant row the feature exists to make impossible.

---

## Decision 4 — `tenantId` is added to the downstream contract, not the client-facing one

**Decision**: The orders service's `GET /orders/{orderId}` response gains `tenantId`. The BFF's
`/bff/orders/{orderId}` response is **unchanged**.

**Rationale**: FR-005a asks that reading an order back *from the orders service* shows the tenant.
The storefront has no use for the value, and the client-facing contract is what the generated
frontend client is built from — widening it would mean regenerating the API client to carry a field
no screen displays. Adding a field to the downstream contract is additive and therefore
non-breaking, since Principle II already requires consumers to tolerate unknown fields.

**Consequence**: No frontend client regeneration, no storefront change, no `bff-openapi.yaml` change.
The verification step reads the orders service itself (Decision 5).

**Alternatives considered**: Surface `tenantId` through the BFF so the verification step could use
the already-published gateway port. Rejected — it puts a field on a browser-facing contract purely to
make an internal check convenient, and the story explicitly asks to "query the orders service
directly".

---

## Decision 5 — The direct query uses the internal port publish, and proves the gate in the same step

**Decision**: The demo runs with internal ports published and queries
`http://localhost:5041/orders/{id}` twice: once **without** `X-Tenant-Id` (expected to fail) and once
**with** `X-Tenant-Id: contoso` (expected to return the order, tenant included).

**Rationale**: By default only `:4173` and `:5300` are published
([stack-interface.md](specs/005-one-command-local-run/contracts/stack-interface.md)), and the debug
override that publishes the rest already exists. Calling the orders service straight from the host is
the literal reading of the acceptance criterion. The unauthenticated call is not extra work — it is
US2 acceptance scenario 2 and FR-006 demonstrated in the same breath, because a host-originated call
has no gateway to stamp a tenant for it.

**Alternatives considered**: Query the database directly with `sqlcmd`. Rejected — the spec's own
assumption forbids it, and a demo that teaches reaching into another component's store teaches the
wrong thing.

---

## Decision 6 — Per-hop evidence comes from the collector's span log

**Decision**: The demo captures `docker compose logs otel-collector` for the run window and asserts
a span was recorded for each of `Gateway.Api`, `Bff.Api`, `Products.Api`, `Baskets.Api`, and
`Orders.Api`. The demo layers a collector config that differs from the default in **one line**:
`service.telemetry.logs.level` is raised from `warn` to `info`.

**Rationale**: Every service already exports OTLP traces through `ServiceDefaults`, and the collector
already receives them ([otel-collector-config.yaml](docker/otel-collector-config.yaml)). This is
component-activity evidence — exactly the Option B reading recorded in the spec's Clarifications —
with no new instrumentation. The services' own container logs are not usable for this: every service
sets `Microsoft.AspNetCore: Warning`, so successful requests produce no log line at all.

**Revised after measurement (T001)** — this decision originally called for raising the debug
exporter to `verbosity: detailed`, on the assumption that `normal` might omit the service name. Both
halves of that assumption turned out wrong, and the real obstacle was somewhere else entirely. See
the measurement finding at the end of this file. The default `verbosity: normal` stays as it is.

**Alternatives considered**: Following one request across hops by its correlation ID — which
`CorrelationIdMiddleware` would actually make possible today. Rejected deliberately: the spec's
Clarifications place request-level correlation in Phase 3 (SCRUM-25/26), and a half-built version
here would be discarded by the story that owns it. Also considered `verbosity: detailed`: it works
and prints the same attribute, but spends roughly twenty lines per span where `normal` spends three,
which makes `hops.txt` worse to read for no gain.

---

## Decision 7 — The clean-basket step calls the baskets service, not checkout

**Decision**: The demo empties the basket with `POST http://localhost:5188/baskets/current/clear`
carrying `X-Tenant-Id` and `X-Subject-Id`.

**Rationale**: The existing walkthrough spec resets state by posting to `/bff/checkout` and ignoring
the status. That works, but if the basket happens to be non-empty it **places a real order** — so the
reset step would manufacture order records the demo then has to explain. The baskets service already
exposes a clear route
([BasketsApiClient.cs:64](services/bff/src/Bff.Api/DownstreamClients/BasketsApiClient.cs#L64)), and
the demo already has the internal ports published for Decision 5.

**Alternatives considered**: Add a client-facing `DELETE /bff/basket` route. Rejected — new public
surface for a test-setup concern, on a contract the storefront would then be entitled to use.

---

## Decision 8 — Stills are committed under `docs/`; the video is not committed at all

**Decision**: Per-step screenshots are written to `docs/demo/` and committed. The video and the raw
evidence log are written to `artifacts/demo/`, which is git-ignored, and the video is attached to
SCRUM-16.

**Rationale**: The spec's clarification puts the walkthrough plus stills in the repository and keeps
the video out. Playwright's natural output directories — `test-results/`, `playwright-report/` — are
already git-ignored, so anything written there cannot be the committed evidence. `docs/demo/` sits
next to the walkthrough that embeds the images.

**Alternatives considered**: Committing to `test-results/` and un-ignoring it. Rejected — it would
un-ignore every future test run's output alongside the four images anyone actually wants.

---

## Decision 9 — One demo command, mirroring the existing script trio

**Decision**: `scripts/demo.ps1` and `scripts/demo.sh` join `up`, `down`, and `reset`. The command
brings the stack up in demo mode if it is not already, clears the basket, runs the Playwright demo
project, performs the two direct order queries, gathers the hop evidence, and prints one labelled
summary.

**Rationale**: FR-007c wants re-running to cost one action. The repository already establishes that a
contributor-facing operation is a script pair in `scripts/` that delegates to Compose and does the
checking Compose cannot. A fifth script in the same shape needs no explanation.

**Alternatives considered**: A `pnpm demo` script in the storefront package. Rejected — the demo spans
Compose, the storefront, and two backend services; hanging it off one frontend package would misplace
its ownership.

---

## Decision 10 — Demo mode is one override file, layered like the debug override

**Decision**: `docker-compose.demo.yml` layers over the default stack: it publishes the internal
service ports and mounts the demo collector config. It is applied only by the demo command.

**Rationale**: `docker-compose.debug.yml` already establishes the override pattern and already
publishes the internal ports, so the demo file is small. Keeping demo mode out of the default stack
means the "nothing but the gateway is reachable" property that 005 deliberately built stays true for
everyone not running the demo.

**Alternatives considered**: Reusing `docker-compose.debug.yml` unchanged and living with `normal`
collector verbosity. Rejected on Decision 6's determinism grounds. Also considered making demo mode a
flag on `up.ps1`; rejected because the demo command already needs to own the compose invocation.

---

## Decision 11 — Cold start is validated by the documented sequence, run once

**Decision**: `reset` → `up` → `demo` is written into the walkthrough as the cold-start procedure and
executed once during implementation, with its result recorded. Routine repeat runs skip it.

**Rationale**: FR-007a and SC-003a ask for exactly one cold-start proof, and 005 already guarantees
that a reset stack reseeds the catalogue through migration history
([CatalogSeed.cs](services/products/src/Products.Api/Data/CatalogSeed.cs)). Making every run pay the
full rebuild would make the repeat run expensive enough that nobody repeats it, which is the failure
FR-007 is written against.

---

## Decision 12 — Nothing here touches the build pipeline

**Decision**: The demo is not added to any pipeline, blocking or scheduled.

**Rationale**: Recorded directly from the spec's Clarifications. Gating belongs to SCRUM-22, which
owns the build gate. Stated here so a reviewer does not read its absence as an oversight.

---

## Finding — the tenant-scoped store the spec assumes does not exist yet

The spec's FR-005b says the stored tenant identifier "MUST NOT weaken or replace the existing
tenant-scoped store separation — that separation remains the enforcement boundary". **That separation
is not implemented.** [Program.cs:26](services/orders/src/Orders.Api/Program.cs#L26) resolves one
fixed connection string, `OrdersDb`, for every request. What actually enforces tenancy today is the
`RequireTenantId()` gate on the line above it: an unresolved request cannot obtain a `DbContext` at
all.

This is the same Principle V gap 004 and 005 both recorded and neither closed. This feature does not
close it either — schema-per-tenant is not in its scope — but it changes the honest description of the
enforcement boundary from "separate stores" to "the resolution gate", and the plan's Constitution
Check says so rather than repeating the spec's assumption.

One thing this feature does improve: with the tenant persisted on the row, the day schema-per-tenant
does land, existing orders can be routed to the right schema instead of being unattributable.

---

## Measurement finding (T001) — what the collector actually prints, and what was blocking it

Measured on 2026-08-19 against `otel/opentelemetry-collector-contrib:0.159.0`, the image the stack
pulls. Method: ran the collector alone with each candidate config and posted one synthetic OTLP span
carrying `service.name=Orders.Api` to `/v1/traces`. Building the full stack was unnecessary — the
question was entirely about the collector's own output.

**The real obstacle was not verbosity. It was `service.telemetry.logs.level: warn`.**

The existing [otel-collector-config.yaml](docker/otel-collector-config.yaml) sets that level to
suppress the collector's startup chatter. But the debug exporter writes its span output through the
same logger, at `info` — so with `level: warn`, **nothing is printed at all**, at any verbosity. The
first probe run posted a span successfully (HTTP 200) and produced an empty log. That is the one
line the demo config has to change.

**Both verbosity levels print the service name.** With `level: info`:

| Verbosity | Output for one span | Carries `service.name` |
|---|---|---|
| `normal` (the current default) | 3 lines | Yes — `ResourceTraces #0 service.name=Orders.Api` |
| `detailed` | ~20 lines | Yes — `     -> service.name: Str(Orders.Api)` |

So the plan's concern was unfounded in both directions: `normal` is not too sparse, and `detailed`
buys nothing but volume. The demo keeps `verbosity: normal`.

**What T033/T034 must parse**, at `normal`:

```text
ResourceTraces #0 service.name=Orders.Api
ScopeTraces #0 Microsoft.AspNetCore
GET /orders/{orderId} 5b8efff798038103d269b633813fc60c eee19b7ec3c1b174 http.request.method=GET
```

**One trap worth naming.** Every collector log line carries the collector's *own* resource block as
JSON, which contains `"service.name": "otelcol-contrib"`. A naive `grep service.name` therefore
matches every line and reports the collector as a hop. The assertion must anchor on the equals form
produced by the exporter — `service.name=<Name>` — and must exclude `otelcol-contrib`.

**Consequences for the task list**: T003 becomes a one-line change rather than a verbosity switch,
and T033/T034 have a known parse target. No design change beyond Decision 6's revision above.

### Follow-up measurement (T005) — health checks would have faked the hop evidence

Repeated against the real stack once demo mode was up, rather than the synthetic probe. Two things
came out of it, one confirming and one alarming.

**Confirming**: the demo collector config works in situ. One request to `/bff/products` through the
gateway, and `docker compose logs otel-collector` carried per-service span lines exactly as the probe
predicted. The `service.name=<Name>` equals-form never collides with the collector's own JSON
`"service.name": "otelcol-contrib"`, so the trap named above is avoidable with a plain pattern.

**Alarming**: counting spans per service is not evidence of anything. Every service is healthchecked
by Docker every five seconds, and each check emits a span. A window of any length therefore shows all
six services active — **including services the demo never touched**. Measured directly: after a single
`/bff/products` call, an unfiltered count reported Baskets, Orders, and Parties as busy, with 13
spans each. They had served nothing but their own health probe.

An assertion built that way would pass on a stack where the demo failed to run at all. It would be
worse than no evidence, because it would look like evidence.

**The fix, measured working**: anchor on `ResourceTraces` (not bare `service.name=`, which also
appears on `ResourceLog` lines from the logs pipeline), then drop spans whose `url.path` starts with
`/health`. The same single request then reports exactly the three components that served it:

```text
Bff.Api        2  ['/bff/products']
Gateway.Api    2  ['/bff/products']
Products.Api   3  ['/products']
```

Baskets, Orders, and Parties are correctly absent. Run the full checkout flow and all five expected
components appear, for real reasons.

**Consequences for the task list**: T033 must parse `ResourceTraces` blocks and exclude `/health`
paths; T034's assertion must run against that filtered set. Without the filter the assertion is
vacuous — it is the difference between FR-011a meaning something and FR-011a being decoration.

**Incidental**: spans already carry `correlation.id`, propagated by `ServiceDefaults`'
`CorrelationIdMiddleware`. Following one request across all five hops is therefore genuinely available
today, not merely theoretically. Decision 6's rejection of that route stands — the spec's
Clarifications put request-level correlation in Phase 3 — but it is a deferral by choice, not by
capability, and SCRUM-25/26 will find the groundwork already laid.
