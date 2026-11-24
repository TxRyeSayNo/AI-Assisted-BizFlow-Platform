import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChartSlice, ReportChart } from './report-chart';

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
    RouterLink,
    TranslatePipe,
    ReportChart,
  ],
  templateUrl: './reports.html',
  styleUrls: ['../organization/departments.scss', '../requests/requests.scss', './reports.scss'],
})
export class Reports implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
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

  // -------------------------------------------------------------------------------------------
  // F5, additive: downloads and charts. Both are inert unless the ReportExport feature is on, so
  // a deployment with the flag off renders this page exactly as it did before.
  // -------------------------------------------------------------------------------------------
  readonly features = inject(FeatureFlagsService);
  readonly exporting = signal(false);

  /** The tabs that have a download endpoint; the manager dashboard has none. */
  canExport(): boolean {
    return this.features.reportExport() && this.activeTab() !== 'manager';
  }

  /** Chart series for the tab on screen, derived from data already loaded. */
  readonly chartSeries = computed<
    { title: string; kind: 'bar' | 'line' | 'pie'; data: ChartSlice[] }[]
  >(() => {
    switch (this.activeTab()) {
      case 'company': {
        const report = this.companyData();
        if (!report) return [];
        return [
          {
            title: this.i18n.t('reports.chartDeptTasks', 'Nhiệm vụ theo phòng ban'),
            kind: 'bar',
            data: report.departmentMetrics.map((d) => ({
              label: d.departmentName,
              value: d.totalTasks,
            })),
          },
          {
            title: this.i18n.t('reports.chartCompletion', 'Hoàn thành so với tổng'),
            kind: 'pie',
            data: [
              {
                label: this.i18n.t('reports.chartCompleted', 'Hoàn thành'),
                value: report.totalTasksCompleted,
              },
              {
                label: this.i18n.t('reports.chartRemaining', 'Còn lại'),
                value: Math.max(0, report.totalTasksCreated - report.totalTasksCompleted),
              },
            ],
          },
        ];
      }
      case 'workload': {
        const report = this.workloadData();
        if (!report) return [];
        return [
          {
            title: this.i18n.t('reports.chartActiveByPerson', 'Đang làm theo nhân sự'),
            kind: 'bar',
            data: report.items.map((i) => ({ label: i.fullName, value: i.activeTasks })),
          },
          {
            title: this.i18n.t('reports.chartAvgPerMember', 'Trung bình mỗi thành viên'),
            kind: 'line',
            data: report.departmentBreakdown.map((d) => ({
              label: d.departmentName,
              value: Math.round(d.avgTasksPerMember * 10) / 10,
            })),
          },
        ];
      }
      case 'sla': {
        const report = this.slaData();
        if (!report) return [];
        return [
          {
            title: this.i18n.t('reports.chartCompliance', 'Tuân thủ SLA'),
            kind: 'pie',
            data: [
              {
                label: this.i18n.t('reports.chartCompliant', 'Đúng hạn'),
                value: report.compliantCount,
              },
              {
                label: this.i18n.t('reports.chartBreached', 'Vi phạm'),
                value: report.breachedCount,
              },
            ],
          },
          {
            title: this.i18n.t('reports.chartByService', 'Yêu cầu theo dịch vụ'),
            kind: 'bar',
            data: report.serviceBreakdown.map((s) => ({
              label: s.serviceName,
              value: s.totalRequests,
            })),
          },
        ];
      }
      default: {
        const report = this.managerData();
        if (!report) return [];
        return [
          {
            title: this.i18n.t('reports.chartTasksByStatus', 'Nhiệm vụ theo trạng thái'),
            kind: 'bar',
            data: Object.entries(report.taskMetrics.byStatus).map(([label, value]) => ({
              label,
              value,
            })),
          },
        ];
      }
    }
  });

  /** Downloads the report on screen, reusing the filters already applied. */
  exportCurrent(format: 'xlsx' | 'csv' | 'pdf'): void {
    if (this.exporting() || !this.canExport()) return;
    const tab = this.activeTab();
    const raw = this.filters.getRawValue();
    let params = new HttpParams().set('format', format);
    if (raw.from) params = params.set('from', new Date(raw.from).toISOString());
    if (raw.to) params = params.set('to', new Date(raw.to).toISOString());
    if (tab === 'workload' && raw.departmentId)
      params = params.set('departmentId', raw.departmentId);
    this.exporting.set(true);
    this.http
      .get(`/api/v1/reports/${tab}/export`, { params, observe: 'response', responseType: 'blob' })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (response) => {
          this.exporting.set(false);
          this.save(
            response.body,
            this.fileNameOf(response.headers.get('content-disposition'), tab, format),
          );
        },
        error: () => {
          this.exporting.set(false);
          this.error.set(
            this.i18n.t('reports.errExport', 'Không xuất được báo cáo. Vui lòng thử lại.'),
          );
        },
      });
  }

  private fileNameOf(disposition: string | null, tab: string, format: string): string {
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition ?? '');
    return match ? decodeURIComponent(match[1]) : `${tab}-report.${format}`;
  }

  private save(blob: Blob | null, fileName: string): void {
    if (!blob) return;
    try {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      // A host without object URLs (or a headless test) simply gets no download prompt.
    }
  }

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
            this.error.set(
              err?.error?.message ??
                this.i18n.t('reports.errManager', 'Failed to load manager dashboard.'),
            );
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
            this.error.set(
              err?.error?.message ??
                this.i18n.t('reports.errCompany', 'Failed to load company report.'),
            );
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
            this.error.set(
              err?.error?.message ??
                this.i18n.t('reports.errWorkload', 'Failed to load workload report.'),
            );
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
            this.error.set(
              err?.error?.message ?? this.i18n.t('reports.errSla', 'Failed to load SLA report.'),
            );
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
