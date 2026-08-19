# Running the platform locally

One command brings up the whole platform — every service, every dependency it declares, and the
storefront — with nothing installed but Docker.

```bash
cp .env.example .env       # no editing required
./scripts/up.ps1           # or ./scripts/up.sh
```

Then open **<http://localhost:4173>**.

That is the entire setup. If you needed a third step, that is a defect in this feature rather than a
missing instruction ([spec SC-002](../specs/005-one-command-local-run/spec.md)).

## Prerequisites

| Need | Why |
|---|---|
| Docker with Compose v2 | Everything runs in containers. Nothing else is required — no .NET SDK, no Node, no pnpm |
| **6 GB** allocated to the Docker daemon | See [What it costs](#what-it-costs); the start command checks this and refuses with a clear message if it is lower |
| ~10 GB free disk | Base images and eleven built images |

## The commands

| Command | What it does |
|---|---|
| `./scripts/up.ps1` · `up.sh` | Builds and starts everything. Returns only when the platform is usable |
| `./scripts/down.ps1` · `down.sh` | Stops everything. **Keeps your data** |
| `./scripts/reset.ps1` · `reset.sh` | Stops everything and **discards data**. The next start behaves like a first run |
| `./scripts/up.sh --debug` · `up.ps1 -PublishInternalPorts` | Also publishes the internal ports — see [Reaching past the front door](#reaching-past-the-front-door) |
| `./scripts/demo.ps1` · `demo.sh` | Runs the Phase 1 order demo end to end — see [The demo](#the-demo) |

All of them delegate to Docker Compose against the repository's default `docker-compose.yml`, so
`docker compose up --build --wait`, `docker compose down`, and `docker compose down --volumes` work
too. The scripts exist for the prerequisite checks Compose cannot do: with no daemon it prints a
socket error, and with no `.env` it would substitute an empty password and let the database fail
later for a reason that looks unrelated.

`down` and `reset` are separate commands on purpose. Stopping for the day and starting over are
different intentions, and conflating them is how somebody loses an afternoon's test orders.

## The demo

One command places a real order through the running stack, reads it back, and reports what it
proved:

```bash
./scripts/demo.ps1        # or ./scripts/demo.sh
```

It brings the platform up in demo mode if it is not already, so it is also a reasonable way to start
the stack for the first time. A repeat run against a warm stack takes about ten seconds
(`-SkipStart` / `--skip-start`).

**[docs/demo-phase-1.md](demo-phase-1.md)** is the walkthrough: what the flow looks like step by
step with screenshots, the path a checkout takes through the services, and which Phase 1 exit
criteria the run evidences. Read that rather than this file if what you want is to understand what
the platform does.

Demo mode differs from the default stack in two narrow ways: it publishes the orders and baskets
services so the demo can query them directly, and it makes the telemetry collector print the spans
the demo reads back. Nothing else changes, and `up`/`down`/`reset` are unaffected.

## What you get

| Address | What |
|---|---|
| <http://localhost:4173> | The storefront |
| <http://localhost:5300> | The gateway — the only backend address the storefront uses |

**Nothing else is published.** The services, the database, the broker, the cache, and the collector
are reachable only on the Compose network. That is deliberate: the storefront is required to reach
the platform's single entry point and nothing else, and leaving the rest unpublished makes that a
property of the environment rather than a rule somebody has to remember.

Fifteen components start. Eleven keep running; four are migrators that apply each service's schema
and exit. A healthy stack looks like this:

```bash
docker compose ps -a
```

- **10** `Up (healthy)` — SQL Server, Redis, RabbitMQ, the four domain services, the BFF, the
  gateway, the storefront
- **1** `Up` with no health status — the OpenTelemetry collector, whose image is distroless and
  carries no probe tool, so Compose can only gate on it running
- **4** `Exited (0)` — the migrators

## What it costs

Measured on the development machine this was built on, with base images already pulled:

| | Time |
|---|---|
| Start with images already built | **~60 seconds** |
| Start after a reset (fresh databases, schema applied, catalog seeded) | **~87 seconds** |
| Start after changing frontend source | **~10 minutes** — the storefront image rebuilds from source |

Memory, steady state after serving requests:

| Component | Memory |
|---|---|
| SQL Server | ~780 MB, growing to ~1.6 GB under sustained use |
| RabbitMQ | ~125 MB |
| Six .NET services | ~45–70 MB each |
| Collector, storefront, Redis | ~40 MB combined |
| **Total** | **~1.3 GB idle, ~2.2 GB observed peak** |

The 6 GB floor the start command enforces is not that figure — it is that figure plus the headroom
BuildKit needs, because the command always builds. A machine at exactly 2.5 GB would run the stack
and fail to build it.

**One honest gap**: these numbers were taken with Docker's base images already present. A genuinely
clean machine also pulls roughly 2 GB of base images on the first run, which is bandwidth-dependent
and was not measured here.

## Two things that will confuse you

### The first request after a big rebuild can fail

The health gates say a service can reach its database. They do not say the platform can serve a
request — on a cold start the first call through any path pays JIT compilation, EF model building,
and connection-pool creation. That exceeded the BFF's three-second downstream budget in testing, so
`up` now warms the request path before reporting success.

Separately, and less tidily: a ten-minute image rebuild leaves the machine busy enough that requests
can time out for a while afterwards — every route answering `504` while every health check reports
healthy. It resolves on its own; requests settled back to 30–70 ms. If you see this immediately
after a large rebuild, wait rather than debug.

### The stack shares one database server. Deployment does not

All four service databases live on **one** SQL Server container here. Each service still gets its
own database and its own connection string, and no service is configured with another's — but the
*server* is shared, and that is a local convenience for the memory floor, **not the deployed
topology**.

Deployed environments give each service its own database server, per constitution Principle I. Do
not read this consolidation as permission to share a database between services, or to reach across
from one service's connection to another's data.

If you want the faithful per-service-server topology locally, `docker-compose.deps.yml` still
provides it — that file also exists to demonstrate that a service runs without its neighbours, which
this stack deliberately cannot show.

## Reaching past the front door

Sometimes you need an internal port — most often the BFF's OpenAPI document, to regenerate the
frontend's API client.

```bash
./scripts/up.sh --debug            # or ./scripts/up.ps1 -PublishInternalPorts
```

That layers `docker-compose.debug.yml` over the default file and publishes the BFF (5301), the four
domain services (5088, 5188, 5041, 5204), and RabbitMQ's management interface (15672).

It also switches the BFF into its Development environment so the OpenAPI document is actually
mapped, while restoring the compose hostnames its Development configuration would otherwise point at
`localhost`. Publishing the port alone is not enough — see the comments in that file.

## When something is wrong

The start command fails rather than reporting a half-working stack, and names the component:

```text
container ecomerce-stack-storefront-1 is unhealthy
```

From there:

```bash
docker compose logs <component>          # e.g. docker compose logs bff-api
docker compose ps -a                     # what is healthy, what exited, and with what code
```

A missing dependency behaves the same way. Starting without the database fails in about 90 seconds
naming every component that could not proceed, rather than starting a stack that fails at first use.

## Related

- [`specs/005-one-command-local-run/quickstart.md`](../specs/005-one-command-local-run/quickstart.md) — the scenarios that validate all of the above
- [`frontend/README.md`](../frontend/README.md) — the storefront's own commands
- [`services/README.md`](../services/README.md) — the services, and the data-isolation rules this stack bends locally
