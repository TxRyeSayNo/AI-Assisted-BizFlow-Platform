import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TaskDetail } from './task-detail';

describe('scoped task detail', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'task-1' })) } },
        {
          provide: SessionService,
          useValue: {
            hasPermission: (perm: string) => perm === 'records.archive' || perm.startsWith('tasks.'),
            identity: () => ({ userId: 'u1', tenantId: 't1' }),
          },
        },
      ],
    }),
  );
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((r) => r.url === '/api/v1/comments').forEach((r) => r.flush([]));
    http.match((r) => r.url === '/api/v1/attachments').forEach((r) => r.flush([]));
    http.verify();
  });
  it('renders nonrevealing unavailable errors and clears stale data during reload', () => {
    const fixture = TestBed.createComponent(TaskDetail);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne((r) => r.url === '/api/v1/tasks/task-1')
      .flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('unavailable or outside your access scope');
    fixture.componentInstance.page('reports', 2);
    const request = http.expectOne((r) => r.url === '/api/v1/tasks/task-1');
    expect(request.request.params.get('reportPage')).toBe('2');
    expect(request.request.params.get('checklistPage')).toBe('1');
    request.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(fixture.componentInstance.data()).toBeNull();
  });

  it('archives completed task when authorized and confirmed', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const fixture = TestBed.createComponent(TaskDetail);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne((r) => r.url === '/api/v1/tasks/task-1')
      .flush({
        task: {
          taskId: 'task-1',
          title: 'Quarterly compliance audit',
          status: 'COMPLETED',
          priority: 'HIGH',
          createdAt: '2026-10-07T12:00:00Z',
        },
        checklist: { items: [], page: 1, pageSize: 20, total: 0 },
        reports: { items: [], page: 1, pageSize: 20, total: 0 },
        results: { items: [], page: 1, pageSize: 20, total: 0 },
      });
    fixture.detectChanges();

    expect(fixture.componentInstance.canArchive()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Archive task');

    fixture.componentInstance.archive();

    const archiveReq = http.expectOne('/api/v1/records/task/task-1/archive');
    expect(archiveReq.request.method).toBe('POST');
    archiveReq.flush({
      recordId: 'task-1',
      recordType: 'TASK',
      status: 'COMPLETED',
      archivedAt: '2026-10-07T14:00:00Z',
      message: 'Task archived successfully.',
    });

    http.expectOne((r) => r.url === '/api/v1/tasks/task-1').flush({
      task: {
        taskId: 'task-1',
        title: 'Quarterly compliance audit',
        status: 'COMPLETED',
        priority: 'HIGH',
        createdAt: '2026-10-07T12:00:00Z',
      },
      checklist: { items: [], page: 1, pageSize: 20, total: 0 },
      reports: { items: [], page: 1, pageSize: 20, total: 0 },
      results: { items: [], page: 1, pageSize: 20, total: 0 },
    });
  });
});
