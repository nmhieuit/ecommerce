/**
 * Where the signed-in shopper's access token lives.
 *
 * `sessionStorage`, not `localStorage`: the token is a bearer credential, so it should die with
 * the tab rather than sit on disk across browser restarts, and it should not be shared with other
 * tabs' scripts. It survives a refresh, which is what keeps a shopper signed in while they browse.
 * The basket is unaffected — it is the server's, resolved from the token's subject (spec FR-006).
 *
 * A tiny external store rather than React state so the api-client's transport can read the token
 * without importing React, and so a 401 raised deep inside a query can sign the shopper out.
 */

const STORAGE_KEY = 'storefront.session';

export interface Session {
  readonly accessToken: string;
  /** Epoch milliseconds after which the token must not be sent. */
  readonly expiresAt: number;
}

type Listener = () => void;

const listeners = new Set<Listener>();
let current: Session | null | undefined;

function read(): Session | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (raw === null) return null;

    const parsed = JSON.parse(raw) as Partial<Session>;
    if (typeof parsed.accessToken === 'string' && typeof parsed.expiresAt === 'number') {
      return { accessToken: parsed.accessToken, expiresAt: parsed.expiresAt };
    }
  } catch {
    // Storage blocked or the value is not ours: behave as signed out rather than crash the app.
  }

  return null;
}

/** The live session, or `null` when signed out or the token has expired. */
export function getSession(): Session | null {
  current ??= read();

  if (current !== null && current.expiresAt <= Date.now()) {
    clearSession();
    return null;
  }

  return current;
}

export function getAccessToken(): string | null {
  return getSession()?.accessToken ?? null;
}

export function setSession(session: Session): void {
  current = session;

  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // Kept in memory only; a refresh will sign the shopper out, which is the safe failure.
  }

  emit();
}

export function clearSession(): void {
  const hadSession = current !== null && current !== undefined;
  current = null;

  try {
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Nothing further to clean up.
  }

  if (hadSession) emit();
}

export function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Test seam: forget the in-memory copy so the next read goes back to storage. */
export function resetSessionCache(): void {
  current = undefined;
}

function emit(): void {
  listeners.forEach((listener) => listener());
}
