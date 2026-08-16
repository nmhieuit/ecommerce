import { useLocation } from 'react-router-dom';
import { Confirmation } from './Confirmation';
import type { PlacedOrder } from './PlacedOrder';

interface ConfirmationLocationState {
  readonly order?: PlacedOrder;
}

/**
 * Reads the order the checkout handed over through navigation state.
 *
 * A shopper who reaches this route directly — typed URL, bookmark, refresh — arrives with no state,
 * and `Confirmation` shows its "nothing to show" case rather than inventing a reference
 * (spec Edge Cases).
 */
export function ConfirmationScreen() {
  const { state } = useLocation() as { state: ConfirmationLocationState | null };

  return <Confirmation order={state?.order} />;
}
