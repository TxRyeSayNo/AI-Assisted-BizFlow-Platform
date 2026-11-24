import { test, expect } from '@playwright/test';

test.use({ timezoneId: 'Asia/Ho_Chi_Minh' });
const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
for (const [name, viewport] of [
  ['desktop', { width: 1440, height: 900 }],
  ['mobile', { width: 390, height: 844 }],
] as const) {
  test(`date, hour and minute selectors preserve the submitted deadline on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set local evaluation credentials.');
    await page.setViewportSize(viewport);
    const login = await request.post(`${base}/api/v1/auth/login`, { data: {
      identifier: process.env['BIZFLOW_EVAL_IDENTIFIER'], password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: process.env['BIZFLOW_EVAL_TENANT'],
    } });
    expect(login.ok()).toBeTruthy();
    const session = await login.json();
    // Browser-only form fixture; the task POST below never reaches the server.
    session.session.permissions = [...session.session.permissions, 'tasks.create'];
    await page.addInitScript(session => {
      sessionStorage.setItem('bizflow_session', JSON.stringify(session));
    }, session);
    // Inspect submission without creating a task or triggering business notifications.
    await page.route('**/api/v1/tasks', route => route.request().method() === 'POST'
      ? route.fulfill({ status: 422, json: { message: 'UI evaluation: no task persisted.' } }) : route.continue());
    await page.goto(`${base}/tasks/new`);
    await page.locator('[formControlName=title]').fill('Kiểm tra chọn ngày giờ');
    const picker = page.locator('bf-date-time-picker');
    const date = picker.locator('input');
    await date.click();
    await expect(page.locator('mat-calendar')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Tháng sau', exact: true })).toBeVisible();
    await expect(page.locator('.mat-calendar-period-button')).toContainText(/tháng/i);
    await page.locator('.mat-calendar-body-cell:not(.mat-calendar-body-disabled)').nth(10).click();
    await expect(date).toHaveValue(/^\d{2}\/\d{2}\/\d{4}$/);
    await date.fill('25/11/2099');
    await date.press('Tab');
    const submit = page.locator('.submit-task-btn');
    await expect(submit).toBeDisabled();
    const hour = picker.locator('select').nth(0);
    const minute = picker.locator('select').nth(1);
    await hour.selectOption('09');
    await expect(submit).toBeDisabled();
    await minute.selectOption('05');
    await expect(submit).toBeEnabled();
    expect(await picker.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBeTruthy();
    await picker.scrollIntoViewIfNeeded();
    await page.screenshot({ path: `artifacts/date-time-picker-${name}.png`, fullPage: true });
    const posted = page.waitForRequest(r => new URL(r.url()).pathname === '/api/v1/tasks' && r.method() === 'POST');
    await submit.click();
    expect((await posted).postDataJSON().deadline).toBe('2099-11-25T02:05:00.000Z');
    await expect(page.getByText('Vui lòng kiểm tra lại tiêu đề, danh mục kiểm tra và hạn chót. Nhiệm vụ chưa được tạo.', { exact: true })).toBeVisible();
    await picker.getByRole('button', { name: 'Xóa thời gian' }).click();
    await expect(date).toHaveValue('');
    await expect(hour).toHaveValue('');
    await expect(minute).toHaveValue('');
    await expect(submit).toBeEnabled();
    // Date-only filters use the same click-to-open behavior.
    await page.route('**/api/v1/tasks/board*', route => route.fulfill({ json: { columns: [], columnSize: 50 } }));
    await page.goto(`${base}/tasks/board`);
    const dateFilter = page.locator('bf-date-picker input').first();
    await expect(dateFilter).toBeEditable();
    await expect.poll(() => dateFilter.getAttribute('readonly')).toBeNull();
    await dateFilter.click();
    await expect(page.locator('mat-calendar')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Tháng trước', exact: true })).toBeVisible();
    await page.keyboard.press('Escape');
  });
}
