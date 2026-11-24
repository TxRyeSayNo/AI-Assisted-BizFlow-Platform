import { expect, test } from '@playwright/test';

// UI contract tests with a mocked response. These are NOT TST-AUTH-001 or TST-SEC-001.
test('login layout, validation and generic credential failure', async ({ page }, testInfo) => {
  await page.route('**/api/v1/auth/login', route => route.fulfill({
    status: 401, contentType: 'application/json', body: JSON.stringify({ code: 'AUTH.INVALID_CREDENTIALS', message: 'internal-secret' }),
  }));
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: /đăng nhập tài khoản/i })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: /đăng nhập ngay/i }).click();
  await page.getByRole('textbox', { name: /mã không gian làm việc/i }).fill('bizflow-corp');
  await page.getByRole('textbox', { name: /tài khoản/i }).fill('EMP001');
  await page.locator('input[formControlName="password"]').fill('not-a-real-password');
  await page.getByRole('button', { name: /đăng nhập ngay/i }).click();
  await expect(page.getByRole('alert').first()).toBeVisible();
  await expect(page.getByRole('alert').first()).not.toContainText('internal-secret');
  await expect(page.locator('input[formControlName="password"]')).toHaveValue('');
  await page.screenshot({ path: testInfo.outputPath('login.png'), fullPage: true });
});

test('unauthenticated direct navigation returns to sign-in', async ({ page }) => {
  await page.goto('/workspace');
  await expect(page).toHaveURL(/\/login$/);
});
