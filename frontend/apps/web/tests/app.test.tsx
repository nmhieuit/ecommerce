import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from '@/App';
import { server } from './msw/server';

/**
 * The shell, not any one screen: providers mount, the router resolves, and the landing route
 * renders. What the catalog itself does is ProductList's own suite (tests/catalog); the landing
 * route's request is stubbed here only so mounting the shell does not reach the network.
 */
describe('application shell', () => {
  beforeEach(() => {
    // The shell configures the client against the real default origin (the local gateway), so the
    // stub has to match that rather than a test-only host.
    server.use(
      http.get('http://localhost:5300/bff/products', () => HttpResponse.json({ items: [] })),
    );
  });

  it('renders the landing route inside the shell', async () => {
    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Products' })).toBeInTheDocument();
  });

  it('exposes navigation to the shopper', () => {
    render(<App />);

    const nav = screen.getByRole('navigation', { name: 'Main' });

    expect(nav).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Basket' })).toBeInTheDocument();
  });
});
