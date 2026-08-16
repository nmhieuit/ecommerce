import { useListProducts } from '@ecommerce/api-client';
import { formatMoney } from '@/shared/money';
import { ErrorState } from '@/shared/ErrorState';
import { CatalogLoading, EmptyCatalog } from './CatalogStates';
import { AddToBasketButton } from './AddToBasketButton';

/**
 * Spec US1 / FR-001: the products available to buy, each with its name and price, retrieved from
 * the backend.
 *
 * The data comes from the generated TanStack Query hook (ADR-0004) — no hand-written fetch, and no
 * copy of the response into a client store (Principle IX). The hook's cache is the only place this
 * data lives.
 */
export function ProductList() {
  const { data, isPending, isError, refetch } = useListProducts();

  if (isPending) {
    return <CatalogLoading />;
  }

  // The status check is not redundant with `isError`: the generated result type is a union over
  // every declared response, so narrowing on it is what makes `data.data` the product list rather
  // than "product list or problem details". It also degrades correctly — if the transport ever
  // stopped throwing on a failure status, this still renders the error rather than a blank grid.
  if (isError || data.status !== 200) {
    // Deliberately a fixed message rather than the backend's: what failed downstream is not
    // something the shopper can act on, and problem details are for the logs (spec FR-012).
    return (
      <ErrorState
        message="We could not load the products. Please try again."
        onRetry={() => void refetch()}
      />
    );
  }

  const products = data.data.items;

  if (products.length === 0) {
    return <EmptyCatalog />;
  }

  return (
    <ul aria-label="Products" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {products.map((product) => (
        <li key={product.id} className="rounded border border-current/15 p-4">
          <h2 className="font-medium">{product.name}</h2>
          <p className="mt-1">{formatMoney(product.price)}</p>
          <AddToBasketButton productId={product.id} productName={product.name} />
        </li>
      ))}
    </ul>
  );
}
