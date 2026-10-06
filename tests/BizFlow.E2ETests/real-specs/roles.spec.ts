import { test, expect } from './fixtures';

test('authorized administrator creates a custom role and grants/removes permissions through the real API', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByLabel('Workspace key', { exact: true }).fill(process.env.BIZFLOW_E2E_ADMIN_KEY!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await page.getByRole('link', { name: 'Settings', exact: true }).click();
  await page.getByRole('link', { name: 'Roles and permissions', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Roles and permissions', exact: true })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /COMPANY_ADMIN/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /PLATFORM_ADMIN/ })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Configure COMPANY_ADMIN', exact: true })).toHaveCount(0);
  const name = `Support ${testInfo.project.name}`;
  await page.getByLabel('New role name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Create role', exact: true }).click();
  await expect(page.getByRole('rowheader', { name: new RegExp(name) })).toBeVisible();
  await page.getByRole('button', { name: `Configure ${name}`, exact: true }).click();
  await page.getByRole('checkbox', { name: 'roles.configure (Workspace)', exact: true }).check();
  await page.getByRole('button', { name: 'Save permissions', exact: true }).click();
  await expect(page.getByText('Permissions saved.', { exact: false })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: new RegExp(name) })).toContainText('1 permission');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  // Full-page captures of a scrolled viewport can composite fixed off-screen elements into the image.
  await page.evaluate(() => window.scrollTo(0, 0));
  await expect(page.locator('.skip-link')).not.toBeInViewport();
  await page.screenshot({ path: testInfo.outputPath('role-permissions.png'), fullPage: true });
  await page.getByRole('checkbox', { name: 'roles.configure (Workspace)', exact: true }).uncheck();
  await page.getByRole('button', { name: 'Save permissions', exact: true }).click();
  await expect(page.getByRole('rowheader', { name: new RegExp(name) })).toContainText('0 permissions');
  await page.getByLabel('New role name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Create role', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('A role with this name already exists');
});
