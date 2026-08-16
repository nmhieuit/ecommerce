/**
 * The package's public surface.
 *
 * The generated hooks are re-exported from here once the BFF's routes exist and `pnpm generate`
 * has been run against them (T028 for the catalog, T045 for the basket, T061 for checkout). Until
 * then this exports only the transport configuration, which is hand-written on purpose — it is the
 * one file in this package that is not generated (see src/http/fetcher.ts).
 */
export { configureApiClient, ApiError, bffFetch } from './http/fetcher';
export type { ApiClientConfig } from './http/fetcher';
