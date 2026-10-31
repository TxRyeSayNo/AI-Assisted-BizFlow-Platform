import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../auth/session';
import { AiAgentService, SubtaskBranch } from './ai-agent.service';

describe('AiAgentService - Unified AI Agent Workspace', () => {
  let service: AiAgentService;
  let http: HttpTestingController;

  const mockSession = {
    signedIn: () => true,
    hasPermission: (perm: string) => true,
  };

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        AiAgentService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: mockSession },
      ],
    });

    service = TestBed.inject(AiAgentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('initializes with default welcome message and default tabs', () => {
    expect(service.messages().length).toBeGreaterThanOrEqual(1);
    expect(service.messages()[0].sender).toBe('robot');
    expect(service.activeTab()).toBe('chat');
    expect(service.isOpen()).toBe(false);
  });

  it('opens and closes workspace and manages tabs', () => {
    service.open('branches', true);
    expect(service.isOpen()).toBe(true);
    expect(service.activeTab()).toBe('branches');
    expect(service.isExpanded()).toBe(true);

    const deptReq = http.expectOne('/api/v1/departments?pageSize=100');
    deptReq.flush([{ id: 'dept-1', name: 'Phòng Công nghệ Thông tin' }]);

    expect(service.departments().length).toBe(1);

    service.close();
    expect(service.isOpen()).toBe(false);
  });

  it('sets proposals into branches and handles branch selection for detail screen', () => {
    const proposals = [
      {
        title: 'Nhánh 1: Cài đặt hệ thống',
        description: 'Mô tả chi tiết',
        type: 'Assignment',
        departmentName: 'Phòng IT',
        priority: 'HIGH',
        suggestedDurationDays: 2,
        checklistItems: ['Bước 1', 'Bước 2'],
      },
      {
        title: 'Nhánh 2: Đào tạo nhân sự',
        description: 'Mô tả nhánh 2',
        type: 'SupportRequest',
        departmentName: 'Phòng Nhân sự',
        priority: 'MEDIUM',
        suggestedDurationDays: 3,
        checklistItems: ['Chuẩn bị', 'Đào tạo'],
      },
    ];

    service.setProposals(proposals);

    expect(service.branches().length).toBe(2);
    expect(service.branches()[0].title).toBe('Nhánh 1: Cài đặt hệ thống');
    expect(service.branches()[0].departmentName).toBe('Phòng IT');
    expect(service.selectedBranch()).toBeNull();

    // Select branch 1 for detail view
    service.selectBranch(service.branches()[0]);
    expect(service.selectedBranch()?.title).toBe('Nhánh 1: Cài đặt hệ thống');

    // Return to overview
    service.selectBranch(null);
    expect(service.selectedBranch()).toBeNull();
  });

  it('submits a single branch when user clicks Send on branch detail', () => {
    const branch: SubtaskBranch = {
      id: 'b-1',
      branchIndex: 1,
      title: 'Tác vụ độc lập',
      description: 'Mô tả',
      type: 'Assignment',
      departmentId: null,
      departmentName: 'Phòng IT',
      priority: 'HIGH',
      suggestedDurationDays: 2,
      checklistItems: ['B1'],
      status: 'pending',
    };

    service.branches.set([branch]);
    service.submitSingleBranch(branch);

    const req = http.expectOne('/api/v1/tasks/ai-decomposition/confirm');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.items.length).toBe(1);
    expect(req.request.body.items[0].title).toBe('Tác vụ độc lập');

    req.flush({ totalCreated: 1, items: [{ id: 'task-1' }] });

    expect(service.branches()[0].status).toBe('created');
    expect(service.messages().some((m) => m.text.includes('Tác vụ độc lập'))).toBe(true);
  });

  it('submits all branches at once from branch overview screen', () => {
    const branches: SubtaskBranch[] = [
      {
        id: 'b-1',
        branchIndex: 1,
        title: 'Tác vụ 1',
        description: 'M1',
        type: 'Assignment',
        departmentId: null,
        departmentName: 'Phòng IT',
        priority: 'HIGH',
        suggestedDurationDays: 2,
        checklistItems: [],
        status: 'pending',
      },
      {
        id: 'b-2',
        branchIndex: 2,
        title: 'Tác vụ 2',
        description: 'M2',
        type: 'SupportRequest',
        departmentId: null,
        departmentName: 'Phòng HR',
        priority: 'MEDIUM',
        suggestedDurationDays: 1,
        checklistItems: [],
        status: 'pending',
      },
    ];

    service.branches.set(branches);
    service.submitAllBranches();

    const req = http.expectOne('/api/v1/tasks/ai-decomposition/confirm');
    expect(req.request.body.items.length).toBe(2);

    req.flush({ totalCreated: 2, items: [{ id: 'task-1' }, { id: 'task-2' }] });

    expect(service.branches().every((b) => b.status === 'created')).toBe(true);
  });

  it('handles in-place natural language modification of branch department', () => {
    const branches: SubtaskBranch[] = [
      {
        id: 'b-1',
        branchIndex: 1,
        title: 'Chuẩn bị phòng họp',
        description: 'Mô tả',
        type: 'Assignment',
        departmentName: 'Phòng Hành chính',
        priority: 'MEDIUM',
        suggestedDurationDays: 1,
        checklistItems: [],
        status: 'pending',
      },
    ];

    service.branches.set(branches);

    // User chats: "Đổi phòng ban nhánh 1 thành IT"
    const handled = service.handleInPlaceMutation('Đổi phòng ban nhánh 1 thành IT');
    expect(handled).toBe(true);
    expect(service.branches()[0].departmentName).toBe('IT');
    expect(service.messages().some((m) => m.text.includes('nhánh #1'))).toBe(true);
  });

  it('handles in-place natural language modification of branch title', () => {
    const branches: SubtaskBranch[] = [
      {
        id: 'b-1',
        branchIndex: 1,
        title: 'Tên cũ',
        description: 'Mô tả',
        type: 'Assignment',
        departmentName: 'Phòng IT',
        priority: 'MEDIUM',
        suggestedDurationDays: 1,
        checklistItems: [],
        status: 'pending',
      },
    ];

    service.branches.set(branches);

    const handled = service.handleInPlaceMutation('Đổi tiêu đề nhánh 1 thành Kiểm thử bảo mật toàn diện');
    expect(handled).toBe(true);
    expect(service.branches()[0].title).toBe('Kiểm thử bảo mật toàn diện');
  });

  it('handles in-place deletion and addition of branches', () => {
    const branches: SubtaskBranch[] = [
      {
        id: 'b-1',
        branchIndex: 1,
        title: 'Nhánh 1',
        description: 'M1',
        type: 'Assignment',
        departmentName: 'Phòng IT',
        priority: 'MEDIUM',
        suggestedDurationDays: 1,
        checklistItems: [],
        status: 'pending',
      },
      {
        id: 'b-2',
        branchIndex: 2,
        title: 'Nhánh 2',
        description: 'M2',
        type: 'Assignment',
        departmentName: 'Phòng HR',
        priority: 'MEDIUM',
        suggestedDurationDays: 1,
        checklistItems: [],
        status: 'pending',
      },
    ];

    service.branches.set(branches);

    // Delete branch 1
    const deleted = service.handleInPlaceMutation('Xóa nhánh 1');
    expect(deleted).toBe(true);
    expect(service.branches().length).toBe(1);
    expect(service.branches()[0].title).toBe('Nhánh 2');

    // Add branch
    const added = service.handleInPlaceMutation('Thêm nhánh mới: Cấu hình tường lửa');
    expect(added).toBe(true);
    expect(service.branches().length).toBe(2);
    expect(service.branches()[1].title).toContain('Cấu hình tường lửa');
  });

  it('manages chat history sessions (start new chat, load session, delete session)', () => {
    // Add messages to current session
    service.messages.set([
      { id: '1', sender: 'user', text: 'Kế hoạch phát hành phần mềm', time: '10:00' },
      { id: '2', sender: 'robot', text: 'Tôi đã tiếp nhận kế hoạch.', time: '10:01' },
    ]);

    // Persist session
    service.persistCurrentSession();
    expect(service.chatSessions().length).toBe(1);
    expect(service.chatSessions()[0].title).toContain('Kế hoạch phát hành');

    const savedId = service.chatSessions()[0].id;

    // Start new chat
    service.startNewChat();
    expect(service.messages().length).toBe(1); // welcome message
    expect(service.currentSessionId()).not.toBe(savedId);

    // Load old session
    service.loadSession(savedId);
    expect(service.currentSessionId()).toBe(savedId);
    expect(service.messages().length).toBe(2);

    // Delete session
    service.deleteSession(savedId);
    expect(service.chatSessions().length).toBe(0);
  });
});

