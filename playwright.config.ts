import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 90000,
  use: {
    channel: 'msedge',
    headless: true,
    viewport: { width: 1440, height: 900 },
  },
});
