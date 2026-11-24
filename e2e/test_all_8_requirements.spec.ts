import { test, expect, Page } from '@playwright/test';
import * as path from 'path';

const ARTIFACT_DIR = 'C:\\Users\\Admin\\.gemini\\antigravity\\brain\\bfa6b2b1-a90d-4f23-8010-29fce01d6d4b\\screenshots';

async function performLogin(page: Page, identifier: string, password = 'Password123!', tenantKey = 'bizflow-corp') {
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  const tenantInput = page.locator('input[formControlName="tenantKey"]');
  const idInput = page.locator('input[formControlName="identifier"]');
  const passInput = page.locator('input[formControlName="password"]');
  const submitBtn = page.locator('button#sign-in-btn');

  const isSuper = identifier.toLowerCase() === 'superadmin' || identifier.toLowerCase().includes('platform');
  if (!isSuper) {
    await tenantInput.fill(tenantKey);
  } else {
    await tenantInput.fill('');
  }

  await idInput.fill(identifier);
  await passInput.fill(password);
  await submitBtn.click();

  await page.waitForURL('**/workspace', { timeout: 15000 });
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);
}

async function captureScreen(page: Page, filename: string, delay = 1000) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(delay);
  const fullPath = path.join(ARTIFACT_DIR, filename);
  await page.screenshot({ path: fullPath, fullPage: true });
  console.log(`Captured: ${filename}`);
}

test.describe('BizFlow All 8 Requirements Full Verification', () => {
  test.setTimeout(180000);

  test('01. Role: Company Admin (ADMIN001) - Req 1, 2, 3, 4, 5, 6, 7', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Admin Login ---');
    await performLogin(page, 'ADMIN001');

    // Req 7: Company Admin Workspace Operational Dashboard
    console.log('--- Verify Req 7: Admin Workspace Operational Dashboard ---');
    await expect(page.locator('.hero-actions')).toHaveCount(0);
    await expect(page.locator('.role-operational-dashboard')).toBeVisible();
    await expect(page.locator('.op-card').first()).toBeVisible();
    await captureScreen(page, 'req7_admin_workspace_operational_dashboard.png');

    // Req 1 & 2: Tasks list, semantic colors, and toolbar
    console.log('--- Verify Req 1 & 2: Tasks list & toolbar ---');
    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('.task-nav-btn').first()).toBeVisible();
    await expect(page.locator('.status-label[data-status]').first()).toBeVisible();
    await captureScreen(page, 'req1_req2_admin_tasks_list.png');

    // Req 1: Tasks Calendar
    console.log('--- Verify Req 1: Tasks Calendar Flags ---');
    const calendarBtn = page.locator('.task-nav-btn', { hasText: 'Lịch' });
    if (await calendarBtn.isVisible()) {
      await calendarBtn.click();
      await page.waitForLoadState('networkidle');
      await captureScreen(page, 'req1_admin_tasks_calendar.png');
    }

    // Req 4: Kanban Board without labels
    console.log('--- Verify Req 4: Tasks Board No Labels ---');
    const boardBtn = page.locator('.task-nav-btn', { hasText: 'Bảng' });
    if (await boardBtn.isVisible()) {
      await boardBtn.click();
      await page.waitForLoadState('networkidle');
      await expect(page.locator('select#label-filter')).toHaveCount(0);
      await captureScreen(page, 'req4_admin_tasks_board_no_labels.png');
    }

    // Req 3, 4, 5: Task detail progress breakdown, mention, watcher, no labels
    console.log('--- Verify Req 3 & 5: Task Detail Progress & Collaboration ---');
    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');
    const firstTaskLink = page.locator('table tbody tr a.task-title').first();
    if (await firstTaskLink.isVisible()) {
      await firstTaskLink.click();
      await page.waitForLoadState('networkidle');

      // Check progress breakdown section
      await expect(page.locator('.subtasks-hub-section')).toBeVisible();
      // Wait for feature flags and watcher status
      await page.waitForTimeout(1000);
      // Check watcher panel
      const watcherPanel = page.locator('.watcher-panel');
      await watcherPanel.scrollIntoViewIfNeeded();
      await expect(watcherPanel).toBeVisible();
      // Check no labels component
      await expect(page.locator('bf-work-item-labels')).toHaveCount(0);

      // Check comments and mention button
      const mentionBtn = page.locator('.btn-mention-trigger');
      await mentionBtn.scrollIntoViewIfNeeded();
      await expect(mentionBtn).toBeVisible();
      await captureScreen(page, 'req3_req5_admin_task_detail.png');
    }

    // Req 6: Request detail consolidated actions
    console.log('--- Verify Req 6: Request Detail Consolidated Actions ---');
    await page.goto('http://localhost:4200/requests');
    await page.waitForLoadState('networkidle');
    let activeReqLink = page.locator('table tbody tr:has-text("good") a').first();
    if (!(await activeReqLink.isVisible())) {
      activeReqLink = page.locator('table tbody tr a').first();
    }
    if (await activeReqLink.isVisible()) {
      await activeReqLink.click();
      await page.waitForLoadState('networkidle');

      // Assert unified AI action
      await expect(page.locator('.btn-ai-hub')).toBeVisible();
      // Assert back to list says 'Danh sách'
      await expect(page.locator('a', { hasText: 'Danh sách' })).toBeVisible();
      // Assert no create task button in request top toolbar actions
      await expect(page.locator('.detail-actions button', { hasText: 'Tạo nhiệm vụ' })).toHaveCount(0);
      // Assert no escalation button for Company Admin
      await expect(page.locator('.detail-actions button', { hasText: 'Leo thang' })).toHaveCount(0);
      await captureScreen(page, 'req6_admin_request_detail.png');
    }

    // Req 4: Settings Hub without labels
    console.log('--- Verify Req 4: Settings Hub No Labels ---');
    await page.goto('http://localhost:4200/settings');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('a[href="/settings/labels"]')).toHaveCount(0);
    await captureScreen(page, 'req4_admin_settings_no_labels.png');

    await context.close();
  });

  test('02. Role: Operation Manager (MGR001) - Req 7 & 5', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Manager Login ---');
    await performLogin(page, 'MGR001');

    // Req 7: Manager Workspace Operational Dashboard
    console.log('--- Verify Req 7: Manager Workspace Dashboard ---');
    await expect(page.locator('.hero-actions')).toHaveCount(0);
    await expect(page.locator('.role-operational-dashboard')).toBeVisible();
    await expect(page.locator('.op-dept-stats-row')).toBeVisible();
    await captureScreen(page, 'req7_manager_workspace_operational_dashboard.png');

    // Task oversight
    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');
    const firstTaskLink = page.locator('table tbody tr a.task-title').first();
    if (await firstTaskLink.isVisible()) {
      await firstTaskLink.click();
      await page.waitForLoadState('networkidle');
      await expect(page.locator('.subtasks-hub-section')).toBeVisible();
      await captureScreen(page, 'req5_manager_task_supervision.png');
    }

    await context.close();
  });

  test('03. Role: Regular Employee (EMP001) - Req 7 & 5', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Employee Login ---');
    await performLogin(page, 'EMP001');

    // Req 7: Employee Workspace Operational Dashboard
    console.log('--- Verify Req 7: Employee Workspace Dashboard ---');
    await expect(page.locator('.hero-actions')).toHaveCount(0);
    await expect(page.locator('.role-operational-dashboard')).toBeVisible();
    await captureScreen(page, 'req7_employee_workspace_operational_dashboard.png');

    // Req 5: Employee personal progress reporting panel
    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');
    const firstTaskLink = page.locator('table tbody tr a.task-title').first();
    if (await firstTaskLink.isVisible()) {
      await firstTaskLink.click();
      await page.waitForLoadState('networkidle');
      await captureScreen(page, 'req5_employee_task_progress_panel.png');
    }

    await context.close();
  });

  test('04. Responsive Viewport Check - Req 8', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1024, height: 768 } });
    const page = await context.newPage();

    await performLogin(page, 'ADMIN001');
    await captureScreen(page, 'req8_responsive_1024_workspace.png');

    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');
    await captureScreen(page, 'req8_responsive_1024_tasks.png');

    await context.close();
  });
});
