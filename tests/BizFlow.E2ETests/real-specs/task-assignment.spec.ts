import { test, expect } from './fixtures';

test('manager assigns real work and recipient opens its in-app notification', async ({ page }, info) => {
  const key = process.env['BIZFLOW_E2E_TASK_CREATE_KEY_ASSIGNMENT_' + info.project.name.toUpperCase()]!;
  async function login(code: string) {
    await page.goto('/login');
    await page.getByLabel('Employee code', { exact: true }).fill(code);
    await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
    await page.getByLabel('Workspace key', { exact: true }).fill(key);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await expect(page).toHaveURL(/\/workspace$/);
  }
  await login('EMP001'); await page.getByRole('link', { name: 'View tasks', exact: true }).click();
  await page.getByRole('link', { name: 'New task', exact: true }).click();
  await page.getByLabel('Task title', { exact: true }).fill('Assigned operational review');
  await page.getByRole('button', { name: 'Create draft', exact: true }).click();
  await page.getByRole('button', { name: 'Assign task', exact: true }).click();
  await page.getByLabel('Assignment target', { exact: true }).click();
  await page.getByRole('option', { name: 'Assignment recipient · ASSIGNEE', exact: true }).click();
  await page.getByLabel('Assignment note (optional)', { exact: true }).fill('Please review the operational evidence.');
  await page.screenshot({ path: info.outputPath('assignment-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Confirm assignment', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Task summary' })).toContainText('ASSIGNED');
  await expect(page.getByRole('region', { name: 'Task summary' })).toContainText('Assignment recipient');
  await login('ASSIGNEE');
  await page.getByRole('link', { name: 'Notifications', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Task assigned', exact: true })).toContainText('Assigned operational review');
  await page.getByRole('link', { name: 'Open task', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Assigned operational review', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Assign task', exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('recipient-task.png'), fullPage: true });
  await page.getByRole('button', { name: 'Accept task', exact: true }).click();
  await page.getByLabel('Acceptance note (optional)', { exact: true }).fill('Ready to undertake the review.');
  await page.screenshot({ path: info.outputPath('acceptance-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Confirm acceptance', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Task summary' })).toContainText('ACCEPTED');
  await expect(page.getByRole('button', { name: 'Accept task', exact: true })).toHaveCount(0);
  await login('EMP001');
  await page.getByRole('link', { name: 'Notifications', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Task accepted', exact: true })).toContainText('Assigned operational review');
});
