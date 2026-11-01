import { test, expect } from '@playwright/test';

test('verify AI Agent reasoning, clarifying questions, and decomposition for meeting prep', async ({ page }) => {
  // 1. Login
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  await page.fill('input[formControlName="tenantKey"]', 'bizflow-corp');
  await page.fill('input[formControlName="identifier"]', 'ADMIN001');
  await page.fill('input[formControlName="password"]', 'Password123!');
  await page.click('button#sign-in-btn');

  await page.waitForURL('**/workspace', { timeout: 15000 });
  await page.waitForTimeout(1500);

  // 2. Open AI Widget
  const fab = page.locator('button.robot-fab-button');
  if (await fab.isVisible()) {
    await fab.click();
    await page.waitForTimeout(1000);
  }

  const chatInput = page.locator('input.chat-input');
  const sendBtn = page.locator('button.send-btn');
  const widget = page.locator('.robot-chat-window');

  // Turn 1: User asks vague meeting request
  await chatInput.fill('chuẩn bị cho cuộc họp ngày mai');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1500);

  // Check that AI asked clarifying questions instead of rejecting
  const lastMsg = page.locator('.message-wrapper.robot').last();
  const text = await lastMsg.innerText();
  expect(text).toContain('Thời gian & Thời lượng');
  expect(text).toContain('Địa điểm / Hình thức');
  expect(text).not.toContain('không tìm thấy thông tin');

  // Capture screenshot of clarifying questions
  await widget.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_meeting_clarification.png',
  });

  // Turn 2: User provides clarified details
  await chatInput.fill('Họp lúc 9h sáng mai tại phòng họp lớn, gồm Ban Giám đốc và IT, HR, cần chuẩn bị máy chiếu và tài liệu in');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(2000);

  // Check that AI now decomposed into branches and shows action card
  const branchCard = page.locator('button.action-card-btn').last();
  await expect(branchCard).toBeVisible();

  // Capture screenshot of the multi-turn conversation and decomposition result
  await widget.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_meeting_branches_decomposed.png',
  });

  // Turn 3: Click to open branching workspace
  await branchCard.click();
  await page.waitForTimeout(1500);

  // Capture screenshot of branches screen
  await widget.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_meeting_branch_overview.png',
  });

  console.log('ALL_MEETING_AGENT_TESTS_AND_SCREENSHOTS_SUCCESSFUL');
});

