import { expect, type Page } from '@playwright/test';

/**
 * Signs the storefront in through its own login form, the way a shopper would.
 *
 * The credential is the dev/test account the identity server provisions
 * (services/identity TestUserSeed + the TestUserPassword in the repository's .env) and is read from
 * the environment — never written into this repository:
 *
 *   E2E_USERNAME=postman-test@local.test  E2E_PASSWORD=<TestUserPassword from .env>  pnpm e2e
 */
export async function signIn(page: Page): Promise<void> {
  const username = process.env.E2E_USERNAME;
  const password = process.env.E2E_PASSWORD;

  if (!username || !password) {
    throw new Error('Set E2E_USERNAME and E2E_PASSWORD (the dev test user) to run the e2e suite.');
  }

  await page.goto('/login');
  await page.getByLabel('Username').fill(username);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Products' })).toBeVisible();
}

/** The Authorization header for calls the test makes itself (outside the page's own fetches). */
export async function authorizationHeader(page: Page): Promise<Record<string, string>> {
  const token = await page.evaluate(() => {
    const raw = window.sessionStorage.getItem('storefront.session');
    return raw === null ? null : (JSON.parse(raw) as { accessToken: string }).accessToken;
  });

  return token === null ? {} : { Authorization: `Bearer ${token}` };
}
