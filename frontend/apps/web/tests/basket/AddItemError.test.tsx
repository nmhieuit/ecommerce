import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { AddToBasketButton } from '@/features/catalog/AddToBasketButton';
import { server } from '../msw/server';

/**
 * Spec US2 acceptance scenario 5: "when the shopper attempts to add a product [and it fails], a
 * clear error is shown and the basket is not left displaying an item that was never actually
 * added."
 *
 * The second half is the one worth engineering for. A basket that optimistically shows an item the
 * backend rejected is worse than one that shows nothing — the shopper would check out believing
 * they had bought it.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';
const NOTEBOOK = '9f8d6b1e-0001-4000-8000-000000000001';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

describe('AddToBasketButton', () => {
  it('adds the product when the request succeeds', async () => {
    const added: unknown[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async ({ request }) => {
        added.push(await request.json());
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.click(screen.getByRole('button', { name: /add .*to basket/i }));

    await waitFor(() => expect(added).toHaveLength(1));

    // Product and quantity only. A price sent from here would be a price the shopper chose
    // (contracts/bff-openapi.yaml — AddBasketItemRequest).
    expect(added[0]).toEqual({ productId: NOTEBOOK, quantity: 1 });
  });

  it('shows a clear error when the request fails', async () => {
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.click(screen.getByRole('button', { name: /add .*to basket/i }));

    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toHaveTextContent(
      /could not add/i,
    );
  });

  /**
   * The control is unavailable while the request is in flight, so a shopper hammering it cannot
   * queue five additions of one product — the same guard FR-016 relies on at checkout.
   */
  it('disables itself while the addition is in flight', async () => {
    let release: (() => void) | undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });

    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async () => {
        await held;
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    const button = screen.getByRole('button', { name: /add .*to basket/i });
    await userEvent.click(button);

    await waitFor(() => expect(button).toBeDisabled());

    release?.();
    await waitFor(() => expect(button).toBeEnabled());
  });

  /**
   * FR-017 and SC-009: the whole flow is completable by keyboard, so adding to the basket cannot
   * be pointer-only.
   */
  it('can be operated by keyboard', async () => {
    const added: unknown[] = [];
    server.use(
      http.post(`${GATEWAY_ORIGIN}/bff/basket/items`, async ({ request }) => {
        added.push(await request.json());
        return HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        });
      }),
    );

    renderWithQueryClient(<AddToBasketButton productId={NOTEBOOK} productName="Field Notes Notebook" />);

    await userEvent.tab();
    expect(screen.getByRole('button', { name: /add .*to basket/i })).toHaveFocus();

    await userEvent.keyboard('{Enter}');

    await waitFor(() => expect(added).toHaveLength(1));
  });
});
