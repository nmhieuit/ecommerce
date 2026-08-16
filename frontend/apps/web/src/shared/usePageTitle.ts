import { useEffect } from 'react';

const SITE_NAME = 'Storefront';

/**
 * Sets the document title for the current screen.
 *
 * WCAG 2.2 "Page Titled" (2.4.2, Level A) applies per page, and in a single-page app the browser
 * never changes the title on its own — so without this every screen would announce itself as the
 * same page to a screen reader, and every browser tab and history entry would read alike.
 */
export function usePageTitle(title: string): void {
  useEffect(() => {
    document.title = `${title} · ${SITE_NAME}`;
  }, [title]);
}
