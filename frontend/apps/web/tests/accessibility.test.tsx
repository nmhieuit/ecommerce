import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from '@/App';
import { server } from './msw/server';

/**
 * Spec FR-017 and SC-009 cover the controls; this suite covers the three shell-level properties a
 * per-component test cannot see, because each is about moving *between* screens.
 */
describe('shell accessibility', () => {
  beforeEach(() => {
    // jsdom's URL survives between tests, and a router reads it when it is created — so without
    // this the second test in this file would start wherever the first navigated to.
    window.history.replaceState({}, '', '/');

    server.use(
      http.get('http://localhost:5300/bff/products', () => HttpResponse.json({ items: [] })),
      http.get('http://localhost:5300/bff/basket', () =>
        HttpResponse.json({
          id: 'b1b1b1b1-0000-4000-8000-000000000001',
          customerRef: 'phase1-stub-user',
          items: [],
          total: 0,
        }),
      ),
    );
  });

  /**
   * WCAG 2.4.1 "Bypass Blocks". First in the tab order so it is reachable before the navigation it
   * exists to bypass.
   */
  it('offers a skip link as the first focusable element', async () => {
    render(<App />);

    await userEvent.tab();

    expect(screen.getByRole('link', { name: /skip to content/i })).toHaveFocus();
  });

  /**
   * WCAG 2.4.2 "Page Titled". A single-page app never changes the title on its own, so without
   * this every screen announces itself as the same page.
   */
  it('gives each screen its own document title', async () => {
    render(<App />);

    await waitFor(() => expect(document.title).toMatch(/^Products/));

    await userEvent.click(screen.getByRole('link', { name: 'Basket' }));

    await waitFor(() => expect(document.title).toMatch(/^Basket/));
  });

  /**
   * A client-side navigation otherwise leaves focus on the link that was clicked, stranding a
   * keyboard or screen-reader user in the header while the content beneath them changes.
   */
  it('moves focus to the new screen after navigating', async () => {
    render(<App />);

    await userEvent.click(screen.getByRole('link', { name: 'Basket' }));

    // The router's transition is asynchronous, so focus is still on the link when the click
    // resolves. Waiting for the new screen to actually be on the page first states the property
    // that matters — once the content has changed, focus has followed it — rather than racing the
    // transition and asserting on a moment that means nothing.
    await screen.findByRole('heading', { name: 'Basket' });

    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
  });

  it('does not steal focus on first render', () => {
    render(<App />);

    expect(screen.getByRole('main')).not.toHaveFocus();
  });
});
