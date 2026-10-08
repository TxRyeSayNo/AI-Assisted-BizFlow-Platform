import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import {
  CompanyReportResult,
  ManagerDashboardResult,
  Reports,
  SlaPerformanceReportResult,
  WorkloadReportResult,
} from './reports';

describe('Reports and Dashboard component', () => {
  let fixture: ComponentFixture<Reports>;
  let component: Reports;
  let httpMock: HttpTestingController;

  const mockManagerData: ManagerDashboardResult = {
    taskMetrics: {
      totalTasks: 12,
      activeTasks: 6,
      completedTasks: 4,
      overdueTasks: 1,
      cancelledTasks: 1,
      byStatus: { IN_PROGRESS: 4, COMPLETED: 4 },
      byPriority: { HIGH: 5, MEDIUM: 7 },
    },
    requestMetrics: {
      totalRequests: 10,
      pendingRequests: 5,
      closedRequests: 3,
      rejectedRequests: 1,
      cancelledRequests: 1,
      byStatus: { SUBMITTED: 2, CLOSED: 3 },
      byPriority: { HIGH: 4 },
    },
    departmentSummaries: [
      {
        departmentId: '019f7f8a-0000-7000-8000-000000000001',
        departmentName: 'Engineering',
        activeTasks: 4,
        completedTasks: 2,
        overdueTasks: 1,
        totalRequests: 5,
      },
    ],
    generatedAt: '2026-10-07T12:00:00Z',
  };

  const mockCompanyData: CompanyReportResult = {
    totalTasksCreated: 100,
    totalTasksCompleted: 80,
    taskCompletionRatePercent: 80.0,
    averageTaskCompletionTimeHours: 12.5,
    totalRequestsSubmitted: 50,
    totalRequestsClosed: 45,
    requestResolutionRatePercent: 90.0,
    averageRequestResolutionTimeHours: 24.0,
    departmentMetrics: [
      {
        departmentId: '019f7f8a-0000-7000-8000-000000000001',
        departmentName: 'Engineering',
        totalTasks: 60,
        completedTasks: 50,
        totalRequests: 30,
        closedRequests: 28,
      },
    ],
    recentAuditActivityCount: 120,
    generatedAt: '2026-10-07T12:00:00Z',
  };

  const mockWorkloadData: WorkloadReportResult = {
    items: [
      {
        userId: '019f7f8a-0000-7000-8000-000000000010',
        fullName: 'Alice Engineer',
        employeeCode: 'EMP001',
        departmentId: '019f7f8a-0000-7000-8000-000000000001',
        departmentName: 'Engineering',
        activeTasks: 5,
        completedTasks: 10,
        overdueTasks: 0,
        totalAssigned: 15,
      },
    ],
    departmentBreakdown: [
      {
        departmentId: '019f7f8a-0000-7000-8000-000000000001',
        departmentName: 'Engineering',
        activeTasks: 5,
        totalMembers: 3,
        avgTasksPerMember: 1.7,
      },
    ],
    generatedAt: '2026-10-07T12:00:00Z',
  };

  const mockSlaData: SlaPerformanceReportResult = {
    totalTracked: 40,
    compliantCount: 38,
    breachedCount: 2,
    complianceRatePercent: 95.0,
    avgElapsedHours: 16.2,
    serviceBreakdown: [
      {
        serviceId: '019f7f8a-0000-7000-8000-000000000020',
        serviceName: 'Hardware Setup',
        totalRequests: 20,
        compliantRequests: 19,
        breachedRequests: 1,
        complianceRatePercent: 95.0,
      },
    ],
    generatedAt: '2026-10-07T12:00:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Reports],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantName: 'Acme Corp', tenantId: 'tenant-1' }),
            hasPermission: () => true,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Reports);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads and renders manager dashboard on init', () => {
    fixture.detectChanges();

    const deptReq = httpMock.expectOne('/api/v1/departments');
    deptReq.flush([]);

    const dashReq = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager');
    dashReq.flush(mockManagerData);

    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Dashboard & Reports');
    expect(el.textContent).toContain('Total Tasks');
    expect(el.textContent).toContain('12');
    expect(el.textContent).toContain('Active Tasks');
    expect(el.textContent).toContain('6');
    expect(el.textContent).toContain('Engineering');
  });

  it('switches to company report tab and loads company metrics', () => {
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/departments').flush([]);
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager').flush(mockManagerData);
    fixture.detectChanges();

    component.selectTab('company');
    fixture.detectChanges();

    const compReq = httpMock.expectOne((r) => r.url === '/api/v1/reports/company');
    compReq.flush(mockCompanyData);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Company Performance Overview');
    expect(el.textContent).toContain('80%');
    expect(el.textContent).toContain('90%');
    expect(el.textContent).toContain('12.5 hrs');
  });

  it('switches to workload report tab and loads user workload table', () => {
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/departments').flush([]);
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager').flush(mockManagerData);
    fixture.detectChanges();

    component.selectTab('workload');
    fixture.detectChanges();

    const wlReq = httpMock.expectOne((r) => r.url === '/api/v1/reports/workload');
    wlReq.flush(mockWorkloadData);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('User Workload Distribution');
    expect(el.textContent).toContain('Alice Engineer');
    expect(el.textContent).toContain('EMP001');
    expect(el.textContent).toContain('1.7');
  });

  it('switches to SLA performance tab and loads SLA compliance', () => {
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/departments').flush([]);
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager').flush(mockManagerData);
    fixture.detectChanges();

    component.selectTab('sla');
    fixture.detectChanges();

    const slaReq = httpMock.expectOne((r) => r.url === '/api/v1/reports/sla');
    slaReq.flush(mockSlaData);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('SLA Compliance Summary');
    expect(el.textContent).toContain('95%');
    expect(el.textContent).toContain('38');
    expect(el.textContent).toContain('Hardware Setup');
  });

  it('applies date preset and reloads current tab', () => {
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/departments').flush([]);
    httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager').flush(mockManagerData);
    fixture.detectChanges();

    component.setPreset(7);
    fixture.detectChanges();

    const reloadReq = httpMock.expectOne((r) => r.url === '/api/v1/dashboard/manager');
    expect(reloadReq.request.params.has('from')).toBe(true);
    expect(reloadReq.request.params.has('to')).toBe(true);
    reloadReq.flush(mockManagerData);
  });
});
