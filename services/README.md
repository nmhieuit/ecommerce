# Services

Four independently deployable microservice shells — `parties`, `products`, `baskets`, `orders` —
each owning its own database, its own health probes, and its own container image. None of them
requires any of the others to be running.

| Service | Database | Connection-string key | Local port | Local DB port |
|---|---|---|---|---|
| parties | `parties` | `ConnectionStrings:PartiesDb` | 5204 | 14330 |
| products | `products` | `ConnectionStrings:ProductsDb` | 5088 | 14331 |
| baskets | `baskets` | `ConnectionStrings:BasketsDb` | 5188 | 14332 |
| orders | `orders` | `ConnectionStrings:OrdersDb` | 5041 | 14333 |

## Data isolation is a guarantee, not a convention

Constitution Principle I and spec requirements FR-004/FR-005 say each service owns its data
exclusively: no service may read or write another service's database, and no service may hold a
credential, connection, or shared dependency that would let it try. Spec SC-003 sets the bar higher
than intent — **zero cross-service data access must be *verifiable by a repeatable check***.

Three structural properties make that true, and each one is enforced by something that fails the
build rather than by reviewer vigilance.

### 1. A service is only ever given its own connection string

Each service's `Program.cs` registers exactly one `DbContext`, bound to one service-scoped
configuration key (`PartiesDb`, `ProductsDb`, …). Keys are service-scoped rather than a generic
`DefaultConnection` precisely so that a copy-paste between services fails loudly instead of
silently resolving to whatever the destination service had configured.

`appsettings.json` carries a credential-free string naming only that service's own host and
database; real values are injected at runtime from the cluster secret store as
`ConnectionStrings__<Service>Db` (Principle VI — secrets never live in source, config, or images).

### 2. Nothing is shared between services except telemetry wiring

`shared/ServiceDefaults` is the only cross-service dependency the constitution permits
(Principle VII). It wires OpenTelemetry and correlation IDs and touches no persistence, so there is
no shared data-access assembly through which a connection could leak. Each service's Dockerfile
copies only its own sources, so another service's configuration is not even present in the image.

### 3. There is no fallback path when a service's own database is down

A service whose database is unreachable reports **not ready** and stops there. It does not degrade
to another service's store or to a shared default. This is the failure mode that matters: a silent
fallback would be a cross-tenant, cross-service data leak that looks like a healthy service.

## The enforcement mechanisms

| Check | Location | What it would catch |
|---|---|---|
| Connection-string isolation scan | [`tests/CrossServiceIsolation.Tests/ConnectionStringScanner.cs`](../tests/CrossServiceIsolation.Tests/ConnectionStringScanner.cs) | Any service's configuration naming another service's database, host, or connection-string key |
| No-fallback readiness test | `services/*/tests/*.Api.IntegrationTests/ReadinessTests.cs` | A service becoming ready by reaching a database that isn't its own |

### The connection-string scanner

`ConnectionStringScanner` reads every `appsettings*.json` under `services/` — base files and
per-environment overrides alike, since a leak in an override is exactly as real — and flags a
violation when a service's configuration:

- declares a connection-string key belonging to another service (e.g. `parties` declaring `OrdersDb`);
- names another service's database (`Database=orders` in the parties service); or
- points at another service's host (`Server=orders-db`, with or without a port suffix).

It discovers services from the directory layout rather than a hardcoded list, so `logistics` and
`invoices` are covered the day their folders appear, with no change to the scanner.

Two design choices keep the check from passing for the wrong reason:

- It reads **files, not running services**. A service that merely happens not to use a connection it
  was handed still fails — possession of the credential is the violation, not its use.
- Its result reports **what was examined**, not only what was objected to. A scan that resolved the
  wrong directory or matched no files after a layout change reports zero violations and looks
  identical to genuine isolation, so `ConnectionStringIsolationTests` asserts the scan actually saw
  all four services, and separately feeds the scanner deliberately-broken fixtures to prove it can
  still detect a breach.

Run it with the rest of the suite, or on its own:

```bash
dotnet test tests/CrossServiceIsolation.Tests
```

### The no-fallback readiness test

Each service's `ReadinessTests` starts a real SQL Server via Testcontainers (Principle III — no
in-memory substitutes) and then does something deliberately adversarial: it hands the service an
**unreachable** connection string for its own database while simultaneously offering it a
**perfectly reachable** one under every *other* service's key. The service must still return
`503` with `self-database` unhealthy. Passing means the service ignored a working database that
wasn't its own, which is spec US2's second acceptance scenario stated as an executable assertion.

## Known local-development caveat

`docker-compose.deps.yml` gives all four local database containers the same `MSSQL_SA_PASSWORD`
from a single `.env` value. Isolation between them locally therefore rests on host and port
separation, not on distinct credentials — a developer who deliberately edited a connection string
could reach another local container. This affects local development only: deployed environments
inject a distinct `ConnectionStrings__<Service>Db` per service from the cluster secret store, and
the scanner above fails the build if any committed configuration ever names another service's
database. Worth tightening to per-service local passwords if local realism matters more than the
one-value convenience.

## Running a service

See [`specs/001-scaffold-service-shells/quickstart.md`](../specs/001-scaffold-service-shells/quickstart.md)
for the full walkthrough. In short — start only the one database the service needs, to keep proving
independence:

```bash
cp .env.example .env          # set a local SA password
docker compose -f docker-compose.deps.yml up -d parties-db
dotnet run --project services/parties/src/Parties.Api
curl http://localhost:5204/health/live    # 200
curl http://localhost:5204/health/ready   # 200 once the database is reachable
```

Health-endpoint response shapes are specified in
[`contracts/health-check.md`](../specs/001-scaffold-service-shells/contracts/health-check.md).
