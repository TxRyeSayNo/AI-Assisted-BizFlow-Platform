import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { ProjectDashboardPage, ProjectSummaryView } from './dashboard-model';
import { ProjectDashboard } from './project-dashboard';

function project(overrides: Partial<ProjectSummaryView> = {}): ProjectSummaryView {
  return {
    projectId: '019f8000-0000-7000-8000-000000000001',
    title: 'Dự án Alpha',
    status: 'IN_PROGRESS',
    priority: 'MEDIUM',
    deadline: '2026-12-01T00:00:00Z',
    ownerUserId: '019f8000-0000-7000-8000-000000000010',
    ownerName: 'Lê Hoàng Nam',
    ownerDepartmentId: null,
    ownerDepartmentName: 'Phòng Kỹ thuật',
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
    ...overrides,
  };
}

function page(items: ProjectSummaryView[]): ProjectDashboardPage {
  return { items, page: 1, pageSize: 25, total: items.length, generatedAt: '2026-11-19T08:00:00Z' };
}

describe('Project dashboard', () => {
  let fixture: ComponentFixture<ProjectDashboard>;
  let httpMock: HttpTestingController;

  function setup(permissions: string[]): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ProjectDashboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantId: 'tenant-1', tenantName: 'Acme' }),
            hasPermission: (permission: string) => permissions.includes(permission),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(ProjectDashboard);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('groups projects into large and small sections', () => {
    setup(['tasks.read.tenant']);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === '/api/v1/departments').flush({ items: [] });
    httpMock
      .expectOne((r) => r.url === '/api/v1/dashboard/projects')
      .flush(
        page([
          project(),
          project({
            projectId: '019f8000-0000-7000-8000-000000000002',
            title: 'Dự án Beta',
            sizeCategory: 'LARGE',
            workItemCount: 12,
            participantCount: 6,
            progressPercent: 70,
          }),
        ]),
      );
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.large().map((x) => x.title)).toEqual(['Dự án Beta']);
    expect(component.small().map((x) => x.title)).toEqual(['Dự án Alpha']);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Dự án lớn');
    expect(text).toContain('Dự án nhỏ');
    expect(text).toContain('36%');
    expect(text).toContain('Lê Hoàng Nam');
  });

  it('offers the department filter only to someone who can read beyond their own work', () => {
    setup(['tasks.read.managed', 'tasks.read.own']);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === '/api/v1/departments').flush({ items: [] });
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/projects').flush(page([]));
    fixture.detectChanges();

    expect(fixture.componentInstance.canFilterByDepartment()).toBe(true);
    expect(fixture.componentInstance.isPersonalScope()).toBe(false);
  });

  it('warns an employee that other people rows are hidden and skips the department lookup', () => {
    setup(['tasks.read.own', 'tasks.read.assigned']);
    fixture.detectChanges();

    // No department request is issued for a personal scope.
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/projects').flush(page([project()]));
    fixture.detectChanges();

    expect(fixture.componentInstance.isPersonalScope()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Bạn đang xem các dự án mà bạn tham gia');
    expect(text).not.toContain('Cấp dưới của tôi');
  });

  it('sends the normalized filters and renders the empty state', () => {
    setup(['tasks.read.tenant']);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/departments').flush({ items: [] });
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/projects').flush(page([]));
    fixture.detectChanges();

    fixture.componentInstance.filters.setValue({
      search: '  alpha  ',
      size: 'LARGE',
      departmentId: 'dept-1',
    });
    fixture.componentInstance.applyFilters();

    const request = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/projects');
    expect(request.request.params.get('search')).toBe('alpha');
    expect(request.request.params.get('size')).toBe('LARGE');
    expect(request.request.params.get('departmentId')).toBe('dept-1');
    request.flush(page([]));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Chưa có dự án nào');
  });

  it('surfaces a server error without clearing the page', () => {
    setup(['tasks.read.tenant']);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/departments').flush({ items: [] });
    httpMock
      .expectOne((r) => r.url === '/api/v1/dashboard/projects')
      .flush(
        { code: 'ACCESS.DENIED', message: 'Bạn không có quyền.' },
        { status: 403, statusText: 'Forbidden' },
      );
    fixture.detectChanges();

    expect(fixture.componentInstance.error()).toBe('Bạn không có quyền.');
    expect(fixture.componentInstance.busy()).toBe(false);
  });
});
