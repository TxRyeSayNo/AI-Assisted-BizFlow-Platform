import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { RequestDetail, RequestDetailView } from './request-detail';

describe('request detail component', () => {
  const mockDetail: RequestDetailView = {
    requestId: '01a12000-0000-7000-8000-000000000055',
    title: 'Server Migration Plan',
    description: 'Detailed migration requirements',
    status: 'DRAFT',
    priority: 'HIGH',
    serviceId: 'srv-1',
    serviceName: 'Infrastructure',
    categoryId: 'cat-1',
    categoryName: 'Cloud Servers',
    requesterId: 'usr-1',
    requesterName: 'Bob Architect',
    requesterEmail: 'bob@example.com',
    parentRequestId: null,
    revisedFromRequestId: null,
    createdAt: '2026-10-07T10:00:00Z',
    updatedAt: '2026-10-07T10:00:00Z',
    resolvedAt: null,
    closedAt: null,
    currentRoutingDepartmentId: null,
    currentRoutingDepartmentName: null,
    currentRoutingUserId: null,
    currentRoutingUserName: null,
    routedAt: null,
    receivedAt: null,
    rejectionReason: null,
  };

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'requests/:id', component: RequestDetail }]),
        {
          provide: ActivatedRoute,
          useValue: { params: of({ id: '01a12000-0000-7000-8000-000000000055' }) },
        },
        {
          provide: SessionService,
          useValue: {
            hasPermission: () => true,
            identity: () => ({
              userId: 'usr-1',
              fullName: 'Bob Architect',
              tenantId: '01a10000-0000-7000-8000-000000000001',
              tenantName: 'Acme Corp',
              permissions: ['requests.submit', 'requests.confirm', 'requests.resolve'],
            }),
          },
        },
      ],
    }),
  );

  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((r) => r.url === '/api/v1/comments').forEach((r) => r.flush([]));
    http.match((r) => r.url === '/api/v1/attachments').forEach((r) => r.flush([]));
    http.verify();
  });

  function flushInitRequests(
    http: HttpTestingController,
    detail: RequestDetailView = mockDetail,
  ) {
    const req = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    req.flush(detail);

    const deptsReq = http.expectOne('/api/v1/departments?pageSize=100');
    deptsReq.flush({ items: [{ departmentId: 'dept-1', name: 'IT Ops', code: 'OPS' }] });

    const usersReq = http.expectOne('/api/v1/users?pageSize=100');
    usersReq.flush({
      items: [
        {
          userId: 'usr-2',
          displayName: 'Alice Engineer',
          employeeCode: 'EMP002',
          departmentId: 'dept-1',
        },
      ],
    });
  }

  it('loads and renders request details', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    flushInitRequests(TestBed.inject(HttpTestingController));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Server Migration Plan');
    expect(fixture.nativeElement.textContent).toContain('Infrastructure');
    expect(fixture.nativeElement.textContent).toContain('Cloud Servers');
    expect(fixture.nativeElement.textContent).toContain('Bob Architect');
    expect(fixture.nativeElement.textContent).toContain('bob@example.com');
    expect(fixture.nativeElement.textContent).toContain('Draft');
    expect(fixture.nativeElement.textContent).toContain('High');
    expect(fixture.nativeElement.textContent).toContain('Detailed migration requirements');
  });

  it('renders non-revealing error message when request is not found', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    const req = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    req.flush({ code: 'NOT.FOUND' }, { status: 404, statusText: 'Not Found' });

    http.expectOne('/api/v1/departments?pageSize=100').flush({ items: [] });
    http.expectOne('/api/v1/users?pageSize=100').flush({ items: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Request not found or you do not have permission to view it.',
    );
  });

  it('shows submit button for draft and submits with idempotency key', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http);
    fixture.detectChanges();

    expect(fixture.componentInstance.canSubmit()).toBe(true);

    fixture.componentInstance.submit();

    const submitPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/submit',
    );
    expect(submitPost.request.headers.has('Idempotency-Key')).toBe(true);
    submitPost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      status: 'SUBMITTED',
      updatedAt: '2026-10-07T10:05:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({ ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canSubmit()).toBe(false);
  });

  it('routes submitted request to department and user', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canRoute()).toBe(true);

    fixture.componentInstance.openModal('route');
    fixture.componentInstance.routeDepartmentId.set('dept-1');
    fixture.componentInstance.routeUserId.set('usr-2');
    fixture.componentInstance.routeReason.set('Urgent server upgrade');

    fixture.componentInstance.executeRoute();

    const routePost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/route',
    );
    expect(routePost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(routePost.request.body).toEqual({
      toDepartmentId: 'dept-1',
      toUserId: 'usr-2',
      reason: 'Urgent server upgrade',
      source: 'MANUAL',
    });

    routePost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      routingId: '01a12000-0000-7000-8000-000000000099',
      status: 'ROUTED',
      toDepartmentId: 'dept-1',
      toUserId: 'usr-2',
      routedAt: '2026-10-07T10:10:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      status: 'ROUTED',
      currentRoutingDepartmentName: 'IT Ops',
      currentRoutingUserName: 'Alice Engineer',
      routedAt: '2026-10-07T10:10:00Z',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('IT Ops');
    expect(fixture.nativeElement.textContent).toContain('Alice Engineer');
    expect(fixture.componentInstance.canReceive()).toBe(true);
  });

  it('receives routed request with confirmation note', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, {
      ...mockDetail,
      status: 'ROUTED',
      currentRoutingDepartmentName: 'IT Ops',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.canReceive()).toBe(true);

    fixture.componentInstance.openModal('receive');
    fixture.componentInstance.receiveNote.set('Diagnostics underway');
    fixture.componentInstance.executeReceive();

    const receivePost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/receive',
    );
    expect(receivePost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(receivePost.request.body).toEqual({
      note: 'Diagnostics underway',
    });

    receivePost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      status: 'RECEIVED',
      receivedAt: '2026-10-07T10:15:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      status: 'RECEIVED',
      receivedAt: '2026-10-07T10:15:00Z',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.canStart()).toBe(true);
  });

  it('starts work on received request to transition to In Progress', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'RECEIVED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canStart()).toBe(true);

    fixture.componentInstance.executeStartWork();

    const startPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/start',
    );
    expect(startPost.request.headers.has('Idempotency-Key')).toBe(true);
    startPost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      status: 'IN_PROGRESS',
      startedAt: '2026-10-07T10:20:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({ ...mockDetail, status: 'IN_PROGRESS' });
    fixture.detectChanges();

    expect(fixture.componentInstance.data()?.status).toBe('IN_PROGRESS');
  });

  it('rejects submitted request with mandatory reason', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canReject()).toBe(true);

    fixture.componentInstance.openModal('reject');
    fixture.componentInstance.rejectReason.set('Hardware out of inventory');
    fixture.componentInstance.executeReject();

    const rejectPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/reject',
    );
    expect(rejectPost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(rejectPost.request.body).toEqual({
      reason: 'Hardware out of inventory',
    });

    rejectPost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      status: 'REJECTED',
      reason: 'Hardware out of inventory',
      rejectedAt: '2026-10-07T10:25:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      status: 'REJECTED',
      rejectionReason: 'Hardware out of inventory',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Request Rejected');
    expect(fixture.nativeElement.textContent).toContain('Hardware out of inventory');
  });

  it('resolves in-progress request with resolution details', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'IN_PROGRESS' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canResolve()).toBe(true);

    fixture.componentInstance.openModal('resolve');
    fixture.componentInstance.resolveContent.set('Replaced hardware module and tested telemetry.');
    fixture.componentInstance.executeResolve();

    const resolvePost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/resolve',
    );
    expect(resolvePost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(resolvePost.request.body).toEqual({
      content: 'Replaced hardware module and tested telemetry.',
    });

    resolvePost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      resolutionId: 'res-1',
      revisionNo: 1,
      content: 'Replaced hardware module and tested telemetry.',
      status: 'RESOLVED',
      resolvedAt: '2026-10-07T10:30:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      status: 'RESOLVED',
      resolvedAt: '2026-10-07T10:30:00Z',
      resolutions: [
        {
          resolutionId: 'res-1',
          revisionNo: 1,
          content: 'Replaced hardware module and tested telemetry.',
          resolvedAt: '2026-10-07T10:30:00Z',
          resolverUserId: 'usr-2',
          resolverName: 'Alice Engineer',
        },
      ],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.actionSuccess()).toContain('Request resolved successfully.');
    expect(fixture.nativeElement.textContent).toContain('Resolution History');
    expect(fixture.nativeElement.textContent).toContain('Revision #1');
    expect(fixture.nativeElement.textContent).toContain('Replaced hardware module and tested telemetry.');
  });

  it('confirms resolution and closes request', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'RESOLVED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canConfirm()).toBe(true);

    fixture.componentInstance.openModal('confirm');
    fixture.componentInstance.confirmDecision.set('CONFIRMED');
    fixture.componentInstance.confirmNote.set('Everything looks great and works properly.');
    fixture.componentInstance.executeConfirm();

    const confirmPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/confirm',
    );
    expect(confirmPost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(confirmPost.request.body).toEqual({
      decision: 'CONFIRMED',
      note: 'Everything looks great and works properly.',
    });

    confirmPost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      confirmationId: 'conf-1',
      decision: 'CONFIRMED',
      status: 'CLOSED',
      confirmedAt: '2026-10-07T10:35:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      status: 'CLOSED',
      closedAt: '2026-10-07T10:35:00Z',
      confirmations: [
        {
          confirmationId: 'conf-1',
          milestoneType: 'RESOLUTION',
          decision: 'CONFIRMED',
          note: 'Everything looks great and works properly.',
          decidedAt: '2026-10-07T10:35:00Z',
          actorUserId: 'usr-1',
          actorName: 'Bob Architect',
        },
      ],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.actionSuccess()).toContain('Resolution confirmed and request closed.');
    expect(fixture.nativeElement.textContent).toContain('Requester Reviews & Decisions');
    expect(fixture.nativeElement.textContent).toContain('Confirmed & Accepted');
    expect(fixture.nativeElement.textContent).toContain('Everything looks great and works properly.');
  });

  it('submits rework for resolved request with feedback', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'RESOLVED' });
    fixture.detectChanges();

    fixture.componentInstance.openModal('confirm');
    fixture.componentInstance.confirmDecision.set('REWORK');
    fixture.componentInstance.confirmNote.set('');

    // Attempting rework with empty note sets validation error
    fixture.componentInstance.executeConfirm();
    expect(fixture.componentInstance.actionError()).toContain('note explaining why rework is needed');

    // Fill note and submit
    fixture.componentInstance.confirmNote.set('Noise still persists on reboot.');
    fixture.componentInstance.executeConfirm();

    const confirmPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/confirm',
    );
    expect(confirmPost.request.body).toEqual({
      decision: 'REWORK',
      note: 'Noise still persists on reboot.',
    });

    confirmPost.flush({
      requestId: '01a12000-0000-7000-8000-000000000055',
      confirmationId: 'conf-2',
      decision: 'REWORK',
      status: 'IN_PROGRESS',
      confirmedAt: '2026-10-07T10:38:00Z',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({ ...mockDetail, status: 'IN_PROGRESS' });
    fixture.detectChanges();

    expect(fixture.componentInstance.actionSuccess()).toContain('Rework requested and sent back to in-progress.');
  });

  it('creates revised request from rejected request', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'REJECTED', rejectionReason: 'Insufficient scope' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canRevise()).toBe(true);

    fixture.componentInstance.openModal('revise');
    expect(fixture.componentInstance.reviseTitle()).toContain('Revised: Server Migration Plan');

    fixture.componentInstance.reviseTitle.set('Revised: Full Server Migration Plan v2');
    fixture.componentInstance.reviseDescription.set('Added full scope and hardware inventory.');
    fixture.componentInstance.revisePriority.set('URGENT');
    fixture.componentInstance.reviseSubmitImmediately.set(true);

    fixture.componentInstance.executeRevise();

    const revisePost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/revise',
    );
    expect(revisePost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(revisePost.request.body).toEqual({
      title: 'Revised: Full Server Migration Plan v2',
      description: 'Added full scope and hardware inventory.',
      priority: 'URGENT',
      submitImmediately: true,
    });

    revisePost.flush({
      requestId: '01a12000-0000-7000-8000-000000000099',
      sourceRequestId: '01a12000-0000-7000-8000-000000000055',
      title: 'Revised: Full Server Migration Plan v2',
      status: 'SUBMITTED',
      createdAt: '2026-10-07T10:40:00Z',
    });

    expect(fixture.componentInstance.actionSuccess()).toContain('Revised request created successfully.');
  });

  it('renders linked tasks in the request detail view', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, {
      ...mockDetail,
      tasks: [
        {
          taskId: 'task-0001-0000-0000-000000000001',
          title: 'Provision Cloud Database Instance',
          status: 'IN_PROGRESS',
          priority: 'HIGH',
          deadline: '2026-10-15T12:00:00Z',
          createdAt: '2026-10-07T10:00:00Z',
          assignedUserId: 'usr-2',
          assignedUserName: 'Alice Engineer',
          assignedDepartmentId: 'dept-1',
          assignedDepartmentName: 'IT Ops',
        },
      ],
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Linked Tasks');
    expect(text).toContain('1 task');
    expect(text).toContain('Provision Cloud Database Instance');
    expect(text).toContain('Alice Engineer');
  });

  it('allows creating a linked task from request and refreshes details', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'IN_PROGRESS' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canCreateTask()).toBe(true);

    fixture.componentInstance.openModal('create-task');
    expect(fixture.componentInstance.activeModal()).toBe('create-task');
    expect(fixture.componentInstance.taskTitle()).toContain('Task: Server Migration Plan');

    fixture.componentInstance.taskTitle.set('Setup Redis Cluster');
    fixture.componentInstance.taskDescription.set('Configure cluster with 3 shards.');
    fixture.componentInstance.taskPriority.set('HIGH');
    fixture.componentInstance.newChecklistItem.set('Configure shard 1');
    fixture.componentInstance.addChecklistItem();
    fixture.componentInstance.newChecklistItem.set('Configure shard 2');
    fixture.componentInstance.addChecklistItem();
    expect(fixture.componentInstance.taskChecklist().length).toBe(2);

    fixture.componentInstance.removeChecklistItem(1);
    expect(fixture.componentInstance.taskChecklist()).toEqual(['Configure shard 1']);

    fixture.componentInstance.executeCreateTask();

    const taskPost = http.expectOne(
      '/api/v1/requests/01a12000-0000-7000-8000-000000000055/tasks',
    );
    expect(taskPost.request.headers.has('Idempotency-Key')).toBe(true);
    expect(taskPost.request.body).toEqual({
      title: 'Setup Redis Cluster',
      description: 'Configure cluster with 3 shards.',
      priority: 'HIGH',
      deadline: null,
      checklist: ['Configure shard 1'],
    });

    taskPost.flush({
      taskId: '01a13000-0000-7000-8000-000000000001',
      title: 'Setup Redis Cluster',
      status: 'DRAFT',
      priority: 'HIGH',
      deadline: null,
      createdAt: '2026-10-07T11:00:00Z',
      requestId: '01a12000-0000-7000-8000-000000000055',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({
      ...mockDetail,
      tasks: [
        {
          taskId: '01a13000-0000-7000-8000-000000000001',
          title: 'Setup Redis Cluster',
          status: 'DRAFT',
          priority: 'HIGH',
          deadline: null,
          createdAt: '2026-10-07T11:00:00Z',
          assignedUserId: null,
          assignedUserName: null,
          assignedDepartmentId: null,
          assignedDepartmentName: null,
        },
      ],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.activeModal()).toBeNull();
    expect(fixture.componentInstance.actionSuccess()).toContain('Task created successfully and linked to this request.');
  });

  it('prevents task creation when request is in inactive status (e.g., CANCELLED or CLOSED)', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'CANCELLED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canCreateTask()).toBe(false);
  });

  it('archives closed request when authorized and confirmed', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'CLOSED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.canArchive()).toBe(true);

    fixture.componentInstance.executeArchive();

    const archiveReq = http.expectOne('/api/v1/records/request/01a12000-0000-7000-8000-000000000055/archive');
    expect(archiveReq.request.method).toBe('POST');
    archiveReq.flush({
      recordId: '01a12000-0000-7000-8000-000000000055',
      recordType: 'REQUEST',
      status: 'CLOSED',
      archivedAt: '2026-10-07T14:00:00Z',
      message: 'Request archived successfully.',
    });

    const reloadReq = http.expectOne('/api/v1/requests/01a12000-0000-7000-8000-000000000055');
    reloadReq.flush({ ...mockDetail, status: 'CLOSED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.actionSuccess()).toContain('Request archived successfully.');
  });

  it('triggers AI multi-intent analysis and executes split request', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    fixture.componentInstance.analyzeMultiIntent();
    expect(fixture.componentInstance.aiBusy()).toBe(true);

    const aiReq = http.expectOne('/api/v1/ai/request-multi-intent');
    expect(aiReq.request.method).toBe('POST');
    expect(aiReq.request.body.requestId).toBe(mockDetail.requestId);
    aiReq.flush({
      requestId: mockDetail.requestId,
      isMultiIntent: true,
      confidenceScore: 0.92,
      primaryIntent: 'Server upgrade',
      intents: [
        {
          intentId: '01a14000-0000-7000-8000-000000000001',
          title: 'Upgrade Database Server',
          description: 'Migrate DB to Postgres 18',
          suggestedServiceId: 'srv-1',
          suggestedServiceName: 'Infrastructure',
        },
        {
          intentId: '01a14000-0000-7000-8000-000000000002',
          title: 'Configure Firewall',
          description: 'Open port 5432 for DB',
          suggestedServiceId: 'srv-2',
          suggestedServiceName: 'Security',
        },
      ],
      reasoning: 'Request spans two independent service domains',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.aiBusy()).toBe(false);
    expect(fixture.componentInstance.activeModal()).toBe('ai-split');
    expect(fixture.componentInstance.aiMultiIntent()?.intents?.length).toBe(2);

    // Confirm split
    fixture.componentInstance.confirmSplit();
    const splitReq = http.expectOne(`/api/v1/requests/${mockDetail.requestId}/split`);
    expect(splitReq.request.method).toBe('POST');
    expect(splitReq.request.body.splits.length).toBe(2);
    splitReq.flush({
      parentRequestId: mockDetail.requestId,
      splitCount: 2,
      childRequestIds: ['01a14000-0000-7000-8000-000000000011', '01a14000-0000-7000-8000-000000000012'],
    });

    const reloadReq = http.expectOne(`/api/v1/requests/${mockDetail.requestId}`);
    reloadReq.flush({ ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    expect(fixture.componentInstance.activeModal()).toBeNull();
    expect(fixture.componentInstance.actionSuccess()).toContain('2 yêu cầu con');
  });

  it('triggers AI request routing recommendation and populates target department', () => {
    const fixture = TestBed.createComponent(RequestDetail);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    flushInitRequests(http, { ...mockDetail, status: 'SUBMITTED' });
    fixture.detectChanges();

    fixture.componentInstance.recommendRouting();
    expect(fixture.componentInstance.aiBusy()).toBe(true);

    const aiReq = http.expectOne('/api/v1/ai/request-routing');
    expect(aiReq.request.method).toBe('POST');
    aiReq.flush({
      requestId: mockDetail.requestId,
      recommendedDepartmentId: '01a10000-0000-7000-8000-000000000010',
      recommendedDepartmentName: 'IT Operations',
      confidenceScore: 0.95,
      rationale: 'Infrastructure issues are handled by IT Operations',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.aiBusy()).toBe(false);
    expect(fixture.componentInstance.activeModal()).toBe('route');
    expect(fixture.componentInstance.routeDepartmentId()).toBe('01a10000-0000-7000-8000-000000000010');
    expect(fixture.componentInstance.routeReason()).toContain('IT Operations');
    expect(fixture.componentInstance.routeSource()).toBe('AI');
  });
});
