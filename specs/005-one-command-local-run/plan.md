# Implementation Plan: One-Command Local Run with Real Containers

**Branch**: `005-one-command-local-run` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-one-command-local-run/spec.md`

## Summary

Make the whole platform start with one command. A `docker-compose.yml` at the repository root brings
up fifteen components — one SQL Server hosting a database per service, a broker, a cache, a telemetry
collector, four migration steps, six services, and the storefront — with every one health-gated so
the command cannot report success while anything is still broken. Thin wrapper scripts check the
prerequisites Compose cannot and then delegate to it.

Two preconditions have to be cleared first, both discovered rather than assumed: **five of the six
service images cannot build** (their Dockerfiles copy `shared/ServiceDefaults` but not
`shared/Tenancy`, referenced since 003), and **the storefront has no image at all**. A convention
test over the Dockerfiles is added so the first cannot recur silently.

The acceptance test is 004's Playwright walkthrough re-pointed at the containerized stack — it
already parameterises both addresses it needs.

## Technical Context

**Language/Version**: No new language. Docker Compose v2 as the topology description; PowerShell and
POSIX shell for the two wrapper scripts; existing C# / .NET 10 and the existing TypeScript frontend,
neither of which gains runtime code in this feature.

**Primary Dependencies**: Docker Engine with Compose v2 (contributor prerequisite) ·
`mcr.microsoft.com/mssql/server:2022-latest` (already used by `docker-compose.deps.yml`) ·
`redis:7-alpine` and `rabbitmq:3-management-alpine` (new, health-gated, unused by code — spec FR-017)
· `otel/opentelemetry-collector-contrib` (new — research Decision 10) · `nginx:alpine` for the
storefront · `mcr.microsoft.com/dotnet/runtime-deps:10.0` for the migration bundles.

**Storage**: One SQL Server container hosting `products`, `baskets`, `orders`, and `parties`
databases, each with its own connection string (spec FR-018). Two named volumes — SQL Server data and
RabbitMQ state — survive `down` and are removed by reset. Redis holds nothing and gets no volume.

**Testing**: A new convention test asserting every service image copies every shared project its
`.csproj` references (the check that would have caught the broken Dockerfiles) · `docker compose
config` validation · 004's end-to-end walkthrough run against the stack, which is the acceptance
criterion for US2.

**Target Platform**: A contributor's laptop — Windows, macOS, or Linux — with Docker installed and
nothing else. Windows is this repository's primary development platform, which is why the wrapper
ships in both PowerShell and POSIX forms.

**Project Type**: Local development infrastructure. No shopper-facing behaviour changes.

**Performance Goals**: Under 10 minutes from clean checkout to usable storefront on first run; under
3 minutes on subsequent runs (spec SC-001).

**Constraints**: A documented memory floor the stack fits inside (spec SC-009) — roughly 3.5 GB of
container demand, so 6 GB allocated to the Docker daemon · exactly two manual setup steps (SC-002) ·
only the gateway and storefront reachable from the host (research Decision 8) · a failure names its
component within 2 minutes (SC-005).

**Scale/Scope**: 15 components, 6 Dockerfiles to fix, 1 Dockerfile to create, 4 migration bundles,
3 wrapper commands in 2 shells, 1 new test project.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see the bottom of this file.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| I. Service Autonomy and Bounded Context | Each service keeps its own database and its own connection string, and no service's configuration names another's (spec FR-018). The **server** is shared locally, which the deployed topology does not do. | DEVIATION (local only) — see Complexity Tracking |
| II. Contract-First Integration | No API or event contract changes. The stack's own interface — commands, addresses, configuration — is written down before implementation in [contracts/stack-interface.md](./contracts/stack-interface.md). | PASS |
| III. Test-First Development | The convention test over the Dockerfiles is written before the Dockerfiles are fixed, and it is what makes FR-014 stay fixed. Compose validity and the end-to-end walkthrough follow. Infrastructure is not exempt: the defect this feature has to clear existed precisely because nothing tested it. | PASS |
| IV. Event-Driven by Default | The broker runs and nothing publishes to it. Unchanged from ADR-0011's recorded deviation — this feature neither narrows nor widens it. | DEVIATION (carried, tracked by SCRUM-18/31) |
| V. Tenant Isolation Is a Security Boundary | Propagation and enforcement are unchanged and still hold. **The missing schema-per-tenant separation is unchanged and still missing** (carried from 004), and consolidating onto one database server makes it more visible without making it worse. | FAIL (pre-existing) — see Complexity Tracking |
| VI. Secure by Default | The database password comes from `.env`, is git-ignored, and is never baked into an image (spec FR-013). Containers keep running as the non-root `$APP_UID`; the `curl` install runs before that switch. The stub-identity deviation is carried unchanged from 002/003. | DEVIATION (carried, tracked by SCRUM-23) |
| VII. Observable by Default | Better than before: the collector gives `ServiceDefaults`' OTLP export somewhere to go, so traces, metrics, and logs are readable locally for the first time. The Elastic backend remains out of scope. | PASS |
| VIII. Performance and Resilience Budgets | Startup budgets are stated and measurable (SC-001), and a resource floor is documented (SC-009). Health gates mean a slow component delays success rather than being reported as ready. No service's own budgets change. | PASS |
| IX. Frontend Discipline | The storefront gains an image serving its existing production build; no application code changes. The bundle budget from 004 is unaffected — the image serves exactly what `pnpm build` produces. | PASS |
| X. Toggle-Gated, Reversible Delivery | Rollback is the previous Compose file; nothing here ships behind a toggle because nothing here is shopper-facing behaviour. Migrations are unchanged — this feature changes *when* they run, not what they do. | PASS |

**Gate result**: proceed, with four entries in Complexity Tracking. Only one of them (Principle I) is
new; the rest are carried forward, and the Principle V failure is the same one 004 recorded and
nobody has yet decided about.

## Project Structure

### Documentation (this feature)

```text
specs/005-one-command-local-run/
├── plan.md                          # This file
├── research.md                      # Phase 0 — 12 decisions + 1 finding
├── data-model.md                    # Phase 1 — component inventory, gates, volumes
├── quickstart.md                    # Phase 1 — 8 validation scenarios
├── contracts/
│   └── stack-interface.md           # commands, addresses, configuration
├── checklists/requirements.md       # spec quality checklist (16/16)
└── tasks.md                         # Phase 2 — created by /speckit-tasks, NOT by this command
```

### Source Code (repository root)

```text
docker-compose.yml                           # NEW — the whole stack, the repository default
docker-compose.deps.yml                      # UNCHANGED — per-service servers, single-service runs
.env.example                                 # UNCHANGED — already carries the one value needed

scripts/                                     # NEW
├── up.ps1 / up.sh                           # prerequisite checks, then `compose up --build --wait`
├── down.ps1 / down.sh                       # `compose down`
└── reset.ps1 / reset.sh                     # `compose down -v`

services/*/src/*.Api/Dockerfile              # CHANGED — copy shared/Tenancy (5 of 6); add curl;
                                             #   add a `migrator` stage for the four with a DbContext
frontend/apps/web/Dockerfile                 # NEW — build, then nginx with history fallback
frontend/apps/web/nginx.conf                 # NEW — SPA fallback (research Decision 5)

services/gateway/src/Gateway.Api/            # CHANGED — allowed origins gain the storefront's
  appsettings.Development.json               #   published origin (research Decision 7)

tests/ContainerConventionTests/              # NEW — every image copies every shared project it needs
docker/otel-collector-config.yaml            # NEW — receive OTLP, export to stdout

docs/                                        # CHANGED — the one command, prerequisites, resource
                                             #   floor, and what the stack does not promise
```

**Structure Decision**: the stack file goes to the repository root as `docker-compose.yml` — the
default filename — so the documented command carries no `-f` flag. That removes the most likely
transcription error from the one line a new contributor has to get right. The existing
`docker-compose.deps.yml` stays untouched beside it, because it demonstrates two things the
consolidated stack deliberately cannot: that a service runs without its neighbours, and the
per-service-server topology of the deployed environment.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Principle I** — one database server hosts all four service databases locally, where the deployed topology gives each its own. | Four SQL Server containers plus six services plus a broker, a cache, a collector, and the storefront is roughly 8 GB before anything is used, and SC-001 and SC-009 both fail on a normal laptop. Decided in the spec's clarification session, not here. | Rejected keeping four servers: it is faithful and unusable daily, and the daily loop is what this feature exists to fix. **Scope of the deviation**: local only. Each service still owns its own database and connection string; `tests/CrossServiceIsolation.Tests` scans configuration and is unaffected; `docker-compose.deps.yml` retains the faithful topology; FR-019 requires the documentation to say which is which. |
| **Principle V** — no schema-per-tenant separation. | Not introduced here and not made worse here. 003's plan specified `HasDefaultSchema` per tenant and marked its tasks complete; the code has never contained it, and 004 recorded the same failure. Consolidating onto one server makes it easier to notice, which is the only thing this feature changes about it. | Nothing simpler was rejected — **this remains an open gap awaiting a maintainer decision**, now across two features. It is contained to fix (resolve the schema from the tenant context at each `AddDbContext`, plus a migration per service). **Time-bound**: before a second tenant is configured, and before any of this is exposed outside a laptop. |
| **Principle IV** — the broker runs with nothing publishing to it. | The spec's clarification chose to run the platform's declared dependencies even where no code uses them, so the story that first needs one finds it present. ADR-0011 already records why no messaging exists. | Rejected wiring a real publisher here: that is SCRUM-18's event schemas and SCRUM-31's outbox, several times this feature's size. **Honest reading**: the broker's health check means "available", never "working". **Time-bound**: closed by SCRUM-18/SCRUM-31. |
| **Principle VI** — the stub identity still always authenticates, and no authorization policy exists. | Carried unchanged from 002 and 003. This feature runs what exists; it does not change who may call what. | Rejected standing up the identity server: that is SCRUM-23/Phase 3. **Time-bound**: before this path is exposed outside local/demo. |

Two notes that are not violations but should not be silent:

- **The storefront image is not deployable.** Vite inlines the backend origin at build time, and the
  correct value is the host-published `http://localhost:5300` rather than a Compose hostname, because
  the browser runs on the host. The image is therefore specific to this local stack. Runtime
  configuration is the fix when deployment matters (research Decision 6).
- **An OpenTelemetry Collector is slightly more than the spec asked for.** The spec put observability
  *backends* out of scope; a collector is not the backend but the endpoint `ServiceDefaults` already
  targets, and without it every service logs OTLP export failures continuously — noise that makes the
  failures FR-010 is about harder to see. The alternative is `OTEL_SDK_DISABLED=true`, which is
  quieter and blinds local development. **One container's difference; worth confirming before
  implementation.**

## Post-Design Constitution Re-Check

Re-evaluated after [data-model.md](./data-model.md) and [contracts/stack-interface.md](./contracts/stack-interface.md)
were written. No verdict changed. Three things were confirmed by the design rather than assumed:

1. **The health gates are the platform's own, not a second opinion.** Every service probe the stack
   waits on is `/health/live` or `/health/ready` as defined in 001's health-check contract, and each
   domain service's readiness opens a real connection to its own database. A TCP-connect healthcheck
   would have reported a service healthy while its database was unreachable — the silent success the
   ticket's third test scenario exists to catch, reintroduced by the very mechanism meant to prevent
   it.
2. **Not publishing the internal ports turns an assertion into a property.** Spec 004's SC-010 says
   the storefront addresses only the entry point. With only the gateway and storefront published, a
   contributor *cannot* call the BFF directly from the host — there is no address. The `debug`
   profile exists for the case that genuinely needs it.
3. **No client-supplied value reaches configuration.** Every connection string, origin, and endpoint
   is set by Compose from values the repository controls; the single contributor-supplied value is a
   local database password copied from a template without editing.

The Principle V gap is unchanged by this design and is the one item that should be decided rather
than carried into a third feature.
