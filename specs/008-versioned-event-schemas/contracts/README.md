# Contracts

This feature's contract artifacts are the JSON Schema documents themselves, not a separate
description of them. They are not duplicated here; this file points to where they live and how they
are governed.

- **Source of truth**: `shared/EventContracts/schemas/OrderPlaced.v1.schema.json` and
  `shared/EventContracts/schemas/BasketCheckedOut.v1.schema.json` — JSON Schema 2020-12, one file
  per event version, immutable once committed (see [research.md](../research.md) Decision 3).
- **Code-level shape**: `shared/EventContracts/OrderPlacedV1.cs` and
  `shared/EventContracts/BasketCheckedOutV1.cs` — the C# records services will construct once
  SCRUM-31 wires actual publishing. See [data-model.md](../data-model.md) for the full field list.
- **Versioning and deprecation policy**: `shared/EventContracts/README.md` — the `{EventName}V{N}`
  convention, and how a version is superseded and eventually removed.
- **Proof the contract holds**: `shared/EventContracts.UnitTests/` —
  `SchemaValidationTests` (a produced event validates against its schema),
  `TolerantReaderTests` (a consumer survives unknown fields), and
  `SchemaImmutabilityTests` (a published version cannot be silently edited). See
  [quickstart.md](../quickstart.md) for how to run them.

No HTTP contract is introduced or changed by this feature — it is scoped entirely to asynchronous
event schemas.
