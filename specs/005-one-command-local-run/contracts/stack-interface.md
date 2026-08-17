# Contract: The local stack's interface

**Feature**: 005-one-command-local-run · **Status**: Design-time contract · **Date**: 2026-08-17

This feature exposes no HTTP API. Its interface is what a contributor types, what they can reach,
and what they must configure — so that is what this contract fixes. Changing anything here changes
what a person has to relearn, which is the cost this feature exists to remove.

## Commands

| Command | Does | Guarantees |
|---|---|---|
| `./scripts/up.ps1` · `./scripts/up.sh` | Builds and starts the whole stack | Returns only when every component is healthy; non-zero if any is not (FR-002, FR-010) |
| `./scripts/down.ps1` · `./scripts/down.sh` | Stops the stack | Every container removed, every port released; data kept (FR-006, FR-007) |
| `./scripts/reset.ps1` · `./scripts/reset.sh` | Stops the stack and discards data | Next start behaves as a first run, seed catalog included (FR-008) |

All three delegate to Docker Compose against the repository's default `docker-compose.yml`, so
`docker compose up --build --wait`, `docker compose down`, and `docker compose down -v` remain
equivalent for anyone who prefers them. The scripts exist for the prerequisite checks Compose cannot
do (research Decision 2).

### Prerequisite failures

The start command fails before touching Compose, naming exactly one missing thing (FR-011):

| Condition | Message names |
|---|---|
| Docker not installed | Docker, and where to get it |
| Docker daemon not responding | The daemon, and that it needs starting |
| `.env` absent | The file, and the template to copy |
| Daemon memory below the documented floor | The floor, and what is currently allocated |

## Addresses

| What | Address | Notes |
|---|---|---|
| Storefront | `http://localhost:4173` | The URL the documentation tells you to open (FR-004) |
| Platform entry point | `http://localhost:5300` | The only backend address the storefront uses (FR-005) |

**Nothing else is published.** The services, the database, the broker, the cache, and the collector
are reachable only on the Compose network. A contributor cannot call the BFF or a domain service
directly, because there is no address to call — which is spec 004's SC-010 enforced by the
environment rather than asserted by a test (research Decision 8).

### The `debug` profile

`docker compose --profile debug up` additionally publishes the internal services on their existing
development ports (BFF 5301, products 5088, baskets 5188, orders 5041, parties 5204), plus the
broker's management interface. Needed when regenerating the API client from the BFF's OpenAPI
document while the stack — rather than the per-service workflow — is what is running.

## Configuration

| Variable | Set in | Purpose |
|---|---|---|
| `MSSQL_SA_PASSWORD` | `.env` (copied from `.env.example`, unedited) | The local database password. The only value a contributor touches |

Everything else is set by Compose and needs no contributor input: per-service connection strings,
the OTLP endpoint, the gateway's allowed origins, and the storefront's backend origin.

**`.env` is local-only.** It is git-ignored, its template says so, and it is never the path a real
secret takes — deployed environments inject `ConnectionStrings__<Service>Db` from the cluster secret
store (constitution Principle VI).

## Health

Every component answers a health question, and the start command waits on all of them
([data-model.md](../data-model.md) — Health Gate). The services' probes are the platform's own
`/health/live` and `/health/ready`, whose response shape is fixed by
[`specs/001-scaffold-service-shells/contracts/health-check.md`](../../001-scaffold-service-shells/contracts/health-check.md).
This feature adds no new health contract; it consumes the one that exists.

## What this contract does not promise

- **That the broker and cache do anything.** Both run and both report healthy; no service connects
  to either (spec FR-017, Clarifications). "Available", not "working".
- **That the images are deployable.** The storefront's backend origin is baked in at build time and
  points at a host address, so its image is specific to this local stack (research Decision 6).
- **Anything about Kubernetes, CI, or a deployed environment.** Out of scope (spec Assumptions).
