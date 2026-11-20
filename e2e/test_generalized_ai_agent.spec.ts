import { test, expect } from '@playwright/test';

test('verify Generalized AI Agent critical thinking, counter-questioning, and multi-department decomposition', async ({ page }) => {
  // 1. Login to BizFlow
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  await page.fill('input[formControlName="tenantKey"]', 'bizflow-corp');
  await page.fill('input[formControlName="identifier"]', 'ADMIN001');
  await page.fill('input[formControlName="password"]', 'Password123!');
  await page.click('button#sign-in-btn');

  await page.waitForURL('**/workspace', { timeout: 15000 });
  await page.waitForTimeout(1500);

  // 2. Open Robot AI Widget
  const fab = page.locator('button.robot-fab-button');
  if (await fab.isVisible()) {
    await fab.click();
    await page.waitForTimeout(1000);
  }

  const chatInput = page.locator('input.chat-input');
  const sendBtn = page.locator('button.send-btn');
  const widget = page.locator('.robot-chat-window');

  // 3. Ask an open-ended non-meeting business goal: Marketing Campaign
  await chatInput.fill('tổ chức chiến dịch Marketing ra mắt sản phẩm mới');
  await sendBtn.click();
  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(1500);

  // 4. Verify AI Agent Critical Thinking & Clarifying Response
  const lastMsg = page.locator('.message-wrapper.robot').last();
  const text = await lastMsg.innerText();
  expect(text).toContain('Góc nhìn Phản biện');
  expect(text).toContain('Mục tiêu & KPI cụ thể');
  expect(text).toContain('Đối tượng khách hàng & Kênh tiếp cận');
  expect(text).not.toContain('không tìm thấy thông tin');

  // Capture screenshot of AI Agent Critical Thinking & Counter-Questions
  await widget.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_agent_critical_marketing_thinking.png',
  });

  // 5. Click the Quick Template chip or send clarified details
  const templateChip = page.locator('.action-card-btn').last();
  if (await templateChip.isVisible()) {
    await templateChip.click();
    await page.waitForTimeout(500);
    await sendBtn.click();
  } else {
    await chatInput.fill('Chiến dịch ra mắt sản phẩm mới vào đầu tháng tới, mục tiêu đạt 500 khách hàng tiềm năng, ngân sách 30 triệu, phối hợp giữa Marketing, Design và Sales');
    await sendBtn.click();
  }

  await page.waitForSelector('.loading-bubble', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(2000);

  // 6. Verify that AI Agent synthesized the plan into multi-department branches
  const finalMsg = page.locator('.message-wrapper.robot').last();
  const finalText = await finalMsg.innerText();
  expect(finalText).toContain('phân rã công việc đa phòng ban');
  expect(finalText).toContain('nhánh nhiệm vụ');

  // Capture screenshot of synthesized multi-department branches
  await widget.screenshot({
    path: 'C:\\Users\\admin\\.gemini\\antigravity\\brain\\3db4b9de-2cfa-42f2-a995-62b72b23ff07\\ai_agent_marketing_branches_decomposed.png',
  });
});
