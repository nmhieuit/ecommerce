/**
 * The package's public surface.
 *
 * Everything under `./generated` is produced by Orval from the BFF's OpenAPI document and is never
 * hand-edited (ADR-0004, Principle II). The transport configuration below is the one hand-written
 * file in this package — it is what binds every generated call to the gateway and nothing else.
 */
export * from './generated/endpoints';
export * from './generated/model';

export { configureApiClient, ApiError, bffFetch } from './http/fetcher';
export type { ApiClientConfig } from './http/fetcher';
