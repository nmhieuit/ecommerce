# Phase 1 Data Model: One-Command Local Run

**Feature**: [spec.md](./spec.md) · **Research**: [research.md](./research.md) · **Date**: 2026-08-17

This feature stores no business data of its own. Its "model" is the stack's topology: what runs,
what each thing waits for, and what survives a restart. That is what the entities below describe.

---

## Stack Component

One thing the command starts. Every component has:

| Field | Meaning |
|-------|---------|
| Name | What a contributor sees in `docker compose ps` and in a failure message (FR-010) |
| Kind | Dependency, migrator, service, storefront, or telemetry |
| Health gate | How the stack decides this component is ready — see [Health Gate](#health-gate) |
| Waits for | Components that must reach their gate first |
| Published port | Host port, if any. Only the gateway and storefront have one (research Decision 8) |

### The inventory

| Component | Kind | Health gate | Waits for | Published |
|---|---|---|---|---|
| `sqlserver` | Dependency | Server accepts a query | — | — |
| `redis` | Dependency | Responds to a ping | — | — |
| `rabbitmq` | Dependency | Reports running | — | — |
| `otel-collector` | Telemetry | Container running *(see below)* | — | — |
| `products-migrate` | Migrator | Exits 0 | `sqlserver` healthy | — |
| `baskets-migrate` | Migrator | Exits 0 | `sqlserver` healthy | — |
| `orders-migrate` | Migrator | Exits 0 | `sqlserver` healthy | — |
| `parties-migrate` | Migrator | Exits 0 | `sqlserver` healthy | — |
| `products-api` | Service | `/health/ready` | its migrator succeeded | — |
| `baskets-api` | Service | `/health/ready` | its migrator succeeded | — |
| `orders-api` | Service | `/health/ready` | its migrator succeeded | — |
| `parties-api` | Service | `/health/ready` | its migrator succeeded | — |
| `bff-api` | Service | `/health/ready` | the four domain services healthy | — |
| `gateway-api` | Service | `/health/live` | `bff-api` healthy | **5300** |
| `storefront` | Storefront | Serves `index.html` | — | **4173** |

Fifteen components. Four migrators run once and exit; the rest stay up.

**Why `gateway-api` gates on liveness, not readiness**: it owns no database, and its readiness check
is deliberately empty — spec 002 decided that making the gateway's readiness depend on the BFF would
pull it out of rotation during a downstream outage, when its job is to still answer and return a
clear error. Compose expresses that dependency instead.

**Why the storefront waits for nothing**: it is static files. The browser calls the gateway
afterwards, so a storefront that loads before the backend is ready shows its own error state — which
is behaviour spec 004 already covers (FR-012).

---

## Health Gate

The condition that makes FR-002 real: the command must not report success while anything is still
starting, unhealthy, or failed.

| Gate kind | Applies to | Passes when |
|---|---|---|
| Command probe | `sqlserver`, `redis`, `rabbitmq` | The dependency's own CLI reports it serving |
| HTTP probe | Every service, and the storefront | `/health/ready` (or `/health/live`) answers 200 |
| Completion | Migrators | The container exits with status 0 |
| Running only | `otel-collector` | The container reaches a running state |

**The collector's gate is weaker than the rest, and that is a limitation rather than a choice.**
Its image is distroless — no shell, no probe tool — so there is nothing to run a health check with,
and Compose's `--wait` falls back to "running" for a container that declares none. Verified at
implementation time: the other three report `(healthy)` and the collector reports only `Up`.

In practice the container exiting on a bad config is what catches the realistic failure, and the
collector's ports were confirmed accepting connections on the network. But a collector that started
and then stopped serving would satisfy this gate, which the three command-probed dependencies would
not.

**The HTTP probes are the platform's own.** Each domain service's readiness check opens a real
connection to its own database, so a service reports ready only when it can actually reach its data
(spec 001). Reusing them means the stack has one notion of healthy rather than two that can
disagree.

**Why not a TCP connect**: it would report every service healthy the moment it binds a port,
including one whose database is unreachable — the silent success the ticket's third test scenario
exists to catch.

---

## Stack Data

What survives between runs, and what does not.

| Store | Holds | Survives `down` | Removed by reset |
|---|---|---|---|
| SQL Server volume | The four service databases: catalog, baskets, orders, parties | Yes (FR-007) | Yes (FR-008) |
| RabbitMQ volume | Broker state | Yes | Yes |
| Redis | Nothing yet | N/A — no volume | N/A |
| Built images | Compiled services and storefront | Yes | Optional |

**The seeded catalog is not a separate concern.** It arrives through the products migration
(spec 004, FR-018), so it is created by the same step that creates the schema and removed by the
same reset. There is no seeding command to forget.

**Redis has no volume on purpose.** It holds nothing today; when the basket moves to it, a volume is
the change that makes basket contents survive a restart, and that decision belongs to the story that
makes the move.

---

## Configuration

| Value | Source | Notes |
|---|---|---|
| Database password | `.env`, copied from `.env.example` unedited | The only manual configuration step (SC-002) |
| Service connection strings | Compose environment, one per service | Each names its own database; none names another's (FR-018) |
| OTLP endpoint | Compose environment | Points at the collector (research Decision 10) |
| Storefront backend origin | Build argument | Baked into the image; must be the host address, not the compose hostname (research Decision 6) |
| Gateway allowed origins | Compose environment | Must include the storefront's published origin (research Decision 7) |

Nothing above is written into an image except the storefront's origin, which Vite inlines at build
time and which research Decision 6 records as a limitation rather than a design.

---

## Startup sequence

```text
sqlserver ─┬─► products-migrate ──► products-api ─┐
           ├─► baskets-migrate  ──► baskets-api  ─┤
           ├─► orders-migrate   ──► orders-api   ─┼─► bff-api ──► gateway-api
           └─► parties-migrate  ──► parties-api  ─┘

redis, rabbitmq, otel-collector ──► (no dependants yet)
storefront ──► (independent; the browser reaches the gateway)
```

Each arrow is a gate, not an ordering hint: a migrator that fails stops its service, a service that
never becomes healthy stops the BFF, and `--wait` returns non-zero for any of it (FR-002, FR-010).
