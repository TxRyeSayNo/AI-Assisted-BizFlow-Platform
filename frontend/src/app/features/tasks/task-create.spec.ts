import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TaskCreate } from './task-create';

describe('task creation replay safety', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('prevents double submit and retains the exact payload and key on unknown outcomes', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Original', checklist: 'One\nTwo' });
    component.save();
    component.save();
    const http = TestBed.inject(HttpTestingController);
    const first = http.expectOne('/api/v1/tasks');
    const key = first.request.headers.get('Idempotency-Key');
    const body = first.request.body;
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.uncertain()).toBe(true);
    expect(component.form.disabled).toBe(true);
    component.form.patchValue({ title: 'Changed programmatically' });
    component.save();
    const retry = http.expectOne('/api/v1/tasks');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    expect(retry.request.body).toEqual(body);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    retry.flush({ taskId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' });
    component.save();
    http.expectNone('/api/v1/tasks');
  });
  it('allows correction after known validation failure and normalizes a future local deadline', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Work', deadline: '2099-01-01T12:00' });
    component.save();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/tasks');
    expect(request.request.body.deadline).toBe(new Date('2099-01-01T12:00').toISOString());
    request.flush({}, { status: 422, statusText: 'Invalid' });
    expect(component.form.enabled).toBe(true);
  });
  it('does not send denied or past-deadline submissions', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Work', deadline: '2000-01-01T12:00' });
    component.save();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/tasks');
    vi.spyOn(component.session, 'hasPermission').mockReturnValue(false);
    component.form.patchValue({ deadline: '' });
    component.save();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/tasks');
  });

  it('prefills form values when initiated from AI assistant query parameters', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({
              aiTitle: 'Triển khai SSO Google',
              aiPriority: 'HIGH',
              aiDescription: 'Cấu hình OAuth2 Client ID và Secret',
              aiChecklist: 'Tạo Google Cloud Project\nThêm Redirect URI',
              requestId: 'req-link-123',
            }),
          },
        },
      ],
    });

    const component = TestBed.createComponent(TaskCreate).componentInstance;
    expect(component.form.value.title).toBe('Triển khai SSO Google');
    expect(component.form.value.priority).toBe('HIGH');
    expect(component.form.value.description).toBe('Cấu hình OAuth2 Client ID và Secret');
    expect(component.form.value.checklist).toBe('Tạo Google Cloud Project\nThêm Redirect URI');
    expect(component.requestId()).toBe('req-link-123');
  });

  it('assists task creation inline using assistWithAi and updates form controls with AI draft suggestion', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.aiPrompt.set('Tích hợp cổng thanh toán VNPay');
    component.assistWithAi();

    expect(component.aiBusy()).toBe(true);

    const http = TestBed.inject(HttpTestingController);
    const req = http.expectOne('/api/v1/ai/task-assistance');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ prompt: 'Tích hợp cổng thanh toán VNPay' });

    req.flush({
      title: 'Tích hợp cổng thanh toán VNPay Sandbox',
      description: 'Cấu hình Merchant ID, Secret Key và URL IPN',
      priority: 'HIGH',
      checklistItems: ['Đăng ký Sandbox Merchant', 'Code webhook callback', 'Kiểm thử thanh toán QR code'],
      confidence: 0.96,
      reasoning: 'Giao dịch thanh toán là luồng xử lý critical yêu cầu bảo mật cao',
    });

    expect(component.aiBusy()).toBe(false);
    expect(component.form.value.title).toBe('Tích hợp cổng thanh toán VNPay Sandbox');
    expect(component.form.value.description).toBe('Cấu hình Merchant ID, Secret Key và URL IPN');
    expect(component.form.value.priority).toBe('HIGH');
    expect(component.form.value.checklist).toBe('Đăng ký Sandbox Merchant\nCode webhook callback\nKiểm thử thanh toán QR code');
    expect(component.aiMessage()).toContain('AI đã tạo bản thảo');
  });

  it('handles AI assistance failure gracefully with localized notification', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.aiPrompt.set('Lỗi mạng giả định');
    component.assistWithAi();

    const http = TestBed.inject(HttpTestingController);
    const req = http.expectOne('/api/v1/ai/task-assistance');
    req.flush('Failed', { status: 500, statusText: 'Internal Error' });

    expect(component.aiBusy()).toBe(false);
    expect(component.aiMessage()).toBe('Không thể kết nối trợ lý AI. Vui lòng thử lại.');
  });
});