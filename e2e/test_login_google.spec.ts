import { test, expect } from '@playwright/test';

test('capture google sign-in popup or dialog', async ({ page, context }) => {
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  // Listen for popup
  const [popup] = await Promise.all([
    page.waitForEvent('popup', { timeout: 10000 }).catch(() => null),
    page.locator('.btn-google-signin').click(),
  ]);

  if (popup) {
    console.log('Popup URL:', popup.url());
    await popup.waitForLoadState('domcontentloaded');
    await popup.waitForTimeout(2000);
    console.log('Popup final URL:', popup.url());
    await popup.screenshot({ path: 'C:/Users/admin/.gemini/antigravity/brain/3db4b9de-2cfa-42f2-a995-62b72b23ff07/google_popup_live.png' });
  } else {
    console.log('No external popup detected, checking in-page dialog');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: 'C:/Users/admin/.gemini/antigravity/brain/3db4b9de-2cfa-42f2-a995-62b72b23ff07/google_inpage_dialog.png' });
  }
});
