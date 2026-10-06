import { test, expect } from './fixtures';

test('manager creates a persisted draft and reopens scoped task details', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByLabel('Workspace key', { exact: true }).fill(process.env['BIZFLOW_E2E_TASK_CREATE_KEY_' + testInfo.project.name.toUpperCase()]!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await page.getByRole('link', { name: 'View tasks', exact: true }).click();
  await page.getByRole('link', { name: 'New task', exact: true }).click();
  await page.getByLabel('Task title', { exact: true }).fill('Prepare operational review');
  await page.getByLabel('Description', { exact: true }).fill('Gather evidence for the team review.');
  await page.getByLabel('Deadline (local time, optional)', { exact: true }).fill('2099-01-01T12:00');
  await page.getByLabel('Checklist (one item per line)', { exact: true }).fill('Collect evidence\nReview findings');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  // Reset the viewport before full-page capture; otherwise Chromium can paint an
  // offscreen fixed skip link at the old scroll offset in the stitched image.
  await page.evaluate(() => window.scrollTo(0, 0));
  expect(await page.locator('.skip-link').evaluate(element => element.getBoundingClientRect().bottom <= 0)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('task-create-form.png'), fullPage: true });
  await page.getByRole('button', { name: 'Create draft', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Prepare operational review', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Task summary' })).toContainText('DRAFT');
  await expect(page.getByRole('region', { name: 'Checklist', exact: true })).toContainText('Collect evidence');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('task-created-detail.png'), fullPage: true });
  await page.getByRole('link', { name: 'Tasks', exact: true }).click();
  await expect(page.getByText('1 task · Page 1', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Prepare operational review', exact: true }).click();
  await expect(page.getByText('Gather evidence for the team review.', { exact: true })).toBeVisible();
});
