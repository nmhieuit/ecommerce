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
 * Spec FR-008 and SC-004: "the storefront blocks the attempt in the interface and sends no checkout
 * request to the backend at all."
 *
 * The second half is what this suite is really for. A request the server rejects is a failure of
 * this scenario, not a pass — the criterion is zero requests, not zero orders.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('CheckoutButton with an empty basket', () => {
  it('is not operable', () => {
    renderWithQueryClient(<CheckoutButton itemCount={0} onCheckedOut={() => {}} />);

    expect(screen.getByRole('button', { name: /check out/i })).toBeDisabled();
  });

  it('sends no checkout request when the shopper tries anyway', async () => {
    const attempts: string[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, ({ request }) => {
        attempts.push(request.url);
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={0} onCheckedOut={() => {}} />);

    // `pointerEventsCheck: 0` so the click is genuinely attempted against the disabled control
    // rather than being refused by the test library before it reaches the component.
    await userEvent.click(screen.getByRole('button', { name: /check out/i }), {
      pointerEventsCheck: 0,
    });

    expect(attempts).toHaveLength(0);
  });

  it('becomes operable once the basket holds something', async () => {
    const attempts: string[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/checkout`, ({ request }) => {
        attempts.push(request.url);
        return HttpResponse.json(
          { id: 'aaaaaaaa-0000-4000-8000-000000000001', placedAtUtc: '2026-08-16T12:00:00Z', total: 12.5 },
          { status: 201 },
        );
      }),
    );

    renderWithQueryClient(<CheckoutButton itemCount={1} onCheckedOut={() => {}} />);

    const button = screen.getByRole('button', { name: /check out/i });
    expect(button).toBeEnabled();

    await userEvent.click(button);

    await waitFor(() => expect(attempts).toHaveLength(1));
  });
});
