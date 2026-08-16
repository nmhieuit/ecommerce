import { useQueryClient } from '@tanstack/react-query';
import { getGetCurrentBasketQueryKey, useAddBasketItem } from '@ecommerce/api-client';
import { ErrorState } from '@/shared/ErrorState';

export interface AddToBasketButtonProps {
  readonly productId: string;
  readonly productName: string;
}

/**
 * Spec FR-003: the shopper adds a listed product to their basket from the product list.
 *
 * The request names the product and a quantity, and nothing else. There is no price field to send
 * — the BFF resolves that from the catalog, because a price the client could set is a discount the
 * client could grant (004 contracts/bff-openapi.yaml).
 */
export function AddToBasketButton({ productId, productName }: AddToBasketButtonProps) {
  const queryClient = useQueryClient();

  const { mutate, isPending, isError } = useAddBasketItem({
    mutation: {
      onSuccess: () => {
        // The basket is server state; the way it becomes correct after a write is by asking the
        // server again, not by patching a client-side copy (Principle IX). This is also what makes
        // US2 scenario 5 hold — nothing appears in the basket that the backend did not accept.
        void queryClient.invalidateQueries({ queryKey: getGetCurrentBasketQueryKey() });
      },
    },
  });

  return (
    <>
      <button
        type="button"
        // Unavailable while in flight, so a shopper clicking repeatedly cannot queue several
        // additions of one product.
        disabled={isPending}
        onClick={() => mutate({ data: { productId, quantity: 1 } })}
        className="mt-3 rounded border border-current/30 px-3 py-1 disabled:opacity-50"
      >
        {/* The product name is in the accessible name, not only in the row above it: a screen
            reader user tabbing through a grid of identical "Add to basket" buttons otherwise has
            no way to tell which is which. */}
        Add {productName} to basket
      </button>

      {isError ? (
        <div className="mt-2">
          <ErrorState message={`We could not add ${productName} to your basket. Please try again.`} />
        </div>
      ) : null}
    </>
  );
}
