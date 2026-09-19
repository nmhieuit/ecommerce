import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { usePageTitle } from '@/shared/usePageTitle';
import { useAuth } from './useAuth';
import { SignInError } from './identityClient';

const MESSAGES = {
  'invalid-credentials': 'Incorrect username or password.',
  unavailable: 'Sign-in is unavailable right now. Please try again in a moment.',
} as const;

export function LoginScreen() {
  usePageTitle('Sign in');

  const { isAuthenticated, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting) return;

    setIsSubmitting(true);
    setError(null);

    try {
      await signIn(username.trim(), password);
      void navigate(from, { replace: true });
    } catch (caught) {
      setError(MESSAGES[caught instanceof SignInError ? caught.reason : 'unavailable']);
      setIsSubmitting(false);
    }
  }

  return (
    <main className="mx-auto max-w-sm p-6 text-[var(--color-ink)]">
      <h1 className="text-2xl font-semibold">Sign in</h1>

      <form onSubmit={(event) => void handleSubmit(event)} className="mt-6 flex flex-col gap-4">
        <label className="flex flex-col gap-1">
          Username
          <input
            type="text"
            name="username"
            autoComplete="username"
            required
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            className="rounded border border-current/30 p-2"
          />
        </label>

        <label className="flex flex-col gap-1">
          Password
          <input
            type="password"
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="rounded border border-current/30 p-2"
          />
        </label>

        {error ? (
          <p role="alert" className="rounded border border-current/20 p-3">
            {error}
          </p>
        ) : null}

        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded border border-current/30 p-2 disabled:opacity-60"
        >
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
