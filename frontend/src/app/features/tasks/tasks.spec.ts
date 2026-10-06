import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Tasks } from './tasks';

describe('scoped task list', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/tasks');
  const empty = { items: [], total: 0, page: 1, pageSize: 25 };
  const row = {
    taskId: 'task',
    title: '<img src=x onerror=alert(1)>',
    status: 'IN_PROGRESS',
    priority: 'CRITICAL',
    deadline: null,
    requestId: null,
    assignedUserId: null,
    assignedDepartmentId: null,
  };

  it('loads only paging parameters and renders accessible empty and loading states', () => {
    const fixture = TestBed.createComponent(Tasks);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading tasks');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No tasks match your access scope');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    expect(buttons.find((b) => b.textContent?.includes('Next'))?.disabled).toBe(true);
  });

  it('escapes task text, distinguishes request linkage and clears stale data on denied refresh', () => {
    const fixture = TestBed.createComponent(Tasks);
    pending().flush({ ...empty, total: 1, items: [row] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    for (const text of [
      row.title,
      'Independent task',
      'Unassigned',
      'No deadline',
      'In progress',
      'Critical priority',
    ])
      expect(fixture.nativeElement.textContent).toContain(text);
    fixture.componentInstance.load(1);
    expect(fixture.componentInstance.result()).toBeNull();
    pending().flush({ message: 'private detail' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
  });

  it('keeps all applied filters across paging and cancels outdated requests', () => {
    const fixture = TestBed.createComponent(Tasks);
    const old = pending();
    fixture.componentInstance.filters.setValue({
      search: ' work ',
      status: 'OVERDUE',
      priority: 'HIGH',
    });
    fixture.componentInstance.apply();
    expect(old.cancelled).toBe(true);
    const filtered = pending();
    expect(filtered.request.params.get('search')).toBe('work');
    expect(filtered.request.params.get('status')).toBe('OVERDUE');
    expect(filtered.request.params.get('priority')).toBe('HIGH');
    filtered.flush({ ...empty, total: 30 });
    fixture.componentInstance.filters.setValue({ search: 'unapplied', status: '', priority: '' });
    fixture.componentInstance.load(2);
    const next = pending();
    expect(next.request.params.get('search')).toBe('work');
    expect(next.request.params.get('page')).toBe('2');
    next.flush(empty);
  });

  it('renders responsibility and dates without inventing lifecycle changes', () => {
    const fixture = TestBed.createComponent(Tasks);
    pending().flush({
      ...empty,
      total: 1,
      items: [
        {
          ...row,
          title: 'Queue task',
          requestId: 'request',
          assignedDepartmentId: 'dept',
          assignedDepartmentName: 'Operations',
          deadline: '2026-10-05T10:00:00Z',
        },
      ],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Department queue');
    expect(fixture.nativeElement.textContent).toContain('Operations');
    expect(fixture.nativeElement.textContent).toContain('Linked to a request');
    expect(fixture.nativeElement.querySelector('time').getAttribute('datetime')).toBe(
      '2026-10-05T10:00:00Z',
    );
    expect(fixture.nativeElement.textContent).toContain('In progress');
  });

  it('supports safe retry and cancels retrieval on destruction', () => {
    const fixture = TestBed.createComponent(Tasks);
    pending().flush({ message: 'private' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tasks could not be loaded');
    expect(fixture.nativeElement.textContent).not.toContain('private');
    fixture.componentInstance.load(1);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
