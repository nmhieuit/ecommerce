# Ecommerce SDLC Practice Platform — Product Roadmap

**Purpose**: a solo-operator exercise where one person rotates through Product Owner, Developer, QA, DevOps, and SRE to practice the full SDLC end to end, built on the architecture defined in [constitution.md](../.specify/memory/constitution.md) (v1.0.0).

**Strategy**: thin slice first. One product flow — **browse → basket → checkout → order** — is pushed through a full walking skeleton, then wrapped in one new SDLC discipline per phase, rather than building all six services in parallel. A second flow (logistics/invoices) is deliberately parked until the pattern is proven once.

---

## Phase 1 — Walking Skeleton
**Role focus**: Product Owner + Developer
**Goal**: the ugliest possible version of the flow, deployed and demoable. No meaningful test coverage or hardening yet — this phase proves the path exists.

Epic: [`SCRUM-5`](https://nmhieuit.atlassian.net/browse/SCRUM-5) — Walking Skeleton: Browse → Basket → Checkout → Order

Stories:
- [`SCRUM-10`](https://nmhieuit.atlassian.net/browse/SCRUM-10) Write a one-pager scoping the thin slice: one product, one basket, one order, one tenant (PO)
- [`SCRUM-11`](https://nmhieuit.atlassian.net/browse/SCRUM-11) Scaffold `parties`, `products`, `baskets`, `orders` service shells using vertical-slice structure
- [`SCRUM-12`](https://nmhieuit.atlassian.net/browse/SCRUM-12) Stub identity: single fake user, but **resolve a real tenant context** (hardcoded to one tenant) so no code path ever touches persistence without it — this is intentionally not deferred to Phase 3
- [`SCRUM-13`](https://nmhieuit.atlassian.net/browse/SCRUM-13) Wire gateway → BFF routing for the three services
- [`SCRUM-14`](https://nmhieuit.atlassian.net/browse/SCRUM-14) Minimal React SPA: product list, add-to-basket, checkout button, order confirmation
- [`SCRUM-15`](https://nmhieuit.atlassian.net/browse/SCRUM-15) Get the whole skeleton runnable locally via one command, containers included
- [`SCRUM-16`](https://nmhieuit.atlassian.net/browse/SCRUM-16) Demo: place one order end to end

---

## Phase 2 — Contract & Test Discipline
**Role focus**: Developer + QA
**Goal**: retrofit the principles the skeleton skipped — contracts before code, red-green-refactor, real integration tests.

Epic: [`SCRUM-6`](https://nmhieuit.atlassian.net/browse/SCRUM-6) — Contracts and Test-First Retrofit

Stories:
- [`SCRUM-17`](https://nmhieuit.atlassian.net/browse/SCRUM-17) Write OpenAPI specs for the BFF routes covering products/baskets/orders; generate client code from them
- [`SCRUM-18`](https://nmhieuit.atlassian.net/browse/SCRUM-18) Define event schemas (`OrderPlaced`, `BasketCheckedOut`) in a shared, versioned contracts location
- [`SCRUM-19`](https://nmhieuit.atlassian.net/browse/SCRUM-19) Retrofit TDD for basket pricing and order-creation logic: failing test first, then implementation
- [`SCRUM-20`](https://nmhieuit.atlassian.net/browse/SCRUM-20) Add integration tests against real dependencies via Testcontainers (SQL Server, Redis, RabbitMQ)
- [`SCRUM-21`](https://nmhieuit.atlassian.net/browse/SCRUM-21) Add consumer-driven contract tests across each BFF/service boundary
- [`SCRUM-22`](https://nmhieuit.atlassian.net/browse/SCRUM-22) Wire the SonarQube quality gate into the build and make it a merge blocker

---

## Phase 3 — Secure & Observable
**Role focus**: DevOps
**Goal**: replace stubs with the real security and observability posture the constitution requires.

Epic: [`SCRUM-7`](https://nmhieuit.atlassian.net/browse/SCRUM-7) — Real Auth, Deny-by-Default, Full Observability

Stories:
- [`SCRUM-23`](https://nmhieuit.atlassian.net/browse/SCRUM-23) Stand up the identity server; replace the Phase 1 fake user with real token issuance
- [`SCRUM-24`](https://nmhieuit.atlassian.net/browse/SCRUM-24) Add deny-by-default authorization policies to every endpoint and message handler
- [`SCRUM-25`](https://nmhieuit.atlassian.net/browse/SCRUM-25) Emit OpenTelemetry traces/metrics/logs via the shared ServiceDefaults component to Elastic
- [`SCRUM-26`](https://nmhieuit.atlassian.net/browse/SCRUM-26) Propagate a correlation ID from the edge through every service, message, and the frontend
- [`SCRUM-27`](https://nmhieuit.atlassian.net/browse/SCRUM-27) Move all secrets to the cluster secret store; remove anything hardcoded or baked into images
- [`SCRUM-28`](https://nmhieuit.atlassian.net/browse/SCRUM-28) Add liveness/readiness probes to every service

---

## Phase 4 — Resilience & Performance
**Role focus**: SRE
**Goal**: turn the constitution's performance budgets from aspiration into something measured and enforced.

Epic: [`SCRUM-8`](https://nmhieuit.atlassian.net/browse/SCRUM-8) — Budgets, Timeouts, and Failure Injection

Stories:
- [`SCRUM-29`](https://nmhieuit.atlassian.net/browse/SCRUM-29) Declare SLOs (latency, error rate, availability) per service in its manifest
- [`SCRUM-30`](https://nmhieuit.atlassian.net/browse/SCRUM-30) Add explicit timeouts plus retry/circuit-breaker policies to every outbound call via `Microsoft.Extensions.Resilience`
- [`SCRUM-31`](https://nmhieuit.atlassian.net/browse/SCRUM-31) Verify the transactional outbox pattern on the order publisher — kill the process mid-publish and confirm no divergence
- [`SCRUM-32`](https://nmhieuit.atlassian.net/browse/SCRUM-32) Run a load/performance test against the constitution's stated budgets (p95/p99 per endpoint class)
- [`SCRUM-33`](https://nmhieuit.atlassian.net/browse/SCRUM-33) Audit for unbounded queries, missing pagination, and N+1 access patterns
- [`SCRUM-34`](https://nmhieuit.atlassian.net/browse/SCRUM-34) Chaos exercise: kill a pod or inject latency and confirm the circuit breaker and dashboards behave as expected

---

## Phase 5 — Operate
**Role focus**: SRE
**Goal**: practice the parts of SRE that only show up once something is live — incidents, error budgets, safe rollback.

Epic: [`SCRUM-9`](https://nmhieuit.atlassian.net/browse/SCRUM-9) — Incident Response and Error-Budget Policy

Stories:
- [`SCRUM-35`](https://nmhieuit.atlassian.net/browse/SCRUM-35) Define an error-budget policy and alerting thresholds tied to the Phase 4 SLOs
- [`SCRUM-36`](https://nmhieuit.atlassian.net/browse/SCRUM-36) Deliberately trigger an incident (inject a bug or outage) and run a real on-call response
- [`SCRUM-37`](https://nmhieuit.atlassian.net/browse/SCRUM-37) Write a blameless postmortem and file the resulting follow-up tickets
- [`SCRUM-38`](https://nmhieuit.atlassian.net/browse/SCRUM-38) Practice toggle-gated delivery: ship a change behind a feature flag, then disable it without a redeploy
- [`SCRUM-39`](https://nmhieuit.atlassian.net/browse/SCRUM-39) Run a quarterly-style constitutional compliance review against the live system

---

## Later / Parked
- Second flow: logistics + invoices (post-order fulfillment path)
- True multi-tenant UI and tenant-isolation tests — Phase 1 stubs a single resolved tenant; this expands to real tenant switching and isolation verification, not a retrofit of the concept itself
- Mobile-web client parity
- Shared design-system package extraction
- Broader ADR backlog for architecturally significant decisions

---

## Jira Mapping Notes
- Each phase → one **Epic**.
- Each bullet → one **Story** (infra-only bullets can be **Task** instead, PM's call).
- Suggested labels: `role:po`, `role:dev`, `role:qa`, `role:devops`, `role:sre`, `phase-1`…`phase-5`.
- Suggested components: `parties`, `products`, `baskets`, `orders`, `gateway`, `bff`, `web`.
- Sequencing is loose, not strictly gated: Phase *N* stories generally assume Phase *N-1*'s epic is "walking," not "done." Real teams don't fully close an epic before starting the next; neither should this exercise.

## Riskiest Assumption
That stubbing tenant resolution to a single hardcoded tenant in Phase 1 won't cause rework later. The constitution treats tenant isolation as a security boundary (Principle V), so the mitigation is architectural, not scheduling: Phase 1 still resolves a real tenant context end to end, just from a hardcoded source — Phase 3/Later upgrades *where the tenant comes from*, not *whether it's resolved at all*.

## Jira Status
All 5 epics and 30 stories are created in the **SCRUM** project (`Product MVPs`) at [nmhieuit.atlassian.net](https://nmhieuit.atlassian.net/jira/software/projects/SCRUM/boards). Every story carries acceptance criteria (Given/When/Then) and test scenarios in its description, plus `role-*` and `phase-*` labels for filtering.

## Next Step
Work Phase 1 (`SCRUM-5`) first — everything else in the board assumes it's walking before it's picked up.
