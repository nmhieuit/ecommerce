/**
 * Spec FR-012: when a backend request fails, times out, or errors, the shopper gets a clear,
 * readable message within a bounded time, can retry, and never sees a hang or a blank screen.
 *
 * One component for every such surface, so "clear error" means the same thing on the catalog, the
 * basket, and the checkout rather than three different half-answers.
 */

export interface ErrorStateProps {
  /** What failed, in the shopper's terms — not the exception's terms. */
  readonly message: string;
  /** Omitted when there is nothing sensible to retry. */
  readonly onRetry?: () => void;
}

export function ErrorState({ message, onRetry }: ErrorStateProps) {
  return (
    // role="alert" so assistive technology announces the failure rather than leaving a screen
    // reader user on a page that silently changed (Principle IX, WCAG 2.2 AA).
    <div role="alert" className="rounded border border-current/20 p-4">
      <p>{message}</p>
      {onRetry ? (
        <button type="button" onClick={onRetry} className="mt-3 underline">
          Try again
        </button>
      ) : null}
    </div>
  );
}

/**
 * The message shown when a request fails for a reason the shopper cannot act on. Deliberately says
 * nothing about which service failed or why: that belongs in the logs, and a shopper cannot use it.
 */
export const GENERIC_ERROR_MESSAGE =
  'Something went wrong loading this page. Please try again.';
