/**
 * The catalog's non-list states. Kept as plain presentational components — no data fetching — so
 * each is testable on its own and so the list component reads as one decision rather than three
 * nested conditionals.
 */

/**
 * Spec FR-002: a catalog with nothing in it is a legitimate answer, not a failure. Deliberately not
 * an `alert`: announcing normality would interrupt a screen-reader user to report that nothing is
 * wrong.
 */
export function EmptyCatalog() {
  return (
    <p className="py-8">No products available right now. Please check back soon.</p>
  );
}

/** The skeleton shown while the catalog is in flight. */
export function CatalogLoading() {
  return (
    // aria-busy plus a live region, rather than a bare spinner: a shopper who cannot see the
    // spinner still learns that something is happening and, shortly, that it finished.
    <p className="py-8" aria-busy="true" aria-live="polite">
      Loading products…
    </p>
  );
}
