# EventContracts

The one authoritative home for this platform's integration event contracts: a versioned JSON Schema
document per event version, and a matching C# record services construct and serialize.

Nothing here is referenced by a service yet. No broker exists (see
[ADR-0011](../../docs/adr/0011-checkout-orchestration.md)); wiring RabbitMQ + MassTransit and
outbox-backed publishing is SCRUM-31's job. This library is the schema half of that work, delivered
first so the contract is reviewed before any publisher is written.

## Current versions

| Event | Current version | Schema file | Prior versions still supported |
|---|---|---|---|
| `OrderPlaced` | `OrderPlacedV1` | [`schemas/OrderPlaced.v1.schema.json`](schemas/OrderPlaced.v1.schema.json) | None — no prior version |
| `BasketCheckedOut` | `BasketCheckedOutV1` | [`schemas/BasketCheckedOut.v1.schema.json`](schemas/BasketCheckedOut.v1.schema.json) | None — no prior version |

Field-by-field definitions live in the schema files themselves and in
[`specs/008-versioned-event-schemas/data-model.md`](../../specs/008-versioned-event-schemas/data-model.md).

## Versioning convention

Per [ADR-0005](../../docs/adr/0005-event-contract-format.md), the version is part of the name, never
a header or an envelope field a consumer has to inspect:

- **Type name**: `{EventName}V{N}` — `OrderPlacedV1`, `BasketCheckedOutV2`, and so on.
- **Schema file**: `{EventName}.v{N}.schema.json` under `schemas/`, JSON Schema 2020-12.
- **`N` starts at 1** for an event's first published shape.
- **Nested types are not independently versioned.** `OrderLineV1` carries the outer event's version
  because it changes only when the outer event does, and lives under `$defs` in the same schema
  file rather than getting one of its own.

### A published version is immutable

Once a schema file is committed it is frozen. `SchemaImmutabilityTests` in
`../EventContracts.UnitTests/` hashes each committed schema and fails if the content changes at all
— breaking or not, including a typo fix in a `description`.

This is deliberately blunter than a breaking-vs-non-breaking classifier. JSON Schema diffing is a
genuinely hard problem (required-field additions, type narrowing, enum restriction each need
separate handling), and treating *any* post-publish edit as a violation has zero false negatives for
the case that actually matters. The cost is an occasional forced version bump for a cosmetic change,
which is cheap. Consumer-aware compatibility analysis belongs to the consumer-driven contract
testing effort in [ADR-0006](../../docs/adr/0006-contract-testing-tool.md) (SCRUM-21), not here.

### Adding a new version

1. Add `schemas/{Event}.v{N+1}.schema.json`. Do **not** edit the existing version file.
2. Add a `{Event}V{N+1}` record alongside the existing one. The old type stays.
3. Add a `SchemaValidationTests` case and a `TolerantReaderTests` case for the new version.
4. Add the new file's hash constant to `SchemaImmutabilityTests` once it is committed.
5. Update the version table above, moving the superseded version into "prior versions still
   supported" and noting the date it was superseded.

## Deprecation window

A superseded version does not disappear when its replacement ships. `V{N}` stays defined, compiled,
and covered by its own tests until **both** of these hold:

1. `V{N+1}` has shipped, and
2. no known consumer still depends on `V{N}` — confirmed through the consumer-driven contract tests
   tracked in [ADR-0006](../../docs/adr/0006-contract-testing-tool.md) (SCRUM-21).

There is deliberately no fixed day count. A number picked today would be picked with no consumers to
measure against, and would either be ignored or enforced arbitrarily; "confirmed to have no
consumers" is the condition that actually makes removal safe. Once consumer-driven contract testing
is in place and consumer counts are observable, revisit this and set a concrete window.

Only `V1` of each event exists today, so nothing has been superseded and no removal has happened.

## Consuming these contracts

- **Producers** must publish payloads that validate against the schema: it uses
  `additionalProperties: false` at the top level, so a producer cannot quietly add a field without
  shipping a new version. `SchemaValidationTests` proves a serialized record satisfies its schema.
- **Consumers** are tolerant readers. `System.Text.Json` ignores unrecognized JSON properties by
  default, so a consumer built against `V{N}` keeps working when it receives a payload carrying
  fields it has never heard of. `TolerantReaderTests` proves this rather than assuming it — the
  schema's strictness governs what a producer may publish, not what a consumer must reject.
- **JSON property names** are pinned on each record with `[JsonPropertyName]`, so the wire format
  matches the schema regardless of the serializer's naming policy.
- **Schema files are embedded resources**, loadable by bare file name:
  `typeof(OrderPlacedV1).Assembly.GetManifestResourceStream("OrderPlaced.v1.schema.json")`.

## Tests

```bash
dotnet test shared/EventContracts.UnitTests
```

See [`specs/008-versioned-event-schemas/quickstart.md`](../../specs/008-versioned-event-schemas/quickstart.md)
for what each test suite proves and how to verify the immutability check actually catches an edit.
