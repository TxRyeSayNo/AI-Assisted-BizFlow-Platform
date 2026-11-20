import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface DepartmentRow {
  departmentId: string;
  code: string;
  name: string;
  parentDepartmentId: string | null;
  status: 'ACTIVE' | 'INACTIVE';
  createdAt: string;
}
interface DepartmentPage {
  items: DepartmentRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-departments',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  templateUrl: './departments.html',
  styleUrl: './departments.scss',
})
export class Departments {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly session = inject(SessionService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private request?: Subscription;
  private applied = { search: '', status: '' };
  readonly identity = this.session.identity;
  readonly filters = this.fb.nonNullable.group(this.applied);
  readonly result = signal<DepartmentPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly labels = { ACTIVE: 'Active', INACTIVE: 'Inactive' };

  // Create-department modal state.
  readonly createOpen = signal(false);
  readonly creating = signal(false);
  readonly allDepartments = signal<DepartmentRow[]>([]);
  readonly createForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    parentDepartmentId: [''],
  });

  get canManage(): boolean {
    return (
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('users.manage') ||
      false
    );
  }

  constructor() {
    this.load(1);
    this.loadAllDepartments();
  }

  loadAllDepartments(): void {
    this.http
      .get<DepartmentPage>('/api/v1/departments?pageSize=200')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => this.allDepartments.set(data.items),
        error: () => this.allDepartments.set([]),
      });
  }

  openCreate(): void {
    this.createForm.reset({ code: '', name: '', parentDepartmentId: '' });
    this.createOpen.set(true);
  }

  closeCreate(): void {
    this.createOpen.set(false);
  }

  submitCreate(): void {
    if (this.createForm.invalid || this.creating()) {
      this.createForm.markAllAsTouched();
      return;
    }
    const v = this.createForm.getRawValue();
    this.creating.set(true);
    this.http
      .post('/api/v1/departments', {
        code: v.code.trim(),
        name: v.name.trim(),
        parentDepartmentId: v.parentDepartmentId || null,
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.toast.success('Đã tạo phòng ban mới.');
          this.closeCreate();
          this.load(1);
          this.loadAllDepartments();
        },
        error: (err: HttpErrorResponse) => {
          this.creating.set(false);
          this.toast.error(err?.error?.message || 'Không thể tạo phòng ban. Vui lòng thử lại.');
        },
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
    this.request = this.http
      .get<DepartmentPage>('/api/v1/departments', { params })
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
}
