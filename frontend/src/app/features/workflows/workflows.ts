import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export interface WorkflowRow {
  workflowId: string;
  name: string;
  businessType: 'TASK' | 'REQUEST';
  status: 'DRAFT' | 'ACTIVE' | 'INACTIVE';
  latestVersionId: string | null;
  latestVersionNo: number | null;
  latestVersionStatus: string | null;
  versionCount?: number;
}

export interface VersionSummary {
  versionId: string;
  versionNo: number;
  status: 'Draft' | 'Published' | 'Retired';
  publishedAt: string | null;
  createdAt: string;
}

interface WorkflowPage {
  items: WorkflowRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-workflows',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    DatePipe,
  ],
  templateUrl: './workflows.html',
  styleUrls: ['../organization/departments.scss', './workflows.scss'],
})
export class Workflows {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', businessType: '', status: '' };
  
  readonly session = inject(SessionService);
  private readonly fb = inject(FormBuilder);

  readonly filters = this.fb.nonNullable.group(this.applied);
  readonly createForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    businessType: 'REQUEST',
  });

  readonly result = signal<WorkflowPage | null>(null);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly mutationError = signal('');
  readonly uncertain = signal(false);
  readonly page = signal(1);

  // Modals & UI states
  readonly showCreateModal = signal(false);
  readonly showHistoryModal = signal(false);
  readonly selectedWorkflowForHistory = signal<WorkflowRow | null>(null);
  readonly versionHistory = signal<VersionSummary[]>([]);
  readonly loadingHistory = signal(false);

  readonly deleteCandidate = signal<WorkflowRow | null>(null);
  readonly deleting = signal(false);

  readonly toast = signal<{ message: string; type: 'success' | 'error' } | null>(null);

  readonly labels: Record<string, string> = {
    TASK: 'Nhiệm vụ',
    REQUEST: 'Yêu cầu',
    DRAFT: 'Bản nháp',
    Draft: 'Bản nháp',
    ACTIVE: 'Đang hoạt động',
    INACTIVE: 'Tạm dừng',
    PUBLISHED: 'Đã ban hành',
    Published: 'Đã ban hành',
    RETIRED: 'Đã lưu trữ',
    Retired: 'Đã lưu trữ',
  };

  constructor() {
    this.load(1);
  }

  showToast(message: string, type: 'success' | 'error' = 'success') {
    this.toast.set({ message, type });
    setTimeout(() => {
      if (this.toast()?.message === message) {
        this.toast.set(null);
      }
    }, 4000);
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load(1);
  }

  resetFilters() {
    this.filters.reset({ search: '', businessType: '', status: '' });
    this.applied = { search: '', businessType: '', status: '' };
    this.load(1);
  }

  load(page: number) {
    if (this.saving()) return;
    this.request?.unsubscribe();
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    for (const [key, value] of Object.entries(this.applied)) {
      if (value.trim()) params = params.set(key, value.trim());
    }
    this.request = this.http
      .get<WorkflowPage>('/api/v1/workflows', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
          this.uncertain.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
          this.busy.set(false);
        },
      });
  }

  openCreateModal() {
    this.createForm.reset({ name: '', businessType: 'REQUEST' });
    this.showCreateModal.set(true);
  }

  closeCreateModal() {
    this.showCreateModal.set(false);
  }

  create() {
    this.createAndDesign();
  }

  createAndDesign() {
    this.createForm.markAllAsTouched();
    if (!this.session.hasPermission('workflows.configure') || this.createForm.invalid || this.saving()) {
      return;
    }
    const val = this.createForm.getRawValue();
    const name = val.name.trim();
    if (!name) return;

    this.saving.set(true);
    this.http
      .post<WorkflowRow>('/api/v1/workflows', { name, businessType: val.businessType })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.saving.set(false);
          this.showCreateModal.set(false);
          this.showToast('Khởi tạo quy trình bản thảo thành công!', 'success');
          if (row.latestVersionId) {
            this.router.navigate(['/settings/workflows', row.workflowId, 'versions', row.latestVersionId]);
          } else {
            this.load(1);
          }
        },
        error: (failure: HttpErrorResponse) => {
          this.saving.set(false);
          if (failure.status === 403) {
            this.denied.set(true);
            return;
          }
          this.showToast('Không thể tạo quy trình: ' + (failure.error?.error || failure.error?.title || 'Lỗi hệ thống'), 'error');
        },
      });
  }

  toggleStatus(row: WorkflowRow) {
    if (!this.session.hasPermission('workflows.configure')) return;
    const nextStatus = row.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE';
    this.http
      .patch(`/api/v1/workflows/${row.workflowId}/status`, { status: nextStatus })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.showToast(`Đã chuyển trạng thái quy trình sang "${this.labels[nextStatus]}".`, 'success');
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.showToast('Cập nhật trạng thái thất bại: ' + (err.error?.error || 'Có lỗi xảy ra'), 'error');
        },
      });
  }

  openHistory(row: WorkflowRow) {
    this.selectedWorkflowForHistory.set(row);
    this.showHistoryModal.set(true);
    this.loadingHistory.set(true);
    this.versionHistory.set([]);

    this.http
      .get<VersionSummary[]>(`/api/v1/workflows/${row.workflowId}/versions`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (versions) => {
          this.versionHistory.set(versions);
          this.loadingHistory.set(false);
        },
        error: () => {
          this.loadingHistory.set(false);
          this.showToast('Không thể tải danh sách phiên bản', 'error');
        },
      });
  }

  closeHistory() {
    this.showHistoryModal.set(false);
    this.selectedWorkflowForHistory.set(null);
  }

  confirmDelete(row: WorkflowRow) {
    this.deleteCandidate.set(row);
  }

  cancelDelete() {
    this.deleteCandidate.set(null);
  }

  executeDelete() {
    const candidate = this.deleteCandidate();
    if (!candidate || this.deleting()) return;

    this.deleting.set(true);
    this.http
      .delete(`/api/v1/workflows/${candidate.workflowId}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.deleteCandidate.set(null);
          this.showToast('Đã xóa quy trình bản thảo thành công.', 'success');
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.deleting.set(false);
          this.showToast('Không thể xóa quy trình: ' + (err.error?.error || 'Chỉ có thể xóa quy trình chưa có bản ban hành chính thức.'), 'error');
        },
      });
  }
}
