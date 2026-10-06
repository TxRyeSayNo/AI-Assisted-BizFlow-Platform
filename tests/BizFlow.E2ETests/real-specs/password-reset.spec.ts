import { test, expect } from './fixtures';

test('real reset link changes password once and permits a new login', async ({ page }, testInfo) => {
  const project = testInfo.project.name.toUpperCase();
  const token = process.env[`BIZFLOW_E2E_RESET_TOKEN_${project}`]!;
  const tenantKey = process.env[`BIZFLOW_E2E_RESET_KEY_${project}`]!;
  const password = 'Browser-reset-passphrase!912';
  // Token came from the real forgot-password endpoint and Hangfire worker; only mail transport
  // is captured by the isolated harness. No production reset-token generation shortcut is used.
  await page.goto(`/reset-password#token=${encodeURIComponent(token)}`);
  await expect(page).toHaveURL(/\/reset-password$/);
  await expect(page.getByRole('heading', { name: 'Reset your password' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  // Capture layout before any credential is filled and after the fragment has been removed.
  await page.screenshot({ path: testInfo.outputPath('reset-layout.png'), fullPage: true });
  await page.getByLabel('Employee code or company email', { exact: true }).fill('EMP001');
  await page.getByLabel('Workspace key', { exact: true }).fill(tenantKey);
  await page.getByLabel('New password', { exact: true }).fill(password);
  await page.getByLabel('Confirm new password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Change password', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Your password has been changed');
  const replay = await page.request.post('/api/v1/auth/reset-password', { data: {
    identifier: 'EMP001', tenantKey, resetToken: token, newPassword: 'Another-browser-password!567',
  } });
  expect(replay.status()).toBe(401);
  await page.getByRole('link', { name: 'Back to sign in' }).click();
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Workspace key', { exact: true }).fill(tenantKey);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await expect(page.getByRole('heading', { name: 'Welcome, Test Employee' })).toBeVisible();
});
