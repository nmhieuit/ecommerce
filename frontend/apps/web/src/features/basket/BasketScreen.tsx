import { useNavigate } from 'react-router-dom';
import { useGetCurrentBasket } from '@ecommerce/api-client';
import { CheckoutButton } from '@/features/checkout/CheckoutButton';
import { BasketView } from './BasketView';

/**
 * The basket screen: what is in the basket, and the way out of it.
 *
 * Reads the basket through the same generated hook `BasketView` uses. That is one request, not two
 * — TanStack Query dedupes by key — and it lets the checkout control know whether there is anything
 * to check out without `BasketView` having to hand it up through props.
 */
export function BasketScreen() {
  const navigate = useNavigate();
  const { data } = useGetCurrentBasket();

  const itemCount = data?.status === 200 ? data.data.items.length : 0;

  return (
    <>
      <h1 className="text-2xl font-semibold">Basket</h1>

      <div className="mt-6">
        <BasketView />
      </div>

      <CheckoutButton
        itemCount={itemCount}
        onCheckedOut={(order) =>
          // The created order travels as navigation state rather than being refetched: it is the
          // result of a mutation the shopper just performed, not a cached resource
          // (data-model.md — Client-side state). `replace` so the browser's back button returns to
          // the basket rather than re-entering a confirmation for an order already placed.
          void navigate('/confirmation', { state: { order }, replace: true })
        }
      />
    </>
  );
}
