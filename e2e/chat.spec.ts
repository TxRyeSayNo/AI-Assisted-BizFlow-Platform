import { test, expect } from '@playwright/test';

test('check page', async ({ page }) => {
  await page.goto('http://localhost:4200/login');
  await expect(page).toHaveTitle(/BizFlow/);
});

