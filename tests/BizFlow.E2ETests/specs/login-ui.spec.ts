import { expect, test } from '@playwright/test';

// UI contract tests with a mocked response. These are NOT TST-AUTH-001 or TST-SEC-001.
test('login layout, validation and generic credential failure', async ({ page }, testInfo) => {
  await page.route('**/api/v1/auth/login', route => route.fulfill({
    status: 401, contentType: 'application/json', body: JSON.stringify({ code: 'AUTH.INVALID_CREDENTIALS', message: 'internal-secret' }),
  }));
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByText('Enter your employee code (up to 50 characters).')).toBeVisible();
  await page.getByRole('textbox', { name: 'Employee code', exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill('not-a-real-password');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('We could not sign you in');
  await expect(page.getByRole('alert')).not.toContainText('internal-secret');
  await expect(page.getByLabel('Password', { exact: true })).toHaveValue('');
  await page.screenshot({ path: testInfo.outputPath('login.png'), fullPage: true });
});

test('company email mode and workspace-context recovery', async ({ page }) => {
  await page.route('**/api/v1/auth/login', route => route.fulfill({
    status: 409, contentType: 'application/json', body: JSON.stringify({ code: 'AUTH.TENANT_CONTEXT_REQUIRED' }),
  }));
  await page.goto('/login');
  await page.getByRole('radio', { name: 'Company email', exact: true }).click();
  await page.getByRole('textbox', { name: 'Company email', exact: true }).fill('employee@company.test');
  await page.getByLabel('Password', { exact: true }).fill('not-a-real-password');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('workspace key');
  await expect(page.getByRole('textbox', { name: 'Workspace key' })).toHaveAttribute('aria-required', 'true');
});

test('unauthenticated direct navigation returns to sign-in', async ({ page }) => {
  await page.goto('/workspace');
  await expect(page).toHaveURL(/\/login$/);
});
