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
  console.log(`Captured screenshot: ${filename}`);
}

test.describe('Home Page Layout and Watch Button Verification', () => {
  test.setTimeout(120000);

  test('01. Admin: Home Page KPI position, removed modules, horizontal Tasks above Requests, no Drafts', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Admin Login ---');
    await performLogin(page, 'ADMIN001');

    // 1. Verify greeting header exists
    const heroHeader = page.locator('.hero-command-card');
    await expect(heroHeader).toBeVisible();

    // 2. Verify KPI row exists and is right after hero header
    const kpiRow = page.locator('.kpi-metric-row');
    await expect(kpiRow).toBeVisible();

    // Verify 4 KPI boxes
    const kpiBoxes = page.locator('.kpi-box');
    await expect(kpiBoxes).toHaveCount(4);
    console.log('Verified 4 KPI boxes present');

    // 3. Verify modules-section (Thực thi công việc & điều phối dịch vụ) is completely REMOVED
    const modulesSection = page.locator('.modules-section');
    await expect(modulesSection).toHaveCount(0);
    const moduleCards = page.locator('.module-card');
    await expect(moduleCards).toHaveCount(0);
    console.log('Verified modules-section is completely removed');

    // 4. Verify Tasks is placed above Requests in stacked layout
    const opCards = page.locator('.op-stacked-layout .op-card');
    await expect(opCards).toHaveCount(2);

    const firstCardHeader = await opCards.nth(0).locator('.op-header-title h3').textContent();
    const secondCardHeader = await opCards.nth(1).locator('.op-header-title h3').textContent();
    console.log('Card 1 Title (Top):', firstCardHeader?.trim());
    console.log('Card 2 Title (Bottom):', secondCardHeader?.trim());

    // First card must be tasks
    expect(firstCardHeader).toContain('Nhiệm vụ');
    // Second card must be requests
    expect(secondCardHeader).toContain('Yêu cầu');

    // 5. Verify horizontal cards container
    const horizontalContainers = page.locator('.op-horizontal-cards');
    const containerCount = await horizontalContainers.count();
    console.log('Horizontal cards containers count:', containerCount);

    // 6. Verify NO draft badges in the cards
    const statusBadges = await page.locator('.op-task-card .status-badge, .op-req-card .status-badge').allTextContents();
    for (const b of statusBadges) {
      expect(b.toLowerCase()).not.toContain('draft');
      expect(b.toLowerCase()).not.toContain('nháp');
    }
    console.log('Verified no DRAFT cards displayed, total visible cards:', statusBadges.length);

    await captureScreen(page, 'workspace_admin_horizontal_layout.png');

    // 7. Verify Watch Button on Task Detail
    console.log('--- Navigate to Tasks and verify Watch Button ---');
    await page.goto('http://localhost:4200/tasks');
    await page.waitForLoadState('networkidle');

    // Click on the first actual task title link in the table/list
    const taskTitleLink = page.locator('a.task-title').first();
    await expect(taskTitleLink).toBeVisible();
    await taskTitleLink.click();
    await page.waitForURL(/\/tasks\/[0-9a-fA-F-]+/, { timeout: 10000 });
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1000);

    // Check watch button
    const watchBtn = page.locator('.btn-watch-action');
    await expect(watchBtn).toBeVisible();
    const watchText = await watchBtn.textContent();
    console.log('Initial Watch button text:', watchText?.trim());

    // Verify no scroll width overflow (horizontal scrollbar bug from conic gradient)
    const hasHorizontalScroll = await page.evaluate(() => {
      return document.documentElement.scrollWidth > document.documentElement.clientWidth;
    });
    console.log('Page has horizontal scroll:', hasHorizontalScroll);
    expect(hasHorizontalScroll).toBe(false);

    // Toggle watch button
    await watchBtn.click();
    await page.waitForTimeout(1000);
    const toggledText = await watchBtn.textContent();
    console.log('Toggled Watch button text:', toggledText?.trim());

    // Toggle back
    await watchBtn.click();
    await page.waitForTimeout(1000);

    await captureScreen(page, 'task_detail_watch_button_clean.png');
    await context.close();
  });

  test('02. Manager: Home Page horizontal Tasks above Requests', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Manager Login ---');
    await performLogin(page, 'MGR001');

    // 1. Verify greeting header and KPI row
    await expect(page.locator('.hero-command-card')).toBeVisible();
    await expect(page.locator('.kpi-metric-row')).toBeVisible();
    await expect(page.locator('.modules-section')).toHaveCount(0);

    // 2. Verify Tasks above Requests in stacked layout
    const opCards = page.locator('.op-stacked-layout .op-card');
    await expect(opCards).toHaveCount(2);

    const firstCardHeader = await opCards.nth(0).locator('.op-header-title h3').textContent();
    const secondCardHeader = await opCards.nth(1).locator('.op-header-title h3').textContent();
    console.log('Manager Card 1 (Top):', firstCardHeader?.trim());
    console.log('Manager Card 2 (Bottom):', secondCardHeader?.trim());

    expect(firstCardHeader).toContain('Nhiệm vụ');
    expect(secondCardHeader).toContain('Yêu cầu');

    await captureScreen(page, 'workspace_manager_horizontal_layout.png');
    await context.close();
  });

  test('03. Employee: Home Page horizontal Tasks above Requests', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    console.log('--- Employee Login ---');
    await performLogin(page, 'EMP001');

    // 1. Verify greeting header and KPI row
    await expect(page.locator('.hero-command-card')).toBeVisible();
    await expect(page.locator('.kpi-metric-row')).toBeVisible();
    await expect(page.locator('.modules-section')).toHaveCount(0);

    // 2. Verify Tasks above Requests in stacked layout
    const empOpCards = page.locator('.op-stacked-layout .op-card');
    await expect(empOpCards).toHaveCount(2);

    const empFirstCardHeader = await empOpCards.nth(0).locator('.op-header-title h3').textContent();
    const empSecondCardHeader = await empOpCards.nth(1).locator('.op-header-title h3').textContent();
    console.log('Employee Card 1 (Top):', empFirstCardHeader?.trim());
    console.log('Employee Card 2 (Bottom):', empSecondCardHeader?.trim());

    expect(empFirstCardHeader).toContain('Nhiệm vụ');
    expect(empSecondCardHeader).toContain('Yêu cầu');

    await captureScreen(page, 'workspace_employee_horizontal_layout.png');
    await context.close();
  });
});
