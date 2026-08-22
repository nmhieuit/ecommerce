# Implementation Plan: Versioned Event Schemas — OrderPlaced, BasketCheckedOut

**Branch**: `008-versioned-event-schemas` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-versioned-event-schemas/spec.md`

## Summary

Codebase inspection during planning confirmed this feature is genuinely greenfield: no
`MassTransit` package reference, no event schema, and no shared contracts package exist anywhere in
the repository. [ADR-0011](../../docs/adr/0011-checkout-orchestration.md) already recorded this gap
explicitly and deferred two things to two different stories — schema definition to **this** story
(SCRUM-18), and standing up RabbitMQ/MassTransit plus outbox-backed publishing to **SCRUM-31**
("replace this orchestration with an outbox-backed saga"). [ADR-0005](../../docs/adr/0005-event-contract-format.md)
already made the two decisions this feature must execute against: JSON Schema (not Avro/Protobuf,
not a schema-registry service) in a shared contracts *package*, with `{EventName}V{N}` type names
carrying the version, and two open action items this feature closes: "create the shared contracts
package/repo location" and "establish the versioning convention and document the deprecation-window
policy."

This plan therefore scopes strictly to the contract layer: a new shared class library,
`shared/EventContracts`, holding versioned C# record types and their hand-authored JSON Schema
documents for `OrderPlacedV1` and `BasketCheckedOutV1`, plus a test suite proving (a) a produced
event validates against its schema, (b) a consumer tolerates unknown fields, and (c) a published
schema version cannot be silently edited without shipping a new version. It deliberately does
**not** wire `Orders.Api`, `Baskets.Api`, or the BFF checkout flow to actually construct or publish
these events over a broker — no broker exists yet, and per ADR-0011's own trade-off analysis,
"building a partial version here would either be torn out [by SCRUM-31] or quietly become the
version that ships." Referencing an event type from a service with nothing to do with it yet would
be dead code, not a completed capability.

## Technical Context

**Language/Version**: C# 13 / .NET 10, matching every other project in the repository
(`Directory.Build.props`).

**Primary Dependencies**: None in the shared library itself — `OrderPlacedV1` and
`BasketCheckedOutV1` are dependency-free C# records plus JSON Schema documents shipped as content
files. The test project adds `JsonSchema.Net` 9.4.0 (JSON Schema 2020-12 validator; MIT-licensed,
no transitive runtime surface) purely to validate serialized instances against the committed schema
files — a test-time concern only, consistent with ADR-0005's point that runtime schema validation is
not part of this platform's safety net.

**Storage**: N/A — no persistence introduced.

**Testing**: xUnit, matching `shared/Tenancy.UnitTests` (the closest analog: a dependency-light
shared library with pure-logic tests, no Testcontainers needed because nothing here talks to
external infrastructure).

**Target Platform**: Class library consumed by future .NET services; no standalone runtime surface.

**Project Type**: Shared library addition to the existing single-repo, multi-service solution
(`Ecommerce.slnx`) — same shape as `shared/ServiceDefaults` and `shared/Tenancy`.

**Performance Goals**: N/A — no request path, no message throughput; this feature ships no runtime
call.

**Constraints**: Published schema versions are immutable once committed (Research Decision 3) —
enforced by a frozen-content test, not a runtime check, since no CI pipeline definition
(Jenkinsfile) exists yet in this repository to host a git-history-aware gate.

**Scale/Scope**: Two events (`OrderPlaced`, `BasketCheckedOut`), one version each (`V1`) to start;
one new shared project, one new test project; zero changes to any existing service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| II. Contract-First Integration | This feature exists to satisfy this principle for events, mirroring what 007 already did for HTTP: schemas defined and reviewed in a shared location before any publisher/consumer implementation exists. Versioning via explicit `V{N}` type names, a deprecation-window policy, and tolerant-reader proof are all directly required by this principle and are this feature's entire scope. **PASS**. |
| III. Test-First Development | Every capability (schema validation, tolerant reading, version immutability) ships as a failing-then-passing xUnit test before/alongside the record types, per the repository's established `shared/Tenancy.UnitTests` pattern. No Testcontainers dependency needed — nothing here touches SQL Server, Redis, or RabbitMQ. **PASS**. |
| IV. Event-Driven by Default / outbox / idempotent consumers | Not implicated by this feature's actual scope: no publisher, no consumer, no broker wiring is added here. The pre-existing, ADR-0011-documented deviation (checkout is synchronous, not event-driven, until SCRUM-31) is unchanged by this feature — this feature does not attempt to close that gap, only the schema half of it. **N/A for this feature; deviation tracked in ADR-0011, not repeated here.** |
| V. Tenant Isolation | Both event schemas carry a required `tenantId` field (Key Entities, data-model.md), matching "propagated explicitly through gateway → BFF → services → **events**." Required (not optional) at the schema level, even though today's `Basket` read model doesn't yet surface a tenant id — the contract is allowed to be stricter than current implementation, since contract-first means the schema is authored ahead of the implementation that will satisfy it. **PASS**. |
| VII. Observable by Default | Both event schemas carry a required `correlationId` field, matching "a correlation ID MUST be generated at the edge and propagated across every synchronous call and every message." **PASS**. |
| I, VI, VIII, IX, X | Not implicated: no service boundary or database access is added (I), no externally reachable surface or PII beyond what already exists in the domain (VI), no runtime call to budget (VIII), no frontend change (IX), no non-trivial runtime behavior change to toggle (X — this is a library with no consumer yet, so nothing to toggle). **N/A**. |

No violations. Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/008-versioned-event-schemas/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
├── EventContracts/
│   ├── EventContracts.csproj            # net10.0 class library, zero package dependencies
│   ├── OrderPlacedV1.cs                 # record + XML doc (fields, required/optional)
│   ├── BasketCheckedOutV1.cs            # record + XML doc
│   ├── schemas/
│   │   ├── OrderPlaced.v1.schema.json       # JSON Schema 2020-12, immutable once committed
│   │   └── BasketCheckedOut.v1.schema.json  # JSON Schema 2020-12, immutable once committed
│   └── README.md                        # Versioning convention + deprecation-window policy
│
└── EventContracts.UnitTests/
    ├── EventContracts.UnitTests.csproj  # xUnit + JsonSchema.Net (test-only dependency)
    ├── SchemaValidationTests.cs         # FR-009 / Test Scenario 1: produced event validates
    ├── TolerantReaderTests.cs           # FR-007 / Test Scenario 3: unknown fields don't fail deserialization
    └── SchemaImmutabilityTests.cs       # FR-006 / Test Scenario 2: published version can't be silently edited

Ecommerce.slnx                            # + 2 new <Project> entries under /shared/
Directory.Packages.props                  # + JsonSchema.Net 9.4.0 (test-only)
```

**Structure Decision**: New shared library alongside the existing `shared/ServiceDefaults` and
`shared/Tenancy` (+ `Tenancy.UnitTests`) projects — same solution folder, same
class-library-plus-unit-test-project shape, same absence of a `FrameworkReference` (unlike
`Tenancy`/`ServiceDefaults`, `EventContracts` needs no ASP.NET Core types at all, since it defines
plain data contracts, not middleware). No existing service's `.csproj` is touched by this feature.

## Complexity Tracking

*No violations — table intentionally omitted.*
