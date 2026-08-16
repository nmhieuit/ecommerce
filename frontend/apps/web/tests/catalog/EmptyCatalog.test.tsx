import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmptyCatalog } from '@/features/catalog/CatalogStates';

/**
 * Spec FR-002 and US1 acceptance scenario 2: an empty catalog gets an explicit state — "not an
 * empty page, a spinner that never resolves, or an error". Zero products is a legitimate answer,
 * and the shopper is entitled to be told so.
 */
describe('EmptyCatalog', () => {
  it('tells the shopper there is nothing to buy yet', () => {
    render(<EmptyCatalog />);

    expect(screen.getByText(/no products available/i)).toBeInTheDocument();
  });

  /**
   * An empty catalog is not a failure, so it must not be announced as one. `role="alert"` here
   * would interrupt a screen reader user to report normality.
   */
  it('does not present itself as an error', () => {
    render(<EmptyCatalog />);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
