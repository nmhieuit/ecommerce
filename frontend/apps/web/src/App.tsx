/**
 * Placeholder root. The real shell — router, QueryClientProvider, and the single gateway origin
 * every request is bound to — arrives with T020 in the Foundational phase; the catalog screen it
 * renders arrives with T031. This exists so Phase 1's scaffolding is verifiably runnable rather
 * than a directory of config files nobody has started.
 */
export function App() {
  return (
    <main className="min-h-screen bg-[var(--color-surface)] p-8 text-[var(--color-ink)]">
      <h1 className="text-2xl font-semibold">Storefront</h1>
      <p className="mt-2">Scaffolding in place. Screens land in the user story phases.</p>
    </main>
  );
}
