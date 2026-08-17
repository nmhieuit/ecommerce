# Phase 0 Research: One-Command Local Run

**Feature**: [spec.md](./spec.md) · **Branch**: `005-one-command-local-run` · **Date**: 2026-08-17

Every decision below was checked against the repository as it stands on 2026-08-17. Where an earlier
document and the code disagree, the code wins and the disagreement is called out.

---

## Decision 1 — Docker Compose, with the stack file as the repository default

**Decision**: Orchestrate with Docker Compose, in a `docker-compose.yml` at the repository root.

**Rationale**: The constitution's local-development rule is "every service MUST be runnable locally
with its real dependencies via containers by a single command" — it names containers, not a
particular orchestrator. Compose is already the repository's container vocabulary
(`docker-compose.deps.yml`), every service already has a Dockerfile, and the service-to-service
addresses baked into configuration (`http://products-api:8080`, `http://bff-api:8080`) are already
compose-network hostnames. Naming the file `docker-compose.yml` means the documented command needs
no `-f` flag, which removes the most common transcription error from the one thing a new contributor
must type correctly.

**Alternatives considered**:

- *.NET Aspire*: a genuinely strong fit for .NET local orchestration, and the repo's
  `ServiceDefaults` follows its conventions. Rejected because adopting it means introducing an
  AppHost project and a second way to describe the topology, and the constitution's Technology
  Constraints fix the stack without mentioning it — that is an amendment, not a feature decision.
- *kind / k3d*: closest to the deployed target, and wrong for a laptop's daily loop. Rebuild-and-load
  cycles are minutes rather than seconds.

---

## Decision 2 — A thin wrapper script is the documented command; Compose remains usable directly

**Decision**: Document `./scripts/up.ps1` (and `./scripts/up.sh`) as *the* command. Each verifies
prerequisites, then delegates to `docker compose up --build --wait`.

**Rationale**: FR-011 requires a missing prerequisite to be named. Compose cannot do that — with no
Docker daemon it prints a socket error, and with a missing `.env` it substitutes an empty password
and the database container fails later for a reason that looks unrelated. The wrapper checks that
Docker is installed, that the daemon responds, that `.env` exists, and that the daemon has the
documented memory available, failing with one sentence naming whichever is missing.

`--build` satisfies FR-009 (a source change is running code, not a stale image). `--wait` satisfies
FR-002 (the command does not return success until every container with a healthcheck is healthy, and
returns non-zero if one does not).

**Deliberately not automated**: the wrapper does **not** create `.env` for you. SC-002 fixes the
setup at exactly two manual steps — copy the template, run the command — and a script that silently
generates a credentials file teaches a habit that is wrong the moment it is not local.

**Alternatives considered**:

- *Raw `docker compose up --build --wait` as the documented command*: one less file, but FR-011
  becomes unimplementable and the failure modes above stay confusing.
- *A Makefile*: fewer moving parts on Unix, another prerequisite on Windows — which is this
  repository's primary development platform.

---

## Decision 3 — One SQL Server, one database per service, and no database-creation step

**Decision**: A single SQL Server container hosts a database per service. There is **no** separate
database-creation step: EF Core's migration application creates a database that does not exist.

**Rationale**: The topology is settled by the spec (FR-018, Clarifications). What research adds is
that the existing `*-db-init` containers — which exist purely to `CREATE DATABASE` before a service
connects — are unnecessary once migrations run as their own step. Verified during
004-minimal-shopping-spa: the integration suites point EF at database names that do not exist
(`basket-current-first-visit`, `checkout-happy-orders`, and others), call `MigrateAsync()`, and the
databases are created. The same mechanism serves the stack.

That removes four containers from the design and one class of ordering bug — a service starting
between "database created" and "schema applied".

**Consequence**: `docker-compose.deps.yml` keeps its per-service servers and its `*-db-init`
containers unchanged. It demonstrates the deployed topology and single-service independence, which
the consolidated stack deliberately cannot (spec Assumptions).

---

## Decision 4 — Migrations ship as self-contained bundles, run as init containers

**Decision**: Each service's Dockerfile gains a `migrator` stage that produces an EF Core migration
bundle. Compose runs each bundle as a short-lived container that must exit successfully before that
service starts.

**Rationale**: The spec's Assumptions rejected migrate-on-startup — it races when more than one
instance starts, and the platform's services are meant to be horizontally scalable. Of the ways to
run migrations as their own step, a bundle is the only one that does not put an SDK image in the
stack: `Microsoft.EntityFrameworkCore.Design` is referenced with `PrivateAssets="all"`, so
`dotnet ef` is a build-time tool and is not present at runtime. A self-contained bundle is a single
executable on a `runtime-deps` base — tens of megabytes rather than the ~800 MB an SDK image costs,
multiplied by four services.

Compose's `depends_on: { condition: service_completed_successfully }` is what makes this a gate
rather than a hope: a failed migration stops its service from starting at all, and `--wait` reports
it (FR-010).

**Alternatives considered**:

- *An SDK-based migrator container running `dotnet ef database update`*: simplest to write, four
  large images to pull, and it puts a build tool in the run-time topology.
- *`Migrate()` at service startup behind a Development-only flag*: one line of code and no new
  containers, but it is the pattern the spec explicitly rejected, and it makes the service's
  readiness depend on a schema change it might be racing another replica to apply.

---

## Decision 5 — The storefront gets an nginx image, with SPA history fallback

**Decision**: Build the storefront's static output and serve it from `nginx:alpine`, configured so
that any unmatched path returns `index.html`.

**Rationale**: The fallback is not a nicety. The storefront uses client-side routing, and two of its
three screens live at real paths (`/basket`, `/confirmation`). Spec 004's FR-011 requires the basket
to survive a page refresh, its quickstart Scenario 3 refreshes on `/basket`, and the end-to-end
walkthrough calls `page.reload()` there. A static server without history fallback answers 404 to
exactly that reload — the feature would appear to work until someone pressed F5.

**Alternatives considered**:

- *Serving the built assets from the gateway*: removes a container and the cross-origin problem
  entirely, and is probably where this ends up in deployment. Rejected here because it makes the
  gateway a static-file server, which is a larger change to a component this feature is not
  otherwise touching.
- *Running the Vite dev server in a container*: rejected — it would ship a development server as the
  local platform, and the built output is what the bundle budget (spec 004 FR-025) is measured on.

---

## Decision 6 — The storefront's backend origin is baked at image build, and must be host-reachable

**Decision**: Build the storefront image with `VITE_GATEWAY_ORIGIN=http://localhost:5300`.

**Rationale**: Vite inlines `import.meta.env.*` at build time, so this value is fixed when the image
is built rather than read when the container starts. The important consequence is *which* address is
correct: the browser runs on the host, not inside the compose network, so the origin must be the
host-published `http://localhost:5300` — **not** the compose hostname `gateway-api:8080`, which the
browser cannot resolve. This is the single most likely thing to get wrong here, and it fails as a
connection error in the browser with a healthy-looking stack behind it.

**Limitation, accepted**: the image is environment-specific. Promoting one image across environments
would need runtime configuration — a `config.js` fetched at startup, or placeholder substitution in
the entrypoint. That is deployment work, and deployment is out of scope for this feature (spec
Assumptions). Recorded so the next person does not discover it by trying.

---

## Decision 7 — The storefront is published on 4173, and the gateway must admit that origin

**Decision**: Publish the containerized storefront on host port **4173**. Add that origin to the
gateway's allowed-origins configuration, alongside the existing `http://localhost:5173`.

**Rationale**: 5173 is the Vite dev server's port. Serving the container there would mean the stack
and `pnpm dev` cannot run at once, and whichever started second would fail to bind — precisely the
"held port" failure US3 is about. 4173 is Vite's own preview port, so it reads as "the built app" to
anyone who knows the tool.

The gateway's CORS allow-list currently names only `http://localhost:5173`
(`appsettings.Development.json`, added in 004 after the end-to-end walkthrough found the storefront
blocked in a real browser). A new origin needs adding or the containerized storefront hits the same
wall — and `Gateway.Api.IntegrationTests/StorefrontCorsTests` is where that stays fixed.

---

## Decision 8 — Only the gateway and the storefront are published; everything else stays internal

**Decision**: Publish host ports for the gateway (5300) and the storefront (4173) only. Every other
container is reachable only on the compose network. A `debug` Compose profile publishes the rest for
troubleshooting.

**Rationale**: Spec 004's FR-014 and SC-010 say the storefront reaches the platform's single entry
point and nothing else. Not publishing the other ports turns that from an assertion into a property
of the environment — a contributor cannot accidentally call the BFF directly because there is no
address to call. It also reduces the port surface US3 is about, and removes clashes with services a
contributor is running from their IDE at the same time.

**Consequence**: `pnpm generate` reads the BFF's OpenAPI document from `localhost:5301`, which the
default stack does not publish. The existing per-service workflow still covers that, and the `debug`
profile publishes it when the stack is what you have running. This belongs in the documentation
FR-012 requires.

---

## Decision 9 — Health checks need a probe binary the runtime image does not ship

**Decision**: Install `curl` in each service image's final stage, before dropping to the non-root
user, and give every service a Compose healthcheck against its own `/health/ready`
(the gateway: `/health/live`).

**Rationale**: FR-002's gate is only as good as the healthcheck behind it, and the .NET runtime
images carry no shell utility able to make an HTTP request. Without one, the only available
healthcheck is a TCP connect, which reports a service healthy while its database is unreachable —
which is exactly the silent failure test scenario 3 is written to catch.

Every service already exposes the two probes (`/health/live`, `/health/ready`), and each domain
service's readiness check opens a real connection to its own database. Pointing Compose at those
endpoints reuses a gate the platform already has rather than inventing a second notion of healthy.

**Cost, accepted**: roughly 10 MB per image, and an `apt-get` layer that runs as root before
`USER $APP_UID` — the images still run as non-root, which is what Principle VI requires.

**Alternatives considered**:

- *A chiselled or distroless base*: smaller and more secure, and it has no shell at all, so the
  healthcheck problem gets worse rather than better.
- *TCP-only healthchecks*: free, and they lie.

---

## Decision 10 — An OpenTelemetry Collector is included, because its absence is noisy

**Decision**: Add an OpenTelemetry Collector container with a debug exporter, and point every
service at it via `OTEL_EXPORTER_OTLP_ENDPOINT`.

**Rationale**: `ServiceDefaults` calls `AddOtlpExporter()` for traces, metrics, and logs with no
endpoint configured, so the SDK defaults to `http://localhost:4317`. Inside a container that is the
container itself, and every service would retry and log export failures continuously. A stack whose
services all emit recurring errors makes the real failures FR-010 is about harder to see, and reads
as broken to a first-time contributor.

A collector is one small container that the services genuinely connect to — unlike the broker and
cache, which the spec knowingly runs hollow. It also makes the correlation-ID, tenant, and subject
scopes this platform has invested in actually visible while developing.

**Scope note — this is slightly more than the spec asked for.** The spec's Assumptions put
"observability backends" out of scope. A collector is not the backend (Elastic is); it is the
endpoint `ServiceDefaults` already targets. The alternative is setting `OTEL_SDK_DISABLED=true` for
the local stack, which is smaller and quieter but turns off telemetry in the one environment where a
developer would most like to read it. **Worth confirming before implementation** — it is a
one-container difference either way.

---

## Decision 11 — Two volumes, and a reset that is a separate command

**Decision**: Persist SQL Server data and RabbitMQ data in named volumes. `down` keeps them; a
separate documented reset removes them.

**Rationale**: FR-007 (a restart preserves data) and FR-008 (a reset discards it) are different
commands because they are different intentions, and conflating them is how someone loses an
afternoon's test orders by stopping the stack. Redis needs no volume — it holds nothing, and will
hold a cache when something finally uses it.

---

## Decision 12 — The acceptance test is the walkthrough that already exists

**Decision**: Verify this feature by running 004's Playwright walkthrough against the containerized
stack, plus a new convention test over the Dockerfiles.

**Rationale**: The walkthrough already parameterises both addresses it needs — `STOREFRONT_URL` and
`GATEWAY_ORIGIN` — so pointing it at the containerized stack requires no new test, only different
values. That is a much stronger acceptance criterion than "the containers are up", and it is exactly
what US2 asks for.

The convention test is the more interesting one. FR-014 exists because every service image copies
`shared/ServiceDefaults` but not `shared/Tenancy`, which five of the six services have referenced
since 003 — the images have not been built since that dependency appeared, and nothing noticed. A
test that reads each service's project references and asserts its Dockerfile copies every shared
project among them would have failed the day the reference was added. It belongs beside
`tests/CrossServiceIsolation.Tests` and `tests/StructureConventionTests`, which are the same idea
applied to configuration and folder layout.

---

## Finding — five of six service images cannot build

Recorded as FR-014, verified in detail here.

`Baskets.Api`, `Bff.Api`, `Orders.Api`, `Parties.Api`, and `Products.Api` all reference
`shared/Tenancy/Tenancy.csproj`. None of their Dockerfiles copies that project, so `dotnet restore`
inside the build stage fails on a missing reference. `Gateway.Api` is the exception: it produces the
tenant and subject headers from claims rather than reading them, references only `ServiceDefaults`,
and its image is fine.

This is not a regression introduced by this feature — it has been true since 003-stub-identity-tenant-context
added the library, and no image has been built since. It is a precondition of everything else here.
