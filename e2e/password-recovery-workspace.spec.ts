import { test, expect } from '@playwright/test';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
for (const [name, viewport] of [
  ['desktop', { width: 1440, height: 900 }],
  ['mobile', { width: 390, height: 844 }],
] as const) {
  test(`company password recovery requires workspace and linked identity on ${name}`, async ({ page }) => {
    await page.setViewportSize(viewport);
    let requested = 0;
    await page.route('**/api/v1/auth/forgot-password', route => {
      requested++;
      expect(route.request().postDataJSON()).toEqual({ identifier: 'EMP001', tenantKey: 'company-a' });
      return route.fulfill({ status: 202, json: { message: 'Accepted' } });
    });
    await page.goto(`${base}/forgot-password`);
    await page.locator('[formControlName=identifier]').fill('EMP001');
    await page.locator('#send-reset-btn').click();
    expect(requested).toBe(0);
    await expect(page.getByText('Thiếu workspace-key', { exact: true })).toBeVisible();
    await page.locator('[formControlName=tenantKey]').fill('company-a');
    await page.locator('#send-reset-btn').click();
    await expect(page.locator('.confirmation-panel')).toContainText('email đã liên kết');
    expect(requested).toBe(1);
    await expect(page.locator('#reset-title')).toBeInViewport();
    expect(await page.locator('.animated-border-wrapper').evaluate(el => el.scrollTop)).toBe(0);
    await page.screenshot({ path: `artifacts/password-recovery-${name}.png`, fullPage: true, animations: 'disabled' });

    // Test-only fragment and intercepted API: never change a real account's password.
    let resetCount = 0;
    await page.route('**/api/v1/auth/reset-password', route => {
      resetCount++;
      const input = route.request().postDataJSON();
      expect(input.tenantKey).toBe('company-a');
      expect(input.identifier).toBe('EMP001');
      expect(input.resetToken).toBe('browser-test-only');
      return route.fulfill({ status: 200, json: {} });
    });
    await page.goto(`${base}/reset-password#token=browser-test-only`);
    await expect(page).toHaveURL(`${base}/reset-password`);

    await expect(page.locator('.auth-intro, .public-header')).toHaveCount(0);
    await expect(page.locator('.auth-card .reset-brand')).toBeVisible();
    await page.locator('.reset-brand img').evaluate((el: HTMLImageElement) => el.decode());
    await page.evaluate(() => document.fonts.ready);
    const card = await page.locator('.auth-card').boundingBox();
    expect(card).not.toBeNull();
    expect(Math.abs(card!.x + card!.width / 2 - viewport.width / 2)).toBeLessThan(2);
    if (card!.height + 48 <= viewport.height) {
      expect(Math.abs(card!.y + card!.height / 2 - viewport.height / 2)).toBeLessThan(2);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(viewport.width);
    await page.screenshot({ path: `artifacts/reset-password-${name}.png`, fullPage: true, animations: 'disabled' });

    await page.locator('[formControlName=identifier]').fill('EMP001');
    await page.locator('[formControlName=newPassword]').fill('Test-only-password!123');
    await page.locator('[formControlName=confirmPassword]').fill('Test-only-password!123');
    await page.locator('button[type=submit]').click();
    expect(resetCount).toBe(0);
    await page.locator('[formControlName=tenantKey]').fill('company-a');
    await page.locator('button[type=submit]').click();
    await expect(page.locator('[formControlName=newPassword]')).toHaveCount(0);
    expect(resetCount).toBe(1);
  });
}
