import { useCallback, useEffect, useMemo, useSyncExternalStore } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { AuthContext } from './useAuth';
import type { AuthContextValue } from './useAuth';
import { requestToken } from './identityClient';
import { clearSession, getSession, setSession, subscribe } from './tokenStore';

export function AuthProvider({ children }: { readonly children: ReactNode }) {
  const queryClient = useQueryClient();
  const session = useSyncExternalStore(subscribe, getSession, getSession);
  const isAuthenticated = session !== null;

  // Whatever is cached belongs to the caller who fetched it. When the session ends — by sign-out,
  // expiry, or a 401 — drop it, or the next person to sign in on this tab would briefly see the
  // previous shopper's basket.
  useEffect(() => {
    if (!isAuthenticated) queryClient.clear();
  }, [isAuthenticated, queryClient]);

  const signIn = useCallback(async (username: string, password: string) => {
    setSession(await requestToken(username, password));
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ isAuthenticated, signIn, signOut: clearSession }),
    [isAuthenticated, signIn],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
