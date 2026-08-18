# Quickstart: Validate the One-Command Local Run

Validates this feature against [spec.md](./spec.md)'s acceptance scenarios and success criteria. The
command surface and addresses are fixed by
[contracts/stack-interface.md](./contracts/stack-interface.md); the component inventory and its gates
are in [data-model.md](./data-model.md).

Unusually for a quickstart, **the setup section below _is_ the feature**. If it takes more than the
two steps it lists, the feature has failed.

## Prerequisites

- Docker installed and running, with at least the documented memory allocated to the daemon.
- Nothing else. No .NET SDK, no Node, no pnpm — that is the point.

## Setup

```bash
cp .env.example .env       # step 1: no editing required
./scripts/up.ps1           # step 2: or ./scripts/up.sh
```

Then open **http://localhost:4173**.

That is the whole of it (SC-002). Anything you had to do beyond these two steps is a defect in this
feature, not a missing instruction.

---

## Scenario 1 — First run from a clean checkout (spec US1, SC-001)

1. On a machine that has never built this repository, run the two steps above and time them.
2. **Expect**: the command returns success, and the storefront is usable, in **under 10 minutes**.
3. **Expect**: the command did not return until everything was healthy — no "wait a moment for the
   services to come up" (FR-002).

```bash
docker compose ps
```

4. **Expect**: fifteen components listed, in exactly these states
   ([data-model.md](./data-model.md) — the inventory):

   | Count | State | Which |
   |---|---|---|
   | 10 | `Up (healthy)` | `sqlserver`, `redis`, `rabbitmq`, the four domain services, `bff-api`, `gateway-api`, `storefront` |
   | 1 | `Up` — no healthcheck | `otel-collector`, whose distroless image has no probe tool |
   | 4 | `Exited (0)` | the migrators, which run once and stop |

   The collector is the one component whose gate is "running" rather than "healthy". That is a
   limitation of its image, recorded rather than hidden — everything else must say `healthy`.

**Subsequent-run check (SC-001)**: `./scripts/down.ps1` then `./scripts/up.ps1` again — **under
3 minutes**, because images are already built.

## Scenario 2 — The storefront works end to end (spec US2, SC-003)

1. Open `http://localhost:4173`.
2. **Expect**: three products, each with a price — Field Notes Notebook $12.50, Ceramic Pour-Over Set
   $48.00, Linen Apron $34.25. Present without any seeding step (FR-003, US2 scenario 4).
3. Add the notebook twice and the apron once, then open the basket.
4. **Expect**: two lines, the notebook at quantity 2, total **$59.25**.
5. Refresh the page.
6. **Expect**: the basket is unchanged — and the page loads at all. A container serving static files
   without history fallback answers 404 here (research Decision 5).
7. Check out.
8. **Expect**: a confirmation showing the order's identifier and $59.25.
9. Open the browser's network tab and repeat the flow.
10. **Expect**: every request goes to `localhost:5300`. Zero to any other address (FR-005).

## Scenario 3 — Stop and restart cleanly (spec US3, SC-004)

```bash
./scripts/down.ps1                              # or ./scripts/down.sh
docker ps -a --filter "name=ecomerce-stack"     # expect: nothing
```

Filter on `ecomerce-stack`, not `ecomerce`: the stack runs under its own Compose project name so
that stopping it cannot tear down `docker-compose.deps.yml`'s containers, which are named
`ecomerce-*`. A broader filter would report those as leftovers of this stack, which they are not.

1. **Expect**: zero containers under the `ecomerce-stack` project, and ports 4173 and 5300 free.
2. **Expect**: both volumes still present — `docker volume ls` shows `ecomerce-stack_sqlserver-data`
   and `ecomerce-stack_rabbitmq-data`. Stopping keeps data; only the reset command discards it
   (FR-007, FR-008).
3. Start again, and open the storefront.
4. **Expect**: the order placed in Scenario 2 is still readable through the gateway, and the basket
   state is as you left it (FR-007).
5. Repeat the stop/start cycle **10 times**. **Expect**: no orphaned containers and no port
   conflicts on any cycle (SC-004).

## Scenario 4 — Reset returns to a first run (spec FR-008, SC-008)

```bash
./scripts/reset.ps1
./scripts/up.ps1
```

1. **Expect**: the storefront shows the three seeded products and an empty basket.
2. **Expect**: the order from Scenario 2 is gone.

## Scenario 5 — A missing dependency fails obviously (ticket test scenario 3, SC-005)

1. Start the stack, then stop the database container by hand:

   ```bash
   docker compose stop sqlserver
   ```

2. Restart the stack without it and **expect** the command to fail within 2 minutes, naming the
   component — not to report success and fail later at first use.
3. Restore it and confirm the stack recovers.

**The stricter version**: temporarily remove a service's migrator from the Compose file and start.
**Expect** that service to fail rather than start against a database with no schema.

## Scenario 6 — A source change is actually running (spec FR-009)

1. With the stack up, change a visible string in the storefront — the catalog heading, say.
2. `./scripts/up.ps1` again.
3. **Expect**: the new text on screen. A stale image silently reused is the failure this catches.

## Scenario 7 — Prerequisites are named, not guessed (spec FR-011)

| Do this | Expect |
|---|---|
| Rename `.env` away and start | Fails naming `.env` and the template, before any container starts |
| Stop the Docker daemon and start | Fails naming the daemon |

Neither may produce a partial stack.

## Scenario 8 — Only the entry point is reachable (research Decision 8)

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5300/bff/products   # expect 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5301/bff/products   # expect refused
```

**Expect**: the gateway answers and the BFF is unreachable from the host. Then confirm the `debug`
profile publishes it when asked:

```bash
docker compose --profile debug up -d
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5301/bff/products   # expect 200
```

---

## Automated checks

```bash
# The acceptance test: 004's walkthrough, pointed at the containerized stack.
cd frontend/apps/web
STOREFRONT_URL=http://localhost:4173 GATEWAY_ORIGIN=http://localhost:5300 pnpm exec playwright test
```

The walkthrough already parameterises both addresses, so this needs no new test — only different
values (research Decision 12). It asserts the flow, zero console errors, single-origin traffic, the
double-checkout guard, and keyboard-only completion, all against containers.

```bash
# Container conventions: every service image copies every shared project its csproj references.
dotnet test tests/ContainerConventionTests

# Compose file validity.
docker compose config --quiet
```

The convention test is the one that matters most here: it is what would have caught five service
images being unbuildable since 003 (research, Finding).
