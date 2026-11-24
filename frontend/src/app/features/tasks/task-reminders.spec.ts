import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { ReminderMark, TaskReminders } from './task-reminders';

describe('task deadline reminders', () => {
  const config = '/api/v1/notifications/reminders/config';

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const enable = () => {
    TestBed.inject(FeatureFlagsService).load();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/features')
      .flush({ kanban: true, calendar: true, mentions: true, deadlineReminders: true });
  };

  const mark = (trigger: string, minutes: number, status = 'Scheduled'): ReminderMark => ({
    id: `mark-${trigger}`,
    taskId: 'task-1',
    userId: 'user-1',
    leadTimeMinutes: minutes,
    remindAt: '2026-12-01T00:00:00Z',
    triggerType: trigger,
    status,
    createdAt: '2026-11-01T00:00:00Z',
  });

  const create = (hasDeadline = true) => {
    const fixture = TestBed.createComponent(TaskReminders);
    fixture.componentRef.setInput('taskId', 'task-1');
    fixture.componentRef.setInput('hasDeadline', hasDeadline);
    fixture.detectChanges();
    return fixture;
  };

  it('stays completely inert while the feature is off', () => {
    const fixture = create();
    TestBed.inject(HttpTestingController).verify();
    expect(fixture.nativeElement.querySelector('mat-checkbox')).toBeNull();
  });

  it('asks for nothing when the task carries no deadline', () => {
    enable();
    create(false);
    TestBed.inject(HttpTestingController).verify();
  });

  it('shows the four documented marks and preselects the scheduled ones', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();

    const request = http.expectOne((r) => r.url === config);
    expect(request.request.params.get('taskId')).toBe('task-1');
    request.flush([mark('24h', 1440), mark('1h', 60), mark('Overdue', 0, 'Cancelled')]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('mat-checkbox').length).toBe(4);
    expect([...fixture.componentInstance.selected()].sort()).toEqual(['1h', '24h']);
  });

  it('posts the chosen lead times and cancels the marks that were cleared', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http.expectOne((r) => r.url === config).flush([mark('24h', 1440), mark('1h', 60)]);
    fixture.detectChanges();

    // Drop the 24h warning and add the overdue one.
    fixture.componentInstance.toggle('24h');
    fixture.componentInstance.toggle('Overdue');
    fixture.componentInstance.save();

    const posted = http.expectOne((r) => r.method === 'POST' && r.url === config);
    expect(posted.request.body.taskId).toBe('task-1');
    expect([...posted.request.body.leadTimeMinutes].sort((a: number, b: number) => a - b)).toEqual([
      0, 60,
    ]);
    posted.flush([mark('1h', 60), mark('Overdue', 0)]);

    // The cleared 24h mark is cancelled by id.
    http
      .expectOne({ method: 'DELETE', url: '/api/v1/notifications/reminders/mark-24h' })
      .flush(null);

    expect([...fixture.componentInstance.selected()].sort()).toEqual(['1h', 'Overdue']);
    expect(fixture.componentInstance.busy()).toBe(false);
    expect(TestBed.inject(ToastService).toasts()[0].type).toBe('success');
  });

  it('clearing every box cancels instead of posting an empty list', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http.expectOne((r) => r.url === config).flush([mark('24h', 1440), mark('1h', 60)]);
    fixture.detectChanges();

    fixture.componentInstance.toggle('24h');
    fixture.componentInstance.toggle('1h');
    fixture.componentInstance.save();

    // An empty list would mean "use the defaults" to the server, so nothing is posted.
    http
      .expectOne({ method: 'DELETE', url: '/api/v1/notifications/reminders/mark-24h' })
      .flush(null);
    http
      .expectOne({ method: 'DELETE', url: '/api/v1/notifications/reminders/mark-1h' })
      .flush(null);
    http.verify();

    expect([...fixture.componentInstance.selected()]).toEqual([]);
  });

  it('reports a failed save without leaking server text and stays usable', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http.expectOne((r) => r.url === config).flush([]);
    fixture.detectChanges();

    fixture.componentInstance.toggle('1h');
    fixture.componentInstance.save();
    http
      .expectOne((r) => r.method === 'POST' && r.url === config)
      .flush({ message: 'private detail' }, { status: 503, statusText: 'Unavailable' });

    expect(fixture.componentInstance.busy()).toBe(false);
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).not.toContain('private detail');
  });

  it('renders nothing when the task is outside the callers scope', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http.expectOne((r) => r.url === config).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.available()).toBe(false);
    expect(fixture.nativeElement.querySelector('mat-checkbox')).toBeNull();
  });
});
