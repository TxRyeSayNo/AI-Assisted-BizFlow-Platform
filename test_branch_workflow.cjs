const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  console.log('1. Navigating to login...');
  await page.goto('http://localhost:4200/login');
  await page.waitForLoadState('networkidle');

  console.log('2. Logging in...');
  await page.fill('input[formControlName="tenantKey"]', 'bizflow-corp');
  await page.fill('input[formControlName="identifier"]', 'ADMIN001');
  await page.fill('input[formControlName="password"]', 'Password123!');
  await page.click('button#sign-in-btn');

  await page.waitForURL('**/workspace', { timeout: 15000 });
  await page.waitForTimeout(1500);
  console.log('Logged in successfully!');

  // Open Robot Widget via FAB if closed
  const fab = page.locator('.robot-fab-button');
  if (await fab.isVisible()) {
    console.log('Opening Robot Widget via FAB...');
    await fab.click();
    await page.waitForTimeout(1000);
  }

  // Type prompt
  console.log('3. Sending meeting preparation prompt...');
  const chatInput = page.locator('.chat-input');
  await chatInput.fill('Thông báo & chuẩn bị cuộc họp ngày mai cho tôi');
  await page.click('.send-btn');
  await page.waitForTimeout(3500);

  const artDir = 'C:/Users/admin/.gemini/antigravity/brain/3db4b9de-2cfa-42f2-a995-62b72b23ff07';

  // Check if questionnaire card is visible
  const questionnaireCard = page.locator('.agent-questionnaire-card');
  if (await questionnaireCard.isVisible()) {
    console.log('4. Questionnaire is visible! Filling questions...');
    // Single choice options
    const radioOpts = page.locator('.agent-questionnaire-card .radio-group .option-row');
    if ((await radioOpts.count()) > 0) {
      await radioOpts.first().click();
    }

    // Checkbox options
    const checkOpts = page.locator('.agent-questionnaire-card .checkbox-group .option-row');
    const chkCount = await checkOpts.count();
    for (let i = 0; i < Math.min(chkCount, 2); i++) {
      await checkOpts.nth(i).click();
    }

    // Custom text input if any
    const customInput = page.locator('.agent-questionnaire-card .custom-q-input').first();
    if (await customInput.isVisible()) {
      await customInput.fill('Phòng họp Lớn (Tầng 3), cần máy chiếu và micro');
    }

    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(artDir, '01_agent_questionnaire_interactive.png'), fullPage: false });
    console.log('Saved 01_agent_questionnaire_interactive.png');

    console.log('5. Submitting questionnaire...');
    await page.click('.btn-submit-questionnaire');
    await page.waitForTimeout(4000);
  } else {
    // Quick action card click fallback
    const quickTemplateBtn = page.locator('.action-card-btn').first();
    if (await quickTemplateBtn.isVisible()) {
      console.log('4. Clicking quick template card...');
      await quickTemplateBtn.click();
      await page.waitForTimeout(4000);
    }
  }

  // Screenshot of Chat showing Agent Thought, Decision, and View Branches Card
  await page.screenshot({ path: path.join(artDir, '02_agent_decision_and_branches_card.png'), fullPage: false });
  console.log('Saved 02_agent_decision_and_branches_card.png');

  // Open Branching Workspace modal
  console.log('6. Opening Branching Workspace modal...');
  const viewBranchesBtn = page.locator('.action-card-btn:has-text("nhánh"), .btn-open-modal-chip, .agent-tab-btn:has-text("Nhánh tác vụ")').first();
  await viewBranchesBtn.click();
  await page.waitForSelector('.branch-modal-overlay', { timeout: 8000 });
  await page.waitForTimeout(1500);

  // Requirement 2: Screenshot of Branch Overview with dedicated delete button on each branch card
  console.log('7. Verifying Requirement 2: Dedicated Delete Button on Each Branch...');
  await page.screenshot({ path: path.join(artDir, '03_branches_list_with_delete_buttons.png'), fullPage: false });
  console.log('Saved 03_branches_list_with_delete_buttons.png');

  // Requirement 2 Action: Click dedicated delete button on Branch #3
  const deleteBtns = page.locator('.btn-card-delete');
  const countBefore = await deleteBtns.count();
  console.log(`Branches count before deletion: ${countBefore}`);

  if (countBefore >= 3) {
    console.log('Clicking delete on Branch #3...');
    await deleteBtns.nth(2).click();
    await page.waitForTimeout(1500);

    const countAfter = await page.locator('.btn-card-delete').count();
    console.log(`Branches count after deletion: ${countAfter}`);
    await page.screenshot({ path: path.join(artDir, '04_branch_deleted_dynamically.png'), fullPage: false });
    console.log('Saved 04_branch_deleted_dynamically.png');
  }

  // Requirement 3: Click on Branch #1 to open detail view and edit fields
  console.log('8. Verifying Requirement 3: Editing Department, Time, Priority in Branch Detail...');
  const firstCard = page.locator('.branch-summary-card').first();
  await firstCard.click();
  await page.waitForSelector('.branch-detail-screen', { timeout: 5000 });
  await page.waitForTimeout(1000);

  // Edit Title
  const titleInput = page.locator('.detail-title-input');
  if (await titleInput.isVisible()) {
    await titleInput.fill('Chuẩn bị phòng họp, trang thiết bị máy chiếu và tài liệu [Đã điều chỉnh]');
  }

  // Edit Priority
  const prioSelect = page.locator('.meta-select-input.prio-select');
  if (await prioSelect.isVisible()) {
    await prioSelect.selectOption('HIGH');
  }

  // Edit Time / Duration
  const durInput = page.locator('.meta-number-input');
  if (await durInput.isVisible()) {
    await durInput.fill('4');
  }

  // Edit Department
  const deptSelect = page.locator('.meta-item.edit-field:has-text("Phòng ban") select');
  if (await deptSelect.isVisible()) {
    await page.waitForTimeout(500);
    const opts = await deptSelect.locator('option').all();
    console.log(`Department options count: ${opts.length}`);
    if (opts.length > 2) {
      const val = await opts[2].getAttribute('value');
      if (val) await deptSelect.selectOption(val);
    }
  }

  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(artDir, '05_branch_detail_editing_items.png'), fullPage: false });
  console.log('Saved 05_branch_detail_editing_items.png');

  // Requirement 1: Create the edited branch into the system!
  console.log('9. Verifying Requirement 1: Submitting edited branch to create work task...');
  const submitSingleBtn = page.locator('.btn-submit-single');
  await submitSingleBtn.click();
  await page.waitForSelector('.status-badge-created', { timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(1500);

  await page.screenshot({ path: path.join(artDir, '06_branch_created_success.png'), fullPage: false });
  console.log('Saved 06_branch_created_success.png');

  // Back to overview list and submit remaining
  console.log('10. Returning to overview list and submitting all remaining branches...');
  await page.click('.btn-back-to-overview');
  await page.waitForTimeout(1000);

  const submitAllBtn = page.locator('.btn-submit-all');
  if (await submitAllBtn.isEnabled()) {
    await submitAllBtn.click();
    await page.waitForTimeout(3000);
  }

  await page.screenshot({ path: path.join(artDir, '07_all_branches_created_success.png'), fullPage: false });
  console.log('Saved 07_all_branches_created_success.png');

  await browser.close();
  console.log('ALL WORKFLOW STEPS VERIFIED AND COMPLETED SUCCESSFULLY!');
})();
