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

export interface WorkflowRow {
  workflowId: string;
  name: string;
  businessType: 'TASK' | 'REQUEST';
  status: 'DRAFT' | 'ACTIVE' | 'INACTIVE';
  latestVersionId: string | null;
  latestVersionNo: number | null;
  latestVersionStatus: string | null;
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
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './workflows.html',
  styleUrls: ['../organization/departments.scss', './workflows.scss'],
})
export class Workflows {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', businessType: '', status: '' };
  readonly session = inject(SessionService);
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly createForm = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    businessType: 'REQUEST',
  });
  readonly result = signal<WorkflowPage | null>(null);
  readonly created = signal<WorkflowRow | null>(null);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly mutationError = signal('');
  readonly uncertain = signal(false);
  readonly page = signal(1);
  readonly labels: Record<string, string> = {
    TASK: 'Task',
    REQUEST: 'Request',
    DRAFT: 'Draft',
    ACTIVE: 'Active',
    INACTIVE: 'Inactive',
    PUBLISHED: 'Published',
    RETIRED: 'Retired',
  };

  constructor() {
    this.load(1);
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load(1);
  }

  load(page: number) {
    if (this.saving()) return;
    this.request?.unsubscribe();
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    for (const [key, value] of Object.entries(this.applied))
      if (value.trim()) params = params.set(key, value.trim());
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
          this.created.set(null);
        },
      });
  }

  create() {
    this.createForm.markAllAsTouched();
    if (
      !this.session.hasPermission('workflows.configure') ||
      this.denied() ||
      this.busy() ||
      this.saving() ||
      this.uncertain() ||
      this.createForm.invalid
    )
      return;
    const value = this.createForm.getRawValue();
    const name = value.name.trim();
    if (!name) return;
    this.saving.set(true);
    this.mutationError.set('');
    this.created.set(null);
    this.http
      .post<WorkflowRow>('/api/v1/workflows', { name, businessType: value.businessType })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.saving.set(false);
          this.created.set(row);
          this.createForm.reset({ name: '', businessType: 'REQUEST' });
          this.applied = { search: name, businessType: '', status: '' };
          this.filters.setValue(this.applied);
          this.load(1);
        },
        error: (failure: HttpErrorResponse) => {
          this.saving.set(false);
          if (failure.status === 403) {
            this.denied.set(true);
            this.result.set(null);
            return;
          }
          this.uncertain.set(failure.status !== 422);
          this.mutationError.set(
            failure.status === 422
              ? 'Check the workflow name and business type, then try again.'
              : 'Creation could not be confirmed. Reload the library and check for your draft before trying again.',
          );
        },
      });
  }
}
