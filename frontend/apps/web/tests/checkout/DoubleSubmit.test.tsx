import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { CheckoutButton } from '@/features/checkout/CheckoutButton';
import { server } from '../msw/server';

/**
 * Spec FR-016 and SC-008: "triggering checkout more than once for the same basket MUST NOT create
 * more than one order."
 *
 * Two guards stand behind that promise. This suite covers the first — the control is unavailable
 * while a checkout is in flight. The second, the backend refusing a checkout of an already-emptied
 * basket, is covered by the BFF's CheckoutTests. Either alone would be thin; the client-side one is
 * what stops the request being sent twice in the first place.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('CheckoutButton double submission', () => {
  it('issues exactly one checkout request when clicked twice in rapid succession', async () => {
    const attempts: string[] = [];
    let release: (() => void) | undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, async ({ request }) => {
        attempts.push(request.url);
        await held;
        return HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        );
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={1} onCheckedOut={() => {}} />);

    const button = screen.getByRole('button', { name: /check out/i });

    await userEvent.click(button);
    await waitFor(() => expect(button).toBeDisabled());

    // The second click lands while the first is still in flight — the exact race FR-016 is about.
    await userEvent.click(button, { pointerEventsCheck: 0 });

    release?.();

    await waitFor(() => expect(attempts).toHaveLength(1));
  });

  it('reports the created order exactly once', async () => {
    const confirmations: { id: string }[] = [];

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, () =>
        HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        ),
      ),
    );

    renderWithQueryClient(
      <CheckoutButton itemCount={1} onCheckedOut={(order) => confirmations.push(order)} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /check out/i }));

    await waitFor(() => expect(confirmations).toHaveLength(1));
    expect(confirmations[0]?.id).toBe('aaaaaaaa-0000-4000-8000-000000000001');
  });

  /**
   * Spec US3 acceptance scenario 4: a failed checkout shows a clear error, no confirmation, and
   * leaves the basket intact so the shopper can retry.
   */
  it('shows an error and reports no order when checkout fails', async () => {
    const confirmations: unknown[] = [];

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(
      <CheckoutButton itemCount={1} onCheckedOut={(order) => confirmations.push(order)} />,
    );

    await userEvent.click(screen.getByRole('button', { name: /check out/i }));

    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(confirmations).toHaveLength(0);
  });
});
