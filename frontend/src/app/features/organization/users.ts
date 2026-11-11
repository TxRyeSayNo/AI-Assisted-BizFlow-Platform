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

export interface BatchImportUserRow {
  employeeCode: string;
  fullName: string;
  email: string;
  password?: string;
  departmentCode?: string;
  roleCode?: string;
}

export interface BatchImportValidationItem {
  rowIndex: number;
  employeeCode: string;
  email: string;
  fullName?: string;
  departmentCode?: string | null;
  role?: string | null;
  isValid: boolean;
  errors?: string[];
  activationToken?: string | null;
  errorMessage?: string | null;
}

export interface BatchImportUsersResult {
  totalProcessed: number;
  validCount: number;
  importedCount: number;
  dryRun: boolean;
  items: BatchImportValidationItem[];
  totalRows: number;
  validRows: number;
  errorRows: number;
  createdCount: number;
  validationItems: BatchImportValidationItem[];
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
  private applied = { search: '', status: '' };
  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<UserPage | null>(null);
  readonly busy = signal(false);
  readonly actionBusy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly labels = { ACTIVE: 'Hoạt động', INACTIVE: 'Ngừng hoạt động', LOCKED: 'Tạm khóa' };

  // Demotion modal state
  readonly demoteModalOpen = signal(false);
  readonly demoteTargetUser = signal<UserRow | null>(null);
  readonly demoteResponsibilities = signal<UserResponsibilitiesView | null>(null);
  readonly selectedReplacementManagerId = signal<string>('');

  // Batch import state
  readonly batchModalOpen = signal(false);
  readonly batchInputText = signal('');
  readonly batchDryRunResult = signal<BatchImportUsersResult | null>(null);
  readonly batchExecuting = signal(false);
  readonly selectedImportFile = signal<File | null>(null);

  // Ownership transfer state
  readonly transferModalOpen = signal(false);
  readonly transferTargetUser = signal<UserRow | null>(null);

  get canManageRoles(): boolean {
    return (
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get canCreateUsers(): boolean {
    return (
      this.session.hasPermission('users.create') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get canImportUsers(): boolean {
    return (
      this.session.hasPermission('users.import') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get canUpdateUsers(): boolean {
    return (
      this.session.hasPermission('users.update') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get canUpdateStatus(): boolean {
    return (
      this.session.hasPermission('users.status.update') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get canAssignRoles(): boolean {
    return (
      this.session.hasPermission('users.roles.assign') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  get isCompanyAdmin(): boolean {
    return (
      this.session.hasPermission('roles.configure') ||
      false
    );
  }

  constructor() {
    this.load(1);
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

  openTransferOwnershipModal(user: UserRow): void {
    if (!this.isCompanyAdmin || this.actionBusy()) return;
    if (user.userId === this.identity()?.userId) {
      this.toast.error('Bạn không thể bàn giao quyền quản trị cho chính mình.');
      return;
    }
    this.transferTargetUser.set(user);
    this.transferModalOpen.set(true);
  }

  closeTransferOwnershipModal(): void {
    this.transferModalOpen.set(false);
    this.transferTargetUser.set(null);
  }

  confirmTransferOwnership(): void {
    const target = this.transferTargetUser();
    if (!target) return;
    this.actionBusy.set(true);
    this.http.post<UserRow>(`/api/v1/users/${target.userId}/transfer-ownership`, { targetUserId: target.userId })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.toast.success(`Đã bàn giao quyền Quản trị viên sang cho ${target.fullName}.`);
          this.closeTransferOwnershipModal();
          this.actionBusy.set(false);
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.actionBusy.set(false);
          const msg = err.error?.message || 'Không thể chuyển quyền quản trị viên.';
          this.toast.error(msg, 'Lỗi bàn giao Admin');
        }
      });
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

  openBatchImportModal(): void {
    this.batchModalOpen.set(true);
    this.batchDryRunResult.set(null);
    this.selectedImportFile.set(null);
    this.batchInputText.set(
      'Mã NV, Họ và Tên, Email, Mã Phòng Ban, Vai Trò\n' +
      'EMP010, Nguyen Van A, a.nguyen@bizflow.local, TECH, EMPLOYEE\n' +
      'EMP011, Tran Thi B, b.tran@bizflow.local, HR, EMPLOYEE\n' +
      'EMP012, Le Van C, c.le@bizflow.local, TECH, MANAGER'
    );
  }

  closeBatchImportModal(): void {
    this.batchModalOpen.set(false);
    this.batchDryRunResult.set(null);
    this.selectedImportFile.set(null);
  }

  downloadTemplate(format: 'xlsx' | 'csv' = 'xlsx'): void {
    window.open(`/api/v1/users/import-template?format=${format}`, '_blank');
  }

  downloadErrorReport(): void {
    const res = this.batchDryRunResult();
    const items = res?.items || res?.validationItems;
    if (!items || items.length === 0) return;
    this.http.post('/api/v1/users/import-errors', items, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'bizflow_import_errors.csv';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Không thể tải báo cáo lỗi.')
    });
  }

  parseCsvRows(text: string): BatchImportUserRow[] {
    const lines = text.split('\n').map((l) => l.trim()).filter((l) => l.length > 0);
    if (lines.length <= 1) return [];

    const startIndex = lines[0].toLowerCase().includes('email') || lines[0].toLowerCase().includes('mã') ? 1 : 0;
    const rows: BatchImportUserRow[] = [];

    for (let i = startIndex; i < lines.length; i++) {
      const parts = lines[i].split(',').map((p) => p.trim().replace(/^["']|["']$/g, ''));
      if (parts.length >= 3) {
        rows.push({
          employeeCode: parts[0] || `EMP${String(i).padStart(3, '0')}`,
          fullName: parts[1] || 'Nhân sự mới',
          email: parts[2] || '',
          departmentCode: parts[3] || undefined,
          roleCode: parts[4] || 'EMPLOYEE',
        });
      }
    }
    return rows;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    const file = input.files[0];
    this.selectedImportFile.set(file);

    if (file.name.endsWith('.xlsx') || file.name.endsWith('.xls')) {
      this.batchInputText.set(
        `[Đã chọn tệp Excel: ${file.name} (${(file.size / 1024).toFixed(1)} KB)]\nNhấn "Kiểm tra Hợp lệ" để hệ thống phân tích trực tiếp tệp Excel này.`
      );
    } else {
      const reader = new FileReader();
      reader.onload = (e) => {
        const text = (e.target?.result as string) || '';
        this.batchInputText.set(text);
      };
      reader.readAsText(file);
    }
  }

  private normalizeBatchResult(res: any): BatchImportUsersResult {
    const rawItems = res.items || res.validationItems || [];
    const items: BatchImportValidationItem[] = rawItems.map((it: any) => ({
      rowIndex: it.rowIndex,
      employeeCode: it.employeeCode,
      email: it.email,
      fullName: it.fullName,
      departmentCode: it.departmentCode,
      role: it.role,
      isValid: it.isValid,
      errors: it.errors || (it.errorMessage ? [it.errorMessage] : []),
      activationToken: it.activationToken,
      errorMessage: it.errors && it.errors.length > 0 ? it.errors.join(' | ') : it.errorMessage || null,
    }));

    const total = res.totalProcessed ?? res.totalRows ?? items.length;
    const valid = res.validCount ?? res.validRows ?? items.filter((x: any) => x.isValid).length;
    const imported = res.importedCount ?? res.createdCount ?? 0;

    return {
      totalProcessed: total,
      validCount: valid,
      importedCount: imported,
      dryRun: res.dryRun ?? false,
      items,
      totalRows: total,
      validRows: valid,
      errorRows: total - valid,
      createdCount: imported,
      validationItems: items,
    };
  }

  runDryRunValidation(): void {
    const file = this.selectedImportFile();
    if (file && (file.name.endsWith('.xlsx') || file.name.endsWith('.xls'))) {
      this.batchExecuting.set(true);
      const formData = new FormData();
      formData.append('file', file);
      formData.append('dryRun', 'true');

      this.http.post<any>('/api/v1/users/upload-import', formData).subscribe({
        next: (raw) => {
          this.batchExecuting.set(false);
          const res = this.normalizeBatchResult(raw);
          this.batchDryRunResult.set(res);
          if (res.errorRows > 0) {
            this.toast.warning(`Phát hiện ${res.errorRows} dòng dữ liệu không hợp lệ. Vui lòng kiểm tra bảng đối soát.`);
          } else {
            this.toast.success(`Hợp lệ 100% (${res.validRows}/${res.totalRows} nhân sự). Sẵn sàng nhập vào hệ thống.`);
          }
        },
        error: (err) => {
          this.batchExecuting.set(false);
          const msg = err?.error?.message || 'Không thể kiểm tra dữ liệu file Excel.';
          this.toast.error(msg);
        }
      });
      return;
    }

    const rows = this.parseCsvRows(this.batchInputText());
    if (rows.length === 0) {
      this.toast.error('Vui lòng nhập ít nhất 1 dòng dữ liệu nhân sự hợp lệ theo định dạng CSV/Excel.');
      return;
    }

    this.batchExecuting.set(true);
    this.http
      .post<any>('/api/v1/users/batch-import', {
        dryRun: true,
        users: rows.map(r => ({
          employeeCode: r.employeeCode,
          fullName: r.fullName,
          email: r.email,
          departmentCode: r.departmentCode,
          role: r.roleCode
        })),
      })
      .subscribe({
        next: (raw) => {
          this.batchExecuting.set(false);
          const res = this.normalizeBatchResult(raw);
          this.batchDryRunResult.set(res);
          if (res.errorRows > 0) {
            this.toast.warning(
              `Phát hiện ${res.errorRows} dòng dữ liệu không hợp lệ. Vui lòng kiểm tra bảng đối soát.`,
            );
          } else {
            this.toast.success(
              `Hợp lệ 100% (${res.validRows}/${res.totalRows} nhân sự). Bạn có thể xác nhận lưu vào hệ thống.`,
            );
          }
        },
        error: (err) => {
          this.batchExecuting.set(false);
          const msg = err?.error?.message || 'Không thể kiểm tra dữ liệu đối soát.';
          this.toast.error(msg);
        },
      });
  }

  commitBatchImport(): void {
    const file = this.selectedImportFile();
    if (file && (file.name.endsWith('.xlsx') || file.name.endsWith('.xls'))) {
      this.batchExecuting.set(true);
      const formData = new FormData();
      formData.append('file', file);
      formData.append('dryRun', 'false');

      this.http.post<any>('/api/v1/users/upload-import', formData).subscribe({
        next: (raw) => {
          this.batchExecuting.set(false);
          const res = this.normalizeBatchResult(raw);
          this.closeBatchImportModal();
          this.toast.success(`Nhập thành công ${res.createdCount} nhân sự mới vào hệ thống!`);
          this.load(1);
        },
        error: (err) => {
          this.batchExecuting.set(false);
          const msg = err?.error?.message || 'Không thể nhập dữ liệu từ file Excel.';
          this.toast.error(msg);
        }
      });
      return;
    }

    const rows = this.parseCsvRows(this.batchInputText());
    if (rows.length === 0) return;

    this.batchExecuting.set(true);
    this.http
      .post<any>('/api/v1/users/batch-import', {
        dryRun: false,
        users: rows.map(r => ({
          employeeCode: r.employeeCode,
          fullName: r.fullName,
          email: r.email,
          departmentCode: r.departmentCode,
          role: r.roleCode
        })),
      })
      .subscribe({
        next: (raw) => {
          this.batchExecuting.set(false);
          const res = this.normalizeBatchResult(raw);
          this.closeBatchImportModal();
          this.toast.success(`Nhập thành công ${res.createdCount} nhân sự mới vào hệ thống!`);
          this.load(1);
        },
        error: (err) => {
          this.batchExecuting.set(false);
          const msg = err?.error?.message || 'Không thể nhập hàng loạt nhân sự vào hệ thống.';
          this.toast.error(msg);
        },
      });
  }
}
