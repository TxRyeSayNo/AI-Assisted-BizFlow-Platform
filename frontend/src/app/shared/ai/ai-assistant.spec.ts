import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AiAssistantComponent } from './ai-assistant';
import { I18nService } from '../../core/i18n/i18n.service';

describe('AiAssistantComponent - Master AI Test Suite', () => {
  let component: AiAssistantComponent;
  let fixture: ComponentFixture<AiAssistantComponent>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiAssistantComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        I18nService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AiAssistantComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
  });

  it('initializes in default task mode with drawer closed', () => {
    expect(component.isOpen).toBe(false);
    expect(component.mode()).toBe('task');
    expect(component.taskSuggestion()).toBeNull();
  });

  it('switches between all 5 AI operational modes cleanly', () => {
    component.setMode('request');
    expect(component.mode()).toBe('request');

    component.setMode('branching');
    expect(component.mode()).toBe('branching');

    component.setMode('summarizer');
    expect(component.mode()).toBe('summarizer');

    component.setMode('risk');
    expect(component.mode()).toBe('risk');

    component.setMode('task');
    expect(component.mode()).toBe('task');
  });

  it('drafts task via AI assistance and links directly to /tasks/new with populated queryParams', () => {
    component.isOpen = true;
    component.setMode('task');
    component.promptText = 'Nâng cấp cụm Kubernetes và kiểm thử tải cho chiến dịch 11/11';

    component.generate();
    expect(component.loading()).toBe(true);

    const req = http.expectOne('/api/v1/ai/task-assistance');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      prompt: 'Nâng cấp cụm Kubernetes và kiểm thử tải cho chiến dịch 11/11',
    });

    req.flush({
      title: 'Nâng cấp Kubernetes Cluster v1.31',
      description: 'Thực hiện rolling update các node worker và chạy k6 load testing',
      priority: 'HIGH',
      suggestedDurationDays: 4,
      checklistItems: ['Sao lưu etcd', 'Cập nhật control plane', 'Chạy k6 test'],
      reasoning: 'Chiến dịch 11/11 yêu cầu hạ tầng chịu tải cao và tính sẵn sàng 99.99%',
      confidence: 0.95,
      recommendationId: 'rec-123',
    });

    expect(component.loading()).toBe(false);
    const suggestion = component.taskSuggestion();
    expect(suggestion).not.toBeNull();
    expect(suggestion?.title).toBe('Nâng cấp Kubernetes Cluster v1.31');
    expect(suggestion?.priority).toBe('HIGH');
    expect(suggestion?.checklistItems.length).toBe(3);

    // Test linkage: Apply & Open in New Task
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component.applyTaskDraft();

    expect(component.isOpen).toBe(false);
    expect(navSpy).toHaveBeenCalledWith(['/tasks/new'], {
      queryParams: {
        aiTitle: 'Nâng cấp Kubernetes Cluster v1.31',
        aiPriority: 'HIGH',
        aiDescription: 'Thực hiện rolling update các node worker và chạy k6 load testing',
        aiChecklist: 'Sao lưu etcd\nCập nhật control plane\nChạy k6 test',
      },
    });
  });

  it('analyzes multi-intent requests and proposes decomposition into child requests', () => {
    component.isOpen = true;
    component.setMode('request');
    component.promptText = 'Cần cấp quyền truy cập VPN và đăng ký mua license Figma Enterprise';

    component.generate();

    const req = http.expectOne('/api/v1/ai/request-multi-intent');
    expect(req.request.method).toBe('POST');
    req.flush({
      isMultiIntent: true,
      confidence: 0.92,
      rationale: 'Phát hiện 2 nhu cầu riêng biệt thuộc 2 phòng ban: Hạ tầng IT và Mua sắm bản quyền.',
      intents: [
        {
          intentIndex: 1,
          title: 'Cấp quyền truy cập VPN từ xa',
          description: 'Cấu hình tài khoản OpenVPN với xác thực 2FA',
          suggestedServiceName: 'IT Infrastructure',
          confidence: 0.92,
        },
        {
          intentIndex: 2,
          title: 'Mua license Figma Enterprise',
          description: 'Cấp phát license thiết kế cho phòng UI/UX',
          suggestedServiceName: 'Procurement',
          confidence: 0.88,
        },
      ],
    });

    const result = component.intentResult();
    expect(result).not.toBeNull();
    expect(result?.isMultiIntent).toBe(true);
    expect(result?.intents.length).toBe(2);

    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component.applyRequestIntent();
    expect(navSpy).toHaveBeenCalledWith(['/requests/new']);
  });

  it('plans and executes multi-branch DAG project with atomic task and request creation', () => {
    component.isOpen = true;
    component.setMode('branching');
    component.promptText = 'Kế hoạch triển khai hệ thống ERP toàn diện';

    component.generate();

    const planReq = http.expectOne('/api/v1/ai/branching/plan');
    expect(planReq.request.method).toBe('POST');
    planReq.flush({
      planId: 'plan-dag-999',
      sourceType: 'DIRECT_INPUT',
      sourceId: null,
      totalBranches: 2,
      estimatedTotalDays: 14,
      rationale: 'Phân tách thành giai đoạn khảo sát nghiệp vụ và triển khai kỹ thuật',
      confirmationToken: 'token-secure-abc',
      plannedAt: '2026-10-31T00:00:00Z',
      branches: [
        {
          branchIndex: 1,
          branchType: 'REQUEST',
          title: 'Khảo sát quy trình kế toán',
          description: 'Thu thập biểu mẫu chứng từ',
          departmentName: 'Phòng Tài chính',
          priority: 'HIGH',
          suggestedDurationDays: 5,
          executionPhase: 1,
          dependsOnBranchIndexes: [],
          checklist: [],
          reasoning: 'Khảo sát ban đầu',
        },
        {
          branchIndex: 2,
          branchType: 'TASK',
          title: 'Cài đặt máy chủ ERP',
          description: 'Cấu hình database PostgreSQL và application server',
          departmentName: 'Phòng IT',
          priority: 'HIGH',
          suggestedDurationDays: 7,
          executionPhase: 2,
          dependsOnBranchIndexes: [1],
          checklist: [],
          reasoning: 'Triển khai kỹ thuật',
        },
      ],
    });

    expect(component.branchingResult()?.totalBranches).toBe(2);
    expect(component.branchingResult()?.confirmationToken).toBe('token-secure-abc');

    // Execute plan
    component.executeBranchingPlan();
    expect(component.executingPlan()).toBe(true);

    const execReq = http.expectOne('/api/v1/ai/branching/execute');
    expect(execReq.request.method).toBe('POST');
    expect(execReq.request.body.planId).toBe('plan-dag-999');
    expect(execReq.request.body.confirmationToken).toBe('token-secure-abc');

    execReq.flush({
      planId: 'plan-dag-999',
      totalCreated: 2,
      createdRecords: [
        {
          branchIndex: 1,
          recordType: 'REQUEST',
          recordId: 'req-001',
          title: 'Khảo sát quy trình kế toán',
          status: 'CREATED',
          departmentName: 'Phòng Tài chính',
        },
        {
          branchIndex: 2,
          recordType: 'TASK',
          recordId: 'tsk-002',
          title: 'Cài đặt máy chủ ERP',
          status: 'CREATED',
          departmentName: 'Phòng IT',
        },
      ],
      executedAt: '2026-10-31T00:00:00Z',
    });

    expect(component.executingPlan()).toBe(false);
    expect(component.planExecutedSuccess()).toBe(true);
    expect(component.executedPlanResult()?.totalCreated).toBe(2);
  });

  it('summarizes document, extracts action items, and spawns DRAFT tasks into system', () => {
    component.isOpen = true;
    component.setMode('summarizer');
    component.promptText = 'Biên bản cuộc họp ban giám đốc: Cần hoàn thành audit bảo mật và nghiệm thu API.';

    component.generate();

    const sumReq = http.expectOne('/api/v1/ai/documents/summarize');
    expect(sumReq.request.method).toBe('POST');
    sumReq.flush({
      sourceTitle: 'Biên bản cuộc họp ban giám đốc',
      sourceType: 'DirectText',
      mode: 'COMPREHENSIVE',
      executiveSummary: 'Ban giám đốc chỉ đạo 2 đầu việc trọng yếu trước ngày phát hành chính thức.',
      keyPoints: ['Bảo mật hạ tầng', 'Hoàn tất nghiệm thu'],
      risksAndCompliance: ['Nguy cơ chậm tiến độ ra mắt'],
      wordCount: 120,
      confidence: 0.94,
      tokensUsed: 350,
      actionItems: [
        {
          orderIndex: 1,
          title: 'Audit bảo mật hệ thống',
          description: 'Thực hiện pentest và kiểm tra lỗ hổng OWASP',
          suggestedDepartment: 'Security',
          suggestedPriority: 'CRITICAL',
          suggestedDurationDays: 3,
        },
        {
          orderIndex: 2,
          title: 'Nghiệm thu API đối tác',
          description: 'Ký kết biên bản tích hợp cổng thanh toán',
          suggestedDepartment: 'Integration',
          suggestedPriority: 'HIGH',
          suggestedDurationDays: 2,
        },
      ],
      summarizedAt: '2026-10-31T00:00:00Z',
    });

    const sumResult = component.summarizerResult();
    expect(sumResult).not.toBeNull();
    expect(sumResult?.actionItems.length).toBe(2);

    // Initially all action items are auto-selected
    expect(component.allSummarizerActionsSelected()).toBe(true);
    expect(component.selectedSummarizerActionIndexes.size).toBe(2);

    // Toggle actions: deselect all
    component.toggleAllSummarizerActions();
    expect(component.allSummarizerActionsSelected()).toBe(false);
    expect(component.selectedSummarizerActionIndexes.size).toBe(0);

    // Toggle actions: re-select all
    component.toggleAllSummarizerActions();
    expect(component.allSummarizerActionsSelected()).toBe(true);
    expect(component.selectedSummarizerActionIndexes.size).toBe(2);

    // Spawn tasks from selected items
    component.createTasksFromSummarizer();
    expect(component.creatingSummarizerTasks()).toBe(true);

    const taskSpawnReq = http.expectOne('/api/v1/ai/documents/create-tasks');
    expect(taskSpawnReq.request.method).toBe('POST');
    expect(taskSpawnReq.request.body.items.length).toBe(2);

    taskSpawnReq.flush({
      totalCreated: 2,
      createdTasks: [
        { taskId: 'task-1', title: 'Audit bảo mật hệ thống', priority: 'CRITICAL', status: 'DRAFT' },
        { taskId: 'task-2', title: 'Nghiệm thu API đối tác', priority: 'HIGH', status: 'DRAFT' },
      ],
      createdAt: '2026-10-31T00:00:00Z',
    });

    expect(component.creatingSummarizerTasks()).toBe(false);
    expect(component.summarizerTasksCreatedSuccess()).toBe(true);
  });

  it('evaluates predictive task risks and presents mitigation recommendations', () => {
    vi.useFakeTimers();
    component.isOpen = true;
    component.setMode('risk');
    component.promptText = 'Nhiệm vụ chậm tiến độ 3 ngày và thiếu nhân sự chuyên môn';

    component.generate();
    vi.runAllTimers();

    const risk = component.riskResult();
    expect(risk).not.toBeNull();
    expect(risk?.level).toBeDefined();
    expect(risk?.score).toBeGreaterThan(0);
    expect(risk?.factors.length).toBeGreaterThan(0);
    expect(risk?.advice.length).toBeGreaterThan(0);
    vi.useRealTimers();
  });

  it('handles backend offline / network failure with deterministic fallback without breaking UI', () => {
    component.isOpen = true;
    component.setMode('task');
    component.promptText = 'Kiểm thử hiệu năng hạ tầng';

    component.generate();

    const req = http.expectOne('/api/v1/ai/task-assistance');
    req.flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });

    expect(component.loading()).toBe(false);
    // Verifies fallback heuristic proposal is safely populated
    const fallback = component.taskSuggestion();
    expect(fallback).not.toBeNull();
    expect(fallback?.title).toContain('Kiểm thử hiệu năng hạ tầng');
    expect(fallback?.priority).toBeDefined();
    expect(fallback?.checklistItems.length).toBeGreaterThan(0);
  });

  it('emits closeRequested and closes overlay cleanly on backdrop click', () => {
    component.isOpen = true;
    let closed = false;
    component.closeRequested.subscribe(() => (closed = true));

    const mockEvent = {
      target: {
        classList: {
          contains: (cls: string) => cls === 'ai-overlay',
        },
      },
    } as unknown as MouseEvent;

    component.closeOnBackdrop(mockEvent);
    expect(component.isOpen).toBe(false);
    expect(closed).toBe(true);
  });
});
