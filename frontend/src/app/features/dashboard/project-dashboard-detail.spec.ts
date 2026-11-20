import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { ProjectDashboardDetailView } from './dashboard-model';
import { ProjectDashboardDetail } from './project-dashboard-detail';

const projectId = '019f8000-0000-7000-8000-000000000001';

function detail(overrides: Partial<ProjectDashboardDetailView> = {}): ProjectDashboardDetailView {
  return {
    project: {
      projectId,
      title: 'Dự án Alpha',
      status: 'IN_PROGRESS',
      priority: 'MEDIUM',
      deadline: '2026-12-01T00:00:00Z',
      ownerUserId: 'user-1',
      ownerName: 'Lê Hoàng Nam',
      ownerDepartmentId: null,
      ownerDepartmentName: 'Phòng Kỹ thuật',
      sizeCategory: 'SMALL',
      progressPercent: 36,
      workItemCount: 4,
      completedWorkItemCount: 1,
      cancelledWorkItemCount: 0,
      overdueWorkItemCount: 1,
      unassignedWorkItemCount: 2,
      participantCount: 2,
      createdAt: '2026-11-01T00:00:00Z',
      updatedAt: '2026-11-18T00:00:00Z',
    },
    workItems: [
      {
        taskId: 'task-1',
        title: 'Phần việc A1',
        status: 'IN_PROGRESS',
        priority: 'MEDIUM',
        deadline: '2026-11-25T00:00:00Z',
        weight: 2,
        progressPercent: 40,
        checklistTotal: 4,
        checklistDone: 2,
        assigneeUserId: 'user-2',
        assigneeName: 'Nguyễn Văn Nhân Viên',
        assigneeDepartmentId: null,
        assigneeDepartmentName: 'Phòng Kỹ thuật',
        isOverdue: false,
        updatedAt: '2026-11-18T00:00:00Z',
      },
    ],
    participants: [
      {
        userId: 'user-2',
        fullName: 'Nguyễn Văn Nhân Viên',
        employeeCode: 'EMP001',
        departmentId: null,
        departmentName: 'Phòng Kỹ thuật',
        assignedWorkItems: 1,
        completedWorkItems: 0,
        inProgressWorkItems: 1,
        overdueWorkItems: 0,
        responsibilityPercent: 25,
        contributionPercent: 27.6,
        currentWorkItemTitle: 'Phần việc A1',
        isSelf: true,
      },
    ],
    canSeeAllParticipants: true,
    hiddenWorkItemCount: 0,
    hiddenParticipantCount: 0,
    myResponsibilityPercent: 25,
    myContributionPercent: 27.6,
    generatedAt: '2026-11-19T08:00:00Z',
    ...overrides,
  };
}

describe('Project dashboard detail', () => {
  let fixture: ComponentFixture<ProjectDashboardDetail>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ProjectDashboardDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: { identity: () => ({ tenantId: 'tenant-1' }), hasPermission: () => true },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: projectId }) } },
        },
      ],
    });
    fixture = TestBed.createComponent(ProjectDashboardDetail);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('renders work items, responsibility split and project totals', () => {
    fixture.detectChanges();
    httpMock.expectOne(`/api/v1/dashboard/projects/${projectId}`).flush(detail());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Dự án Alpha');
    expect(text).toContain('36%');
    expect(text).toContain('Phần việc A1');
    expect(text).toContain('Nguyễn Văn Nhân Viên');
    expect(text).toContain('25%');
    expect(text).toContain('27.6%');
    expect(text).toContain('2/4');
    expect(text).toContain('Bạn');
  });

  it('tells a limited viewer that other rows are hidden while totals stay real', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`/api/v1/dashboard/projects/${projectId}`)
      .flush(
        detail({ canSeeAllParticipants: false, hiddenWorkItemCount: 2, hiddenParticipantCount: 3 }),
      );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Bạn chỉ xem được phần việc của mình');
    expect(text).toContain('Số thành viên ngoài phạm vi quyền xem của bạn');
    expect(text).toContain('36%');
  });

  it('shows an unassigned project without failing', () => {
    fixture.detectChanges();
    httpMock.expectOne(`/api/v1/dashboard/projects/${projectId}`).flush(
      detail({
        workItems: [],
        participants: [],
        myResponsibilityPercent: 0,
        myContributionPercent: 0,
      }),
    );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Không có phần việc nào trong phạm vi của bạn.');
    expect(text).toContain('Dự án này chưa phân công cho ai.');
  });

  it('reports a project outside the caller scope as unavailable', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`/api/v1/dashboard/projects/${projectId}`)
      .flush(
        { code: 'RESOURCE.NOT_FOUND', message: 'Không tìm thấy.' },
        { status: 404, statusText: 'Not Found' },
      );
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe('Không tìm thấy.');
    expect(fixture.componentInstance.data()).toBeNull();
  });
});
