import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export interface TaskMetricsView {
  totalTasks: number;
  activeTasks: number;
  completedTasks: number;
  overdueTasks: number;
  cancelledTasks: number;
  byStatus: Record<string, number>;
  byPriority: Record<string, number>;
}

export interface RequestMetricsView {
  totalRequests: number;
  pendingRequests: number;
  closedRequests: number;
  rejectedRequests: number;
  cancelledRequests: number;
  byStatus: Record<string, number>;
  byPriority: Record<string, number>;
}

export interface DepartmentWorkloadSummaryView {
  departmentId: string;
  departmentName: string;
  activeTasks: number;
  completedTasks: number;
  overdueTasks: number;
  totalRequests: number;
}

export interface ManagerDashboardResult {
  taskMetrics: TaskMetricsView;
  requestMetrics: RequestMetricsView;
  departmentSummaries: DepartmentWorkloadSummaryView[];
  generatedAt: string;
}

export interface CompanyDepartmentMetricView {
  departmentId: string;
  departmentName: string;
  totalTasks: number;
  completedTasks: number;
  totalRequests: number;
  closedRequests: number;
}

export interface CompanyReportResult {
  totalTasksCreated: number;
  totalTasksCompleted: number;
  taskCompletionRatePercent: number;
  averageTaskCompletionTimeHours: number | null;
  totalRequestsSubmitted: number;
  totalRequestsClosed: number;
  requestResolutionRatePercent: number;
  averageRequestResolutionTimeHours: number | null;
  departmentMetrics: CompanyDepartmentMetricView[];
  recentAuditActivityCount: number;
  generatedAt: string;
}

export interface UserWorkloadItemView {
  userId: string;
  fullName: string;
  employeeCode: string;
  departmentId: string | null;
  departmentName: string | null;
  activeTasks: number;
  completedTasks: number;
  overdueTasks: number;
  totalAssigned: number;
}

export interface DepartmentWorkloadItemView {
  departmentId: string;
  departmentName: string;
  activeTasks: number;
  totalMembers: number;
  avgTasksPerMember: number;
}

export interface WorkloadReportResult {
  items: UserWorkloadItemView[];
  departmentBreakdown: DepartmentWorkloadItemView[];
  generatedAt: string;
}

export interface ServiceSlaMetricView {
  serviceId: string;
  serviceName: string;
  totalRequests: number;
  compliantRequests: number;
  breachedRequests: number;
  complianceRatePercent: number;
}

export interface SlaPerformanceReportResult {
  totalTracked: number;
  compliantCount: number;
  breachedCount: number;
  complianceRatePercent: number;
  avgElapsedHours: number | null;
  serviceBreakdown: ServiceSlaMetricView[];
  generatedAt: string;
}

export interface DepartmentOption {
  id: string;
  name: string;
}

@Component({
  selector: 'bf-reports',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './reports.html',
  styleUrls: ['../organization/departments.scss', '../requests/requests.scss', './reports.scss'],
})
export class Reports implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly session = inject(SessionService);

  readonly activeTab = signal<'manager' | 'company' | 'workload' | 'sla'>('manager');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly managerData = signal<ManagerDashboardResult | null>(null);
  readonly companyData = signal<CompanyReportResult | null>(null);
  readonly workloadData = signal<WorkloadReportResult | null>(null);
  readonly slaData = signal<SlaPerformanceReportResult | null>(null);

  readonly departments = signal<DepartmentOption[]>([]);

  private activeSub?: Subscription;

  readonly filters = new FormGroup({
    departmentId: new FormControl('', { nonNullable: true }),
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });

  canViewManager(): boolean {
    return this.session.hasPermission('reports.manager');
  }

  canViewCompany(): boolean {
    return this.session.hasPermission('reports.company');
  }

  canViewWorkload(): boolean {
    return this.session.hasPermission('reports.workload');
  }

  canViewSla(): boolean {
    return this.session.hasPermission('reports.sla');
  }

  ngOnInit(): void {
    this.loadDepartments();

    // Default to the first authorized tab
    if (this.canViewManager()) {
      this.activeTab.set('manager');
    } else if (this.canViewCompany()) {
      this.activeTab.set('company');
    } else if (this.canViewWorkload()) {
      this.activeTab.set('workload');
    } else if (this.canViewSla()) {
      this.activeTab.set('sla');
    }

    this.loadCurrentTab();
  }

  selectTab(tab: 'manager' | 'company' | 'workload' | 'sla'): void {
    this.activeTab.set(tab);
    this.loadCurrentTab();
  }

  applyFilters(): void {
    this.loadCurrentTab();
  }

  resetFilters(): void {
    this.filters.reset({
      departmentId: '',
      from: '',
      to: '',
    });
    this.loadCurrentTab();
  }

  setPreset(days: number): void {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - days);

    this.filters.patchValue({
      from: from.toISOString().split('T')[0],
      to: to.toISOString().split('T')[0],
    });
    this.loadCurrentTab();
  }

  loadCurrentTab(): void {
    this.activeSub?.unsubscribe();
    this.busy.set(true);
    this.error.set(null);

    const raw = this.filters.getRawValue();
    let params = new HttpParams();

    if (raw.departmentId) {
      params = params.set('departmentId', raw.departmentId);
    }
    if (raw.from) {
      params = params.set('from', new Date(raw.from).toISOString());
    }
    if (raw.to) {
      const toDate = new Date(raw.to);
      toDate.setHours(23, 59, 59, 999);
      params = params.set('to', toDate.toISOString());
    }

    const tab = this.activeTab();

    if (tab === 'manager') {
      this.activeSub = this.http
        .get<ManagerDashboardResult>('/api/v1/dashboard/manager', { params })
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.managerData.set(res);
            this.busy.set(false);
          },
          error: (err) => {
            this.error.set(err?.error?.message ?? 'Failed to load manager dashboard.');
            this.busy.set(false);
          },
        });
    } else if (tab === 'company') {
      this.activeSub = this.http
        .get<CompanyReportResult>('/api/v1/reports/company', { params })
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.companyData.set(res);
            this.busy.set(false);
          },
          error: (err) => {
            this.error.set(err?.error?.message ?? 'Failed to load company report.');
            this.busy.set(false);
          },
        });
    } else if (tab === 'workload') {
      this.activeSub = this.http
        .get<WorkloadReportResult>('/api/v1/reports/workload', { params })
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.workloadData.set(res);
            this.busy.set(false);
          },
          error: (err) => {
            this.error.set(err?.error?.message ?? 'Failed to load workload report.');
            this.busy.set(false);
          },
        });
    } else if (tab === 'sla') {
      this.activeSub = this.http
        .get<SlaPerformanceReportResult>('/api/v1/reports/sla', { params })
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.slaData.set(res);
            this.busy.set(false);
          },
          error: (err) => {
            this.error.set(err?.error?.message ?? 'Failed to load SLA report.');
            this.busy.set(false);
          },
        });
    }
  }

  private loadDepartments(): void {
    this.http
      .get<{ items?: DepartmentOption[] } | DepartmentOption[]>('/api/v1/departments')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          if (Array.isArray(res)) {
            this.departments.set(res);
          } else if (res && Array.isArray(res.items)) {
            this.departments.set(res.items);
          }
        },
        error: () => {
          // Non-blocking fallback
        },
      });
  }
}
