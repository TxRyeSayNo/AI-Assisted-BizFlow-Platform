import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { TaskTemplates, TaskTemplateView } from './task-templates';

describe('task templates page', () => {
  const url = '/api/v1/task-templates';

  const configure = (permissions: string[] = ['roles.configure', 'tasks.create']) =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            hasPermission: (code: string) => permissions.includes(code),
            identity: () => ({ tenantName: 'Acme Corp' }),
          },
        },
        { provide: FeatureFlagsService, useValue: { taskTemplates: () => true } },
      ],
    });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const row = (over: Partial<TaskTemplateView> = {}): TaskTemplateView => ({
    templateId: 't-1',
    name: 'Báo cáo tuần',
    titleTemplate: 'Làm báo cáo tuần',
    description: null,
    priority: 'HIGH',
    deadlineOffsetDays: 3,
    recurrenceFrequency: null,
    recurrenceDay: null,
    recurrenceHourUtc: null,
    nextRunAt: null,
    lastRunAt: null,
    archived: false,
    ...over,
  });

  const pending = () => TestBed.inject(HttpTestingController).expectOne((r) => r.url === url);

  it('lists templates and describes each schedule in words', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    pending().flush([
      row(),
      row({
        templateId: 't-2',
        name: 'Dọn hộp thư',
        recurrenceFrequency: 'DAILY',
        recurrenceHourUtc: 9,
        nextRunAt: '2026-11-25T09:00:00Z',
      }),
      row({
        templateId: 't-3',
        name: 'Chốt tháng',
        recurrenceFrequency: 'MONTHLY',
        recurrenceDay: 28,
        recurrenceHourUtc: 7,
      }),
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Báo cáo tuần');
    expect(fixture.componentInstance.scheduleLabel(row())).toBe('');
    expect(
      fixture.componentInstance.scheduleLabel(
        row({ recurrenceFrequency: 'DAILY', recurrenceHourUtc: 9 }),
      ),
    ).toContain('09:00 UTC');
    expect(
      fixture.componentInstance.scheduleLabel(
        row({ recurrenceFrequency: 'MONTHLY', recurrenceDay: 28, recurrenceHourUtc: 7 }),
      ),
    ).toContain('28');
  });

  it('sends the schedule fields as numbers and blanks as null', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    const http = TestBed.inject(HttpTestingController);
    pending().flush([]);

    fixture.componentInstance.form.setValue({
      name: 'Dọn hộp thư',
      titleTemplate: 'Dọn hộp thư đến',
      description: '',
      priority: '',
      deadlineOffsetDays: '1',
      recurrenceFrequency: 'DAILY',
      recurrenceDay: '',
      recurrenceHourUtc: '9',
    });
    fixture.componentInstance.submit();

    const posted = http.expectOne((r) => r.method === 'POST' && r.url === url);
    expect(posted.request.body).toEqual({
      name: 'Dọn hộp thư',
      titleTemplate: 'Dọn hộp thư đến',
      description: null,
      priority: null,
      deadlineOffsetDays: 1,
      recurrenceFrequency: 'DAILY',
      recurrenceDay: null,
      recurrenceHourUtc: 9,
    });
    posted.flush(row());
    pending().flush([row()]);
  });

  it('edits an existing template through PUT and clears the form afterwards', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    const http = TestBed.inject(HttpTestingController);
    pending().flush([row()]);

    fixture.componentInstance.startEdit(row({ recurrenceFrequency: 'WEEKLY', recurrenceDay: 5 }));
    expect(fixture.componentInstance.form.getRawValue().recurrenceDay).toBe('5');
    fixture.componentInstance.submit();

    http.expectOne((r) => r.method === 'PUT' && r.url === `${url}/t-1`).flush(row());
    pending().flush([row()]);
    expect(fixture.componentInstance.editingId()).toBeNull();
  });

  it('creates a task from a template with an idempotency key and opens it', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    const http = TestBed.inject(HttpTestingController);
    pending().flush([row()]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture.componentInstance.instantiate(row());

    const posted = http.expectOne((r) => r.method === 'POST' && r.url === `${url}/t-1/tasks`);
    expect(posted.request.headers.get('Idempotency-Key')).toBeTruthy();
    posted.flush({ taskId: 'task-9' });

    expect(navigate).toHaveBeenCalledWith(['/tasks', 'task-9']);
  });

  it('offers no management controls without the permission and sends nothing on submit', () => {
    configure([]);
    const fixture = TestBed.createComponent(TaskTemplates);
    pending().flush([row()]);
    fixture.detectChanges();

    expect(fixture.componentInstance.canManage()).toBe(false);
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    fixture.componentInstance.form.setValue({
      name: 'X',
      titleTemplate: 'X',
      description: '',
      priority: '',
      deadlineOffsetDays: '',
      recurrenceFrequency: '',
      recurrenceDay: '',
      recurrenceHourUtc: '',
    });
    fixture.componentInstance.submit();
    // The server is the authority; the page simply does not ask.
    TestBed.inject(HttpTestingController).verify();
  });

  it('separates a disabled feature from a denial and from a transient failure', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    pending().flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(fixture.componentInstance.unavailable()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('not enabled on this deployment');

    fixture.componentInstance.load();
    pending().flush({ message: 'private detail' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.componentInstance.denied()).toBe(true);
    expect(fixture.nativeElement.textContent).not.toContain('private detail');

    fixture.componentInstance.load();
    pending().flush({ message: 'private detail' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.componentInstance.error()).toBe(true);
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
  });

  it('shows a curated 4xx message but never a 5xx body', () => {
    configure();
    const fixture = TestBed.createComponent(TaskTemplates);
    const http = TestBed.inject(HttpTestingController);
    pending().flush([]);

    fixture.componentInstance.form.setValue({
      name: 'Trùng',
      titleTemplate: 'Trùng',
      description: '',
      priority: '',
      deadlineOffsetDays: '',
      recurrenceFrequency: '',
      recurrenceDay: '',
      recurrenceHourUtc: '',
    });
    fixture.componentInstance.submit();
    http
      .expectOne((r) => r.method === 'POST')
      .flush(
        { message: 'A template with this name already exists in this workspace.' },
        { status: 409, statusText: 'Conflict' },
      );
    expect(TestBed.inject(ToastService).toasts()[0].message).toContain('already exists');

    fixture.componentInstance.submit();
    http
      .expectOne((r) => r.method === 'POST')
      .flush({ message: 'internal detail' }, { status: 500, statusText: 'Server Error' });
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts[toasts.length - 1].message).not.toContain('internal detail');
  });
});
