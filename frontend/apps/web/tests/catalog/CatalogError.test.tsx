import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ErrorState } from '@/shared/ErrorState';

/**
 * Spec FR-012 and US1 acceptance scenario 3: when the backend is unavailable or slow, the shopper
 * gets "a clear, human-readable error ... and the page remains usable (the shopper can retry)
 * rather than hanging or going blank".
 */
describe('catalog error state', () => {
  it('shows a readable message and announces it', () => {
    render(<ErrorState message="We could not load the products." />);

    const alert = screen.getByRole('alert');

    expect(alert).toHaveTextContent('We could not load the products.');
  });

  it('offers a retry the shopper can operate', async () => {
    const onRetry = vi.fn();
    render(<ErrorState message="We could not load the products." onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: /try again/i }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  /**
   * SC-009 and FR-017: the whole flow is completable by keyboard, so the recovery path from a
   * failure cannot be pointer-only.
   */
  it('reaches and fires retry by keyboard alone', async () => {
    const onRetry = vi.fn();
    render(<ErrorState message="We could not load the products." onRetry={onRetry} />);

    await userEvent.tab();
    expect(screen.getByRole('button', { name: /try again/i })).toHaveFocus();

    await userEvent.keyboard('{Enter}');
    expect(onRetry).toHaveBeenCalledOnce();
  });

  /**
   * When there is nothing sensible to retry, no dead control is offered — a button that does
   * nothing is worse than no button.
   */
  it('omits the retry control when no retry is possible', () => {
    render(<ErrorState message="We could not load the products." />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
