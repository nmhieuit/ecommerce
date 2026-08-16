import { useEffect, useRef, useState } from 'react';
import { createBrowserRouter, Link, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { ProductList } from '@/features/catalog/ProductList';
import { BasketScreen } from '@/features/basket/BasketScreen';
import { ConfirmationScreen } from '@/features/checkout/ConfirmationScreen';
import { usePageTitle } from '@/shared/usePageTitle';

/** The storefront's three screens: browse, basket, confirmation. */

function Layout() {
  const { pathname } = useLocation();
  const mainRef = useRef<HTMLElement>(null);
  const isFirstRender = useRef(true);

  useEffect(() => {
    // A client-side navigation leaves focus wherever the link was, so a keyboard or screen-reader
    // user is silently stranded in the header while the page beneath them changes. Moving focus to
    // the new main region is the standard remedy — skipped on first render, where the browser's own
    // focus is already correct and stealing it would be the disruption.
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }

    mainRef.current?.focus();
  }, [pathname]);

  return (
    <div className="min-h-screen bg-[var(--color-surface)] text-[var(--color-ink)]">
      {/* WCAG 2.4.1 "Bypass Blocks": visible only once focused, so a keyboard user can jump past
          the navigation instead of tabbing through it on every screen. */}
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:m-2 focus:rounded focus:bg-[var(--color-surface)] focus:p-2"
      >
        Skip to content
      </a>

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

      {/* tabIndex -1 so the region can receive programmatic focus without joining the tab order. */}
      <main id="main" ref={mainRef} tabIndex={-1} className="p-6">
        <Outlet />
      </main>
    </div>
  );
}

function Catalog() {
  usePageTitle('Products');

  return (
    <>
      <h1 className="text-2xl font-semibold">Products</h1>
      <div className="mt-6">
        <ProductList />
      </div>
    </>
  );
}

function createRouter() {
  return createBrowserRouter([
    {
      path: '/',
      element: <Layout />,
      children: [
        { index: true, element: <Catalog /> },
        { path: 'basket', element: <BasketScreen /> },
        { path: 'confirmation', element: <ConfirmationScreen /> },
      ],
    },
  ]);
}

export function AppRoutes() {
  // Created per mount rather than at module scope. In the browser that is the same single router
  // either way, but a module-level one is a hidden global: it keeps its history across every mount
  // in a process, so two tests in a file share navigation state and the second one silently starts
  // wherever the first left off.
  const [router] = useState(createRouter);

  return <RouterProvider router={router} />;
}
