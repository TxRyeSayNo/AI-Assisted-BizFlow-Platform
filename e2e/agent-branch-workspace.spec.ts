import { test, expect } from '@playwright/test';

const base = process.env['BIZFLOW_EVAL_WEB_URL'] ?? 'http://localhost:4200';
const departments = ['Phòng Công nghệ & Chuyển đổi số', 'Phòng Tài chính - Kế toán', 'Phòng Hành chính - Nhân sự', 'Phòng Dịch vụ Khách hàng & Vận hành'];
const titles = ['Rà soát hệ thống báo cáo và chuẩn bị phương án hỗ trợ kỹ thuật', 'Tổng hợp doanh thu, chi phí và đối soát số liệu các phòng ban', 'Chuẩn bị tài liệu, cơ sở vật chất và danh sách người tham dự'];

for (const [name, viewport] of [
  ['desktop', { width: 1920, height: 900 }],
  ['laptop', { width: 1366, height: 768 }],
  ['mobile', { width: 390, height: 844 }],
  ['short-screen', { width: 1000, height: 540 }],
] as const) {
  test(`task workspace is centered and usable with ten long tasks on ${name}`, async ({ page, request }) => {
    test.skip(!process.env['BIZFLOW_EVAL_PASSWORD'], 'Set evaluation credentials to run local UI checks.');
    await page.setViewportSize(viewport);
    const login = await request.post(`${base}/api/v1/auth/login`, { data: {
      identifier: process.env['BIZFLOW_EVAL_IDENTIFIER'], password: process.env['BIZFLOW_EVAL_PASSWORD'], tenantKey: process.env['BIZFLOW_EVAL_TENANT'],
    } });
    expect(login.ok()).toBeTruthy();
    await page.addInitScript(({ session, departments, titles }) => {
      sessionStorage.setItem('bizflow_session', JSON.stringify(session));
      sessionStorage.setItem('bizflow_robot_open', 'true');
      const scope = `${session.session.tenantId}_${session.session.userId}`;
      const branches = Array.from({ length: 10 }, (_, i) => ({
        id: `review-${i}`, branchIndex: i + 1, title: titles[i % titles.length],
        description: 'Chuẩn bị đầy đủ tài liệu, kiểm tra dữ liệu và phối hợp các phòng ban để hoàn thành báo cáo đúng hạn. '.repeat(4),
        type: 'Assignment', departmentName: departments[i % departments.length], priority: i % 2 ? 'MEDIUM' : 'HIGH',
        suggestedDurationDays: 2 + i, checklistItems: ['Kiểm tra thông tin', 'Phối hợp triển khai', 'Bàn giao kết quả'], status: 'pending',
      }));
      localStorage.setItem(`bizflow_ai_current_session_${scope}`, JSON.stringify({ id: 'layout-evaluation', branches, messages: [{ id: 'preview', sender: 'robot', text: 'Đề xuất 10 nhiệm vụ để bạn xem xét.', time: '13:00' }] }));
    }, { session: await login.json(), departments, titles });
    // Prevent any accidental business write: this test reviews, edits and removes local proposals only.
    await page.route('**/api/v1/tasks/ai-decomposition/confirm', route => route.fulfill({
      status: 500, json: { code: 'INTERNAL.ERROR', message: 'An unexpected error occurred.' },
    }));
    await page.goto(`${base}/tasks`);
    await page.locator('.active-branches-banner').click();
    const dialog = page.getByRole('dialog', { name: 'Phân chia nhiệm vụ', exact: true });
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('.branch-summary-card')).toHaveCount(10);
    await expect.poll(() => dialog.evaluate(el => {
      const r = el.getBoundingClientRect();
      return Math.abs(r.x + r.width / 2 - window.innerWidth / 2) + Math.abs(r.y + r.height / 2 - window.innerHeight / 2);
    })).toBeLessThan(2);
    expect(await dialog.evaluate(el => el.scrollWidth <= el.clientWidth + 1)).toBeTruthy();
    const list = dialog.locator('.branch-cards-list');
    expect(await list.evaluate(el => el.scrollHeight > el.clientHeight)).toBeTruthy();
    for (const card of await dialog.locator('.branch-summary-card').all()) {
      expect(await card.evaluate(el => el.scrollHeight <= el.clientHeight + 1)).toBeTruthy();
    }
    const footer = dialog.locator('.branch-overview-footer');
    const before = (await footer.boundingBox())!;
    await list.evaluate(el => (el.scrollTop = el.scrollHeight));
    await expect(dialog.getByRole('button', { name: 'Chi tiết nhiệm vụ 10', exact: true })).toBeVisible();
    const after = (await footer.boundingBox())!;
    expect(Math.abs(after.y - before.y)).toBeLessThan(1);
    await list.evaluate(el => (el.scrollTop = 0));
    await dialog.screenshot({ path: `artifacts/task-workspace-${name}.png`, animations: 'disabled' });
    await dialog.getByRole('button', { name: 'Chi tiết nhiệm vụ 1', exact: true }).click();
    await expect(dialog.locator('.detail-content-card')).toBeVisible();
    const title = dialog.locator('.detail-title-input');
    await title.fill('Nhiệm vụ đã chỉnh sửa để rà soát');
    await dialog.getByRole('button', { name: 'Quay lại danh sách', exact: true }).click();
    await expect(dialog.locator('.branch-title-button').first()).toHaveText('Nhiệm vụ đã chỉnh sửa để rà soát');
    await dialog.getByRole('button', { name: 'Xóa nhánh này', exact: true }).first().click();
    await expect(dialog.locator('.branch-summary-card')).toHaveCount(9);
    await dialog.getByRole('button', { name: 'Thêm nhánh mới', exact: true }).click();
    await expect(dialog.locator('.detail-content-card')).toBeVisible();
    await expect(title).toHaveValue('');
    await expect(title).toBeFocused();
    const departmentSelect = dialog.locator('#branch-department');
    await expect(departmentSelect.locator('option:checked')).toHaveText('Chọn phòng ban nhận việc');
    await expect(dialog.locator('.btn-submit-single')).toBeDisabled();
    await title.fill('Chuẩn bị tài liệu cuộc họp');
    await expect(dialog.locator('.btn-submit-single')).toBeDisabled();
    await expect(dialog.locator('.branch-configuration-hint')).toContainText('Chọn phòng ban');
    await expect.poll(() => departmentSelect.locator('option').count(), { timeout: 15000 }).toBeGreaterThan(1);
    const chosenDepartment = await departmentSelect.locator('option').nth(1).textContent();
    await departmentSelect.selectOption({ index: 1 });
    await expect(dialog.locator('.btn-submit-single')).toBeEnabled();
    await departmentSelect.selectOption({ index: 0 });
    await expect(dialog.locator('.btn-submit-single')).toBeDisabled();
    await departmentSelect.selectOption({ index: 1 });
    const creationRequest = page.waitForRequest('**/api/v1/tasks/ai-decomposition/confirm');
    await dialog.locator('.btn-submit-single').click();
    const payload = (await creationRequest).postDataJSON();
    expect(payload.items).toHaveLength(1);
    expect(payload.items[0].title).toBe('Chuẩn bị tài liệu cuộc họp');
    expect(payload.items[0].departmentId).toBeTruthy();
    expect(payload.items[0].departmentName).toBe(chosenDepartment);
    await expect(dialog.getByRole('alert')).toContainText('Máy chủ gặp lỗi khi lưu nhiệm vụ');
    await page.route('**/api/v1/tasks/ai-decomposition/confirm', route => route.fulfill({
      status: 200, json: { totalCreated: 1, items: [{ id: 'mock-created-task' }] },
    }));
    await dialog.locator('.btn-submit-single').click();
    await expect(dialog.locator('.status-badge-created')).toBeVisible();
    await page.route('**/api/v1/tasks/ai-decomposition/confirm', route => route.fulfill({
      status: 500, json: { code: 'INTERNAL.ERROR', message: 'An unexpected error occurred.' },
    }));
    await dialog.getByRole('button', { name: 'Quay lại danh sách', exact: true }).click();
    await expect(dialog.locator('.branch-summary-card')).toHaveCount(10);
    await expect(dialog.locator('.branch-summary-card').last()).toContainText(chosenDepartment!);
    await expect(dialog.locator('.branch-summary-card').last()).toContainText('Chuẩn bị tài liệu cuộc họp');
    await dialog.locator('.btn-card-quick-submit').first().click();
    await expect(dialog.getByRole('alert')).toContainText('Máy chủ gặp lỗi khi lưu nhiệm vụ');
    await expect(dialog.getByRole('alert')).toBeInViewport();
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    expect(await page.evaluate(() => document.documentElement.style.overflow)).not.toBe('hidden');
    await expect(page.locator('.active-branches-banner')).toBeFocused();
  });
}
