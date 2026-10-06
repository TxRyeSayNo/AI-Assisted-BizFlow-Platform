const { chromium, expect } = require('@playwright/test');

// Invoked by the isolated Compose smoke script, not against a user/deployed workspace.
const origin = new URL(process.env.BIZFLOW_SMOKE_URL ?? 'http://invalid');
if (origin.protocol !== 'http:' || origin.hostname !== '127.0.0.1' || !origin.port || origin.username || origin.password) {
  throw new Error('Run deploy/Test-Containers.ps1 -Browser with its disposable loopback project.');
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    for (const viewport of [{ width: 1280, height: 900 }, { width: 375, height: 812 }]) {
      const context = await browser.newContext({ viewport, baseURL: origin.origin });
      context.setDefaultTimeout(15000);
      try {
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.goto('/login');
        await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
        await page.getByLabel('Employee code', { exact: true }).fill('MISSING-SMOKE');
        await page.getByLabel('Password', { exact: true }).fill('not-a-real-password');
        await page.getByRole('button', { name: 'Sign in', exact: true }).click();
        await expect(page.getByRole('alert')).toContainText('We could not sign you in');
        await expect(page.getByLabel('Password', { exact: true })).toHaveValue('');
        await page.getByRole('link', { name: 'Forgot your password?', exact: true }).click();
        await expect(page).toHaveURL(/\/forgot-password$/);
        const resourceId = '00000000-0000-7000-8000-000000000001';
        const protectedRoutes = [
          '/workspace', '/tasks', '/notifications', '/audit', '/platform/audit', '/platform/companies',
          '/settings', '/settings/services', '/settings/sla-profiles',
          `/settings/sla-profiles/${resourceId}/versions`, '/settings/workflows',
          `/settings/workflows/${resourceId}/versions/${resourceId}`,
          '/settings/organization/users', '/settings/organization/roles', '/settings/organization/departments',
        ];
        for (const route of protectedRoutes) {
          await page.goto(route);
          await expect(page).toHaveURL(/\/login$/);
          await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
        }
        expect(errors).toEqual([]);
        console.log(`PASS: packaged Angular browser flow at ${viewport.width}x${viewport.height}.`);
      } finally { await context.close(); }
    }
  } finally { await browser.close(); }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
