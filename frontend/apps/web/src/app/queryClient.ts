import { QueryClient } from '@tanstack/react-query';

/**
 * Principle IX: server state is TanStack Query's, and server data is never copied into a global
 * client store. That makes this cache the basket the shopper sees — which is why the basket
 * survives a refresh without anything being written to browser storage (data-model.md —
 * Client-side state).
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Spec FR-012 / SC-006: the shopper must see a clear failure within 5 seconds. The BFF
        // already caps a downstream call at 3 s, so one retry still lands inside the budget while
        // absorbing a single transient blip. More retries would push past it.
        retry: 1,
        retryDelay: 500,

        // Refetching on every window focus would re-issue basket and catalog reads constantly,
        // which is noise the walking skeleton does not need and makes SC-010's network-tab check
        // harder to read.
        refetchOnWindowFocus: false,

        staleTime: 30_000,
      },
      mutations: {
        // Adding to a basket and placing an order are not safe to replay blindly — FR-016 is
        // explicit that a second checkout must not become a second order.
        retry: 0,
      },
    },
  });
}
