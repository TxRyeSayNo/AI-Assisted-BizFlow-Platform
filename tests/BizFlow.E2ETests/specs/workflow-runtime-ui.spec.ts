import { expect, test } from '@playwright/test';

// Browser UI contracts; persistence and authorization are covered by WorkflowRuntimeTests.
const requestId = '01a12000-0000-7000-8000-000000000055';
test('workflow decisions survive reload and stale revisions refresh the displayed progress', async ({ page }) => {
  await page.addInitScript(() => sessionStorage.setItem('bizflow_session', JSON.stringify({
    accessToken: 'browser-fixture', refreshToken: 'browser-fixture', expiresAt: '2099-01-01T00:00:00Z',
    session: { userId: 'reviewer', fullName: 'Người duyệt', tenantId: 'tenant', tenantName: 'Demo', permissions: ['requests.read.own'] },
  })));
  let completed = false;
  let paused = false;
  let revision = 2;
  let stale = true;
  let posted: unknown;
  const runtime = () => ({ id: 'instance', name: 'Tiếp nhận nhân sự', versionNo: 3, revision,
    status: completed ? 'COMPLETED' : 'RUNNING', formFields: [{key:'budget',label:'Dự toán'}],formData:{budget:1200},
    steps: [{ id:'ai-step', name:'AI chuẩn bị hồ sơ', nodeType:'DEPARTMENT',status:'COMPLETED',policy:'SINGLE',canAct:false,candidates:['Người phụ trách'],decisions:[],
      agent:{id:'ai-run',status:'COMPLETED',attempts:1,errorCode:null,model:'browser-test',result:{summary:'Đã chuẩn bị bản bàn giao',deliverable:'Dự toán 1200 cần người có thẩm quyền duyệt.',needsHumanReview:false,findings:[],evidence:[{source:'fields.budget',quote:'1200'}]}} },
      { id: 'step', name: 'Duyệt dự toán', nodeType: 'APPROVAL', status: completed ? 'COMPLETED' : 'PENDING',
      policy: 'SINGLE', dueAt: '2099-01-01T00:00:00Z', pausedAt:paused?'2026-11-25T00:00:00Z':null,canManageSla:!completed,
      outcome: completed ? 'APPROVE' : null, canAct: !completed && !paused, candidates: ['Người duyệt'],
      decisions: completed ? [{actorName:'Người duyệt',outcome:'APPROVE',note:'Đã kiểm tra',decidedAt:'2026-11-25T00:00:00Z'}] : [] }],
  });
  await page.route('**/api/v1/**', async route => {
    const url = new URL(route.request().url());
    let body: unknown = {items:[]};
    if (url.pathname.endsWith('/decisions')) {
      posted = route.request().postDataJSON();
      if (stale) { stale = false; await route.fulfill({status:409,json:{message:'Quy trình đã thay đổi. Vui lòng tải lại.'}}); return; }
      completed = true; revision++; body = runtime();
    } else if (url.pathname.endsWith('/sla/pause')) {
      expect(route.request().postDataJSON()).toEqual({revision:2,reason:'Chờ chứng từ'});
      paused=true;revision++;body=runtime();
    } else if (url.pathname.endsWith('/sla/resume')) {
      expect(route.request().postDataJSON()).toEqual({revision:3,reason:null});
      paused=false;revision++;body=runtime();
    } else if (url.pathname.endsWith('/agent/history')) body=[{attempt:1,completedAt:'2026-11-25T00:00:00Z',trace:[{round:1,tool:'read_inputs'},{round:2,action:'prepare_deliverable',result:{summary:'Bản bàn giao đã lưu',deliverable:'Bằng chứng còn nguyên sau tải lại.'}}]}];
    else if (url.pathname.endsWith('/workflow')) body = runtime();
    else if (url.pathname === `/api/v1/requests/${requestId}`) body = {
      requestId, workflowVersionId:'version',title:'Chuẩn bị nhân sự',description:'Ngày đầu',status:completed?'CLOSED':'IN_PROGRESS',
      priority:'MEDIUM',serviceId:'service',serviceName:'Nhân sự',categoryId:'category',categoryName:'Tiếp nhận',
      requesterId:'requester',requesterName:'Người đề nghị',parentRequestId:null,revisedFromRequestId:null,
      createdAt:'2026-11-25T00:00:00Z',updatedAt:'2026-11-25T00:00:00Z',resolvedAt:null,closedAt:null,
    };
    else if (/\/(comments|attachments|labels)$/.test(url.pathname)) body=[];
    else if (url.pathname.endsWith('/escalation')) body=null;
    else if (url.pathname.endsWith('/unread-count')) body={count:0};
    await route.fulfill({json:body});
  });
  await page.goto(`/requests/${requestId}`);
  const panel = page.getByRole('region', {name:'Tiến độ quy trình'});
  await expect(panel.getByRole('heading',{name:'Tiếp nhận nhân sự · Phiên bản 3'})).toBeVisible();
  await expect(panel).toContainText('1200');
  await expect(panel).toContainText('Đã chuẩn bị bản bàn giao');
  await panel.getByRole('button',{name:'Xem lịch sử chạy AI'}).click();
  await expect(panel).toContainText('Bản bàn giao đã lưu');
  await panel.getByLabel('Lý do chờ / tạm dừng SLA').fill('Chờ chứng từ');
  await panel.getByRole('button',{name:'Tạm dừng SLA',exact:true}).click();
  await expect(panel).toContainText('SLA đang tạm dừng');
  await expect(panel.getByRole('button',{name:'Chấp thuận',exact:true})).toHaveCount(0);
  await panel.getByRole('button',{name:'Tiếp tục SLA',exact:true}).click();
  await expect(panel.getByRole('button',{name:'Chấp thuận',exact:true})).toBeVisible();
  await panel.getByLabel('Kết quả xử lý / lý do').fill('Đã kiểm tra');
  await panel.getByRole('button',{name:'Chấp thuận',exact:true}).click();
  await expect(page.getByRole('alert').filter({hasText:'Quy trình đã thay đổi'})).toBeVisible();
  await panel.getByRole('button',{name:'Chấp thuận',exact:true}).click();
  await expect(panel).toContainText('Trạng thái: Hoàn tất');
  expect(posted).toEqual({revision:4,outcome:'APPROVE',note:'Đã kiểm tra'});
  await page.reload();
  await expect(panel).toContainText('Trạng thái: Hoàn tất');
  await expect(panel.getByRole('button',{name:'Chấp thuận',exact:true})).toHaveCount(0);
});
