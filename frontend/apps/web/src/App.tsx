import { useState } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from './app/queryClient';
import { resolveGatewayOrigin } from './app/config';
import { AppRoutes } from './app/routes';

// Configured at module load, before any component can render and therefore before any hook can
// fire a request. The generated client throws rather than guessing if this has not run, so a
// missing origin surfaces as a clear error instead of a same-origin 404 against the dev server.
configureApiClient({ baseUrl: resolveGatewayOrigin() });

export function App() {
  // Held in state rather than created inline: a new QueryClient on every render would discard the
  // cache each time, and the basket surviving a refresh depends on that cache being the one place
  // server state lives (data-model.md — Client-side state).
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <AppRoutes />
    </QueryClientProvider>
  );
}
