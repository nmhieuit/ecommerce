import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '@/App';

/**
 * The shell, not any one screen: providers mount, the router resolves, and the landing route
 * renders. Screens are asserted by their own stories' tests (T024 onward) — what this pins is that
 * there is something for them to mount into.
 */
describe('application shell', () => {
  it('renders the landing route inside the shell', () => {
    render(<App />);

    expect(screen.getByRole('heading', { name: 'Products' })).toBeInTheDocument();
  });

  it('exposes navigation to the shopper', () => {
    render(<App />);

    const nav = screen.getByRole('navigation', { name: 'Main' });

    expect(nav).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Basket' })).toBeInTheDocument();
  });
});
