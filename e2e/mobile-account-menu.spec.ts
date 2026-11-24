import { test, expect } from '@playwright/test';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';

for (const [name, width, height, identifier, tenantKey] of [
  ['small-phone', 320, 740, 'ADMIN001', 'bizflow-corp'],
  ['phone', 390, 844, 'EMP001', 'bizflow-corp'],
  ['short-screen', 640, 360, 'ADMIN001', 'bizflow-corp'],
  ['tablet', 768, 1024, 'SUPERADMIN', null],
] as const) {
  test(`account menu navigation, dismissal and logout on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
    const login = await request.post(`${base}/api/v1/auth/login`, {
      data: { identifier, password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey },
    });
    expect(login.ok()).toBeTruthy();
    await page.addInitScript(session => sessionStorage.setItem('bizflow_session', JSON.stringify(session)), await login.json());
    await page.setViewportSize({ width, height });
    await page.goto(`${base}/workspace`);
    const trigger = page.locator('.mobile-account-trigger');
    await expect(trigger).toBeVisible();
    await expect(page.locator('.mobile-profile-link .mobile-account-name')).not.toHaveText('');
    await expect(page.locator('.app-topbar .wordmark')).toBeVisible();
    const brand = (await page.locator('.app-topbar .wordmark').boundingBox())!;
    const notifications = (await page.locator('.app-topbar .notify-btn').boundingBox())!;
    const account = (await page.locator('.mobile-account-control').boundingBox())!;
    expect(brand.x + brand.width).toBeLessThan(notifications.x);
    expect(notifications.x + notifications.width).toBeLessThanOrEqual(account.x);
    expect(await page.locator('.mobile-profile-link .mobile-account-name').evaluate(el => el.clientWidth)).toBeGreaterThan(30);
    await expect(page.locator('.app-topbar .btn-signout')).toBeHidden();
    await expect(page.locator('.app-sidebar')).toBeHidden();
    await expect(page.locator('.app-topbar .notify-btn')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await page.screenshot({ path: `artifacts/mobile-header-${name}.png`, animations: 'disabled' });

    const expectedLinks = await page.locator('.app-sidebar nav a').evaluateAll(links => links.map(a => a.getAttribute('href')));
    await trigger.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(await dialog.locator('nav a').evaluateAll(links => links.map(a => a.getAttribute('href')))).toEqual(expectedLinks);
    for (const label of await dialog.locator('.nav-label').all()) await expect(label).toBeVisible();
    await expect(dialog.locator('.mobile-menu-close')).toBeFocused();
    await page.keyboard.press('Shift+Tab');
    await expect(dialog.locator('.mobile-menu-signout')).toBeFocused();
    expect(await dialog.evaluate(el => el.scrollWidth <= el.clientWidth)).toBeTruthy();
    await page.screenshot({ path: `artifacts/mobile-menu-${name}.png`, animations: 'disabled' });
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(trigger).toBeFocused();

    await trigger.click();
    await page.locator('.mobile-menu-backdrop').click({ position: { x: 2, y: 2 } });
    await expect(dialog).toHaveCount(0);
    await trigger.click();
    await dialog.locator('nav a:not([href="/workspace"])').first().click();
    await expect(dialog).toHaveCount(0);
    await expect(page).not.toHaveURL(`${base}/workspace`);
    await trigger.click();
    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(dialog).toHaveCount(0);
    await expect(page.locator('.app-sidebar')).toBeVisible();
    await expect(page.locator('.app-topbar .wordmark')).toBeVisible();
    await expect(page.locator('.app-topbar .btn-signout')).toBeVisible();
    await expect(trigger).toBeHidden();
    await page.setViewportSize({ width, height });
    await trigger.click();
    await dialog.locator('.mobile-menu-signout').click();
    await expect(page).toHaveURL(`${base}/login`);
    expect(await page.evaluate(() => sessionStorage.getItem('bizflow_session'))).toBeNull();
  });
}
