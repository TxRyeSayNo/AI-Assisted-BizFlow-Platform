import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { RobotWidgetComponent } from './robot-widget';

describe('RobotWidgetComponent', () => {
  let fixture: ComponentFixture<RobotWidgetComponent>;
  let component: RobotWidgetComponent;
  let http: HttpTestingController;
  let router: Router;

  const mockSession = {
    signedIn: () => true,
    hasPermission: () => true,
    identity: () => ({ userId: 'test-user', fullName: 'Tester' }),
  };

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      imports: [RobotWidgetComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: mockSession },
      ],
    });

    fixture = TestBed.createComponent(RobotWidgetComponent);
    component = fixture.componentInstance;
    component.i18n.setLanguage('vi');
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('defaults to minimized floating circular FAB on initial session', () => {
    fixture.detectChanges();
    expect(component.isOpen()).toBe(false);
  });

  it('restores open state if bizflow_robot_open was set in current session', () => {
    sessionStorage.setItem('bizflow_robot_open', 'true');
    component.ngOnInit();
    expect(component.isOpen()).toBe(true);
  });

  it('closes when closeWidget is called and saves dismissal flag', () => {
    fixture.detectChanges();
    component.openWidget();
    expect(component.isOpen()).toBe(true);

    component.closeWidget();
    expect(component.isOpen()).toBe(false);
    expect(sessionStorage.getItem('bizflow_robot_dismissed')).toBe('true');
    expect(sessionStorage.getItem('bizflow_robot_open')).toBeNull();
  });

  it('reopens widget when openWidget is called and clears dismissal flag', () => {
    sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    component.ngOnInit();
    expect(component.isOpen()).toBe(false);

    component.openWidget();
    expect(component.isOpen()).toBe(true);
    expect(sessionStorage.getItem('bizflow_robot_open')).toBe('true');
    expect(sessionStorage.getItem('bizflow_robot_dismissed')).toBeNull();
  });

  it('sends message to /api/v1/ai/chat and displays response', () => {
    fixture.detectChanges();
    component.openWidget();
    component.inputMessage.set('Hôm nay tôi có deadline nào không?');

    component.sendMessage();
    expect(component.busy()).toBe(true);

    const req = http.expectOne('/api/v1/ai/chat');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      message: 'Hôm nay tôi có deadline nào không?',
      locale: 'vi',
    });

    req.flush({
      response: 'Bạn có 1 nhiệm vụ đến hạn hôm nay: Triển khai Robot Chatbot',
      recognizedCommand: 'deadline_today',
      actionCards: [
        {
          actionType: 'NAVIGATE_TASKS',
          title: 'Xem danh sách công việc',
          linkUrl: '/tasks',
        },
      ],
    });

    expect(component.busy()).toBe(false);
    const msgs = component.messages();
    const lastMsg = msgs[msgs.length - 1];
    expect(lastMsg.sender).toBe('robot');
    expect(lastMsg.text).toContain('Triển khai Robot Chatbot');
    expect(lastMsg.actionCards?.length).toBe(1);
  });

  it('navigates when action card is clicked', () => {
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    component.executeAction({
      actionType: 'NAVIGATE_AI_DECOMPOSITION',
      title: 'Phân rã AI',
      linkUrl: '/tasks/ai-decomposition',
    });

    expect(navSpy).toHaveBeenCalledWith(['/tasks/ai-decomposition'], { queryParams: undefined });
  });

  it('executes CONFIRM_ACTION and posts confirmation token to execute endpoint', () => {
    component.executeAction({
      actionType: 'CONFIRM_ACTION',
      title: 'Xác nhận tạo việc',
      linkUrl: '/api/v1/ai/actions/execute',
      payload: {
        toolName: 'create_task_draft',
        argsJson: '{"title":"Test"}',
        confirmationToken: 'valid-test-token',
      },
    });

    expect(component.busy()).toBe(true);

    const req = http.expectOne('/api/v1/ai/actions/execute');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      toolName: 'create_task_draft',
      argsJson: '{"title":"Test"}',
      confirmed: true,
      confirmationToken: 'valid-test-token',
    });

    req.flush({ success: true, resultJson: '{"id":"task-123"}' });

    expect(component.busy()).toBe(false);
    const lastMsg = component.messages()[component.messages().length - 1];
    expect(lastMsg.sender).toBe('robot');
    expect(lastMsg.text).toContain('thành công');
  });

  it('sends quick prompt when chip is clicked', () => {
    fixture.detectChanges();
    component.sendQuickPrompt('/task Cài đặt PostgreSQL 18');

    const req = http.expectOne('/api/v1/ai/chat');
    expect(req.request.body).toEqual({
      message: '/task Cài đặt PostgreSQL 18',
      locale: 'vi',
    });
    req.flush({
      response: 'Đã phân rã yêu cầu thành 2 nhiệm vụ đề xuất.',
      recognizedCommand: '/task',
    });

    expect(component.messages().length).toBeGreaterThan(1);
  });

  it('triggers localized quick prompt via onQuickPrompt', () => {
    fixture.detectChanges();
    component.onQuickPrompt('today');

    const req = http.expectOne('/api/v1/ai/chat');
    expect(req.request.body.message).toBe('Hôm nay tôi có deadline nào không?');
    req.flush({ response: 'Không có deadline.' });
  });

  it('renders interactive questionnaire and agent thought when returned', () => {
    fixture.detectChanges();
    component.openWidget();

    const mockQuestionnaire = {
      formId: 'form_meeting',
      topic: 'Chuẩn bị cuộc họp',
      contextExplanation: 'Vui lòng làm rõ các thông tin:',
      questions: [
        {
          id: 'q_time',
          title: 'Thời gian',
          type: 'single_choice' as const,
          options: [
            { id: 't1', label: '9h00 sáng mai', isDefault: true },
            { id: 't2', label: '14h00 chiều mai' },
          ],
          required: true,
        },
      ],
      submitButtonText: 'Xác nhận & Gửi',
    };

    component.inputMessage.set('Chuẩn bị cuộc họp ngày mai');
    component.sendMessage();

    const req = http.expectOne('/api/v1/ai/chat');
    req.flush({
      response: 'Cần làm rõ thông tin cuộc họp.',
      recognizedCommand: 'AGENT_CLARIFICATION_REQUIRED',
      questionnaire: mockQuestionnaire,
      agentThought: 'Suy nghĩ của Agent: Cần khảo sát người dùng.',
    });

    fixture.detectChanges();

    const lastMsg = component.messages()[component.messages().length - 1];
    expect(lastMsg.questionnaire).toBeDefined();
    expect(lastMsg.agentThought).toContain('Suy nghĩ của Agent');
    expect(component.isOptionSelected(lastMsg.id, 'q_time', 't1', true)).toBe(true);

    // Toggle choice to t2
    component.toggleSingleChoice(lastMsg.id, 'q_time', 't2');
    expect(component.isOptionSelected(lastMsg.id, 'q_time', 't2')).toBe(true);
    expect(component.isOptionSelected(lastMsg.id, 'q_time', 't1')).toBe(false);

    // Submit questionnaire
    component.submitQuestionnaire(lastMsg);

    const submitReq = http.expectOne('/api/v1/ai/chat');
    expect(submitReq.request.body.formId).toBe('form_meeting');
    expect(submitReq.request.body.formAnswers).toEqual([
      { questionId: 'q_time', selectedOptionIds: ['t2'], customText: undefined },
    ]);

    submitReq.flush({
      response: 'Đã hoàn tất phân rã cuộc họp!',
      recognizedCommand: '/task',
    });

    const finalMsg = component.messages()[component.messages().length - 1];
    expect(finalMsg.text).toContain('Đã hoàn tất phân rã');
  });
});
