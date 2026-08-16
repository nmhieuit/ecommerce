import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { Confirmation } from '@/features/checkout/Confirmation';

/**
 * The screen offers a way back to the catalog, so it needs a router in scope. A memory router keeps
 * that a detail of the harness rather than something the component has to be reshaped around.
 */
function renderInRouter(ui: ReactNode) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

/**
 * Spec FR-009: the confirmation shows "the created order's generated identifier as-is — a reference
 * the shopper can read off the screen and quote — together with the order's total."
 */
describe('Confirmation', () => {
  const order = {
    id: 'aaaaaaaa-0000-4000-8000-000000000001',
    placedAtUtc: '2026-08-16T12:00:00Z',
    total: 59.25,
  };

  it('shows the order identifier verbatim', () => {
    renderInRouter(<Confirmation order={order} />);

    // Verbatim, not shortened: SC-005 compares what the shopper sees against the order in the
    // backend, and a truncated reference is no longer reliably unique (Clarifications 2026-08-16).
    expect(screen.getByText(order.id)).toBeInTheDocument();
  });

  it('shows the order total in the single Phase 1 currency', () => {
    renderInRouter(<Confirmation order={order} />);

    expect(screen.getByText('$59.25')).toBeInTheDocument();
  });

  it('tells the shopper their order was placed', () => {
    renderInRouter(<Confirmation order={order} />);

    expect(screen.getByRole('heading', { name: /thank you|order placed|confirmed/i })).toBeInTheDocument();
  });

  /**
   * Spec Edge Cases: "direct navigation to the confirmation screen without having checked out shows
   * a clear 'nothing to show' state rather than a broken screen or a fabricated order reference."
   */
  it('shows a nothing-to-show state when there is no order', () => {
    renderInRouter(<Confirmation order={undefined} />);

    expect(screen.getByText(/no recent order/i)).toBeInTheDocument();

    // Emphatically no invented identifier.
    expect(screen.queryByText(order.id)).not.toBeInTheDocument();
  });
});
