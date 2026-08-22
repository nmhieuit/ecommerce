# Research: Versioned Event Schemas — OrderPlaced, BasketCheckedOut

## Decision 1: Scope to the contract layer only; no broker wiring

**Decision**: Deliver `shared/EventContracts` as a standalone library with its own test suite.
Do not add a `MassTransit` reference, an outbox table, or any publish/consume code to `Orders.Api`,
`Baskets.Api`, or the BFF's checkout flow.

**Rationale**: [ADR-0011](../../docs/adr/0011-checkout-orchestration.md) already investigated this
exact boundary when it accepted synchronous BFF-orchestrated checkout as a time-bound deviation from
Principle IV, and named this feature (SCRUM-18) as covering the schema half of closing that
deviation, with SCRUM-31 covering the transport half ("stand up RabbitMQ + MassTransit and publish
`BasketCheckedOut`" was explicitly evaluated there as Option B and rejected for *that* story only
because the schema didn't exist yet — it becomes the right choice once this feature ships). Building
partial publish wiring now would duplicate work SCRUM-31 exists to do properly, exactly the outcome
ADR-0011's trade-off analysis warned against.

**Alternatives considered**:

- *Also wire `Orders.Api` to construct an `OrderPlacedV1` instance (without publishing it anywhere)
  so the type is "used" by a real service*: rejected — with no consumer and no transport, this is
  dead code with no test that could fail if it drifted, which is worse than no reference at all.
- *Stand up MassTransit + RabbitMQ + outbox now, folding SCRUM-31 into this feature*: rejected —
  multiplies this feature's size several times over (per ADR-0011's own complexity assessment of
  that option) and contradicts the roadmap's explicit phase split.

## Decision 2: JSON Schema 2020-12, hand-authored, C# record mirrors it

**Decision**: For each event version, author a JSON Schema document (`*.v1.schema.json`) by hand as
the reviewable contract artifact, and a matching C# record (`OrderPlacedV1`, `BasketCheckedOutV1`)
as the code-level shape services will eventually construct and serialize. A test proves the two
stay in agreement by serializing a real record instance through `System.Text.Json` (the serializer
MassTransit will use in production, per ADR-0005) and validating the resulting JSON against the
schema.

**Rationale**: [ADR-0005](../../docs/adr/0005-event-contract-format.md) already chose JSON Schema
over Avro/Protobuf specifically for low complexity, no new infrastructure, and `System.Text.Json`
compatibility. Hand-authoring the schema (rather than generating it from the C# type) keeps the
schema itself as the artifact a reviewer reads and approves before implementation — the literal
meaning of "contract-first" — rather than treating it as a build byproduct of the type. For two
events with a handful of fields each, the drift risk between a hand-written record and a hand-written
schema is small and is exactly what the round-trip validation test in Decision 2 exists to catch.

**Alternatives considered**:

- *Generate the JSON Schema from the C# type at build time (e.g. a source generator or reflection
  tool)*: rejected — adds a new tool/dependency for a two-event, low-change-frequency surface, and
  inverts contract-first into code-first-with-a-schema-export. Revisit if the event count grows
  large enough that hand-authored schemas become a maintenance burden.
- *Generate the C# record from the JSON Schema*: rejected for the same reason in the other
  direction, and .NET codegen from JSON Schema is a rougher, less idiomatic path than Orval-for-
  OpenAPI (ADR-0004) — there's no equivalently mature tool for this platform's stack.

## Decision 3: Published schema versions are immutable, enforced by a frozen-content test

**Decision**: Once `OrderPlaced.v1.schema.json` (or any other version file) is committed, its
content is treated as frozen. `SchemaImmutabilityTests` computes a SHA-256 hash of each committed
schema file and asserts it equals a recorded constant. Any edit to an already-published schema file
— breaking or not — changes the hash and fails the test. The only sanctioned way to change a
schema's shape is to add a new version file (`OrderPlaced.v2.schema.json`) and a new type
(`OrderPlacedV2`), never to edit a published one in place.

**Rationale**: The Jira acceptance criteria and spec FR-006 require that "a schema changes in a
breaking way... is caught," and spec Assumptions leave classifying breaking-vs-non-breaking as an
open policy question this feature is free to resolve simply. Rather than building or importing a
breaking-change classifier (JSON Schema diffing is a genuinely hard problem — required-field
additions, type narrowing, enum restriction, etc. all need separate handling), treating *any*
post-publish edit as a violation is strictly simpler, has zero false negatives for breaking changes,
and its only cost — an occasional false positive on a purely cosmetic edit (e.g. fixing a typo in a
`description` field) — is cheap to pay by bumping a version anyway. This also matches
[ADR-0005](../../docs/adr/0005-event-contract-format.md)'s accepted trade-off that "contract-breakage
safety depends on CI contract tests actually running," not a registry.

A git-history-aware CI gate (the same shape as `frontend/packages/api-client`'s `verify-generated`,
which uses `git status --porcelain` to fail on any drift) was considered and is the natural
long-term home for this check, but no `Jenkinsfile` or other CI pipeline definition exists yet
anywhere in this repository to host it. A self-contained xUnit test that runs the same way in local
`dotnet test` and in whatever CI eventually runs the solution's test projects is the right level of
infrastructure for the platform's current state, and can be lifted into a dedicated CI script later
without changing what it checks.

**Alternatives considered**:

- *Full breaking-vs-non-breaking JSON Schema diff (e.g. adopt a diffing library)*: rejected as
  disproportionate for two events and duplicative of the consumer-driven contract testing effort
  already planned in [ADR-0006](../../docs/adr/0006-contract-testing-tool.md) (SCRUM-21) — that
  story is the right place for consumer-aware compatibility analysis; this feature only needs to
  stop an accidental in-place edit.
- *A git-diff based CI script now, ahead of any pipeline definition existing*: rejected — nothing
  would invoke it yet, so it would be unverifiable dead configuration. Revisit once a Jenkinsfile
  exists in this repository.

## Decision 4: No dedicated naming-convention test

**Decision**: Rely on the type names themselves (`OrderPlacedV1`, `BasketCheckedOutV1`) and the
`README.md` documentation to establish the `{EventName}V{N}` convention from
[ADR-0005](../../docs/adr/0005-event-contract-format.md), rather than adding a reflection-based test
that scans the assembly for names matching the pattern.

**Rationale**: With two types, a convention enforced by a test that only two names could ever
violate is ceremony without safety margin — a reviewer reading a PR that adds `OrderPlacedV2Beta` or
similar will catch it exactly as reliably as a passing test would, and more cheaply. Revisit if the
number of event types grows enough that convention drift becomes plausible to miss in review.

**Alternatives considered**:

- *Reflection-based naming test over the `EventContracts` assembly*: rejected for now per above;
  cheap to add later without touching any other decision here.
