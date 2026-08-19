import { defineConfig, devices } from '@playwright/test';

/**
 * The Phase 1 order demo (006-e2e-order-demo). Separate from `playwright.config.ts`, and not a
 * variant of it, for one reason: they point at different things.
 *
 * `playwright.config.ts` runs the 004 walkthrough against the **Vite dev server**, which it starts
 * itself via `webServer`. That is the right target for a test that guards the storefront's own
 * behaviour while someone is working on it.
 *
 * This config runs against the **container stack** — the storefront image on :4173, the gateway on
 * :5300 — and starts nothing. The story asks for a demo "on the deployed skeleton", and the dev
 * server is not that: different bundling, different origin, different CORS treatment. Demonstrating
 * it would prove something adjacent to the claim being made (research.md Decision 1).
 *
 * Bringing the stack up is the demo script's job, not Playwright's, so there is deliberately no
 * `webServer` block here. Run `./scripts/demo.ps1` (or `demo.sh`) rather than this config directly;
 * against a stack that is down, every test here fails at the first navigation, by design.
 */

/** The containerised storefront. Overridable only so the script can pass what it started. */
const storefrontUrl = process.env.DEMO_STOREFRONT_URL ?? 'http://localhost:4173';

export default defineConfig({
  testDir: './demo',

  // One demo, run start to finish, in order. Parallelism would interleave two shoppers through one
  // shared server-side basket and make the recording incoherent.
  fullyParallel: false,
  workers: 1,

  // A demo that needed a second attempt to pass is not evidence of a repeatable flow — it is
  // evidence of a flaky one, and FR-007 is precisely the requirement a retry would paper over.
  retries: 0,

  forbidOnly: !!process.env.CI,
  reporter: [['list']],

  // Everything the run produces that is NOT committed. The stills go to docs/demo/ instead, written
  // explicitly by the spec, because Playwright's output directory is git-ignored and committed
  // evidence cannot live in an ignored path (research.md Decision 8).
  outputDir: '../../../artifacts/demo/playwright',

  use: {
    baseURL: storefrontUrl,

    // The point of the exercise: a replayable recording of the whole flow. Kept out of the
    // repository by .gitignore and attached to SCRUM-16 instead (spec Clarifications).
    video: {
      mode: 'on',
      size: { width: 1280, height: 720 },
    },

    // A trace costs little and turns "the demo failed" into "the demo failed here", which matters
    // when the thing being demonstrated spans five components.
    trace: 'retain-on-failure',

    // Fixed so successive recordings are comparable and the committed stills line up.
    viewport: { width: 1280, height: 720 },
  },

  projects: [{ name: 'demo', use: { ...devices['Desktop Chrome'] } }],
});
