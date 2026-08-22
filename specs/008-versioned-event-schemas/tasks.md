---

description: "Task list template for feature implementation"
---

# Tasks: Versioned Event Schemas — OrderPlaced, BasketCheckedOut

**Input**: Design documents from `/specs/008-versioned-event-schemas/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: This feature's deliverable *is* test coverage (schema validation, tolerant reading, and
version immutability currently have none, per [plan.md](./plan.md) Constitution Check — Principle
III), so test tasks are listed directly as implementation, not as an optional add-on.

**Organization**: Tasks are grouped by user story, matching spec.md's three P1 stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

## Path Conventions

New shared library alongside `shared/ServiceDefaults` and `shared/Tenancy`: `shared/EventContracts/`
(production) and `shared/EventContracts.UnitTests/` (tests). No existing service directory is
touched.

---

## Phase 1: Setup

**Purpose**: Scaffold the two new projects and register them with the solution. No schema or record
content yet — that is User Story 1's deliverable.

- [ ] T001 [P] Create `shared/EventContracts/EventContracts.csproj`: `net10.0` class library, `ImplicitUsings`/`Nullable` enabled (inherited from `Directory.Build.props`), no package references — matching the dependency-free shape decided in [research.md](./research.md) Decision 2
- [ ] T002 [P] Create `shared/EventContracts.UnitTests/EventContracts.UnitTests.csproj`: `net10.0`, `IsPackable=false`, `PackageReference` to `coverlet.collector`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, a `<Using Include="Xunit" />`, and a `ProjectReference` to `../EventContracts/EventContracts.csproj` — copy the exact shape of `shared/Tenancy.UnitTests/Tenancy.UnitTests.csproj`
- [ ] T003 Register both new projects under the `/shared/` folder in `Ecommerce.slnx`, alongside the existing `ServiceDefaults`/`Tenancy`/`Tenancy.UnitTests` entries (depends on T001, T002)

**Checkpoint**: `dotnet build Ecommerce.slnx` succeeds with two new, empty projects present.

---

## Phase 2: Foundational

**Purpose**: Blocking prerequisites shared by all three user stories.

None beyond Phase 1 — unlike a typical feature, there is no separate infrastructure (database,
auth, routing) that every story needs, because the schema and record definitions produced in User
Story 1 *are* what User Stories 2 and 3 test against, not a separate foundation layer. Proceed
directly to User Story 1.

---

## Phase 3: User Story 1 - Event schemas live in one shared, authoritative location (Priority: P1) 🎯 MVP

**Goal**: `OrderPlaced` and `BasketCheckedOut` each have a versioned JSON Schema and a matching C#
record defined once in `shared/EventContracts`, with nothing duplicated in any service (spec
FR-001, FR-002, FR-008, SC-001).

**Independent Test**: Confirm both schemas exist under `shared/EventContracts/schemas/`, and that a
repository-wide search finds no inline/duplicate definitions of either event in any service.

### Implementation for User Story 1

- [ ] T004 [P] [US1] Create `shared/EventContracts/schemas/OrderPlaced.v1.schema.json`: JSON Schema 2020-12 document for `OrderPlacedV1`, with `required` and `additionalProperties: false` at the top level, per the field table in [data-model.md](./data-model.md) (`eventId`, `occurredAtUtc`, `orderId`, `tenantId`, `correlationId`, `total`, `lines[]` with nested `productId`/`quantity`/`unitPrice`)
- [ ] T005 [P] [US1] Create `shared/EventContracts/schemas/BasketCheckedOut.v1.schema.json`: JSON Schema 2020-12 document for `BasketCheckedOutV1`, same rigor, per [data-model.md](./data-model.md) (`eventId`, `occurredAtUtc`, `basketId`, `customerRef`, `tenantId`, `correlationId`, `items[]` with nested `productId`/`quantity`/`unitPrice`/`lineTotal`, `total`)
- [ ] T006 [P] [US1] Create `shared/EventContracts/OrderPlacedV1.cs`: `sealed record OrderPlacedV1` plus nested `OrderLineV1`, fields matching T004's schema exactly, with XML doc noting each field's required status and its source (`Order.Id`, `Order.PlacedAtUtc`, etc., per data-model.md)
- [ ] T007 [P] [US1] Create `shared/EventContracts/BasketCheckedOutV1.cs`: `sealed record BasketCheckedOutV1` plus nested `BasketLineItemV1`, fields matching T005's schema exactly, with XML doc per data-model.md
- [ ] T008 [US1] Embed the schema files in the assembly: add `<ItemGroup><EmbeddedResource Include="schemas/*.json" /></ItemGroup>` to `shared/EventContracts/EventContracts.csproj`, so tests (and future consumers) can load them via `Assembly.GetManifestResourceStream` without depending on a working directory (depends on T004, T005 existing)
- [ ] T009 [P] [US1] Write `shared/EventContracts/README.md`: document the `{EventName}V{N}` versioning convention, a table of current versions (`OrderPlacedV1`, `BasketCheckedOutV1`, both "current, no prior version"), and the deprecation-window policy from [data-model.md](./data-model.md)'s "Versioning convention" section (FR-004, FR-005, FR-008, SC-004)
- [ ] T010 [US1] Verify no service duplicates either event definition: run `grep -rl "OrderPlaced\|BasketCheckedOut" services/orders services/baskets services/bff --include="*.cs"` from the repo root and confirm zero matches (SC-001) (depends on T006, T007)

**Checkpoint**: `shared/EventContracts` holds both events' complete schema + record definitions;
nothing is duplicated elsewhere. User Story 1 is independently complete and verifiable.

---

## Phase 4: User Story 2 - Breaking schema changes are versioned, not silently shipped (Priority: P1)

**Goal**: Prove a published schema version cannot be silently edited — any post-publish change to
`OrderPlaced.v1.schema.json` or `BasketCheckedOut.v1.schema.json` is caught before it can merge
(spec FR-003, FR-006, SC-002), per the frozen-content mechanism in [research.md](./research.md)
Decision 3.

**Independent Test**: Run the immutability test suite against the committed schemas (passes); then
temporarily edit a committed schema file and re-run (fails); then revert.

### Implementation for User Story 2

- [ ] T011 [US2] Compute the SHA-256 hash of the current `shared/EventContracts/schemas/OrderPlaced.v1.schema.json` and `BasketCheckedOut.v1.schema.json` (e.g. `Get-FileHash -Algorithm SHA256`) to use as the frozen baseline values in T012 (depends on Phase 3 completion)
- [ ] T012 [US2] Write `shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs`: for each of `OrderPlaced.v1.schema.json` and `BasketCheckedOut.v1.schema.json`, load the embedded resource, compute its SHA-256 hash, and assert it equals the recorded baseline constant from T011, with a comment explaining that any legitimate schema change must add a new version file rather than update this constant (FR-003, FR-006)
- [ ] T013 [US2] Run `dotnet test shared/EventContracts.UnitTests --filter FullyQualifiedName~SchemaImmutabilityTests` and confirm it passes against the committed schema files
- [ ] T014 [US2] Verify the check catches a violation (spec User Story 2 Independent Test / SC-002): temporarily add a field to `shared/EventContracts/schemas/OrderPlaced.v1.schema.json`, re-run T013's command, confirm it now fails, then revert the edit

**Checkpoint**: SC-002 confirmed — an unversioned schema edit is caught by a failing test. User
Stories 1 and 2 are both independently complete.

---

## Phase 5: User Story 3 - Consumers tolerate unknown fields (Priority: P1)

**Goal**: Prove deserialization survives unrecognized fields (spec FR-007, SC-003) and that a
produced event validates against its own published schema (spec FR-009, Acceptance Scenario 2).

**Independent Test**: Run the new schema-validation and tolerant-reader test cases; all pass.

### Implementation for User Story 3

- [ ] T015 [US3] Add the test-only schema validator dependency: add a `JsonSchema.Net` `PackageVersion` entry (`9.4.0`, latest stable per [research.md](./research.md) Decision 2) to `Directory.Packages.props` with a comment noting it is test-only (no runtime schema validation per ADR-0005), and add the matching `<PackageReference Include="JsonSchema.Net" />` to `shared/EventContracts.UnitTests/EventContracts.UnitTests.csproj`
- [ ] T016 [P] [US3] Write `shared/EventContracts.UnitTests/SchemaValidationTests.cs`: construct a realistic `OrderPlacedV1` instance, serialize it via `System.Text.Json` (the serializer a future MassTransit publisher will use, per ADR-0005), load `OrderPlaced.v1.schema.json` from the embedded resource, and assert the serialized JSON validates successfully against it (FR-009, spec User Story 3 Acceptance Scenario 2)
- [ ] T017 [P] [US3] Extend `shared/EventContracts.UnitTests/SchemaValidationTests.cs` with the equivalent construct-serialize-validate case for `BasketCheckedOutV1` against `BasketCheckedOut.v1.schema.json`
- [ ] T018 [P] [US3] Write `shared/EventContracts.UnitTests/TolerantReaderTests.cs`: deserialize a JSON payload for `OrderPlacedV1` that includes one or more fields not present on the record (simulating a hypothetical future additive version), assert deserialization succeeds with no exception, and assert every field the record does recognize is populated correctly (FR-007, spec User Story 3 Acceptance Scenario 1)
- [ ] T019 [P] [US3] Extend `shared/EventContracts.UnitTests/TolerantReaderTests.cs` with the equivalent unknown-field-tolerance case for `BasketCheckedOutV1`
- [ ] T020 [US3] Run `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~SchemaValidationTests|FullyQualifiedName~TolerantReaderTests"` and confirm all pass (SC-003, FR-009)

**Checkpoint**: SC-003 confirmed for both events. All three user stories are now independently
complete and verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that the whole feature holds together and nothing regressed.

- [ ] T021 Run the full new test suite: `dotnet test shared/EventContracts.UnitTests` and confirm all tests across all three stories pass together with no interaction effects
- [ ] T022 Run `dotnet build Ecommerce.slnx` and confirm the two new projects build cleanly alongside every existing service and shared project
- [ ] T023 Walk through [quickstart.md](./quickstart.md) top to bottom and confirm every success criterion (SC-001 through SC-004) passes exactly as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Empty — no additional blocking work beyond Setup
- **User Story 1 (Phase 3)**: Depends on Setup (Phase 1) — needs the two empty projects to exist
- **User Story 2 (Phase 4)**: Depends on User Story 1 (Phase 3) — the immutability test needs the
  schema files T004/T005 created and hashed
- **User Story 3 (Phase 5)**: Depends on User Story 1 (Phase 3) — the validation/tolerant-reader
  tests need the records and embedded schemas from T006-T008; independent of User Story 2 (can run
  in parallel with Phase 4)
- **Polish (Phase 6)**: Depends on all three user stories being complete

### Within Each User Story

- User Story 1: T004-T007 are independent per file, parallelizable; T008 depends on T004/T005;
  T009 is independent content, parallelizable with T004-T008; T010 (verification) runs last
- User Story 2: T011 → T012 → T013 → T014 are sequential (each depends on the previous step's
  output)
- User Story 3: T015 (dependency setup) must precede T016-T019 (which need `JsonSchema.Net`
  available for T016/T017); T016-T019 are independent per file/case, parallelizable; T020 runs last

### Parallel Opportunities

- T001, T002 (Setup) in parallel
- T004, T005, T006, T007, T009 (User Story 1) in parallel
- User Story 2 (Phase 4) and User Story 3 (Phase 5) can run in parallel with each other once User
  Story 1 (Phase 3) is complete — they touch entirely different test files
- T016, T017, T018, T019 (User Story 3) in parallel once T015 is done

---

## Parallel Example: User Story 1

```bash
# Launch schema, record, and documentation creation together (different files, no dependencies):
Task: "Create shared/EventContracts/schemas/OrderPlaced.v1.schema.json"
Task: "Create shared/EventContracts/schemas/BasketCheckedOut.v1.schema.json"
Task: "Create shared/EventContracts/OrderPlacedV1.cs"
Task: "Create shared/EventContracts/BasketCheckedOutV1.cs"
Task: "Write shared/EventContracts/README.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: User Story 1
3. **STOP and VALIDATE**: Run T010's verification independently
4. This alone closes ADR-0005's two open action items (shared contracts location exists; versioning
   convention documented) even before the compatibility/tolerant-reader proofs land

### Incremental Delivery

1. Setup (Phase 1) → two empty projects registered in the solution
2. User Story 1 (Phase 3) → schemas and records exist in one shared location (MVP)
3. User Story 2 (Phase 4) + User Story 3 (Phase 5) → in parallel, since neither depends on the
   other — compatibility enforcement and tolerant-reader proof both land
4. Polish (Phase 6) → full suite run and quickstart walkthrough

### Parallel Team Strategy

With two developers, once User Story 1 is complete:

- Developer A: User Story 2 (`SchemaImmutabilityTests`)
- Developer B: User Story 3 (`SchemaValidationTests`, `TolerantReaderTests`)

Both integrate independently — different test files, no shared state.

---

## Notes

- No existing service (`Orders.Api`, `Baskets.Api`, `Bff.Api`) is touched by any task — this
  feature is scoped entirely to `shared/EventContracts` and its test project, per
  [plan.md](./plan.md) Summary and [research.md](./research.md) Decision 1.
- [P] tasks touch different files with no dependencies on each other
- Commit after each task or logical group
