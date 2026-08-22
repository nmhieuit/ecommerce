# Quickstart: Verify Versioned Event Schemas

This feature adds a new shared library, `shared/EventContracts`, plus a test suite that proves the
spec's success criteria. This guide walks through running that suite and, separately, how to add a
new event version once one is needed.

## Prerequisites

- .NET 10 SDK
- No database, broker, or container dependency — `shared/EventContracts.UnitTests` has none.

## SC-001 — schemas live in the shared contracts location, not inline in a publishing service

```bash
find shared/EventContracts/schemas -name "*.schema.json"
grep -rl "OrderPlaced\|BasketCheckedOut" services/orders services/baskets services/bff --include="*.cs"
```

Expect the first command to list `OrderPlaced.v1.schema.json` and `BasketCheckedOut.v1.schema.json`
under `shared/EventContracts/schemas/`. Expect the second command to return **zero matches** — no
service defines or duplicates these events inline, by construction (see [plan.md](./plan.md)
Summary: no publisher exists yet for this feature to duplicate schema into).

## SC-002 — a breaking (or any) change without a new version is caught

```bash
dotnet test shared/EventContracts.UnitTests --filter FullyQualifiedName~SchemaImmutabilityTests
```

Expect this to pass against the committed schema files. To see it catch a violation, temporarily
edit a field in `shared/EventContracts/schemas/OrderPlaced.v1.schema.json` (e.g. add a new required
property) without adding a new version file, re-run the same command, and confirm it now fails —
then revert the edit. This is the mechanism behind Test Scenario 2 in the spec.

## SC-003 — an older consumer tolerates a newer event's unknown fields

```bash
dotnet test shared/EventContracts.UnitTests --filter FullyQualifiedName~TolerantReaderTests
```

Expect this to pass. Each test deserializes a JSON payload containing one or more fields not present
on the target record type and asserts deserialization succeeds with the recognized fields intact —
this is what `System.Text.Json`'s default unknown-member handling gives for free (ADR-0005), proven
rather than assumed.

## SC-004 — current/prior versions and the deprecation window are discoverable in one place

```bash
cat shared/EventContracts/README.md
```

Expect this single file to state, for each event, its current version, any prior (still-supported)
versions, and the deprecation-window policy — no need to read service source code to find this.

## Full suite + producer validation (Test Scenario 1)

```bash
dotnet test shared/EventContracts.UnitTests
```

Expect all tests to pass, including `SchemaValidationTests`, which constructs an `OrderPlacedV1`
instance, serializes it exactly as a future publisher would, and validates the result against
`OrderPlaced.v1.schema.json` — confirming Jira Test Scenario 1 ("publish an `OrderPlaced` event and
confirm it validates against the published schema").

## Adding a new event version (contributor guide, not exercised by tests today)

1. Add `{Event}.v{N+1}.schema.json` under `shared/EventContracts/schemas/` — never edit an existing
   version file (`SchemaImmutabilityTests` will fail if you do).
2. Add a new record type, `{Event}V{N+1}`, alongside the existing one — the old type stays.
3. Add a `SchemaValidationTests` case and a `TolerantReaderTests` case for the new version.
4. Add a `SchemaImmutabilityTests` hash constant for the new file once it's committed.
5. Update `shared/EventContracts/README.md`'s version table and record the deprecation window for
   the version being superseded.
