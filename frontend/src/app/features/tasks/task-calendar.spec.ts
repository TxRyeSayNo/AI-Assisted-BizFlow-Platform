import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { CalendarTask, CalendarView, TaskCalendar } from './task-calendar';

describe('deadline calendar', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: { hasPermission: () => true, identity: () => ({ tenantName: 'Acme Corp' }) },
        },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/tasks/calendar');

  const localIso = (value: Date) =>
    new Date(value.getFullYear(), value.getMonth(), value.getDate(), 9, 0, 0).toISOString();

  const task = (deadline: string, over: Partial<CalendarTask> = {}): CalendarTask => ({
    taskId: 'task-1',
    title: 'Việc có hạn',
    status: 'IN_PROGRESS',
    priority: 'HIGH',
    deadline,
    assignedUserName: 'Nguyễn A',
    assignedDepartmentName: null,
    ...over,
  });

  const view = (items: CalendarTask[], truncated = false): CalendarView => ({
    items,
    from: '',
    to: '',
    limit: 500,
    truncated,
  });

  it('asks for a whole-week aligned month window and groups deadlines into day cells', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    fixture.detectChanges();
    const request = pending();
    const from = new Date(request.request.params.get('from')!);
    const to = new Date(request.request.params.get('to')!);
    // Monday-first grid: both bounds land on a week boundary.
    expect((from.getDay() + 6) % 7).toBe(0);
    expect((to.getDay() + 6) % 7).toBe(6);
    expect(to.getTime()).toBeGreaterThan(from.getTime());

    const today = new Date();
    request.flush(view([task(localIso(today))]));
    fixture.detectChanges();

    // A month grid always spans whole weeks, so the cell count is a multiple of seven.
    const cells = fixture.componentInstance.cells();
    expect(cells.length % 7).toBe(0);
    const todayCell = cells.find((cell) => cell.isToday);
    expect(todayCell).toBeDefined();
    expect(todayCell!.items.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Việc có hạn');
    expect(fixture.nativeElement.querySelectorAll('.calendar-entry').length).toBe(1);
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
  });

  it('drops entries without a usable deadline instead of failing', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    pending().flush(
      view([
        task(localIso(new Date())),
        task('', { taskId: 'task-2', title: 'Không hạn' }),
        task('not-a-date', { taskId: 'task-3', title: 'Hạn sai định dạng' }),
      ]),
    );
    fixture.detectChanges();

    const placed = fixture.componentInstance.cells().flatMap((cell) => cell.items);
    expect(placed.map((item) => item.taskId)).toEqual(['task-1']);
    expect(fixture.nativeElement.textContent).not.toContain('Hạn sai định dạng');
  });

  it('switches to a seven day week window and steps forward and back', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    pending().flush(view([]));

    fixture.componentInstance.setMode('week');
    const week = pending();
    week.flush(view([]));
    expect(fixture.componentInstance.cells().length).toBe(7);

    const before = fixture.componentInstance.window().start.getTime();
    fixture.componentInstance.step(1);
    pending().flush(view([]));
    const after = fixture.componentInstance.window().start.getTime();
    // Exactly one week forward, in milliseconds.
    expect(after - before).toBe(7 * 24 * 60 * 60 * 1000);

    fixture.componentInstance.step(-1);
    pending().flush(view([]));
    expect(fixture.componentInstance.window().start.getTime()).toBe(before);

    // Asking for the mode already shown must not refetch.
    fixture.componentInstance.setMode('week');
    TestBed.inject(HttpTestingController).verify();
  });

  it('sends the applied status and priority filters', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    pending().flush(view([]));

    fixture.componentInstance.filters.setValue({ status: 'OVERDUE', priority: 'CRITICAL' });
    fixture.componentInstance.apply();

    const filtered = pending();
    expect(filtered.request.params.get('status')).toBe('OVERDUE');
    expect(filtered.request.params.get('priority')).toBe('CRITICAL');
    filtered.flush(view([]));
  });

  it('warns when the period held more deadlines than the server returned', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    pending().flush(view([], true));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('more deadlines than can be shown');
  });

  it('separates a disabled feature from a denial and from a transient failure', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
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
    expect(fixture.nativeElement.textContent).toContain('could not be loaded');
  });

  it('cancels an outdated calendar read when the view is destroyed', () => {
    const fixture = TestBed.createComponent(TaskCalendar);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
