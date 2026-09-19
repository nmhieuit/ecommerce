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
let authHooks: AuthHooks = {};

export interface ApiClientConfig {
  /** The gateway's origin — never the BFF's, and never a domain service's. */
  readonly baseUrl: string;
}

/**
 * How the transport learns who the caller is. Kept as two callbacks so this package stays free of
 * any one sign-in mechanism or storage choice: the application owns the token, and this file only
 * attaches it and reports back when the gateway refuses it.
 */
export interface AuthHooks {
  /** The current access token, or `null`/`undefined` when nobody is signed in. */
  readonly getAccessToken?: () => string | null | undefined;
  /** Called when the gateway answers 401 to a request that carried a token. */
  readonly onUnauthorized?: () => void;
}

export function configureAuthHooks(hooks: AuthHooks): void {
  authHooks = hooks;
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
    /**
     * The `X-Correlation-Id` the gateway put on this response, or `null` when the response never
     * arrived (a network error) or — unexpectedly — carried none. Lets a future error report or
     * support ticket quote the one value that ties this failure to the backend's own logs
     * (016-correlation-id-propagation spec US2 AC2; contracts/spa-correlation-visibility-contract.md).
     */
    readonly correlationId: string | null,
  ) {
    super(`Request to ${url} failed with status ${status}.`);
    this.name = 'ApiError';
  }
}

/**
 * Returns the `{ data, status }` envelope Orval's generated code expects — not the bare body.
 * Orval types every operation's result as a union of `{ data: T; status: 200 }` and its failure
 * shapes, and reads `.data` off it; returning the parsed body directly would typecheck against
 * that union while being the wrong shape at runtime.
 *
 * Non-2xx responses throw rather than resolving to their failure member. TanStack Query turns a
 * thrown error into `isError`, which is what the screens act on: spec FR-012 wants one clear,
 * readable message, not the backend's own words. That also means the generated `TError` type
 * (`ProblemDetails`) is nominal — what is actually thrown is an {@link ApiError}, so screens should
 * branch on `isError` rather than read fields off `error`.
 */
export async function bffFetch<TResponse>(
  url: string,
  init?: RequestInit,
): Promise<TResponse> {
  if (baseUrl === undefined) {
    // A missing origin is a wiring bug, not a runtime condition to recover from. Failing here
    // beats silently issuing a same-origin request to the dev server and getting an HTML 404.
    throw new Error('configureApiClient() must be called before any request is made.');
  }

  const accessToken = authHooks.getAccessToken?.();

  const response = await fetch(`${baseUrl}${url}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      // The bearer token is the caller's identity. The tenant and subject are still resolved at
      // the gateway from that token's claims; nothing here sets or forwards them
      // (contracts/subject-id-header.md).
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init?.headers,
    },
    credentials: 'include',
  });

  const text = await response.text();
  const body: unknown = text.length > 0 ? JSON.parse(text) : undefined;

  if (!response.ok) {
    if (response.status === 401 && accessToken) {
      // The token this request carried was refused (expired, revoked, wrong issuer): the session
      // is over, so let the application send the shopper back to sign-in.
      authHooks.onUnauthorized?.();
    }

    throw new ApiError(response.status, url, body, response.headers.get('X-Correlation-Id'));
  }

  return { data: body, status: response.status } as TResponse;
}
