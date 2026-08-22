# Implementation Plan: OpenAPI Specs for BFF Routes + Generated Clients

**Branch**: `007-bff-openapi-contracts` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-bff-openapi-contracts/spec.md`

## Summary

Investigation during planning found that the contract-first OpenAPI pipeline this spec describes
already exists, built as part of [specs/004-minimal-shopping-spa](../004-minimal-shopping-spa/spec.md)
(Jira SCRUM-14) per [ADR-0003](../../docs/adr/0003-bff-implementation-pattern.md) (native ASP.NET
Core OpenAPI generation) and [ADR-0004](../../docs/adr/0004-openapi-client-codegen.md) (Orval
codegen). The BFF's products, baskets, and orders routes already publish an OpenAPI document
generated directly from their typed minimal-API definitions; `frontend/packages/api-client`
already contains a committed, generated-only client with a CI `verify-generated` drift gate; and a
codebase search found zero raw `fetch`/`axios` calls to BFF endpoints outside that package.

This plan therefore scopes to the two things that are **not** yet verifiably true:

1. **Tolerant reader is untested** (User Story 3 / FR-006 / SC-004) — nothing proves the SPA
   survives an unrecognized field in a BFF response.
2. **Route-to-spec parity has not been recorded as verified** (SC-001) — true by construction
   (the OpenAPI document is generated from the same code that executes), but never stated as a
   checked fact.

The approach: add one tolerant-reader test case per domain area (products, baskets, orders) to the
existing per-flow test files, using the project's established MSW + Vitest + Testing Library
pattern, and record the route/spec-parity verification in `quickstart.md` rather than building new
tooling to check something the framework already guarantees. No production code changes are
anticipated; no new contract is introduced.

## Technical Context

**Language/Version**: TypeScript 5.7 (Vite + React 19 SPA) for the new test cases. C# 13 / .NET 10
(BFF) is read-only for this feature — verification only, no code changes expected.

**Primary Dependencies**: Vitest, `@testing-library/react`, `msw` — all already present in
`frontend/apps/web`. No new dependencies.

**Storage**: N/A

**Testing**: Vitest + Testing Library + MSW (frontend, per constitution Principle III and the
existing `frontend/apps/web/tests/**` conventions). No new backend tests anticipated.

**Target Platform**: Web browser SPA (Vite build); Node-based test runner (Vitest) in CI.

**Project Type**: Web application — existing monorepo (`frontend/apps/web`,
`frontend/packages/api-client`, `services/bff`). No new top-level structure.

**Performance Goals**: N/A — no new runtime surface is introduced.

**Constraints**: MUST NOT hand-edit anything under `frontend/packages/api-client/src/generated/**`
(constitution Principle II). New tests MUST reuse the existing MSW `server.use()` /
`HttpResponse.json()` pattern rather than introducing a second mocking approach.

**Scale/Scope**: Three new test cases (one per domain area: products, baskets, orders), appended
to existing test files; one verification record in `quickstart.md`. No new files under `src/`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| II. Contract-First Integration | Already satisfied structurally: OpenAPI is generated from the BFF's own typed route definitions (`.Produces<T>()`), and the client is 100% generated with a CI drift gate (`verify-generated`). This feature adds test evidence for the "tolerant reader" clause already required by this principle; it introduces no new contract and hand-writes no client code. **PASS**. |
| III. Test-First Development | The new tests characterize existing behavior rather than drive new implementation — JSON parsing in the browser doesn't strip unknown properties, and the generated client applies no runtime schema validation on top of its compile-time-only TypeScript types, so the behavior under test is expected to already hold. If any new test fails unexpectedly, that is a real gap requiring an implementation fix before merge (true red-green), not a pre-approved exception. **PASS**. |
| IX. Frontend Discipline | New tests render through the SPA's real components and assert via accessible roles/text, consistent with existing tests (e.g. `ProductList.test.tsx`); no hand-written API calls are introduced. **PASS**. |
| Other principles (I, IV–VIII, X) | Not implicated — no service boundaries, events, tenancy, security, observability, performance budget, or toggle changes. **N/A**. |

No violations. Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/007-bff-openapi-contracts/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
frontend/
├── apps/web/
│   └── tests/
│       ├── catalog/ProductList.test.tsx     # + 1 tolerant-reader case (products)
│       ├── basket/BasketView.test.tsx       # + 1 tolerant-reader case (baskets)
│       └── checkout/Confirmation.test.tsx   # + 1 tolerant-reader case (orders)
└── packages/api-client/                     # No changes — generated-only, verified via quickstart.md

services/bff/
└── src/Bff.Api/Features/{Products,Baskets,Orders}/*Endpoints.cs  # No changes — verified via quickstart.md
```

**Structure Decision**: Reuses the existing web-application monorepo layout established in spec
004 (`frontend/apps/web` + `frontend/packages/api-client` + `services/bff`). No new project,
package, or directory is introduced; the three new test cases live inside their existing per-flow
test files, matching the established one-file-per-flow convention.

## Complexity Tracking

*No violations — table intentionally omitted.*
