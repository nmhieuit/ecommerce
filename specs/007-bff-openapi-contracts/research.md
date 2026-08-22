# Research: OpenAPI Specs for BFF Routes + Generated Clients

## Decision 1: Scope this feature to closing verifiable gaps, not rebuilding the pipeline

**Decision**: Treat SCRUM-17 as already substantially satisfied by
[specs/004-minimal-shopping-spa](../004-minimal-shopping-spa/spec.md) (Jira SCRUM-14) and scope
this feature's work to the two things that are not yet verifiably true: tolerant-reader test
coverage, and a recorded confirmation of route-to-spec parity.

**Rationale**: Codebase inspection found the full pipeline already in place:

- `services/bff/src/Bff.Api/Program.cs` calls `builder.Services.AddOpenApi()` and
  `app.MapOpenApi()`, publishing the OpenAPI document at `/openapi/v1.json` in Development —
  explicitly commented as "the contract-first source of truth for the SPA in SCRUM-14."
- [ADR-0003](../../docs/adr/0003-bff-implementation-pattern.md) and
  [ADR-0004](../../docs/adr/0004-openapi-client-codegen.md) document and accept native OpenAPI
  generation plus Orval codegen as the platform decision.
- `frontend/packages/api-client/package.json` describes itself as "generated from its OpenAPI
  document by Orval (ADR-0004). Never hand-written," with committed output under `src/generated/`.
- `frontend/packages/api-client/package.json` already has a `verify-generated` script that
  regenerates the client and fails (via `git status --porcelain`) if the committed output has
  drifted from the BFF's live document — this is CI enforcement of spec SC-002/SC-003.
- A repository-wide search for raw `fetch(`/`axios(` calls to BFF endpoints under
  `frontend/apps/web/src` returned zero matches.

**Alternatives considered**:

- *Full net-new plan, ignoring the existing implementation*: rejected — would duplicate
  ADR-0003/ADR-0004 decisions and existing spec-004 tasks, and risks producing conflicting
  guidance for work that already shipped.
- *Rewrite spec.md first to formally narrow scope*: considered, but the user opted to proceed
  directly to a narrow plan rather than block on a spec rewrite; this plan documents the actual
  scope being executed, which is the binding record for `/speckit-tasks`.

## Decision 2: Verify route-to-spec parity (SC-001) by construction, not a new automated check

**Decision**: Confirm and record that the OpenAPI document cannot drift from route behavior for
products, baskets, and orders, rather than building a new test that diffs the document against the
routes.

**Rationale**: `ProductsEndpoints.cs`, `BasketsEndpoints.cs`, and `OrdersEndpoints.cs` all declare
their response contracts inline on the same `MapGet`/`MapPost` calls that execute
(`.Produces<ProductListResponse>(StatusCodes.Status200OK)`, `.Produces<BasketResponse>(...)`,
`.Produces<OrderResponse>(...)`, plus `.ProducesProblem(...)` for error paths). ASP.NET Core's
native `AddOpenApi()`/`MapOpenApi()` reads this same metadata to build the published document —
there is no separate, hand-maintained spec file that could diverge from the routes. Structural
drift between the document and route behavior is not possible without also changing the route's
own type signature, which would be caught at compile time.

**Alternatives considered**:

- *New integration test fetching `/openapi/v1.json` and diffing it against reflected route
  metadata*: rejected — this would only be testing that ASP.NET Core's own `AddOpenApi()` works
  correctly, not anything specific to this platform's code.

## Decision 3: Add tolerant-reader coverage as one case per domain area, in existing test files

**Decision**: Add one additional test case to each of `ProductList.test.tsx`, `BasketView.test.tsx`,
and `Confirmation.test.tsx`, each asserting that an unrecognized extra field in the mocked BFF
response does not break rendering.

**Rationale**: Matches the existing one-file-per-flow test organization in
`frontend/apps/web/tests/**`; requires no new test infrastructure; uses the already-established
`server.use(http.get(..., () => HttpResponse.json({...})))` pattern seen throughout the existing
suite (e.g. `respondWithProducts` in `ProductList.test.tsx`). Covering all three domain areas named
in the spec (products, baskets, orders) gives symmetric confidence rather than generalizing from
one example.

**Alternatives considered**:

- *One consolidated `TolerantReader.test.tsx` file*: rejected — splits the assertion away from a
  flow's other behavior tests and breaks the established convention of one file per flow.
- *Test the generated client directly rather than through rendered components*: rejected — the
  actual risk named in the spec ("the generated client does not break") is a user-facing rendering
  concern, and constitution Principle III requires frontend assertions through accessible roles,
  not through library internals.

## Decision 4: No production code changes

**Decision**: Make no changes to `frontend/packages/api-client` (generated code, `fetcher.ts`,
`orval.config.ts`) or to the BFF endpoints.

**Rationale**: JSON parsing in the browser does not strip unrecognized object properties, and the
generated client applies no runtime schema validation on top of its compile-time-only TypeScript
types (Orval is configured with `httpClient: 'fetch'` and no schema-validation output). Tolerant
reader behavior is therefore already structurally present; the gap is proof, not implementation.
Per spec.md's Assumptions section, "tolerant reader" means unknown fields are ignored, not
defended against with new validation machinery — adding runtime validation (e.g., generated Zod
schemas) would be new scope beyond closing the identified gap.

**Alternatives considered**:

- *Add runtime response validation for defense-in-depth*: rejected as out of scope for this
  feature; would be a separate, larger change to the codegen pipeline (ADR-0004 revisit).
