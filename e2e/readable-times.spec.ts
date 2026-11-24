import { test, expect } from '@playwright/test';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
for (const [name, viewport] of [
  ['desktop', { width: 1440, height: 900 }],
  ['mobile', { width: 390, height: 844 }],
] as const) {
  test(`historical meeting times are readable on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
    await page.setViewportSize(viewport);
    const login = await request.post(`${base}/api/v1/auth/login`, { data: {
      identifier: process.env['BIZFLOW_EVAL_IDENTIFIER'], password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: process.env['BIZFLOW_EVAL_TENANT'],
    } });
    expect(login.ok()).toBeTruthy();
    await page.addInitScript(session => sessionStorage.setItem('bizflow_session', JSON.stringify(session)), await login.json());
    // Read-only UI fixture. Never deliver notifications or mark the user's inbox read.
    await page.route('**/api/v1/notifications?*', route => route.fulfill({ json: {
      items: [{ notificationId: 'historical-meeting', type: 'MeetingInvitation', title: 'Cuộc họp báo cáo doanh thu',
        content: 'Nội dung: Báo cáo doanh thu\nThời gian: 2026-11-25T09:00:00+07:00\nĐịa điểm: Phòng họp tầng 3\nCần chuẩn bị: Báo cáo kết quả và phương án triển khai.',
        readAt: null, sentAt: null, objectId: null, objectType: null }],
      total: 1, unreadCount: 1, page: 1, pageSize: 25,
    } }));
    await page.goto(`${base}/notifications`);
    const content = page.locator('.notification-content');
    await expect(content).toContainText('Thời gian: 09:00, 25/11/2026');
    await expect(content).not.toContainText('T09:00:00');
    await expect(content).toContainText('Địa điểm: Phòng họp tầng 3');
    expect(await content.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBeTruthy();
    await page.screenshot({ path: `artifacts/readable-times-${name}.png`, fullPage: true });
  });
}
