import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

/**
 * The Phase 1 demo: one order, placed end to end, on the deployed skeleton (006-e2e-order-demo,
 * User Story 1).
 *
 * This is not a second copy of `e2e/walkthrough.spec.ts`. That spec guards the storefront's own
 * behaviour against the dev server — console errors, keyboard operability, request destinations —
 * and it should keep doing exactly that. This one demonstrates that the *platform* works: the same
 * journey driven through the container stack, producing an order the orders service will hand back,
 * and a recording of it happening (research.md Decision 1).
 *
 * Run it through `./scripts/demo.ps1` (or `demo.sh`), which puts the stack in demo mode and clears
 * the basket first. Run directly against a stack that is down, or dirty, and it fails at the first
 * assertion — deliberately, since a demo that quietly adapts to whatever state it finds is not
 * evidence of anything.
 */

const GATEWAY_ORIGIN = process.env.DEMO_GATEWAY_ORIGIN ?? 'http://localhost:5300';

const NOTEBOOK = 'Field Notes Notebook';
const APRON = 'Linen Apron';

/**
 * Two notebooks at 12.50 and one apron at 34.25. The figures are the seeded catalogue's
 * (`CatalogSeed.cs`), quoted here so a total that silently changes fails loudly rather than being
 * read back from whatever the page happens to show.
 */
const EXPECTED_TOTAL = '$59.25';
const EXPECTED_TOTAL_NUMERIC = 59.25;

/** Repository root, four levels up from this file (frontend/apps/web/demo). */
const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');
const verificationFile = resolve(repositoryRoot, 'artifacts/demo/verification.txt');
const referenceFile = resolve(repositoryRoot, 'artifacts/demo/last-reference.txt');

test('one order, placed end to end, on the running stack', async ({ page }) => {
  /** Adds one of a product and waits for the write to have been accepted. See the call site. */
  const addToBasket = async (productName: string) => {
    const button = page.getByRole('button', { name: `Add ${productName} to basket` });

    await expect(button).toBeEnabled();
    await button.click();
    await expect(button).toBeEnabled();
  };

  // ---- the precondition the demo script is responsible for (FR-007b) ----
  //
  // Asserted rather than assumed. If the basket still holds items from an earlier run, the total
  // below is wrong and the failure would look like a pricing defect; naming the real cause here
  // costs one assertion and saves the wrong investigation.
  await page.goto('/basket');
  await expect(
    page.getByText(/your basket is empty/i),
    'the demo must start from a clean basket — run it through scripts/demo.ps1, which clears it',
  ).toBeVisible();

  // ---- browse (FR-001) ----
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: NOTEBOOK })).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: APRON })).toBeVisible();

  // ---- add to basket ----
  //
  // Each add waits for its own request to settle before the next step. The control disables itself
  // while in flight (AddToBasketButton.tsx) and re-enables when the write has been accepted, so
  // waiting for it to come back is waiting for the server to have the item.
  //
  // Not defensive padding: without it this spec races. Clicking the apron and immediately following
  // the Basket link can render the basket before that third write lands, leaving a basket holding
  // only the two notebooks. The quantity assertion below still passes and the total does not, which
  // reads as a pricing defect rather than the timing one it is. Observed intermittently against the
  // container stack, where the round trip is slower than against a dev server.
  await addToBasket(NOTEBOOK);
  await addToBasket(NOTEBOOK);
  await addToBasket(APRON);

  await page.getByRole('link', { name: 'Basket' }).click();
  await expect(page.getByRole('heading', { name: 'Basket' })).toBeVisible();

  // Two notebooks on one line rather than two lines, and the total the catalogue implies.
  await expect(page.getByText(/quantity:\s*2\s*×\s*\$12\.50/i)).toBeVisible();
  await expect(page.getByText(EXPECTED_TOTAL)).toBeVisible();

  // ---- check out ----
  await page.getByRole('button', { name: 'Check out' }).click();

  // ---- the confirmation carries a reference and a total (FR-002) ----
  await expect(page.getByRole('heading', { name: /your order is placed/i })).toBeVisible();

  const reference = (await page.getByText(/^[0-9a-f-]{36}$/i).innerText()).trim();
  expect(reference, 'the confirmation must show an order reference').toMatch(
    /^[0-9a-f-]{36}$/i,
  );
  await expect(page.getByText(EXPECTED_TOTAL)).toBeVisible();

  // ---- the order the confirmation names actually exists, and matches (FR-003, FR-004) ----
  //
  // Read back through the gateway, which is the only address the storefront may use. The demo also
  // reads it straight from the orders service afterwards, from the script — that call proves tenant
  // attribution and belongs to User Story 2.
  const response = await page.request.get(`${GATEWAY_ORIGIN}/bff/orders/${reference}`);
  expect(response.ok(), `the order ${reference} could not be read back`).toBe(true);

  const order = await response.json();
  expect(order.id).toBe(reference);
  expect(
    order.total,
    'the persisted total must equal the total the shopper was shown',
  ).toBe(EXPECTED_TOTAL_NUMERIC);

  // ---- the basket is empty afterwards, so the next run starts clean (FR-017) ----
  await page.getByRole('link', { name: 'Basket' }).click();
  await expect(page.getByText(/your basket is empty/i)).toBeVisible();

  // ---- hand the reference to the script (FR-012 groundwork) ----
  //
  // Written from here because this is where the reference exists. The script reads both files: the
  // first to print and to query the orders service with, the second to prove a repeat run produced
  // a different order rather than re-reporting this one.
  mkdirSync(dirname(verificationFile), { recursive: true });
  writeFileSync(
    verificationFile,
    [
      'ORDER PLACED',
      `  reference : ${reference}`,
      `  total     : ${EXPECTED_TOTAL}`,
      '',
    ].join('\n'),
    'utf8',
  );
  writeFileSync(referenceFile, reference, 'utf8');
});
