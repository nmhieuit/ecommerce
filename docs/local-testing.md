# Trying the test cases by hand

`docker-compose.local.yml` starts the whole platform with **every port published** and **one SQL
Server per service**, so the failure modes the automated suite asserts can be reproduced by
stopping a container and issuing a request.

Every command and every response on this page was run against the stack described here. Where the
observed behaviour differs from what you might expect from reading the test alone, that is called
out rather than smoothed over.

Test case ids refer to [`test-cases-2026-08-20.xlsx`](test-cases-2026-08-20.xlsx).

---

## Start and stop

```bash
./scripts/local-up.ps1          # or ./scripts/local-up.sh
./scripts/local-down.ps1        # stop, keep data
./scripts/local-down.ps1 -DiscardData   # stop and throw the databases away
```

> **This stack cannot run at the same time as the default one.** It reuses the same ports, so
> `ecomerce-stack` (`docker-compose.yml`) and `ecomerce` (`docker-compose.deps.yml`) collide with
> it on 4173, 5300 and 14330–14333. Run `./scripts/down.ps1` first — `local-up` checks for this and
> refuses with a message naming the other stack rather than letting Compose fail on a port bind.

### Ports

| Component | URL / address | Notes |
|---|---|---|
| Storefront | http://localhost:4173 | the SPA, calls the gateway |
| Gateway | http://localhost:5300 | the only address the storefront uses |
| BFF | http://localhost:5301 | `/openapi/v1.json` published (Development) |
| Products | http://localhost:5088 | |
| Baskets | http://localhost:5188 | |
| Orders | http://localhost:5041 | |
| Parties | http://localhost:5204 | |
| parties-db | `localhost,14330` | `sa` / value of `MSSQL_SA_PASSWORD` in `.env` |
| products-db | `localhost,14331` | |
| baskets-db | `localhost,14332` | |
| orders-db | `localhost,14333` | |
| Redis | `localhost:6379` | nothing connects to it yet |
| RabbitMQ | `localhost:5672` | nothing connects to it yet |
| RabbitMQ UI | http://localhost:15672 | `guest` / `guest` |
| OTel collector | `localhost:4317` (gRPC), `localhost:4318` (HTTP) | traces go to its own log |

Every service runs as `ASPNETCORE_ENVIRONMENT=Development` here, which is what publishes the BFF's
OpenAPI document. `docker-compose.yml` runs them as Production.

---

## Scenario 1 — a service loses its own database, and only its own

**Covers:** TC-BSK-029, TC-BSK-030 · and the same pair for the other services: TC-ORD-024/025,
TC-PTY-005/006, TC-PRD-008/009.

This is the scenario the whole file exists for. With the shared SQL Server of
`docker-compose.yml`, stopping the database takes all four services down at once and "it did not
fall back to another service's database" is unobservable — there is no other database left running
to fall back to.

```bash
# before
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5188/health/ready   # 200
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5088/health/ready   # 200

docker compose -f docker-compose.local.yml stop baskets-db
```

Observed:

```
baskets  :5188 -> 503 in 4.10s
products :5088 -> 200 in 0.03s
orders   :5041 -> 200 in 0.01s
parties  :5204 -> 200 in 0.01s
```

```bash
curl -s http://localhost:5188/health/ready
```

```json
{"status":"Unhealthy","checks":[{"name":"self-database","status":"Unhealthy",
"description":"A network-related or instance-specific error occurred while establishing a
connection to SQL Server. ..."}]}
```

`self-database` is the exact check name TC-BSK-030 asserts on. The three sibling services answering
200 throughout is the other half of that test: Baskets has connection strings for its own database
and nothing else, so there is nothing for it to fall back to.

**Why 4 seconds and not 15.** The connection strings in `docker-compose.local.yml` carry
`Connect Timeout=3;ConnectRetryCount=0`, copied from `appsettings.Development.json`. Without them
the driver retries for the 15-second default and the 503 arrives long after you have stopped
watching for it.

**The container stays up.** `docker compose ps` still shows `baskets-api` as running — it is
answering, with 503. It has no `restart:` policy for exactly this reason; a restarting container
answers nothing. Docker's own health status lags: the healthcheck needs 20 consecutive failures at
5-second intervals before the container is marked `unhealthy`, so trust the HTTP response, not the
`ps` column.

### Bringing it back

**Covers:** TC-BSK-028 (and TC-ORD-023, TC-PTY-004, TC-PRD-007).

```bash
docker compose -f docker-compose.local.yml start baskets-db
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5188/health/ready   # 200
```

**Retry once if the first call still fails.** Observed: a request issued at the exact moment
`baskets-db` reported healthy came back 503 in 0.02 seconds — too fast to have attempted a
connection, i.e. a pooled connection that was still poisoned. The very next request returned 200.
A 503 that persists across several seconds is a real failure; a single instant one on recovery is
the pool draining.

---

## Scenario 2 — a request that never passed the gateway resolved no tenant

**Covers:** TC-PRD-011 · and the same test for the other services: TC-BSK-032, TC-ORD-027,
TC-PTY-008.

Only possible here because every service has a published port. Against `docker-compose.yml` there
is no address with which to call a service directly.

```bash
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5088/products
#   -> 500

curl -o /dev/null -s -w '%{http_code}\n' -H 'X-Tenant-Id: contoso' http://localhost:5088/products
#   -> 200
```

The 500 is the accepted Phase 1 outcome: the service fails loudly rather than answering 200 with
some default tenant's catalog. With the header, the seeded catalog comes back (TC-PRD-002,
TC-PRD-004):

```json
[{"id":"9f8d6b1e-0001-4000-8000-000000000002","name":"Ceramic Pour-Over Set","price":48.00},
 {"id":"9f8d6b1e-0001-4000-8000-000000000001","name":"Field Notes Notebook","price":12.50},
 {"id":"9f8d6b1e-0001-4000-8000-000000000003","name":"Linen Apron","price":34.25}]
```

### A write with no tenant leaves nothing behind

**Covers:** TC-ORD-028.

The status code proves the caller was told no; the row count proves nothing was written, and those
are different claims.

```bash
# count before, POST with no X-Tenant-Id, count after
curl -o /dev/null -s -w '%{http_code}\n' -X POST -H 'Content-Type: application/json' \
  -d '{"items":[{"productId":"9f8d6b1e-0001-4000-8000-000000000001","quantity":1,"unitPrice":12.50}]}' \
  http://localhost:5041/orders
```

Observed: `HTTP 500`, order count `3` before and `3` after.

Counting rows needs a client on `localhost,14333` — any SQL client works, or:

```bash
docker run --rm mcr.microsoft.com/mssql/server:2022-latest \
  /opt/mssql-tools18/bin/sqlcmd -S host.docker.internal,14333 -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -d orders -Q "SELECT COUNT(*) FROM Orders;"
```

> In Git Bash, prefix that with `MSYS_NO_PATHCONV=1` or `/opt/...` is rewritten to a Windows path
> and the container reports "No such file or directory".

---

## Scenario 3 — a downstream service is gone, the gateway still answers

**Covers:** TC-GTW-021, TC-CMN-025, TC-CMN-027, TC-CMN-028.

```bash
docker compose -f docker-compose.local.yml stop products-api

curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5300/health/live   # 200
curl -s -w '\nHTTP %{http_code} in %{time_total}s\n' http://localhost:5300/bff/products
```

Observed:

```json
{"type":"https://ecommerce.internal/errors/downstream-timeout",
 "title":"Downstream service timed out","status":504,
 "detail":"The 'ProductsApi' service did not respond within its timeout budget.",
 "correlationId":"e5c44fc033bf4457a84cb5af134ba4e7",
 "traceId":"00-2badd9f2ab2d0819b3bdb0dc3a2c2a77-cd99d245066c5a14-01"}
HTTP 504 in 3.01s
```

Three things to read off that, each one an assertion in the suite:

- The gateway's own liveness stays 200 while a service behind it is down (TC-GTW-021). Its
  readiness is deliberately empty so a downstream outage cannot pull the gateway out of rotation —
  its job is to still answer, with a clear error.
- The body is RFC 7807 with a `correlationId` (TC-CMN-027), and it names the **logical** service —
  `ProductsApi` — with no host, scheme, or stack trace (TC-CMN-028).
- 3.01 seconds is the BFF's per-downstream budget, well inside the 5-second bound SC-003 requires.

**504 here, 502 in the test.** TC-CMN-025 expects 502 because it injects a transport failure, which
fails immediately. `docker compose stop` removes the container from Docker's network, so the
connection hangs and the BFF's timeout fires first — 504. Both are correct answers to "the
dependency did not answer", which is why the suite's own
`ADownstreamFailure_IsBoundedAndStructured_AgainstARealUnreachableHost` accepts either. What is
being checked is that the failure is bounded, structured, and names the dependency.

```bash
docker compose -f docker-compose.local.yml start products-api
```

---

## Scenario 4 — the whole purchase, end to end

**Covers:** TC-CMN-019, TC-CMN-020, TC-CMN-021, TC-CMN-022, TC-ORD-013, TC-CMN-013.

Do this in the browser at **http://localhost:4173** — add two Field Notes Notebooks and one Linen
Apron, then check out. The same flow through the gateway with curl, which is what was run to
produce the output below:

```bash
N=9f8d6b1e-0001-4000-8000-000000000001   # Field Notes Notebook, $12.50
A=9f8d6b1e-0001-4000-8000-000000000003   # Linen Apron, $34.25

curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$N\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null
curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$N\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null
curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$A\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null

curl -s http://localhost:5300/bff/basket
```

The two notebooks merged onto one line, and the figure the walkthrough quotes:

```json
{"id":"af715f57-f14a-4e04-9c3c-5e2a4ed22cbb","customerRef":"phase1-stub-user","items":[
 {"productId":"9f8d...0001","name":"Field Notes Notebook","quantity":2,"unitPrice":12.50,"lineTotal":25.00},
 {"productId":"9f8d...0003","name":"Linen Apron","quantity":1,"unitPrice":34.25,"lineTotal":34.25}],
 "total":59.25}
```

```bash
curl -s -X POST http://localhost:5300/bff/checkout
#  {"id":"75f5868c-171e-44c9-aa8b-3cdc1bc6be62","placedAtUtc":"...","total":59.25}

curl -s http://localhost:5300/bff/orders/75f5868c-171e-44c9-aa8b-3cdc1bc6be62
#  same id, same total 59.25            → TC-CMN-020

curl -s http://localhost:5300/bff/basket
#  items [], total 0, and the SAME basket id as before → TC-CMN-021

curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5300/bff/checkout
#  409                                   → TC-CMN-022
```

### The order records its tenant

**Covers:** TC-ORD-013.

The BFF's order shape does not carry the tenant, so read the order straight from the orders
service — which this stack publishes on 5041:

```bash
curl -s -H 'X-Tenant-Id: contoso' http://localhost:5041/orders/75f5868c-171e-44c9-aa8b-3cdc1bc6be62
```

```json
{"id":"75f5868c-...","placedAtUtc":"2026-08-20T14:01:46.66","total":59.25,"tenantId":"contoso"}
```

### A price the client chose is discarded

**Covers:** TC-CMN-013.

```bash
curl -s -X POST -H 'Content-Type: application/json' \
  -d "{\"productId\":\"$N\",\"quantity\":1,\"unitPrice\":0.01}" \
  http://localhost:5300/bff/basket/items
```

Observed: the line comes back at `unitPrice 12.5`, total `12.5`. The BFF resolves the price from the
catalog and ignores whatever the caller said about money.

---

## Scenario 5 — the edge behaviours

**Covers:** TC-GTW-036, TC-GTW-026.

```bash
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5300/no-such-path
#   -> 404, promptly, with no cluster or destination named in the body

curl -s -D - -o /dev/null -X OPTIONS \
  -H 'Origin: http://localhost:4173' -H 'Access-Control-Request-Method: GET' \
  http://localhost:5300/bff/products
```

```
HTTP/1.1 204 No Content
Access-Control-Allow-Credentials: true
Access-Control-Allow-Methods: GET
Access-Control-Allow-Origin: http://localhost:4173
```

An origin nobody configured gets no `Access-Control-Allow-Origin` at all — try it with
`-H 'Origin: http://evil.example'`.

---

## Resetting between runs

The basket and the orders persist in the four database volumes, so a scenario that assumes an empty
basket will not behave as written on a second run. Either check out to empty it, or start over:

```bash
./scripts/local-down.ps1 -DiscardData    # ./scripts/local-down.sh --discard-data
./scripts/local-up.ps1
```

The catalog is seeded by the migrations, so a fresh start always has the three products back.
