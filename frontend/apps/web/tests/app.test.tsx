import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { App } from '@/App';

/**
 * A smoke test for the Phase 1 scaffolding itself: it proves jsdom, the React plugin, Testing
 * Library, and the setup file are all actually wired, rather than being a directory of config that
 * has never executed. Asserting through an accessible role (Principle III) from the very first
 * test sets the convention every later test follows.
 */
describe('storefront scaffolding', () => {
  it('renders the application shell', () => {
    render(<App />);

    expect(screen.getByRole('heading', { name: 'Storefront' })).toBeInTheDocument();
  });
});
