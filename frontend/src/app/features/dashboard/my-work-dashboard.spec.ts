import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { MyWorkDashboardView } from './dashboard-model';
import { MyWorkDashboard } from './my-work-dashboard';

function view(overrides: Partial<MyWorkDashboardView> = {}): MyWorkDashboardView {
  return {
    items: [
      {
        workItem: {
          taskId: 'task-1',
          title: 'Phần việc A1',
          status: 'IN_PROGRESS',
          priority: 'MEDIUM',
          deadline: '2026-11-25T00:00:00Z',
          weight: 2,
          progressPercent: 40,
          checklistTotal: 4,
          checklistDone: 2,
          assigneeUserId: 'user-1',
          assigneeName: null,
          assigneeDepartmentId: null,
          assigneeDepartmentName: null,
          isOverdue: false,
          updatedAt: '2026-11-18T00:00:00Z',
        },
        projectId: 'project-1',
        projectTitle: 'Dự án Alpha',
      },
    ],
    page: 1,
    pageSize: 25,
    total: 1,
    myProgressPercent: 40,
    activeWorkItems: 1,
    completedWorkItems: 0,
    overdueWorkItems: 0,
    pendingDepartmentClaims: 2,
    byStatus: { IN_PROGRESS: 1 },
    projects: [
      {
        projectId: 'project-1',
        title: 'Dự án Alpha',
        status: 'IN_PROGRESS',
        priority: 'MEDIUM',
        deadline: null,
        ownerUserId: null,
        ownerName: 'Lê Hoàng Nam',
        ownerDepartmentId: null,
        ownerDepartmentName: null,
        sizeCategory: 'SMALL',
        progressPercent: 36,
        workItemCount: 4,
        completedWorkItemCount: 1,
        cancelledWorkItemCount: 0,
        overdueWorkItemCount: 0,
        unassignedWorkItemCount: 2,
        participantCount: 2,
        createdAt: '2026-11-01T00:00:00Z',
        updatedAt: '2026-11-18T00:00:00Z',
      },
    ],
    generatedAt: '2026-11-19T08:00:00Z',
    ...overrides,
  };
}

describe('My work dashboard', () => {
  let fixture: ComponentFixture<MyWorkDashboard>;
  let httpMock: HttpTestingController;

  function setup(permissions: string[]): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [MyWorkDashboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantId: 'tenant-1' }),
            hasPermission: (permission: string) => permissions.includes(permission),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(MyWorkDashboard);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('never sends a user identifier and renders only the caller own figures', () => {
    setup(['tasks.read.own', 'tasks.read.assigned']);
    fixture.detectChanges();

    const request = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/my-work');
    expect(request.request.params.has('userId')).toBe(false);
    request.flush(view());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Bảng công việc của tôi');
    expect(text).toContain('40%');
    expect(text).toContain('Phần việc A1');
    expect(text).toContain('Dự án Alpha');
    expect(text).toContain('Chờ bạn nhận');
    // The team tab stays hidden for a personal scope.
    expect(text).not.toContain('Cấp dưới của tôi');
  });

  it('offers the team tab to a manager', () => {
    setup(['tasks.read.managed', 'tasks.read.own']);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/my-work').flush(view());
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Cấp dưới của tôi');
  });

  it('shows an empty state for someone with no assigned work', () => {
    setup(['tasks.read.own']);
    fixture.detectChanges();
    httpMock
      .expectOne((r) => r.url === '/api/v1/dashboard/my-work')
      .flush(
        view({
          items: [],
          total: 0,
          myProgressPercent: 0,
          activeWorkItems: 0,
          pendingDepartmentClaims: 0,
          byStatus: {},
          projects: [],
        }),
      );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Bạn chưa được giao phần việc nào.');
    expect(text).toContain('0%');
  });
});
