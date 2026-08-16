# Frontend

The shopper-facing storefront and the generated client it talks through. A pnpm workspace
orchestrated by Turborepo ([ADR-0010](../docs/adr/0010-frontend-monorepo-tooling.md)).

```text
frontend/
├── apps/web/                 # the storefront: React 19, Vite, TypeScript strict
└── packages/api-client/      # TanStack Query hooks generated from the BFF's OpenAPI document
```

## Prerequisites

- Node.js 22 LTS
- pnpm 9 (`corepack enable`, or run commands with `npx pnpm@9`)

## Commands

Run from `frontend/`. Every one of these is a Turborepo task, so it applies to both packages and is
cached between runs.

| Command | What it does |
|---|---|
| `pnpm install` | Install the workspace |
| `pnpm generate` | Regenerate the API client from the BFF's OpenAPI document — **needs a running BFF** |
| `pnpm dev` | Vite dev server on `:5173`, pointed at the gateway on `:5300` |
| `pnpm build` | Typecheck, then production build |
| `pnpm test` | Vitest + Testing Library |
| `pnpm lint` | ESLint — violations fail the build |
| `pnpm typecheck` | `tsc --noEmit` |
| `pnpm size` | Download-size budget check — **fails the build when exceeded** |
| `pnpm e2e` | Playwright walkthrough — needs the full stack and the dev server |

## The storefront talks to exactly one address

Every request goes to the **gateway** (`http://localhost:5300` locally), never to the BFF and never
to a domain service. That is spec FR-014 and SC-010, and it is structural rather than a convention:
all generated hooks route through one hand-written fetcher
([`packages/api-client/src/http/fetcher.ts`](packages/api-client/src/http/fetcher.ts)), which is the
only place a URL is built. There is deliberately no map of per-service addresses to reach for.

Addressing the BFF directly would also skip the only hop that resolves the tenant and the caller, so
those requests would be refused by the services' own gates anyway.

Override the origin with `VITE_GATEWAY_ORIGIN` if you need to.

## The API client is generated, never hand-written

Constitution Principle II requires it, and [ADR-0004](../docs/adr/0004-openapi-client-codegen.md)
chose Orval because it is the only option that emits TanStack Query hooks directly — so there is no
hand-written `useQuery` wrapper at any call site.

`pnpm generate` reads the BFF's document from `http://localhost:5301/openapi/v1.json` (Development
only), so a BFF must be running. The output is **committed**, because a frontend build must not
require a live backend. Regeneration is byte-identical for an unchanged document, and CI fails when
the committed output has drifted.

```bash
# from the repository root, with the BFF running
dotnet run --project services/bff/src/Bff.Api
# then
cd frontend && pnpm generate && git diff --exit-code packages/api-client/src/generated
```

## The bundle budget is a build gate

`.size-limit.json` in `apps/web` declares a gzipped budget per entry screen, and `pnpm size` exits
non-zero when it is exceeded (spec FR-025, SC-011). This is the half of constitution Principle VIII
that Phase 1 can genuinely enforce; Core Web Vitals are declared in the spec but measured in Phase 4
([SCRUM-32](https://nmhieuit.atlassian.net/browse/SCRUM-32)).

If a change pushes a screen over, the answer is code-splitting, not a larger number.

## No design-system package yet

[ADR-0009](../docs/adr/0009-design-system-foundation.md) chose Radix primitives plus Tailwind as the
foundation, and the storefront uses them directly. The *shared, versioned package* that Principle IX
describes is deferred until a second client exists to share it with — with one app there is no
cross-app duplication to prevent. Extraction into `packages/design-system/` is the trigger when
mobile-web arrives.

## Running the whole flow

See [`specs/004-minimal-shopping-spa/quickstart.md`](../specs/004-minimal-shopping-spa/quickstart.md)
for the full walkthrough: databases, migrations, the four services, the gateway, and the storefront.
