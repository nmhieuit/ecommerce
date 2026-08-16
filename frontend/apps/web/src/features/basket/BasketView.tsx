import { useGetCurrentBasket } from '@ecommerce/api-client';
import { formatMoney } from '@/shared/money';
import { ErrorState } from '@/shared/ErrorState';

/**
 * Spec US2 / FR-004: the shopper's basket, with each line's name, quantity, and price, and the
 * basket total.
 *
 * Every amount shown here comes from the backend. Nothing is multiplied or summed in the browser —
 * a total the client works out for itself is a total that can disagree with the one being charged
 * (004 plan.md, post-design re-check).
 */
export function BasketView() {
  const { data, isPending, isError, refetch } = useGetCurrentBasket();

  if (isPending) {
    return (
      <p className="py-8" aria-busy="true" aria-live="polite">
        Loading your basket…
      </p>
    );
  }

  // The status check narrows the generated result union; see ProductList for why it is not
  // redundant with isError.
  if (isError || data.status !== 200) {
    return (
      <ErrorState
        message="We could not load your basket. Please try again."
        onRetry={() => void refetch()}
      />
    );
  }

  const basket = data.data;

  if (basket.items.length === 0) {
    // Not an alert: an empty basket is a legitimate state — a first visit, or the state right
    // after a successful checkout (spec FR-010).
    return <p className="py-8">Your basket is empty.</p>;
  }

  return (
    <>
      <ul aria-label="Basket" className="divide-y divide-current/10">
        {basket.items.map((item) => (
          <li key={item.productId} className="flex items-baseline justify-between gap-4 py-3">
            <div>
              <p className="font-medium">{item.name}</p>
              <p className="text-sm">
                Quantity: {item.quantity} × {formatMoney(item.unitPrice)}
              </p>
            </div>
            <p>{formatMoney(item.lineTotal)}</p>
          </li>
        ))}
      </ul>

      <p className="mt-4 flex justify-between border-t border-current/20 pt-4 font-medium">
        <span>Total</span>
        <span>{formatMoney(basket.total)}</span>
      </p>
    </>
  );
}
