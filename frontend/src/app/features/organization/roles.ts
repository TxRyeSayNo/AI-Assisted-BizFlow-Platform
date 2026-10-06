import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export interface RoleRow {
  roleId: string;
  name: string;
  isSystem: boolean;
  status: 'ACTIVE' | 'INACTIVE';
  permissionIds: string[];
  eTag: string;
}
interface PermissionOption {
  permissionId: string;
  code: string;
  module: string;
  action: string;
  scope: string;
}
interface RolePage {
  items: RoleRow[];
  page: number;
  pageSize: number;
  total: number;
  availablePermissions: PermissionOption[];
}

@Component({
  selector: 'bf-roles',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './roles.html',
  styleUrls: ['./departments.scss', './roles.scss'],
})
export class Roles {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private appliedSearch = '';
  readonly identity = inject(SessionService).identity;
  readonly filters = inject(FormBuilder).nonNullable.group({ search: '' });
  readonly createForm = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
  });
  readonly result = signal<RolePage | null>(null);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly mutationError = signal('');
  readonly notice = signal('');
  readonly conflict = signal(false);
  readonly selected = signal<RoleRow | null>(null);
  readonly selection = signal<string[]>([]);
  readonly page = signal(1);
  readonly scopeLabels: Record<string, string> = {
    TENANT: 'Workspace',
    DEPARTMENT: 'Department',
    SELF: 'Own resources',
    ASSIGNED: 'Assigned resources',
  };

  constructor() {
    this.load(1);
  }

  apply() {
    this.appliedSearch = this.filters.getRawValue().search.trim();
    this.load(1);
  }

  load(page: number) {
    if (this.saving()) return;
    this.request?.unsubscribe();
    this.result.set(null);
    this.selected.set(null);
    this.selection.set([]);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.conflict.set(false);
    this.mutationError.set('');
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (this.appliedSearch) params = params.set('search', this.appliedSearch);
    this.request = this.http
      .get<RolePage>('/api/v1/roles', { params })
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

  select(role: RoleRow) {
    if (this.saving() || role.isSystem) return;
    this.selected.set(role);
    this.selection.set([...role.permissionIds]);
    this.conflict.set(false);
    this.mutationError.set('');
    this.notice.set('');
  }

  toggle(id: string, checked: boolean) {
    if (this.saving() || this.conflict()) return;
    this.selection.update((ids) =>
      checked ? [...new Set([...ids, id])] : ids.filter((value) => value !== id),
    );
  }

  create() {
    this.createForm.markAllAsTouched();
    if (this.busy() || this.saving() || this.createForm.invalid) return;
    const name = this.createForm.getRawValue().name.trim();
    if (!name) return;
    this.saving.set(true);
    this.mutationError.set('');
    this.notice.set('');
    this.http
      .post<RoleRow>('/api/v1/roles', { name })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.createForm.reset();
          this.filters.setValue({ search: name });
          this.appliedSearch = name;
          this.notice.set(
            'Custom role created with no permissions. Choose Configure to add permitted actions.',
          );
          this.load(1);
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  save() {
    const role = this.selected();
    if (!role || role.isSystem || this.saving() || this.busy() || this.conflict()) return;
    this.saving.set(true);
    this.mutationError.set('');
    this.notice.set('');
    this.http
      .put<RoleRow>(
        `/api/v1/roles/${role.roleId}/permissions`,
        { permissionIds: this.selection() },
        { headers: { 'If-Match': role.eTag } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.selected.set(updated);
          this.selection.set([...updated.permissionIds]);
          this.result.update((data) =>
            data
              ? {
                  ...data,
                  items: data.items.map((item) =>
                    item.roleId === updated.roleId ? updated : item,
                  ),
                }
              : null,
          );
          this.notice.set(
            'Permissions saved. They apply to assigned users on their next authorized operation.',
          );
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  private failed(failure: HttpErrorResponse) {
    this.saving.set(false);
    if (failure.status === 403) {
      this.denied.set(true);
      this.result.set(null);
      this.selected.set(null);
      this.selection.set([]);
      return;
    }
    if (failure.error?.code === 'ROLE.VERSION_CONFLICT') {
      this.conflict.set(true);
      this.mutationError.set(
        'This role changed while you were editing. Reload the directory and choose the role again before saving.',
      );
    } else if (failure.error?.code === 'ROLE.NAME_EXISTS') {
      this.mutationError.set('A role with this name already exists. Choose another name.');
    } else if (failure.error?.code === 'ROLE.PERMISSION_NOT_ALLOWED') {
      this.conflict.set(true);
      this.mutationError.set(
        'The allowed permission catalog changed. Reload the directory before saving.',
      );
    } else {
      this.mutationError.set(
        'The change could not be saved. Check your input or reload the directory and try again.',
      );
    }
  }
}
