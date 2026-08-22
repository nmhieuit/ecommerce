# Research: Consumer-Driven Contract Tests Across BFF/Service Boundaries

## Decision 1: Contract testing library — PactNet

**Decision**: Use `PactNet` `5.0.1` (latest stable on NuGet at time of research) for both the HTTP
boundaries and the event-pilot boundary, pinned in `Directory.Packages.props`.

**Rationale**: ADR-0006 already accepted Pact as the platform's consumer-driven contract testing
tool, specifically because "producer's build fails on a broken contract" is Pact's exact core
workflow, including consumer-dependency tracking that a hand-built harness would have to reinvent.
This feature operationalizes that decision; re-litigating the tool choice here would contradict an
already-accepted ADR. `5.0.1` is the current stable release (confirmed against the NuGet
flat-container index: `2.x`–`5.0.1`, no newer stable tag); it ships both the HTTP-pact DSL
(`PactBuilder`, `IPactBuilderV3`) and the message-pact DSL needed for the event pilot.

**Alternatives considered**: A custom XUnit + JSON Schema harness (ADR-0006 Option B) — already
rejected by the ADR for the same reason given there (reinventing consumer-dependency tracking);
not revisited here.

## Decision 2: File-based Pact exchange; no Pact Broker in this feature

**Decision**: Consumer-side tests write Pact JSON files directly to a committed, repo-root `pacts/`
directory (one file per boundary). Provider-side verification tests point `PactVerifier` at that
local file path. No Pact Broker is stood up as part of this feature.

**Rationale**: ADR-0006 lists three action items: (1) stand up a self-hosted Pact Broker, (2) pilot
HTTP-pact on one boundary, (3) pilot message-pact on one event boundary. This feature is the vehicle
for (2) and (3) — spec.md scopes it to the four thin-slice boundaries and does not name broker
infrastructure as a requirement. Pact's file-based workflow (`PactDirectory` on the consumer side,
a local file path on `PactVerifier.ServiceProvider(...).WithFileSource(...)` on the provider side)
delivers the exact guarantee Principle III requires — a producer's own build verifies against a real
consumer expectation and fails when it diverges — without a broker as a prerequisite. It also gives
User Story 3's audit a single, greppable directory instead of requiring network access to a broker
to answer "which boundaries have contract-test coverage." Standing up and operating a Broker service
is real infrastructure work (deployment, availability, CI network access) that belongs to ADR-0006
Action Item 1 as its own scoped effort, not an implicit dependency of this feature.

**Alternatives considered**: Standing up a Pact Broker (e.g. via `docker-compose.deps.yml`, mirroring
the SQL Server/Redis/RabbitMQ container pattern) as part of this feature — rejected as scope creep
beyond the four boundaries spec.md defines; would also block this feature on broker operational
concerns (persistence, availability in CI) unrelated to proving the contract-verification mechanism
itself works. Publishing pact files as CI build artifacts instead of committing them — rejected
because it makes User Story 3's audit (SC-003: "in under 5 minutes, without reading service source
code") depend on finding the right CI run rather than reading the repository directly.

## Decision 3: Event pilot invokes the payload-construction path directly — no MassTransit

**Decision**: The `BasketCheckedOut` event pilot's provider-side (baskets) test calls whatever
function/method would construct a `BasketCheckedOutV1` from a real checkout, and verifies that
constructed payload against the pact `orders` defines. Consumer-side (orders), the pact defines the
expected message shape without any live subscription. No `MassTransit` package reference or message
bus is introduced anywhere.

**Rationale**: Confirmed in `shared/EventContracts/README.md` and ADR-0011: no service currently
publishes or consumes `BasketCheckedOut` or `OrderPlaced` — checkout is synchronous BFF
orchestration today (ADR-0011), and `010-testcontainers-integration-tests` explicitly declined to
introduce MassTransit for the same reason ("nothing for MassTransit to abstract" — Decision 6 in
that feature's research.md). Introducing MassTransit here, ahead of SCRUM-31's outbox/publisher
work, would be scope creep this feature's spec.md Assumptions explicitly rule out. Pact's
message-pact feature is designed exactly for this: it verifies a message *payload* against an
expectation, independent of the transport that will eventually carry it.

**Alternatives considered**: Deferring the event boundary entirely until SCRUM-31 wires the broker —
rejected because spec.md (reflecting the Jira acceptance criteria) explicitly names the event
boundary as one of the four the thin slice must cover now, and ADR-0006 Action Item 3 calls for
piloting the message-pact adapter on one event boundary as separate, unblocked work. Building a
throwaway MassTransit bus purely inside the test — rejected for the same reason
`010-testcontainers-integration-tests` rejected it: it would test MassTransit's own harness behavior,
not the contract, and would need to be redone once a real publisher/consumer exists.

## Decision 4: Event pilot pair — `baskets` (producer) ↔ `orders` (consumer) for `BasketCheckedOut`

**Decision**: Pilot the event boundary on `BasketCheckedOut`, with `baskets` as the eventual
publisher and `orders` as the eventual consumer, rather than `OrderPlaced`.

**Rationale**: Both ADR-0011 (checkout orchestration) and the existing `EventContracts` library
already describe `BasketCheckedOut` as the event that would let `orders` react to a checkout
asynchronously, and both `baskets` and `orders` already exist as services within this feature's own
thin slice (unlike whatever future service would consume `OrderPlaced` — logistics/invoices, neither
of which exists yet). Piloting on a boundary where both real services already exist keeps the
provider-side test grounded in real code (`baskets`' actual checkout logic) rather than a synthetic
stand-in.

**Alternatives considered**: `OrderPlaced` — rejected for this pilot since no consuming service
exists yet in the repository to host a consumer-side pact definition; would require inventing a
placeholder consumer, which proves less than piloting against two real services.

## Decision 5: Provider verification runs in-process — no Testcontainers/Docker dependency

**Decision**: HTTP provider verification hosts the service under test via
`Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory` (already a dependency of every
`*.Api.IntegrationTests` project) rather than a full Testcontainers-backed environment.

**Rationale**: Pact's `PactVerifier` needs a running HTTP endpoint to replay the consumer's expected
requests against. `WebApplicationFactory` already gives every service's test suite an in-process
host; nothing about contract verification (checking response *shape* against an expectation) 
requires a real SQL Server/Redis/RabbitMQ backing it, unlike the integration tests in
`010-testcontainers-integration-tests` that explicitly need real infrastructure to prove
persistence/caching/messaging behavior. Where a route's response depends on seeded data, the
provider test seeds that data the same way the existing `*.Api.IntegrationTests` projects already
do for their in-process host.

**Alternatives considered**: Running each provider verification against a fully containerized
instance of the service (matching the constitution's "no in-memory substitutes" rule for
*integration* tests) — rejected because that rule targets tests proving real infrastructure
behavior; a contract test proves response *shape* against a documented expectation, a narrower claim
`WebApplicationFactory` is sufficient for, and reserving Testcontainers for cases that need it keeps
contract-test runtime fast enough to run on every build (FR-005/FR-006 require this to happen in the
producer's own build, not a separate slow pipeline stage).
