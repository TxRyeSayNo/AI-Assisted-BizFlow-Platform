import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';
import { BoardCard, BoardView, TaskBoard } from './task-board';

describe('task kanban board', () => {
  let opened: unknown[];
  let permissions: string[];

  beforeEach(() => {
    opened = [];
    permissions = [
      'tasks.assign',
      'tasks.accept',
      'tasks.execute',
      'tasks.submit',
      'tasks.confirm',
    ];
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
        {
          provide: MatDialog,
          useValue: {
            open: (component: unknown) => {
              opened.push(component);
              return { afterClosed: () => of(true) };
            },
          },
        },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/tasks/board');

  const card = (over: Partial<BoardCard> = {}): BoardCard => ({
    taskId: 'task-1',
    title: 'Việc cần làm',
    status: 'ASSIGNED',
    priority: 'HIGH',
    deadline: null,
    assignedUserId: 'user-1',
    assignedUserName: 'Nguyễn A',
    assignedDepartmentId: null,
    assignedDepartmentName: null,
    requestId: null,
    ...over,
  });

  const view = (items: BoardCard[]): BoardView => ({
    columnSize: 50,
    columns: [
      'DRAFT',
      'ASSIGNED',
      'ACCEPTED',
      'IN_PROGRESS',
      'SUBMITTED',
      'CONFIRMED',
      'COMPLETED',
      'REJECTED',
      'CANCELLED',
      'OVERDUE',
    ].map((status) => ({
      status: status as BoardCard['status'],
      items: items.filter((item) => item.status === status),
      total: items.filter((item) => item.status === status).length,
    })),
  });

  // A drop event carries only what the component reads from it.
  const dropEvent = (moved: BoardCard) =>
    ({
      previousContainer: { id: 'from' },
      container: { id: 'to' },
      item: { data: moved },
    }) as never;

  it('loads the board without filter parameters and renders one lane per state', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    fixture.detectChanges();
    // The pipe resolves to its English fallback under TestBed, as the sibling task specs assume.
    expect(fixture.nativeElement.textContent).toContain('Loading board');
    const request = pending();
    expect(request.request.params.keys()).toEqual([]);
    request.flush(
      view([card(), card({ taskId: 'task-2', status: 'IN_PROGRESS', title: 'Đang làm' })]),
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.board-lane').length).toBe(10);
    expect(fixture.nativeElement.textContent).toContain('Việc cần làm');
    expect(fixture.nativeElement.textContent).toContain('Đang làm');
    expect(fixture.nativeElement.textContent).toContain('Nothing here');
    // No image markup, matching the task pages' escaping guarantees.
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
  });

  it('sends every applied filter and keeps assignee choices discovered from the cards', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    pending().flush(view([card({ assignedUserId: 'user-7', assignedUserName: 'Trần B' })]));
    fixture.detectChanges();
    expect(fixture.componentInstance.assigneeOptions()).toEqual([['user-7', 'Trần B']]);

    fixture.componentInstance.filters.setValue({
      search: ' gấp ',
      priority: 'HIGH',
      assignedUserId: 'user-7',
      assignedDepartmentId: '',
      deadlineFrom: '2026-11-01',
      deadlineTo: '2026-11-30',
      labelId: 'label-3',
    });
    fixture.componentInstance.apply();

    const filtered = pending();
    expect(filtered.request.params.get('search')).toBe('gấp');
    expect(filtered.request.params.get('priority')).toBe('HIGH');
    expect(filtered.request.params.get('assignedUserId')).toBe('user-7');
    expect(filtered.request.params.get('deadlineFrom')).toBe('2026-11-01');
    expect(filtered.request.params.get('deadlineTo')).toBe('2026-11-30');
    expect(filtered.request.params.get('labelId')).toBe('label-3');
    expect(filtered.request.params.has('assignedDepartmentId')).toBe(false);
    filtered.flush(view([]));

    // A narrowed board must not drop a choice the user can still select.
    expect(fixture.componentInstance.assigneeOptions()).toEqual([['user-7', 'Trần B']]);
  });

  it('replays the existing lifecycle dialog for each transition the server already allows', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    pending().flush(view([]));

    const cases: [BoardCard['status'], string][] = [
      ['DRAFT', 'ASSIGNED'],
      ['REJECTED', 'ASSIGNED'],
      ['ASSIGNED', 'ACCEPTED'],
      ['ACCEPTED', 'IN_PROGRESS'],
      ['OVERDUE', 'IN_PROGRESS'],
      ['IN_PROGRESS', 'SUBMITTED'],
      ['SUBMITTED', 'CONFIRMED'],
      ['SUBMITTED', 'IN_PROGRESS'],
    ];
    for (const [from, to] of cases) {
      expect(fixture.componentInstance.canMove(from, to)).toBe(true);
      fixture.componentInstance.drop(dropEvent(card({ status: from })), to);
      // Each accepted drop re-reads the board once the dialog closes.
      pending().flush(view([]));
    }
    expect(opened.length).toBe(cases.length);
    expect(TestBed.inject(ToastService).toasts()).toEqual([]);
  });

  it('refuses a transition the lifecycle does not permit, leaving the card and board untouched', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    pending().flush(view([card()]));

    for (const [from, to] of [
      ['ASSIGNED', 'COMPLETED'],
      ['DRAFT', 'IN_PROGRESS'],
      ['IN_PROGRESS', 'CONFIRMED'],
      ['COMPLETED', 'IN_PROGRESS'],
      ['ASSIGNED', 'CANCELLED'],
      ['ASSIGNED', 'REJECTED'],
      ['CONFIRMED', 'COMPLETED'],
      ['IN_PROGRESS', 'OVERDUE'],
    ] as [BoardCard['status'], string][]) {
      expect(fixture.componentInstance.canMove(from, to)).toBe(false);
      fixture.componentInstance.drop(dropEvent(card({ status: from })), to);
    }

    expect(opened).toEqual([]);
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).toContain('trả về vị trí cũ');
  });

  it('refuses a permitted transition the caller lacks the permission for', () => {
    permissions = [];
    const fixture = TestBed.createComponent(TaskBoard);
    pending().flush(view([card()]));

    fixture.componentInstance.drop(dropEvent(card({ status: 'ASSIGNED' })), 'ACCEPTED');

    expect(opened).toEqual([]);
    expect(TestBed.inject(ToastService).toasts()[0].message).toContain('không có quyền');
  });

  it('ignores a reorder inside the same lane without calling anything', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    pending().flush(view([card()]));

    const sameLane = {
      previousContainer: { id: 'lane-ASSIGNED' },
      container: { id: 'lane-ASSIGNED' },
      item: { data: card() },
    } as never;
    (sameLane as { container: unknown }).container = (
      sameLane as unknown as { previousContainer: unknown }
    ).previousContainer;
    fixture.componentInstance.drop(sameLane, 'ASSIGNED');

    expect(opened).toEqual([]);
    expect(TestBed.inject(ToastService).toasts()).toEqual([]);
  });

  it('separates a disabled feature from a denial and from a transient failure', () => {
    const fixture = TestBed.createComponent(TaskBoard);
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
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
  });

  it('cancels an outdated board read when the view is destroyed', () => {
    const fixture = TestBed.createComponent(TaskBoard);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
