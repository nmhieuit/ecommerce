import { defineConfig } from 'orval';

/**
 * Principle II: client code MUST be generated from contracts and MUST NOT be hand-written.
 * Principle IX: server state MUST be managed with TanStack Query.
 *
 * Orval is the one tool that satisfies both at once (ADR-0004) — it emits TanStack Query hooks
 * directly, so there is no hand-written `useQuery` wrapper at any call site.
 *
 * The input is the BFF's generated OpenAPI document, which is the authoritative contract. The BFF
 * publishes it at /openapi/v1.json in Development only, so generation runs against a locally
 * running BFF (see quickstart.md) and the output is committed — a frontend build must not require
 * a live backend.
 */
const bffOpenApiUrl = process.env.BFF_OPENAPI_URL ?? 'http://localhost:5301/openapi/v1.json';

export default defineConfig({
  bff: {
    input: {
      target: bffOpenApiUrl,
    },
    output: {
      client: 'react-query',
      httpClient: 'fetch',
      mode: 'tags-split',
      target: './src/generated/endpoints.ts',
      schemas: './src/generated/model',
      // A single custom fetch instance is where the gateway origin and credentials are applied, so
      // no generated call site can address anything else (spec FR-014, SC-010).
      override: {
        mutator: {
          path: './src/http/fetcher.ts',
          name: 'bffFetch',
        },
      },
    },
  },
});
