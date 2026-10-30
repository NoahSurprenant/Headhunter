import { defineConfig, devices } from '@playwright/test';

// The end-to-end tests (e2e/*.spec.ts) against the production build (`npx ng build`) as e2e/serve.mjs
// serves it, with the API mocked. `npm run e2e` builds and runs them.
const port = Number(process.env.E2E_PORT ?? 4300);

export default defineConfig({
  testDir: 'e2e',
  outputDir: 'e2e-results',
  timeout: 90_000,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    ...devices['Desktop Chrome'],
    baseURL: `http://127.0.0.1:${port}`,
    trace: 'retain-on-failure',
    // The globe draws with WebGL; on a runner without a GPU, in software
    launchOptions: {
      args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
      executablePath: process.env.E2E_CHROMIUM || undefined,
    },
  },
  webServer: {
    command: 'node e2e/serve.mjs',
    url: `http://127.0.0.1:${port}/`,
    reuseExistingServer: !process.env.CI,
  },
});
