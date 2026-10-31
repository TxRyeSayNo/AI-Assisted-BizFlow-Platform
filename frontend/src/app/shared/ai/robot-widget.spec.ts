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

  it('opens by default on initial session (BR-029)', () => {
    fixture.detectChanges();
    expect(component.isOpen()).toBe(true);
    expect(sessionStorage.getItem('bizflow_robot_dismissed')).toBeNull();
  });

  it('respects dismissal flag in current session and stays minimized (BR-029)', () => {
    sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    component.ngOnInit();
    expect(component.isOpen()).toBe(false);
  });

  it('closes for current session when closeWidget is called and sets dismissal flag (BR-029)', () => {
    fixture.detectChanges();
    expect(component.isOpen()).toBe(true);

    component.closeWidget();
    expect(component.isOpen()).toBe(false);
    expect(sessionStorage.getItem('bizflow_robot_dismissed')).toBe('true');
  });

  it('reopens widget when openWidget is called and clears dismissal flag', () => {
    sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    component.ngOnInit();
    expect(component.isOpen()).toBe(false);

    component.openWidget();
    expect(component.isOpen()).toBe(true);
    expect(sessionStorage.getItem('bizflow_robot_dismissed')).toBeNull();
  });

  it('sends message to /api/v1/ai/chat and displays response', () => {
    fixture.detectChanges();
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
});
