import { test, Page, Browser } from '@playwright/test';
import * as path from 'path';

const ARTIFACT_DIR = 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\screenshots';

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

test.describe('BizFlow Enterprise Multi-Role Walkthrough', () => {
  test.setTimeout(300000);

  test('01. Role: Platform SuperAdmin', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    console.log('--- 1. Login Page ---');
    await page.goto('http://localhost:4200/login');
    await captureScreen(page, '01_login_page.png');

    console.log('--- 2. SuperAdmin Workspace ---');
    await performLogin(page, 'SUPERADMIN');
    await captureScreen(page, '02_superadmin_workspace.png');

    console.log('--- 3. Platform Companies ---');
    await page.goto('http://localhost:4200/platform/companies');
    await captureScreen(page, '03_superadmin_platform_companies.png');

    console.log('--- 4. Platform Audit ---');
    await page.goto('http://localhost:4200/platform/audit');
    await captureScreen(page, '04_superadmin_platform_audit.png');

    await context.close();
  });

  test('02. Role: Company Admin (ADMIN001)', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    console.log('--- Company Admin Login ---');
    await performLogin(page, 'ADMIN001');
    await captureScreen(page, '05_admin_workspace.png');

    // Tasks suite
    await page.goto('http://localhost:4200/tasks');
    await captureScreen(page, '06_admin_tasks_list.png');

    await page.goto('http://localhost:4200/tasks/new');
    await captureScreen(page, '07_admin_task_create.png');

    await page.goto('http://localhost:4200/tasks/ai-decomposition');
    await captureScreen(page, '08_admin_ai_decomposition.png');

    // Requests suite
    await page.goto('http://localhost:4200/requests');
    await captureScreen(page, '09_admin_requests_list.png');

    await page.goto('http://localhost:4200/requests/new');
    await captureScreen(page, '10_admin_request_create.png');

    // Operational Management
    await page.goto('http://localhost:4200/approvals');
    await captureScreen(page, '11_admin_approvals.png');

    await page.goto('http://localhost:4200/reports');
    await captureScreen(page, '12_admin_reports_dashboard.png');

    await page.goto('http://localhost:4200/sla-monitor');
    await captureScreen(page, '13_admin_sla_monitor.png');

    await page.goto('http://localhost:4200/records/search');
    await captureScreen(page, '14_admin_records_search.png');

    await page.goto('http://localhost:4200/audit');
    await captureScreen(page, '15_admin_audit_log.png');

    await page.goto('http://localhost:4200/notifications');
    await captureScreen(page, '16_admin_notifications.png');

    // Company Settings & Org Structure
    await page.goto('http://localhost:4200/settings');
    await captureScreen(page, '17_admin_settings_hub.png');

    await page.goto('http://localhost:4200/settings/organization/departments');
    await captureScreen(page, '18_admin_settings_departments.png');

    await page.goto('http://localhost:4200/settings/organization/users');
    await captureScreen(page, '19_admin_settings_users.png');

    await page.goto('http://localhost:4200/settings/organization/roles');
    await captureScreen(page, '20_admin_settings_roles.png');

    await page.goto('http://localhost:4200/settings/services');
    await captureScreen(page, '21_admin_settings_services.png');

    await page.goto('http://localhost:4200/settings/sla-profiles');
    await captureScreen(page, '22_admin_settings_sla_profiles.png');

    await page.goto('http://localhost:4200/settings/workflows');
    await captureScreen(page, '23_admin_settings_workflows.png');

    // AI Agent Widget
    await page.goto('http://localhost:4200/workspace');
    await page.waitForLoadState('networkidle');
    const fab = page.locator('button.robot-fab-button');
    if (await fab.isVisible()) {
      await fab.click();
      await page.waitForTimeout(1000);
    }
    await captureScreen(page, '24_admin_ai_assistant_widget.png');

    await context.close();
  });

  test('03. Role: Operational Manager (MGR001)', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    console.log('--- Manager Login ---');
    await performLogin(page, 'MGR001');
    await captureScreen(page, '25_manager_workspace.png');

    await page.goto('http://localhost:4200/tasks');
    await captureScreen(page, '26_manager_tasks.png');

    await page.goto('http://localhost:4200/requests');
    await captureScreen(page, '27_manager_requests.png');

    await page.goto('http://localhost:4200/approvals');
    await captureScreen(page, '28_manager_approvals.png');

    await page.goto('http://localhost:4200/reports');
    await captureScreen(page, '29_manager_reports.png');

    await page.goto('http://localhost:4200/sla-monitor');
    await captureScreen(page, '30_manager_sla_monitor.png');

    await context.close();
  });

  test('04. Role: Regular Employee (EMP001)', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    console.log('--- Employee Login ---');
    await performLogin(page, 'EMP001');
    await captureScreen(page, '31_employee_workspace.png');

    await page.goto('http://localhost:4200/tasks');
    await captureScreen(page, '32_employee_tasks.png');

    await page.goto('http://localhost:4200/tasks/new');
    await captureScreen(page, '33_employee_task_create.png');

    await page.goto('http://localhost:4200/requests');
    await captureScreen(page, '34_employee_requests.png');

    await page.goto('http://localhost:4200/requests/new');
    await captureScreen(page, '35_employee_request_create.png');

    await page.goto('http://localhost:4200/notifications');
    await captureScreen(page, '36_employee_notifications.png');

    await context.close();
  });
});
