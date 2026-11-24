import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';

interface DirectoryItem {
  userId?: string;
  departmentId?: string;
  fullName?: string;
  name?: string;
  employeeCode?: string;
  code?: string;
  departmentName?: string;
  isManager?: boolean;
  roleName?: string;
}

interface DirectoryPage {
  items: DirectoryItem[];
  total: number;
}

export interface AssignmentTargetOption {
  id: string;
  label: string;
  type: 'USER' | 'DEPARTMENT';
  isManager?: boolean;
  departmentName?: string;
}

@Component({
  selector: 'bf-task-assignment',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './task-assignment.html',
  styleUrl: './task-assignment.scss',
})
export class TaskAssignmentEditor {
  readonly data = inject<{ taskId: string; title: string }>(MAT_DIALOG_DATA);
  readonly dialog = inject<MatDialogRef<TaskAssignmentEditor, boolean>>(MatDialogRef);
  readonly session = inject(SessionService);
  private readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private query?: Subscription;
  private key = crypto.randomUUID();
  private submitted?: object;
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly uncertain = signal(false);
  readonly terminal = signal(false);
  readonly error = signal('');
  readonly options = signal<AssignmentTargetOption[]>([]);
  readonly suggestedManagers = signal<AssignmentTargetOption[]>([]);
  readonly suggestedDepartments = signal<AssignmentTargetOption[]>([]);
  readonly page = signal(1);
  readonly total = signal(0);
  readonly form = inject(FormBuilder).nonNullable.group({
    targetType: ['USER'],
    search: [''],
    targetId: ['', Validators.required],
    note: [''],
  });

  isCompanyAdmin(): boolean {
    return this.session.hasPermission('roles.configure');
  }

  constructor() {
    if (this.isCompanyAdmin()) {
      this.form.controls.targetType.setValue('DEPARTMENT');
    }
    this.loadSuggestions();
    this.load(1);
  }

  private loadSuggestions(): void {
    // 1. Load active departments
    this.http
      .get<DirectoryPage>('/api/v1/departments', { params: new HttpParams().set('pageSize', 20).set('status', 'ACTIVE') })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.suggestedDepartments.set(
            (res.items || []).map((d) => ({
              id: d.departmentId!,
              label: `${d.name} (${d.code})`,
              type: 'DEPARTMENT',
            })),
          );
        },
      });

    // 2. Load managers
    this.http
      .get<DirectoryPage>('/api/v1/users', { params: new HttpParams().set('pageSize', 50).set('status', 'ACTIVE') })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          const currentUserId = this.session.identity()?.userId;
          const managers = (res.items || [])
            .filter((u) => u.userId !== currentUserId && (u.isManager || u.roleName === 'MANAGER'))
            .map((u) => ({
              id: u.userId!,
              label: `${u.fullName} · Trưởng phòng ${u.departmentName || 'Bộ phận'} (${u.employeeCode})`,
              type: 'USER' as const,
              isManager: true,
              departmentName: u.departmentName,
            }));
          this.suggestedManagers.set(managers);
        },
      });
  }

  selectSuggestion(targetType: 'USER' | 'DEPARTMENT', targetId: string): void {
    if (this.form.controls.targetType.value !== targetType) {
      this.form.controls.targetType.setValue(targetType);
      this.form.controls.targetId.setValue(targetId);
      this.load(1, targetId);
    } else {
      this.form.controls.targetId.setValue(targetId);
    }
  }

  load(page: number, preselectId?: string) {
    if (this.busy() || this.uncertain() || this.terminal()) return;
    this.query?.unsubscribe();
    this.loading.set(true);
    this.error.set('');
    this.page.set(page);
    const type = this.form.controls.targetType.value;
    const currentUserId = this.session.identity()?.userId;

    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', 50)
      .set('status', 'ACTIVE')
      .set('search', this.form.controls.search.value.trim());

    this.query = this.http
      .get<DirectoryPage>(type === 'USER' ? '/api/v1/users' : '/api/v1/departments', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          const rows = result.items || [];

          const mapped: AssignmentTargetOption[] = rows.map((row) => {
            if (type === 'USER') {
              const isMgr = row.isManager || row.roleName === 'MANAGER';
              const roleTitle = isMgr ? `⭐ Trưởng phòng ${row.departmentName || 'Bộ phận'}` : `👤 ${row.departmentName ? row.departmentName + ' · ' : ''}Nhân viên`;
              return {
                id: row.userId!,
                label: `${row.fullName} · ${roleTitle} (${row.employeeCode})`,
                type: 'USER',
                isManager: isMgr,
                departmentName: row.departmentName,
              };
            } else {
              return {
                id: row.departmentId!,
                label: `🏢 ${row.name} · Mã: ${row.code}`,
                type: 'DEPARTMENT',
              };
            }
          });

          // Sort so managers are at the top when selecting users
          if (type === 'USER') {
            mapped.sort((a, b) => {
              if (a.isManager && !b.isManager) return -1;
              if (!a.isManager && b.isManager) return 1;
              return a.label.localeCompare(b.label);
            });
          }

          this.options.set(mapped);
          this.total.set(result.total);
          this.loading.set(false);

          if (preselectId) {
            this.form.controls.targetId.setValue(preselectId);
          }
        },
        error: () => {
          this.loading.set(false);
          this.total.set(0);
          this.error.set(
            'The target directory is unavailable or you do not have directory access.',
          );
        },
      });
  }
  save() {
    if (
      this.busy() ||
      this.loading() ||
      this.terminal() ||
      !this.session.hasPermission('tasks.assign')
    )
      return;
    if (!this.uncertain()) {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return;
      }
      const value = this.form.getRawValue();
      this.submitted = {
        targetType: value.targetType,
        targetId: value.targetId,
        note: value.note || null,
      };
    }
    this.busy.set(true);
    this.dialog.disableClose = true;
    this.form.disable();
    this.error.set('');
    this.http
      .post<{ taskId: string; assignmentId: string; status: string }>(
        `/api/v1/tasks/${this.data.taskId}/assign`,
        this.submitted,
        { headers: { 'Idempotency-Key': this.key } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (
            result?.taskId !== this.data.taskId ||
            !result.assignmentId ||
            result.status !== 'ASSIGNED'
          ) {
            this.unknown();
            return;
          }
          this.terminal.set(true);
          this.dialog.close(true);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (
            !this.uncertain() &&
            (failure.status === 422 ||
              failure.status === 404 ||
              (failure.status === 403 && failure.error?.code === 'TASK.TARGET_OUT_OF_SCOPE'))
          ) {
            this.form.enable();
            this.key = crypto.randomUUID();
            this.error.set(
              failure.status === 403
                ? this.i18n.t(
                    'taskDialog.outOfScope',
                    'That target is outside your configured management scope. Choose another target or ask your administrator to review your scope.',
                  )
                : this.i18n.t(
                    'taskDialog.invalidTarget',
                    'The task or target is unavailable, or the assignment input is invalid. Review the selection.',
                  ),
            );
          } else if (failure.status === 409 || failure.status === 403 || failure.status === 401) {
            this.terminal.set(true);
            this.error.set(
              this.i18n.t(
                'taskDialog.assignConflict',
                'The task, permissions or saved submission changed. Close and review the current task before acting again.',
              ),
            );
          } else {
            this.unknown();
          }
        },
      });
  }
  close() {
    if (!this.busy()) this.dialog.close(this.uncertain() || this.terminal());
  }
  private unknown() {
    this.uncertain.set(true);
    this.error.set(
      this.i18n.t(
        'taskDialog.assignUncertain',
        'The outcome is uncertain. Retry the same submission safely, or close and review the current task.',
      ),
    );
  }
}
