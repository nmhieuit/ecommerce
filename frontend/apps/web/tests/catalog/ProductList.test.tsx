import { render, screen } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeAll, describe, expect, it } from 'vitest';
import { configureApiClient } from '@ecommerce/api-client';
import { createQueryClient } from '@/app/queryClient';
import { ProductList } from '@/features/catalog/ProductList';
import { server } from '../msw/server';

/**
 * Spec US1 acceptance scenario 1 and FR-001: the shopper opens the storefront and sees the products
 * available to buy, each with its name and price, retrieved from the backend rather than embedded
 * in the client.
 *
 * Assertions go through accessible roles rather than class names or test ids (Principle III), so
 * they describe what a shopper — including one using a screen reader — actually encounters.
 */

const GATEWAY_ORIGIN = 'http://gateway.test';

beforeAll(() => {
  configureApiClient({ baseUrl: GATEWAY_ORIGIN });
});

function renderWithQueryClient(ui: ReactNode) {
  return render(<QueryClientProvider client={createQueryClient()}>{ui}</QueryClientProvider>);
}

function respondWithProducts(items: unknown[]) {
  server.use(
    http.get(`${GATEWAY_ORIGIN}/bff/products`, () => HttpResponse.json({ items })),
  );
}

describe('ProductList', () => {
  it('lists every product with its name and price', async () => {
    respondWithProducts([
      { id: '9f8d6b1e-0001-4000-8000-000000000001', name: 'Field Notes Notebook', price: 12.5 },
      { id: '9f8d6b1e-0001-4000-8000-000000000002', name: 'Ceramic Pour-Over Set', price: 48 },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText('Field Notes Notebook')).toBeInTheDocument();
    expect(screen.getByText('Ceramic Pour-Over Set')).toBeInTheDocument();

    // Formatted, not raw: spec FR-024 is what the shopper reads, and "48" is not a price.
    expect(screen.getByText('$12.50')).toBeInTheDocument();
    expect(screen.getByText('$48.00')).toBeInTheDocument();
  });

  /**
   * A catalog is a list of things, and announcing it as one is how a screen-reader user learns how
   * many products there are before reading them.
   */
  it('presents the catalog as a list', async () => {
    respondWithProducts([
      { id: '9f8d6b1e-0001-4000-8000-000000000003', name: 'Linen Apron', price: 34.25 },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByRole('list', { name: /products/i })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
  });

  /**
   * Spec FR-002: zero products is a legitimate answer, and the shopper must be told so rather than
   * left looking at a page that appears broken.
   */
  it('shows the empty state when the catalog holds nothing', async () => {
    respondWithProducts([]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText(/no products available/i)).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: /products/i })).not.toBeInTheDocument();
  });

  /**
   * Spec FR-012 and US1 acceptance scenario 3: a backend failure produces a readable message and a
   * usable page — never a blank screen and never an endless spinner.
   */
  it('shows a readable error when the backend fails', async () => {
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, () =>
        HttpResponse.json({ title: 'Bad Gateway' }, { status: 502 }),
      ),
    );

    renderWithQueryClient(<ProductList />);

    const alert = await screen.findByRole('alert', {}, { timeout: 5000 });

    expect(alert).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  /**
   * SC-010: every request the storefront makes goes to the single backend surface. Asserted here at
   * the unit level too, because a hardcoded URL introduced in a component would otherwise only be
   * caught by the end-to-end walkthrough (T065).
   */
  it('requests the catalog from the configured gateway origin', async () => {
    const requested: string[] = [];
    server.use(
      http.get(`${GATEWAY_ORIGIN}/bff/products`, ({ request }) => {
        requested.push(request.url);
        return HttpResponse.json({ items: [] });
      }),
    );

    renderWithQueryClient(<ProductList />);
    await screen.findByText(/no products available/i);

    expect(requested).toHaveLength(1);
    expect(requested[0]).toBe(`${GATEWAY_ORIGIN}/bff/products`);
  });

  /**
   * Spec FR-006 / SC-004, and constitution Principle II: "Consumers MUST tolerate unknown fields."
   * The BFF can add a field to `ProductSummary` — a `sku`, say — and deploy it before the SPA's
   * client has been regenerated. The shopper must still see the catalog when that happens, because
   * the alternative is a storefront that goes blank the moment the backend ships ahead of it.
   */
  it('renders products carrying a field the client does not know about', async () => {
    respondWithProducts([
      {
        id: '9f8d6b1e-0001-4000-8000-000000000004',
        name: 'Enamel Camp Mug',
        price: 18,
        sku: 'MUG-ENAMEL-01',
      },
    ]);

    renderWithQueryClient(<ProductList />);

    expect(await screen.findByText('Enamel Camp Mug')).toBeInTheDocument();
    expect(screen.getByText('$18.00')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
