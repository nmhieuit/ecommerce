import { render, screen, within } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { BasketView } from '@/features/basket/BasketView';
import { server } from '../msw/server';

/**
 * Spec US2 acceptance scenario 3 and FR-004: "each item's name, quantity, and price are shown,
 * along with a basket total."
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

interface BasketBody {
  readonly id: string;
  readonly customerRef: string;
  readonly items: readonly {
    readonly productId: string;
    readonly name: string;
    readonly quantity: number;
    readonly unitPrice: number;
    readonly lineTotal: number;
  }[];
  readonly total: number;
}

function respondWithBasket(body: BasketBody) {
  server.use(http.get(`${GATEWAY_ORIGIN}/bff/basket`, () => HttpResponse.json(body)));
}

const notebookLine = {
  productId: '9f8d6b1e-0001-4000-8000-000000000001',
  name: 'Field Notes Notebook',
  quantity: 2,
  unitPrice: 12.5,
  lineTotal: 25,
};

const apronLine = {
  productId: '9f8d6b1e-0001-4000-8000-000000000003',
  name: 'Linen Apron',
  quantity: 1,
  unitPrice: 34.25,
  lineTotal: 34.25,
};

describe('BasketView', () => {
  it('shows each line with its name, quantity, unit price, and line total', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine],
      total: 25,
    });

    renderWithQueryClient(<BasketView />);

    expect(await screen.findByText('Field Notes Notebook')).toBeInTheDocument();

    // Scoped to the line, because a one-line basket's line total and basket total are the same
    // amount — an unscoped match would be ambiguous, and would still pass if the line total
    // vanished entirely.
    const line = within(screen.getByRole('listitem'));

    // Quantity and unit price read as one phrase — "Quantity: 2 × $12.50" — so they are asserted
    // as one, the way the shopper encounters them.
    expect(line.getByText(/quantity:\s*2\s*×\s*\$12\.50/i)).toBeInTheDocument();
    expect(line.getByText('$25.00')).toBeInTheDocument();
  });

  /**
   * The figure quickstart.md Scenarios 2 and 5 quote. The total comes from the backend and is
   * displayed, never recomputed in the browser — a total the client works out for itself is a
   * total that can disagree with the one being charged.
   */
  it('shows the basket total the backend reported', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine, apronLine],
      total: 59.25,
    });

    renderWithQueryClient(<BasketView />);

    expect(await screen.findByText(/total/i)).toBeInTheDocument();
    expect(screen.getByText('$59.25')).toBeInTheDocument();
  });

  it('presents the lines as a list', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [notebookLine, apronLine],
      total: 59.25,
    });

    renderWithQueryClient(<BasketView />);

    expect(await screen.findByRole('list', { name: /basket/i })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  /**
   * An empty basket is a legitimate state — a first-time shopper's, and the state right after a
   * successful checkout (spec FR-010). Not an error.
   */
  it('tells the shopper when the basket is empty', async () => {
    respondWithBasket({
      id: 'b1b1b1b1-0000-4000-8000-000000000001',
      customerRef: 'phase1-stub-user',
      items: [],
      total: 0,
    });

    renderWithQueryClient(<BasketView />);

    expect(await screen.findByText(/your basket is empty/i)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a readable error when the basket cannot be loaded', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/basket`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<BasketView />);

    expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });
});
