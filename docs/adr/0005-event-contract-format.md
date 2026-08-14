# ADR-0005: Event/Integration Contract Format

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Messaging is fixed as RabbitMQ via MassTransit. Principle II requires versioned schemas in a shared contracts location, breaking changes to carry a new explicit version with a deprecation window, and consumers to tolerate unknown fields. Principle IV requires idempotent, out-of-order-tolerant consumers.

## Decision

Use **JSON Schema**, with versioned event contracts in a shared contracts package (not a separate schema-registry service).

## Options Considered

### Option A: JSON Schema (shared contracts package)
| Dimension | Assessment |
|---|---|
| Complexity | Low |
| Cost | None — no new infrastructure |
| Scalability | N/A (schema format, not a runtime concern) |
| Team familiarity | High |

**Pros:** MassTransit's default serialization is JSON via `System.Text.Json`, so this needs no extra serialization layer; `System.Text.Json` ignores unknown members by default, which delivers Principle II's "tolerant reader" requirement for free rather than as something the team must build; human-readable in RabbitMQ's management UI and logs, directly supporting Principle VII's "debuggable in production" goal; versioning is handled by explicit type names (`OrderPlacedV2`) and semver on the shared contracts package, checked by consumer-driven contract tests (ADR-0006) rather than a runtime registry.
**Cons:** No registry-enforced compatibility check at publish time — schema-evolution safety relies on contract tests catching breakage in CI rather than a broker refusing an incompatible publish.

### Option B: Avro + Schema Registry
| Dimension | Assessment |
|---|---|
| Complexity | High — new stateful service to run |
| Cost | Operational cost of a new HA service |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Registry enforces forward/backward compatibility centrally at publish time, not just at test time; compact binary encoding.
**Cons:** Requires deploying and operating an entirely new stateful service with its own HA/backup concerns, for a platform whose event volume doesn't need binary compactness; binary payloads are far harder to inspect in RabbitMQ's UI or structured logs, working against Principle VII; MassTransit's Avro integration is less first-class than its native JSON support.

### Option C: Protobuf
| Dimension | Assessment |
|---|---|
| Complexity | High |
| Cost | Operational cost similar to Avro |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Strong schema evolution via field numbers; compact.
**Cons:** Same debuggability downside as Avro; no gRPC anywhere else in this stack to justify the IDL; would mean the team maintains two different contract *languages* (OpenAPI/JSON Schema for HTTP, Protobuf IDL for events) instead of one consistent JSON-Schema family across both contract types.

## Trade-off Analysis

Avro and Protobuf's registry-enforced compatibility is a genuine safety net, but it comes at the cost of a new stateful service and a debuggability regression, for a platform that doesn't have the event throughput to need binary encoding. JSON Schema, paired with the contract tests already mandated by Principle III, gets equivalent safety without new infrastructure and without sacrificing log/UI readability.

## Consequences

- Contract-breakage safety depends on CI contract tests actually running on every change — there is no runtime backstop like a registry would provide.
- OpenAPI (HTTP) and event contracts both live in the JSON Schema family, so tooling (validators, documentation generators) is shared across both contract types.

## Action Items

1. [ ] Create the shared contracts package/repo location for event schemas
2. [ ] Establish the `TypeNameVN` versioning convention and document the deprecation-window policy
