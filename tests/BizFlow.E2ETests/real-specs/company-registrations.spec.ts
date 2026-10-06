import { test, expect } from './fixtures';

test('platform login opens the real pending registration queue and filters records', async ({ page }, testInfo) => {
  await page.goto('/login');
  await page.getByLabel('Employee code', { exact: true }).fill('PLATFORM');
  await page.getByLabel('Password', { exact: true }).fill(process.env.BIZFLOW_E2E_PASSWORD!);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace$/);
  await page.getByRole('link', { name: 'Review company registrations' }).click();
  await expect(page.getByRole('heading', { name: 'Company registrations', exact: true })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Browser onboarding A/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Browser onboarding B/ })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const firstRow = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: /Browser onboarding A/ }) });
  expect((await firstRow.boundingBox())!.height).toBeLessThan(140);
  await page.screenshot({ path: testInfo.outputPath('registration-queue.png'), fullPage: true });
  if (testInfo.project.name === 'mobile') {
    const region = page.getByRole('region', { name: /Company registration records; scroll/ });
    await region.evaluate(element => { element.scrollLeft = element.scrollWidth; });
    await page.screenshot({ path: testInfo.outputPath('registration-queue-scrolled.png'), fullPage: true });
  }
  await page.getByLabel('Company name or code').fill('BROWSER-ONBOARDING-A');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await expect(page.getByRole('rowheader', { name: /Browser onboarding A/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Browser onboarding B/ })).toHaveCount(0);
  await page.getByLabel('Company name or code').fill('no-matching-company');
  await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
  await expect(page.getByText('No company registrations match these filters.', { exact: false })).toBeVisible();
});
