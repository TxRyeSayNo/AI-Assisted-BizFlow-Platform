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

export interface SlaProfileRow {
  slaProfileId: string;
  name: string;
  status: 'DRAFT' | 'ACTIVE' | 'INACTIVE';
}
interface SlaProfilePage {
  items: SlaProfileRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-sla-profiles',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './sla-profiles.html',
  styleUrls: ['../organization/departments.scss', './sla-profiles.scss'],
})
export class SlaProfiles {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', status: '' };
  readonly session = inject(SessionService);
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly createForm = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });
  readonly result = signal<SlaProfilePage | null>(null);
  readonly created = signal<SlaProfileRow | null>(null);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly mutationError = signal('');
  readonly uncertain = signal(false);
  readonly page = signal(1);
  readonly labels = { DRAFT: 'Draft', ACTIVE: 'Active', INACTIVE: 'Inactive' };
  constructor() {
    this.load(1);
  }
  apply() {
    if (this.saving()) return;
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
      .get<SlaProfilePage>('/api/v1/sla-profiles', { params })
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
      !this.session.hasPermission('sla.configure') ||
      this.denied() ||
      this.busy() ||
      this.saving() ||
      this.uncertain() ||
      this.createForm.invalid
    )
      return;
    const name = this.createForm.getRawValue().name.trim();
    if (!name) return;
    this.saving.set(true);
    this.mutationError.set('');
    this.created.set(null);
    this.http
      .post<SlaProfileRow>('/api/v1/sla-profiles', { name })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.saving.set(false);
          this.created.set(row);
          this.createForm.reset({ name: '' });
          this.applied = { search: name, status: '' };
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
              ? 'Enter a profile name up to 200 characters.'
              : 'Creation could not be confirmed. Reload profiles and check for your draft before trying again.',
          );
        },
      });
  }
}
