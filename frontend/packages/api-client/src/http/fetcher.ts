/**
 * The single outbound call site for the whole frontend.
 *
 * Every generated hook routes through here (see orval.config.ts — `override.mutator`), which is
 * what makes spec FR-014 and SC-010 structural rather than a convention: there is no other place a
 * request could be addressed from, so no screen can reach the BFF or a domain service directly.
 *
 * The origin is injected by the application at startup rather than read from `import.meta.env`
 * here, so this package stays independent of any one bundler's environment mechanism.
 */

let baseUrl: string | undefined;

export interface ApiClientConfig {
  /** The gateway's origin — never the BFF's, and never a domain service's. */
  readonly baseUrl: string;
}

export function configureApiClient(config: ApiClientConfig): void {
  baseUrl = config.baseUrl.replace(/\/$/, '');
}

/** Thrown for any non-2xx response, carrying enough for a screen to say something useful. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly url: string,
    readonly body: unknown,
  ) {
    super(`Request to ${url} failed with status ${status}.`);
    this.name = 'ApiError';
  }
}

export async function bffFetch<TResponse>(
  url: string,
  init?: RequestInit,
): Promise<TResponse> {
  if (baseUrl === undefined) {
    // A missing origin is a wiring bug, not a runtime condition to recover from. Failing here
    // beats silently issuing a same-origin request to the dev server and getting an HTML 404.
    throw new Error('configureApiClient() must be called before any request is made.');
  }

  const response = await fetch(`${baseUrl}${url}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
    // The tenant and subject are resolved at the gateway from the request's identity; nothing here
    // sets or forwards them (contracts/subject-id-header.md).
    credentials: 'include',
  });

  const text = await response.text();
  const body: unknown = text.length > 0 ? JSON.parse(text) : undefined;

  if (!response.ok) {
    throw new ApiError(response.status, url, body);
  }

  return body as TResponse;
}
