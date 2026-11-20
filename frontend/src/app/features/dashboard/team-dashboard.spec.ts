import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { TeamDashboardView } from './dashboard-model';
import { TeamDashboard } from './team-dashboard';

const departmentId = '019f8000-0000-7000-8000-000000000003';

function view(overrides: Partial<TeamDashboardView> = {}): TeamDashboardView {
  return {
    departments: [
      {
        departmentId,
        name: 'Phòng Kỹ thuật',
        parentDepartmentId: null,
        memberCount: 2,
        activeWorkItems: 3,
        overdueWorkItems: 1,
        avgProgressPercent: 55,
      },
    ],
    members: [
      {
        userId: 'user-1',
        fullName: 'Nguyễn Văn Nhân Viên',
        employeeCode: 'EMP001',
        departmentId,
        departmentName: 'Phòng Kỹ thuật',
        status: 'ACTIVE',
        totalAssigned: 3,
        activeWorkItems: 2,
        completedWorkItems: 1,
        overdueWorkItems: 1,
        avgProgressPercent: 60,
        responsibilitySharePercent: 75,
      },
      {
        userId: 'user-2',
        fullName: 'Đỗ Quang Huy',
        employeeCode: 'TECH002',
        departmentId,
        departmentName: 'Phòng Kỹ thuật',
        status: 'ACTIVE',
        totalAssigned: 1,
        activeWorkItems: 1,
        completedWorkItems: 0,
        overdueWorkItems: 0,
        avgProgressPercent: 10,
        responsibilitySharePercent: 25,
      },
    ],
    scopeDepartmentIds: [departmentId],
    tenantWide: false,
    truncated: false,
    memberLimit: 200,
    generatedAt: '2026-11-19T08:00:00Z',
    ...overrides,
  };
}

describe('Team dashboard', () => {
  let fixture: ComponentFixture<TeamDashboard>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamDashboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantId: 'tenant-1' }),
            hasPermission: (permission: string) => permission === 'tasks.read.managed',
          },
        },
      ],
    });
    fixture = TestBed.createComponent(TeamDashboard);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists subordinates with their load and responsibility share', () => {
    fixture.detectChanges();
    const request = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/team');
    expect(request.request.params.has('departmentId')).toBe(false);
    request.flush(view());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Quản lý cấp dưới');
    expect(text).toContain('Nguyễn Văn Nhân Viên');
    expect(text).toContain('EMP001');
    expect(text).toContain('75%');
    expect(text).toContain('25%');
    // The scope label lives inside a closed mat-select panel, so assert the state instead.
    expect(fixture.componentInstance.data()?.tenantWide).toBe(false);
    expect(fixture.componentInstance.data()?.scopeDepartmentIds).toEqual([departmentId]);
  });

  it('sends the chosen department and surfaces an out-of-scope refusal', () => {
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/team').flush(view());
    fixture.detectChanges();

    fixture.componentInstance.filters.setValue({ departmentId: 'someone-elses-department' });
    fixture.componentInstance.load();

    const request = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/team');
    expect(request.request.params.get('departmentId')).toBe('someone-elses-department');
    request.flush(
      {
        code: 'TASK.TARGET_OUT_OF_SCOPE',
        message: 'Phòng ban nằm ngoài phạm vi quản lý của bạn.',
      },
      { status: 403, statusText: 'Forbidden' },
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe('Phòng ban nằm ngoài phạm vi quản lý của bạn.');
  });

  it('explains an unconfigured management scope instead of showing nothing', () => {
    fixture.detectChanges();
    httpMock
      .expectOne((r) => r.url === '/api/v1/dashboard/team')
      .flush(view({ departments: [], members: [], scopeDepartmentIds: [] }));
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Bạn chưa được cấu hình phạm vi quản lý phòng ban nào');
    expect(text).toContain('Không có nhân sự nào trong phạm vi này.');
  });
});
