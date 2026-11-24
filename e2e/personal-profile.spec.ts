import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
for (const [name, width, height] of [['desktop', 1440, 1000], ['mobile', 390, 844], ['small-phone', 320, 740]] as const) {
  test(`personal profile viewing, editing, avatar and password UX on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
    const login = await request.post(`${base}/api/v1/auth/login`, { data: { identifier: 'ADMIN001', password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: 'bizflow-corp' } });
    expect(login.ok()).toBeTruthy();
    const session = await login.json();
    await page.addInitScript(value => sessionStorage.setItem('bizflow_session', JSON.stringify(value)), session);
    const profile = { userId: session.session.userId, fullName: session.session.fullName, nickname: session.session.fullName, employeeCode: 'ADMIN001', email: 'profile@example.test', departmentName: 'Ban Điều hành', tenantName: session.session.tenantName, tenantKey: 'bizflow-corp', roles: ['COMPANY_ADMIN'], hasAvatar: false, createdAt: '2026-11-24T08:00:00Z', minimumPasswordLength: 12 };
    let avatarWrites = 0, passwordWrites = 0;
    const image = readFileSync('frontend/public/assets/brand/bizflow-logo.png');
    // Browser mutations are intercepted. Real persistence, isolation and credential revocation
    // are tested against a disposable PostgreSQL database by PersonalProfileTests.
    await page.route('**/api/v1/auth/profile', route => {
      if (route.request().method() === 'PATCH') {
        expect(route.request().postDataJSON()).toEqual({ nickname: 'Nguyễn Văn Hồ sơ' });
        profile.nickname = route.request().postDataJSON().nickname;
        return route.fulfill({ status: 204 });
      }
      return route.fulfill({ json: profile });
    });
    await page.route('**/api/v1/auth/profile/avatar', route => {
      if (route.request().method() === 'GET') return route.fulfill({ contentType: 'image/png', body: image });
      avatarWrites++;
      profile.hasAvatar = route.request().method() !== 'DELETE';
      if (profile.hasAvatar) {
        expect(route.request().headers()['content-type']).toContain('multipart/form-data');
        expect(route.request().postDataBuffer()!.includes(Buffer.from('avatar.png'))).toBeTruthy();
      }
      return route.fulfill({ status: 204 });
    });
    await page.route('**/api/v1/auth/profile/change-password', route => {
      passwordWrites++;
      const body = route.request().postDataJSON();
      if (body.currentPassword === 'wrong-password') return route.fulfill({ status: 422, json: { code: 'AUTH.CURRENT_PASSWORD_INVALID', message: 'Mật khẩu hiện tại không chính xác.' } });
      return route.fulfill({ status: 204 });
    });
    await page.setViewportSize({ width, height });
    await page.goto(`${base}/workspace`);
    await page.locator(width > 768 ? '.app-topbar .user-chip' : '.mobile-profile-link').click();
    await expect(page).toHaveURL(`${base}/profile`);
    await expect(page.locator('h1')).toHaveText('Hồ sơ cá nhân');
    await expect(page.locator('.account-details')).toContainText('profile@example.test');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await expect(page.locator('.official-full-name')).toHaveText(session.session.fullName);
    await expect(page.locator('input[formControlName=fullName]')).toHaveCount(0);
    await page.locator('#profile-name').fill('Nguyễn Văn Hồ sơ');
    await page.getByRole('button', { name: 'Lưu thông tin', exact: true }).click();
    await expect(page.locator(width > 768 ? '.app-topbar .user-name' : '.mobile-account-name')).toHaveText('Nguyễn Văn Hồ sơ');
    await expect(page.locator('.official-full-name')).toHaveText(session.session.fullName);
    await page.locator('#profile-avatar-input').setInputFiles({ name: 'invalid.png', mimeType: 'image/png', buffer: Buffer.from('not-an-image') });
    await expect(page.getByText('Không đọc được ảnh.', { exact: false })).toBeVisible();
    expect(avatarWrites).toBe(0);
    await page.locator('#profile-avatar-input').setInputFiles({ name: 'photo.png', mimeType: 'image/png', buffer: image });
    await expect(page.locator('.profile-avatar[src]')).toBeVisible();
    expect(avatarWrites).toBe(1);
    await expect(page.locator('.app-topbar .user-avatar-img').first()).toHaveAttribute('src', /^blob:/);
    await page.locator('#profile-current-password').fill('wrong-password');
    await page.locator('#profile-new-password').fill('Profile-test-new!123');
    await page.locator('#profile-confirm-password').fill('mismatch');
    await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await expect(page.getByText('Xác nhận mật khẩu mới chưa khớp.')).toBeVisible();
    expect(passwordWrites).toBe(0);
    await page.locator('#profile-confirm-password').fill('Profile-test-new!123');
    await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await expect(page.getByText('Mật khẩu hiện tại không chính xác.')).toBeVisible();
    await expect(page.locator('#profile-current-password')).toHaveValue('');
    expect(passwordWrites).toBe(1);
    for (const close of await page.locator('.toast-close-btn').all()) await close.click();
    await expect(page.locator('.toast-card')).toHaveCount(0);
    await page.evaluate(() => { document.querySelectorAll('*').forEach(el => { if (el.scrollTop) el.scrollTop = 0; }); window.scrollTo(0, 0); });
    await page.evaluate(() => document.fonts.ready);
    await page.screenshot({ path: `artifacts/personal-profile-${name}.png`, fullPage: true, animations: 'disabled' });
    await page.getByRole('button', { name: 'Xóa ảnh', exact: true }).click();
    await expect(page.locator('.avatar-placeholder')).toBeVisible();
    await page.locator('#profile-current-password').fill('Test-only-current!123');
    await page.locator('#profile-new-password').fill('Profile-test-new!123');
    await page.locator('#profile-confirm-password').fill('Profile-test-new!123');
    await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await expect(page).toHaveURL(`${base}/login`);
    expect(await page.evaluate(() => sessionStorage.getItem('bizflow_session'))).toBeNull();
  });
}

test('profile redirects unauthenticated visitors to login', async ({ page }) => {
  await page.goto(`${base}/profile`);
  await expect(page).toHaveURL(`${base}/login`);
});

test('profile reloads after an earlier failure and chat uses nickname with official-name tooltip', async ({ page, request }) => {
  test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
  const login = await request.post(`${base}/api/v1/auth/login`, { data: { identifier: 'ADMIN001', password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: 'bizflow-corp' } });
  expect(login.ok()).toBeTruthy(); const session = await login.json();
  await page.addInitScript(value => {
    sessionStorage.setItem('bizflow_session', JSON.stringify(value));
    const scope = `${value.session.tenantId}_${value.session.userId}`;
    localStorage.setItem(`bizflow_ai_current_session_${scope}`, JSON.stringify({ id: 'nickname-ui-test', branches: [], messages: [{ id: 'nickname-message', sender: 'user', text: 'Kiểm tra biệt danh', time: '10:00' }] }));
  }, session);
  let requests = 0;
  await page.route('**/api/v1/auth/profile', route => {
    requests++;
    if (requests <= 2) return route.fulfill({ status: 503, json: { message: 'Temporary failure' } });
    return route.fulfill({ json: { userId: session.session.userId, fullName: session.session.fullName, nickname: 'Business Nickname', employeeCode: 'ADMIN001', email: 'profile@example.test', departmentName: 'Điều hành', tenantName: 'BizFlow', tenantKey: 'bizflow-corp', roles: ['COMPANY_ADMIN'], hasAvatar: false, createdAt: '2026-11-24T08:00:00Z', minimumPasswordLength: 12 } });
  });
  await page.goto(`${base}/workspace`);
  await expect.poll(() => requests).toBe(2);
  await page.waitForTimeout(150);
  await page.locator('.app-topbar .user-chip').click();
  await expect(page.locator('.official-full-name')).toHaveText(session.session.fullName);
  await expect(page.locator('#profile-name')).toHaveValue('Business Nickname');
  expect(requests).toBe(3);
  await expect(page.locator('bf-personal-profile [role=alert]')).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.robot-fab-button').click();
  const author = page.locator('.chat-user-identity');
  await expect(author).toContainText('Business Nickname');
  await expect(author).toContainText('Quản trị doanh nghiệp');
  await expect(author).toHaveAttribute('title', session.session.fullName);
  await page.screenshot({ path: 'artifacts/nickname-chat-mobile.png', animations: 'disabled' });
});
