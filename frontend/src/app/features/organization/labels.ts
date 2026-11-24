import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { LabelView } from '../collaboration/work-item-labels';

/**
 * Label catalogue administration (F6). Creating, renaming and retiring a label is a tenant
 * administration capability, which the server enforces with the existing roles.configure
 * permission; the guard on this route is only a convenience.
 */
@Component({
  selector: 'bf-labels',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
  ],
  templateUrl: './labels.html',
  styleUrls: ['./departments.scss', '../collaboration/work-item-labels.scss'],
})
export class Labels {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly session = inject(SessionService);
  readonly features = inject(FeatureFlagsService);
  readonly items = signal<LabelView[]>([]);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly includeArchived = signal(false);
  readonly editingId = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    colour: ['#1A73E8', [Validators.required, Validators.pattern(/^#?[0-9A-Fa-f]{6}$/)]],
  });

  constructor() {
    this.load();
  }

  canManage(): boolean {
    return this.session.hasPermission('roles.configure');
  }

  load() {
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    this.http
      .get<LabelView[]>('/api/v1/labels', {
        params: { includeArchived: this.includeArchived() },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (items) => {
          this.items.set(items ?? []);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.items.set([]);
          this.denied.set(failure.status === 403);
          this.unavailable.set(failure.status === 404);
          this.error.set(![403, 404].includes(failure.status));
          this.busy.set(false);
        },
      });
  }

  toggleArchivedView() {
    this.includeArchived.update((value) => !value);
    this.load();
  }

  startEdit(label: LabelView) {
    this.editingId.set(label.labelId);
    this.form.setValue({ name: label.name, colour: label.colour });
  }

  cancelEdit() {
    this.editingId.set(null);
    this.form.reset({ name: '', colour: '#1A73E8' });
  }

  submit() {
    if (this.busy() || this.form.invalid || !this.canManage()) {
      this.form.markAllAsTouched();
      return;
    }
    const body = this.form.getRawValue();
    const editing = this.editingId();
    this.busy.set(true);
    const request = editing
      ? this.http.put<LabelView>(`/api/v1/labels/${encodeURIComponent(editing)}`, body)
      : this.http.post<LabelView>('/api/v1/labels', body);
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => {
        this.busy.set(false);
        this.cancelEdit();
        this.toast.success(this.i18n.t('labels.saved', 'Đã lưu nhãn.'));
        this.load();
      },
      error: (failure: HttpErrorResponse) => this.failed(failure),
    });
  }

  setArchived(label: LabelView, archived: boolean) {
    if (this.busy() || !this.canManage()) return;
    this.busy.set(true);
    this.http
      .put<LabelView>(`/api/v1/labels/${encodeURIComponent(label.labelId)}/archived`, { archived })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.load();
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  private failed(failure: HttpErrorResponse) {
    this.busy.set(false);
    // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
    const curated = failure?.status < 500 ? failure?.error?.message : null;
    this.toast.error(curated ?? this.i18n.t('labels.errSave', 'Không lưu được nhãn.'));
  }
}
