import { test, expect } from '@playwright/test';

// Render regression against the local app; chat output is controlled and no writes are executed.
const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
const message = [
  '### Đã ghi nhận',
  '- **Nội dung cuộc họp:** Báo cáo doanh thu tháng 11/2026',
  '- **Thời gian họp:** 2026-11-25T09:00:00+07:00',
  '- **Cần chuẩn bị:** Báo cáo doanh thu và các chứng từ liên quan.',
  '',
  '### Cần bổ sung',
  '- Cuộc họp diễn ra **ở đâu**, hoặc có đường dẫn họp trực tuyến nào?',
  '- Thông báo dành cho **toàn công ty** hay một số phòng ban?',
].join('\n');

for (const [name, viewport] of [
  ['desktop', { width: 1440, height: 1000 }],
  ['mobile', { width: 390, height: 844 }],
] as const) {
  test(`agent reply stays readable on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials to run UI validation.');
    await page.setViewportSize(viewport);
    const login = await request.post(`${base}/api/v1/auth/login`, { data: {
      identifier: process.env['BIZFLOW_EVAL_IDENTIFIER'], password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: process.env['BIZFLOW_EVAL_TENANT'],
    } });
    expect(login.ok()).toBeTruthy();
    await page.addInitScript((session) => {
      sessionStorage.setItem('bizflow_session', JSON.stringify(session));
      sessionStorage.setItem('bizflow_robot_open', 'true');
    }, await login.json());
    await page.route('**/api/v1/ai/chat', route => route.fulfill({ json: { response: message, recognizedCommand: 'AGENT_CLARIFICATION_REQUIRED' } }));
    await page.goto(`${base}/tasks`);
    const dialog = page.locator('.robot-chat-window');
    await expect(dialog).toBeVisible();
    if (name === 'desktop') {
      await page.getByRole('button', { name: 'Phóng to không gian làm việc' }).click();
      await expect(dialog).toHaveCSS('width', '960px');
    }
    const input = dialog.locator('textarea, input[placeholder]').last();
    await input.fill('9h sáng mai, báo cáo về doanh thu tháng này');
    await input.press('Enter');
    const content = dialog.locator('bf-agent-message-content').last();
    await expect(content.getByRole('heading', { name: 'Đã ghi nhận' })).toBeVisible();
    await expect(content.getByRole('heading', { name: 'Cần bổ sung' })).toBeVisible();
    await expect(content.locator('time')).toHaveAttribute('datetime', '2026-11-25T09:00:00+07:00');
    await expect(content.locator('time')).toContainText('09:00 · Thứ Tư, 25/11/2026');
    expect(await content.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBeTruthy();
    expect(await dialog.evaluate(el => el.getBoundingClientRect().right <= window.innerWidth && el.getBoundingClientRect().left >= 0)).toBeTruthy();
    await dialog.screenshot({ path: `artifacts/agent-chat-${name}.png`, animations: 'disabled' });
  });
}
