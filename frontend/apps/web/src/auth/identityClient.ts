import { resolveIdentityOrigin } from '@/app/config';

/**
 * Signs a shopper in against the identity server's token endpoint (Resource Owner Password grant,
 * on the dedicated public client `ecommerce-web-spa-password` — services/identity Config.cs).
 *
 * Why not Authorization Code + PKCE, which the SPA's main client is registered for: that flow
 * redirects the browser to an interactive login page hosted by the identity server, and no such
 * page exists yet. Until it does, this is the only way a form in the storefront can obtain a
 * token. The client is seeded per environment (SpaPasswordClient:Enabled), never in production.
 */

export const CLIENT_ID = 'ecommerce-web-spa-password';
export const SCOPE = 'openid profile ecommerce-api';

export type SignInFailure = 'invalid-credentials' | 'unavailable';

export class SignInError extends Error {
  constructor(readonly reason: SignInFailure) {
    super(`Sign-in failed: ${reason}.`);
    this.name = 'SignInError';
  }
}

interface TokenResponse {
  readonly access_token?: string;
  readonly expires_in?: number;
}

/** Slack so a token is not sent in its last moments and then refused mid-flight. */
const EXPIRY_SKEW_MS = 10_000;

export async function requestToken(
  username: string,
  password: string,
): Promise<{ accessToken: string; expiresAt: number }> {
  let response: Response;

  try {
    response = await fetch(`${resolveIdentityOrigin()}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      // A string, not a URLSearchParams instance: same bytes on the wire, and it does not depend on
      // the runtime's URLSearchParams being the one its fetch understands (jsdom's is not).
      body: new URLSearchParams({
        grant_type: 'password',
        client_id: CLIENT_ID,
        scope: SCOPE,
        username,
        password,
      }).toString(),
    });
  } catch {
    // Network failure or a CORS refusal — the shopper cannot fix either, and neither is a wrong
    // password, so it must not be reported as one.
    throw new SignInError('unavailable');
  }

  if (response.status === 400 || response.status === 401) {
    // Duende answers a wrong username or password with 400 invalid_grant.
    throw new SignInError('invalid-credentials');
  }

  if (!response.ok) {
    throw new SignInError('unavailable');
  }

  const body = (await response.json().catch(() => ({}))) as TokenResponse;

  if (!body.access_token) {
    throw new SignInError('unavailable');
  }

  return {
    accessToken: body.access_token,
    expiresAt: Date.now() + (body.expires_in ?? 3600) * 1000 - EXPIRY_SKEW_MS,
  };
}
