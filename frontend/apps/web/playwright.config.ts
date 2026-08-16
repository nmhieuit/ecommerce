import { defineConfig, devices } from '@playwright/test';

/**
 * The end-to-end walkthrough (T065) is the only thing that runs here. It exists because four
 * success criteria are not observable from jsdom at all: zero browser-console errors (SC-002),
 * keyboard-only completion (SC-009), every request addressed to the gateway and nothing else
 * (SC-010), and a rapid double checkout producing exactly one order (SC-008).
 *
 * `pnpm e2e` has nothing to run until T065 adds the spec.
 */
const storefrontUrl = process.env.STOREFRONT_URL ?? 'http://localhost:5173';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: process.env.CI ? 'blob' : 'list',

  use: {
    baseURL: storefrontUrl,
    trace: 'retain-on-failure',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // The backend (gateway on :5300 and the services behind it) is started separately — see
  // quickstart.md. Only the storefront is Playwright's to launch.
  webServer: {
    command: 'pnpm dev',
    url: storefrontUrl,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
