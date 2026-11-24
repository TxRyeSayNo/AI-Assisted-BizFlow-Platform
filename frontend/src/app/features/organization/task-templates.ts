import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface TaskTemplateView {
  templateId: string;
  name: string;
  titleTemplate: string;
  description: string | null;
  priority: string | null;
  deadlineOffsetDays: number | null;
  recurrenceFrequency: string | null;
  recurrenceDay: number | null;
  recurrenceHourUtc: number | null;
  nextRunAt: string | null;
  lastRunAt: string | null;
  archived: boolean;
}

/**
 * Task template administration (F9) with its optional recurrence (F8) — one screen, because a
 * template and a task that repeats are the same thing with and without a schedule.
 *
 * Managing the catalogue is a tenant administration capability, which the server enforces with the
 * existing roles.configure permission; the guard on this route is only a convenience. Creating a
 * task from a template goes through the ordinary task creation endpoint, so it still needs
 * tasks.create — reading the catalogue is not authority to use it.
 */
@Component({
  selector: 'bf-task-templates',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
  ],
  templateUrl: './task-templates.html',
  styleUrls: ['./departments.scss'],
})
export class TaskTemplates {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);

  readonly session = inject(SessionService);
  readonly features = inject(FeatureFlagsService);
  readonly items = signal<TaskTemplateView[]>([]);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly includeArchived = signal(false);
  readonly editingId = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    titleTemplate: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    priority: [''],
    deadlineOffsetDays: ['', [Validators.pattern(/^\d{0,3}$/)]],
    recurrenceFrequency: [''],
    recurrenceDay: ['', [Validators.pattern(/^\d{0,2}$/)]],
    recurrenceHourUtc: ['', [Validators.pattern(/^\d{0,2}$/)]],
  });

  constructor() {
    this.load();
  }

  canManage(): boolean {
    return this.session.hasPermission('roles.configure');
  }

  canCreateTasks(): boolean {
    return this.session.hasPermission('tasks.create');
  }

  load() {
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    this.http
      .get<TaskTemplateView[]>('/api/v1/task-templates', {
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

  startEdit(template: TaskTemplateView) {
    this.editingId.set(template.templateId);
    this.form.setValue({
      name: template.name,
      titleTemplate: template.titleTemplate,
      description: template.description ?? '',
      priority: template.priority ?? '',
      deadlineOffsetDays: template.deadlineOffsetDays?.toString() ?? '',
      recurrenceFrequency: template.recurrenceFrequency ?? '',
      recurrenceDay: template.recurrenceDay?.toString() ?? '',
      recurrenceHourUtc: template.recurrenceHourUtc?.toString() ?? '',
    });
  }

  cancelEdit() {
    this.editingId.set(null);
    this.form.reset({
      name: '',
      titleTemplate: '',
      description: '',
      priority: '',
      deadlineOffsetDays: '',
      recurrenceFrequency: '',
      recurrenceDay: '',
      recurrenceHourUtc: '',
    });
  }

  submit() {
    if (this.busy() || this.form.invalid || !this.canManage()) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body = {
      name: raw.name,
      titleTemplate: raw.titleTemplate,
      description: raw.description.trim() || null,
      priority: raw.priority.trim() || null,
      deadlineOffsetDays: this.number(raw.deadlineOffsetDays),
      recurrenceFrequency: raw.recurrenceFrequency.trim() || null,
      recurrenceDay: this.number(raw.recurrenceDay),
      recurrenceHourUtc: this.number(raw.recurrenceHourUtc),
    };
    const editing = this.editingId();
    this.busy.set(true);
    const request = editing
      ? this.http.put<TaskTemplateView>(
          `/api/v1/task-templates/${encodeURIComponent(editing)}`,
          body,
        )
      : this.http.post<TaskTemplateView>('/api/v1/task-templates', body);
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: () => {
        this.busy.set(false);
        this.cancelEdit();
        this.toast.success(this.i18n.t('templates.saved', 'Đã lưu mẫu công việc.'));
        this.load();
      },
      error: (failure: HttpErrorResponse) => this.failed(failure),
    });
  }

  setArchived(template: TaskTemplateView, archived: boolean) {
    if (this.busy() || !this.canManage()) return;
    this.busy.set(true);
    this.http
      .post<TaskTemplateView>(
        `/api/v1/task-templates/${encodeURIComponent(template.templateId)}/archive`,
        { archived },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.load();
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  /** Creates one task from the template and opens it, so the person lands on real work. */
  instantiate(template: TaskTemplateView) {
    if (this.busy() || template.archived) return;
    this.busy.set(true);
    this.http
      .post<{ taskId: string }>(
        `/api/v1/task-templates/${encodeURIComponent(template.templateId)}/tasks`,
        {},
        // A retried create after an uncertain response must not produce a second task.
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (created) => {
          this.busy.set(false);
          this.toast.success(this.i18n.t('templates.created', 'Đã tạo công việc từ mẫu.'));
          if (created?.taskId) this.router.navigate(['/tasks', created.taskId]);
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  /** How a schedule reads in the list, or an empty string when the template never fires. */
  scheduleLabel(template: TaskTemplateView): string {
    if (!template.recurrenceFrequency) return '';
    const hour = `${(template.recurrenceHourUtc ?? 0).toString().padStart(2, '0')}:00 UTC`;
    if (template.recurrenceFrequency === 'DAILY')
      return `${this.i18n.t('templates.daily', 'Hằng ngày')} ${hour}`;
    if (template.recurrenceFrequency === 'WEEKLY')
      return `${this.i18n.t('templates.weekly', 'Hằng tuần')} T${template.recurrenceDay} ${hour}`;
    return `${this.i18n.t('templates.monthly', 'Hằng tháng')} ${this.i18n.t('templates.dayOf', 'ngày')} ${template.recurrenceDay} ${hour}`;
  }

  private number(value: string): number | null {
    const text = value.trim();
    if (!text) return null;
    const parsed = Number(text);
    return Number.isInteger(parsed) ? parsed : null;
  }

  private failed(failure: HttpErrorResponse) {
    this.busy.set(false);
    // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
    const curated = failure?.status < 500 ? failure?.error?.message : null;
    this.toast.error(curated ?? this.i18n.t('templates.errSave', 'Không lưu được mẫu công việc.'));
  }
}
