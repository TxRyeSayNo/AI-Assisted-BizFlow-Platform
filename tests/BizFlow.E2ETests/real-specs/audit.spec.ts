import { test, expect } from './fixtures';

test('company administrator filters real audited mutations and reads before/after details', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('EMP001');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByLabel('Workspace key', { exact: true }).fill(process.env.BIZFLOW_E2E_ADMIN_KEY!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Settings', exact: true }).click();
  await page.getByRole('link', { name: 'Roles and permissions', exact: true }).click();
  const roleName = `Audit fixture ${testInfo.project.name}`;
  await page.getByLabel('New role name', { exact: true }).fill(roleName);
  await page.getByRole('button', { name: 'Create role', exact: true }).click();
  await page.getByRole('button', { name: `Configure ${roleName}`, exact: true }).click();
  await page.getByRole('checkbox', { name: 'users.read (Workspace)', exact: true }).check();
  await page.getByRole('button', { name: 'Save permissions', exact: true }).click();
  await expect(page.getByText('Permissions saved.', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Workspace', exact: true }).click();
  await page.getByRole('link', { name: 'Audit history', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Workspace audit', exact: true })).toBeVisible();
  await page.getByLabel('Action code', { exact: true }).fill('ROLE.PERMISSIONS_CONFIGURED');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await page.getByRole('button', { name: /^View audit event/ }).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Audit event details', exact: true })).toBeVisible();
  await expect(dialog).toContainText(roleName);
  await expect(dialog.getByRole('heading', { name: 'Before', exact: true })).toBeVisible();
  await expect(dialog.getByRole('heading', { name: 'After', exact: true })).toBeVisible();
  await expect(page.locator('.mat-mdc-dialog-inner-container')).toHaveCSS('opacity', '1');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  // A modal is viewport-bound; avoid full-page compositing during its opening transition.
  await page.screenshot({ path: testInfo.outputPath('audit-details.png'), animations: 'disabled' });
  await dialog.getByText('No metadata recorded.', { exact: true }).scrollIntoViewIfNeeded();
  await expect(dialog.getByText('No metadata recorded.', { exact: true })).toBeInViewport({ ratio: 1 });
  await expect(dialog.getByRole('button', { name: 'Close details', exact: true })).toBeInViewport({ ratio: 1 });
  await page.screenshot({ path: testInfo.outputPath('audit-details-end.png'), animations: 'disabled' });
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^View audit event/ }).first()).toBeFocused();
  await page.getByLabel('Action code', { exact: true }).fill('NO.SUCH.ACTION');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await expect(page.getByText('No audit events match these filters.', { exact: true })).toBeVisible();
});

test('platform audit remains a separate stream from tenant business changes', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('PLATFORM');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Platform audit', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Platform audit', exact: true })).toBeVisible();
  await page.getByLabel('Action code', { exact: true }).fill('AUTH.LOGIN_SUCCEEDED');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await expect(page.getByRole('rowheader', { name: /AUTH.LOGIN_SUCCEEDED/ }).first()).toBeVisible();
  await page.getByRole('button', { name: /^View audit event/ }).first().click();
  await expect(page.getByRole('dialog')).toContainText('Platform event');
  await page.getByRole('button', { name: 'Close details', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('.cdk-overlay-backdrop')).toHaveCount(0);
  const viewButton = page.getByRole('button', { name: /^View audit event/ }).first();
  await expect(viewButton).toHaveCSS('white-space', 'nowrap');
  // Measure the visible label, not Material's overflowing focus/ripple/touch layers.
  const buttonLayout = await viewButton.evaluate(button => {
    const range = document.createRange();
    range.selectNodeContents(button.querySelector('.mdc-button__label')!);
    const textRects = [...range.getClientRects()].filter(rect => rect.width > 0);
    const bounds = button.getBoundingClientRect();
    const cell = button.closest('td')!.getBoundingClientRect();
    return {
      textLines: new Set(textRects.map(rect => rect.top)).size,
      textFits: textRects.every(rect => rect.left >= bounds.left && rect.right <= bounds.right),
      buttonFitsCell: bounds.left >= cell.left && bounds.right <= cell.right,
    };
  });
  expect(buttonLayout).toEqual({ textLines: 1, textFits: true, buttonFitsCell: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('platform-audit.png'), fullPage: true, animations: 'disabled' });
  await page.getByLabel('Action code', { exact: true }).fill('ROLE.PERMISSIONS_CONFIGURED');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await expect(page.getByText('No audit events match these filters.', { exact: true })).toBeVisible();
});
