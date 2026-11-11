import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

export interface UserRow {
  userId: string;
  employeeCode: string;
  fullName: string;
  email: string;
  departmentId: string | null;
  departmentName: string | null;
  status: 'ACTIVE' | 'INACTIVE' | 'LOCKED';
  isManager?: boolean;
  roleName?: string;
}

export interface UserPage {
  items: UserRow[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DepartmentOption {
  id: string;
  name: string;
}

export interface ReplacementManagerDto {
  userId: string;
  fullName: string;
  employeeCode: string;
}

export interface UserResponsibilitiesView {
  userId: string;
  fullName: string;
  employeeCode: string;
  departmentId?: string | null;
  departmentName?: string | null;
  isManager: boolean;
  isSoleDepartmentManager: boolean;
  activeMainTasksCount: number;
  activeChildTasksCount: number;
  pendingApprovalsCount: number;
  eligibleReplacementManagers: ReplacementManagerDto[];
}

@Component({
  selector: 'bf-users',
  standalone: true,
  imports: [
    RouterLink,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  templateUrl: './users.html',
  styleUrls: ['./departments.scss', './users.scss'],
})
export class Users {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private request?: Subscription;
  private applied = { search: '', status: '', departmentId: '' };
  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<UserPage | null>(null);
  readonly busy = signal(false);
  readonly actionBusy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly departments = signal<DepartmentOption[]>([]);
  readonly labels = { ACTIVE: 'Hoạt động', INACTIVE: 'Ngừng hoạt động', LOCKED: 'Tạm khóa' };

  // Demotion modal state
  readonly demoteModalOpen = signal(false);
  readonly demoteTargetUser = signal<UserRow | null>(null);
  readonly demoteResponsibilities = signal<UserResponsibilitiesView | null>(null);
  readonly selectedReplacementManagerId = signal<string>('');

  get canManageRoles(): boolean {
    return (
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  constructor() {
    this.loadDepartments();
    this.load(1);
  }

  loadDepartments() {
    this.http.get<{ items: { departmentId: string; name: string }[] }>('/api/v1/departments?pageSize=100')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          if (res?.items) {
            this.departments.set(res.items.map(d => ({ id: d.departmentId, name: d.name })));
          }
        },
        error: () => {}
      });
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load(1);
  }

  load(page: number) {
    this.request?.unsubscribe();
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (this.applied.status) params = params.set('status', this.applied.status);
    if (this.applied.search.trim()) params = params.set('search', this.applied.search.trim());
    if (this.applied.departmentId) params = params.set('departmentId', this.applied.departmentId);
    this.request = this.http
      .get<UserPage>('/api/v1/users', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
          this.busy.set(false);
        },
      });
  }

  promoteToManager(user: UserRow): void {
    if (!this.canManageRoles || this.actionBusy()) return;
    if (user.userId === this.identity()?.userId) {
      this.toast.error('Bạn không thể tự thay đổi vai trò của chính mình.');
      return;
    }

    if (!confirm(`Xác nhận thăng cấp nhân sự "${user.fullName}" (${user.employeeCode}) lên vai trò Quản lý / Trưởng phòng?`)) {
      return;
    }

    this.actionBusy.set(true);
    this.http.post<any>(`/api/v1/users/${user.userId}/promote-manager`, {}).subscribe({
      next: () => {
        this.actionBusy.set(false);
        this.toast.success(`Đã thăng cấp thành công "${user.fullName}" lên vai trò Quản lý.`);
        this.load(this.page());
      },
      error: (err) => {
        this.actionBusy.set(false);
        const msg = err.error?.message || 'Không thể thăng cấp nhân sự. Vui lòng kiểm tra lại.';
        this.toast.error(msg, 'Lỗi thăng cấp');
      },
    });
  }

  openDemoteModal(user: UserRow): void {
    if (!this.canManageRoles || this.actionBusy()) return;
    if (user.userId === this.identity()?.userId) {
      this.toast.error('Bạn không thể tự hạ cấp tài khoản của chính mình.');
      return;
    }

    this.demoteTargetUser.set(user);
    this.demoteResponsibilities.set(null);
    this.selectedReplacementManagerId.set('');
    this.demoteModalOpen.set(true);
    this.actionBusy.set(true);

    this.http.get<UserResponsibilitiesView>(`/api/v1/users/${user.userId}/responsibilities`).subscribe({
      next: (res) => {
        this.actionBusy.set(false);
        this.demoteResponsibilities.set(res);
        if (res.eligibleReplacementManagers?.length > 0) {
          this.selectedReplacementManagerId.set(res.eligibleReplacementManagers[0].userId);
        }
      },
      error: (err) => {
        this.actionBusy.set(false);
        const msg = err.error?.message || 'Không thể kiểm tra trách nhiệm quản lý.';
        this.toast.error(msg);
        this.closeDemoteModal();
      },
    });
  }

  closeDemoteModal(): void {
    this.demoteModalOpen.set(false);
    this.demoteTargetUser.set(null);
    this.demoteResponsibilities.set(null);
    this.selectedReplacementManagerId.set('');
  }

  executeDemote(): void {
    const user = this.demoteTargetUser();
    const resp = this.demoteResponsibilities();
    if (!user || this.actionBusy()) return;

    if (resp && resp.activeMainTasksCount > 0 && !this.selectedReplacementManagerId()) {
      this.toast.error('Vui lòng chọn Quản lý thay thế để tiếp nhận các công việc chính đang phụ trách.');
      return;
    }

    this.actionBusy.set(true);
    const payload = {
      transferToUserId: this.selectedReplacementManagerId() || null,
    };

    this.http.post<any>(`/api/v1/users/${user.userId}/demote-employee`, payload).subscribe({
      next: () => {
        this.actionBusy.set(false);
        this.closeDemoteModal();
        this.toast.success(`Đã hạ cấp thành công "${user.fullName}" về vai trò Nhân viên.`);
        this.load(this.page());
      },
      error: (err) => {
        this.actionBusy.set(false);
        const msg = err.error?.message || 'Không thể hạ cấp người dùng.';
        this.toast.error(msg, 'Lỗi hạ cấp');
      },
    });
  }
}
