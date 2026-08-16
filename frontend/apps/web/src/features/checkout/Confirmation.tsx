import { Link } from 'react-router-dom';
import { formatMoney } from '@/shared/money';
import type { PlacedOrder } from './PlacedOrder';

export interface ConfirmationProps {
  /** Absent when the screen was reached without checking out. */
  readonly order: PlacedOrder | undefined;
}

/**
 * Spec FR-009: what the shopper sees once the order exists — its identifier, shown as-is, and its
 * total.
 */
export function Confirmation({ order }: ConfirmationProps) {
  if (order === undefined) {
    // Spec Edge Cases: reaching this screen directly shows a clear "nothing to show" state rather
    // than a broken screen or, worse, a fabricated order reference.
    return (
      <>
        <h1 className="text-2xl font-semibold">Order confirmation</h1>
        <p className="mt-4">No recent order to show.</p>
        <Link to="/" className="mt-4 inline-block underline">
          Back to products
        </Link>
      </>
    );
  }

  return (
    <>
      <h1 className="text-2xl font-semibold">Thank you — your order is placed</h1>

      <dl className="mt-6 space-y-3">
        <div>
          <dt className="text-sm">Order reference</dt>
          {/* Verbatim and monospaced so it can be read aloud or copied accurately. SC-005 compares
              exactly this string against the order in the backend. */}
          <dd className="font-mono break-all">{order.id}</dd>
        </div>
        <div>
          <dt className="text-sm">Total</dt>
          <dd className="font-medium">{formatMoney(order.total)}</dd>
        </div>
      </dl>

      <Link to="/" className="mt-6 inline-block underline">
        Continue shopping
      </Link>
    </>
  );
}
