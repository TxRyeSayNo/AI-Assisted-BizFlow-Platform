import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';

if (!process.env.BIZFLOW_E2E_API_URL || !process.env.BIZFLOW_E2E_PASSWORD) {
  throw new Error('Run npm run test:real so the disposable API/database harness owns test credentials.');
}

export default defineConfig({
  testDir: './real-specs',
  outputDir: './test-results-real',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  // Real auth traces contain credentials/tokens. Do not persist network traces or videos.
  use: { baseURL: 'http://127.0.0.1:4281', trace: 'off', video: 'off' },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', use: { ...devices['iPhone 13'], viewport: { width: 375, height: 812 }, defaultBrowserType: 'chromium' } },
  ],
  webServer: {
    command: 'node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4281 --proxy-config ../tests/BizFlow.E2ETests/proxy.real.cjs',
    cwd: path.resolve(__dirname, '../../frontend'),
    url: 'http://127.0.0.1:4281/login',
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
