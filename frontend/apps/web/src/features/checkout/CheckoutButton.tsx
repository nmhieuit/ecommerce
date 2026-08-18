import { useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getGetCurrentBasketQueryKey, useCheckout } from '@ecommerce/api-client';
import { ErrorState } from '@/shared/ErrorState';
import type { PlacedOrder } from './PlacedOrder';

export interface CheckoutButtonProps {
  /** How many lines the basket holds. Zero means there is nothing to check out. */
  readonly itemCount: number;
  readonly onCheckedOut: (order: PlacedOrder) => void;
}

/**
 * Spec FR-007, FR-008, FR-016: the shopper checks out from the basket; an empty basket cannot; and
 * triggering it twice cannot produce two orders.
 *
 * Two guards back FR-016. This is the first — the control is unavailable while a checkout is in
 * flight, so the second click never becomes a second request. The second guard is the backend
 * refusing a checkout of an emptied basket, which is what covers the case where the two clicks
 * somehow do get through.
 */
export function CheckoutButton({ itemCount, onCheckedOut }: CheckoutButtonProps) {
  const queryClient = useQueryClient();
  const isEmpty = itemCount === 0;

  // Set synchronously in the click handler, and the reason it exists rather than relying on
  // `isPending` is a defect that reached a running stack: two clicks in the same tick both call
  // mutate() before React has re-rendered with the pending state, and running 004's walkthrough
  // against containers produced two orders six milliseconds apart. `disabled` cannot close that
  // race — it only takes effect on the next render. A ref is checked and set before either click
  // can return.
  //
  // The server-side guard does not help here either: both requests read a non-empty basket before
  // either had cleared it.
  const isCheckingOut = useRef(false);

  const { mutate, isPending, isError } = useCheckout({
    mutation: {
      onSettled: () => {
        // Released however it ended, so a failed checkout can be retried (US3 scenario 4).
        isCheckingOut.current = false;
      },
      onSuccess: (result) => {
        // Spec FR-010: the basket is empty afterwards. Invalidating rather than assuming keeps the
        // screen showing what the server holds.
        void queryClient.invalidateQueries({ queryKey: getGetCurrentBasketQueryKey() });

        if (result.status === 201) {
          onCheckedOut(result.data);
        }
      },
    },
  });

  return (
    <>
      <button
        type="button"
        // Disabled for an empty basket, so no request is issued at all — spec SC-004 counts
        // requests, not rejected orders.
        // `disabled` is still here: it is what a shopper sees and what assistive technology
        // announces. The ref below is what actually makes FR-016 true.
        disabled={isEmpty || isPending}
        onClick={() => {
          if (isEmpty || isCheckingOut.current) {
            return;
          }

          isCheckingOut.current = true;
          mutate();
        }}
        className="mt-6 rounded border border-current/30 px-4 py-2 disabled:opacity-50"
      >
        {isPending ? 'Placing your order…' : 'Check out'}
      </button>

      {isError ? (
        <div className="mt-3">
          {/* No confirmation is shown and the basket is left intact, so the shopper can simply
              press the button again (spec US3 acceptance scenario 4). */}
          <ErrorState message="We could not place your order. Your basket is unchanged — please try again." />
        </div>
      ) : null}
    </>
  );
}
