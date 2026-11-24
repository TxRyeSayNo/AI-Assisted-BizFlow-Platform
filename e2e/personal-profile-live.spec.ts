import { test, expect } from '@playwright/test';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';

// No route interception: these checks exercise Angular's interceptor, dev proxy,
// authentication middleware and the real profile reader together. Read-only.
for (const [account, width] of [['ADMIN001', 1440], ['ADMIN001', 390], ['EMP001', 1440], ['SUPERADMIN', 1440]] as const) {
  test(`real profile loads after UI login and reload: ${account}, ${width}px`, async ({ page }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
    await page.setViewportSize({ width, height: 900 });
    await page.goto(`${base}/login`);
    await page.locator('[formControlName=identifier]').fill(account);
    if (account !== 'SUPERADMIN') {
      await page.locator('[formControlName=tenantKey]').fill('bizflow-corp');
    }
    await page.locator('[formControlName=password]').fill(process.env['BIZFLOW_EVAL_PASSWORD']!);
    const loginResponse = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/login'));
    const initialProfile = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/profile'));
    await page.locator('form button[type=submit], form button:not([type])').last().click();
    expect((await loginResponse).status()).toBe(200);
    const initial = await initialProfile;
    expect(initial.status()).toBe(200);
    expect(initial.request().headers()['authorization']).toMatch(/^Bearer .+/);
    const profile = await initial.json();
    await page.locator(width > 768 ? '.app-topbar .user-chip' : '.mobile-profile-link').click();
    await expect(page).toHaveURL(`${base}/profile`);
    await expect(page.locator('.official-full-name')).toHaveText(profile.fullName);
    await expect(page.locator('#profile-name')).toHaveValue(profile.nickname);
    await expect(page.locator('.account-details')).toContainText(profile.employeeCode);
    await expect(page.locator('.profile-card[role=alert]')).toHaveCount(0);
    const reloadedProfile = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/profile'));
    await page.reload();
    const reloaded = await reloadedProfile;
    expect(reloaded.status()).toBe(200);
    expect(reloaded.request().headers()['authorization']).toMatch(/^Bearer .+/);
    expect((await reloaded.json()).userId).toBe(profile.userId);
    await expect(page.locator('.official-full-name')).toHaveText(profile.fullName);
    await expect(page.locator('.profile-card[role=alert]')).toHaveCount(0);
  });
}

test('real profile recovers through refresh when the access token is rejected', async ({ page, request }) => {
  test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
  const login = await request.post(`${base}/api/v1/auth/login`, {
    data: { identifier: 'ADMIN001', password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: 'bizflow-corp' },
  });
  expect(login.status()).toBe(200);
  const session = await login.json();
  session.accessToken = 'invalid-access-token-for-refresh-check';
  await page.addInitScript(value => sessionStorage.setItem('bizflow_session', JSON.stringify(value)), session);
  const rejected = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/profile') && r.status() === 401);
  const refresh = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/refresh'));
  const recovered = page.waitForResponse(r => r.url().endsWith('/api/v1/auth/profile') && r.status() === 200);
  await page.goto(`${base}/profile`);
  await rejected;
  expect((await refresh).status()).toBe(200);
  const profile = await recovered;
  expect(profile.request().headers()['authorization']).not.toContain('invalid-access-token');
  await expect(page.locator('.official-full-name')).toHaveText((await profile.json()).fullName);
  await expect(page.locator('.profile-card[role=alert]')).toHaveCount(0);
  await expect(page).toHaveURL(`${base}/profile`);
});
