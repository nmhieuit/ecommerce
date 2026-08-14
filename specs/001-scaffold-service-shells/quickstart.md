# Quickstart: Validating the Service Shells

Validates spec.md's success criteria (SC-001 through SC-004) end-to-end, without requiring the gateway, BFF, or any other service to be running.

## Prerequisites

- .NET 10 SDK installed
- Docker running (for the service's own SQL Server instance and for the Testcontainers-backed integration test suite)
- A fresh clone of the repository

## Validate SC-001 and SC-002: each service runs alone and reports healthy

Repeat for each of the four services (`parties`, `products`, `baskets`, `orders`) — run **one at a time**, with the others stopped, to prove independence:

1. From `services/<service-name>/src/<ServiceName>.Api`, start the service's own database dependency (container) and the service itself using the service's local-run command.
2. Confirm the process reports ready within the timing target: `curl http://localhost:<port>/health/live` → `200 OK`.
3. Confirm the readiness probe passes once the database is reachable: `curl http://localhost:<port>/health/ready` → `200 OK` with `"self-database": "Healthy"`.
4. Stop the service's database dependency only (leave the service process running) and re-check `/health/ready` → expect `503` with `"self-database": "Unhealthy"` — proves readiness reflects real connectivity, not just process liveness (spec FR-003, Edge Cases).
5. Time steps 1–3 from a stopwatch started at `git clone` — expect under 5 minutes (SC-001).

Expected result: all four services pass steps 1–4 independently, with no service requiring another to be running (SC-002).

## Validate SC-003: no cross-service data access is possible

For each service, inspect its configuration/connection string and confirm:

1. It references only its own database/schema.
2. No other service's connection string, credential, or shared data-access assembly is reachable from this service's code or runtime configuration.
3. Attempt (as a manual check, or via the integration test suite) to open a connection from one service's process to another service's database using only what that service has been given — confirm it fails (no route, no credential).

## Validate SC-004: code is organized by feature, not by technical layer

For any one service, open `services/<service-name>/src/<ServiceName>.Api/Features/` and confirm the health-check capability's handler, registration, and any related code live together in one folder — not split across separate top-level `Controllers/`, `Services/`, `Repositories/` folders.

## Running the automated test suites

- Unit tests: from `services/<service-name>/tests/<ServiceName>.Api.UnitTests`, run the standard test command — these must include a test that was written and observed failing before the health-check handler existed (Principle III).
- Integration tests: from `services/<service-name>/tests/<ServiceName>.Api.IntegrationTests`, run the standard test command — these spin up a real SQL Server via Testcontainers and assert the readiness probe's actual database-connectivity behavior described above, not a mocked substitute (Principle III forbids in-memory/fake substitutes here).

## Out of scope for this quickstart

- Running all four services together with one command — that's SCRUM-15.
- Any tenant-specific behavior — tenant resolution isn't implemented yet (SCRUM-12).
- Any business/domain endpoint beyond the health check.
