import { createBrowserRouter, Link, Outlet, RouterProvider } from 'react-router-dom';
import { ProductList } from '@/features/catalog/ProductList';

/**
 * The storefront's routes. Each screen arrives with the user story that owns it — the catalog is
 * here (US1); the basket (US2) and confirmation (US3) are still placeholders and get replaced, not
 * added to.
 */

function Layout() {
  return (
    <div className="min-h-screen bg-[var(--color-surface)] text-[var(--color-ink)]">
      <header className="border-b border-current/10 p-4">
        <nav aria-label="Main">
          <ul className="flex gap-4">
            <li>
              <Link to="/">Products</Link>
            </li>
            <li>
              <Link to="/basket">Basket</Link>
            </li>
          </ul>
        </nav>
      </header>
      <main className="p-6">
        <Outlet />
      </main>
    </div>
  );
}

function Catalog() {
  return (
    <>
      <h1 className="text-2xl font-semibold">Products</h1>
      <div className="mt-6">
        <ProductList />
      </div>
    </>
  );
}

function Placeholder({ title }: { readonly title: string }) {
  return (
    <>
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="mt-2">This screen arrives with its user story.</p>
    </>
  );
}

const router = createBrowserRouter([
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <Catalog /> },
      { path: 'basket', element: <Placeholder title="Basket" /> },
      { path: 'confirmation', element: <Placeholder title="Order confirmation" /> },
    ],
  },
]);

export function AppRoutes() {
  return <RouterProvider router={router} />;
}
