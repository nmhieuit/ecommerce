import { createContext, useContext } from 'react';

export interface AuthContextValue {
  readonly isAuthenticated: boolean;
  readonly signIn: (username: string, password: string) => Promise<void>;
  readonly signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (context === null) {
    throw new Error('useAuth() must be used inside <AuthProvider>.');
  }

  return context;
}
