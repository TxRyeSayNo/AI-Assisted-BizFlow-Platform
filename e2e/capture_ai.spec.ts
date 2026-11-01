import { test } from '@playwright/test';

test('capture AI agent responses and views', async ({ page }) => {
  // 1. Navigate to login
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  // 2. Perform login
  await page.fill('input[formControlName="tenantKey"]', 'bizflow-corp');
  await page.fill('input[formControlName="identifier"]', 'ADMIN001');
  await page.fill('input[formControlName="password"]', 'Password123!');
  await page.click('button#sign-in-btn');

  // 3. Wait for redirect to workspace
  await page.waitForURL('**/workspace', { timeout: 15000 });
  await page.waitForTimeout(1500);

  // 4. Ensure AI widget is open
  const fab = page.locator('button.robot-fab-button');
  if (await fab.isVisible()) {
    await fab.click();
    await page.waitForTimeout(1000);
  }

  const chatInput = page.locator('input.chat-input');
  const sendBtn = page.locator('button.send-btn');

  // Turn 1: Ask about deadlines today
  await chatInput.fill('Hôm nay tôi có deadline nào không?');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1000);

  // Turn 2: Remember personal preference
  await chatInput.fill('/remember Tôi là CEO, ưu tiên phê duyệt các công việc gấp và giải quyết sự cố kỹ thuật');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1000);

  // Turn 3: Ask what the AI remembers about user
  await chatInput.fill('Bạn nhớ gì về tôi?');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1000);

  // Turn 4: Decompose a project
  await chatInput.fill('/task Lên kế hoạch tiếp nhận nhân viên mới và bàn giao thiết bị');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1500);

  const widget = page.locator('.robot-chat-window');

  // 1. Fullpage screenshot with conversation
  await page.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_chat_response.png',
    fullPage: true,
  });

  // 2. Focused screenshot of chat widget
  if (await widget.isVisible()) {
    await widget.screenshot({
      path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_chat_widget_focus.png',
    });
  }

  // 3. Click the last action card (Xem các nhánh phân rã)
  const branchCardBtn = page.locator('button.action-card-btn').last();
  if (await branchCardBtn.isVisible()) {
    await branchCardBtn.click();
    await page.waitForTimeout(1500);

    // Capture branch overview
    await widget.screenshot({
      path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_branches_preview.png',
    });

    // 4. Click the first branch to open detail view
    const firstBranch = page.locator('.branch-card-main').first();
    if (await firstBranch.isVisible()) {
      await firstBranch.click();
      await page.waitForTimeout(1000);

      // Capture branch detail view
      await widget.screenshot({
        path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_branch_detail.png',
      });
    }
  }

  console.log('ALL_SCREENSHOTS_CAPTURED_SUCCESSFULLY');
});
