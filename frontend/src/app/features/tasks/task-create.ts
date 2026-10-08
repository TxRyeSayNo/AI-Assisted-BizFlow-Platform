import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-task-create',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
  ],
  templateUrl: './task-create.html',
  styleUrls: ['../organization/departments.scss', './task-editor.scss'],
})
export class TaskCreate {
  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly busy = signal(false);
  readonly uncertain = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  readonly requestId = signal<string | null>(null);
  readonly aiPrompt = signal('');
  readonly aiBusy = signal(false);
  readonly aiMessage = signal('');
  private key = crypto.randomUUID();
  private submitted?: object;

  constructor() {
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroy)).subscribe((params) => {
      if (params['requestId']) {
        this.requestId.set(params['requestId']);
      }
    });
  }

  readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: [''],
    priority: ['MEDIUM'],
    deadline: [''],
    checklist: [''],
  });

  assistWithAi() {
    const prompt = this.aiPrompt().trim();
    if (!prompt || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiMessage.set('');
    this.http.post<any>('/api/v1/ai/task-assistance', { prompt })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (draft) => {
          this.aiBusy.set(false);
          if (draft) {
            this.form.patchValue({
              title: draft.title || '',
              description: draft.description || '',
              priority: draft.priority || 'MEDIUM',
              checklist: (draft.checklistItems || []).join('\n'),
            });
            this.aiMessage.set(`AI đã tạo bản thảo (Độ tin cậy: ${(draft.confidence * 100).toFixed(0)}%): ${draft.reasoning}`);
          }
        },
        error: () => {
          this.aiBusy.set(false);
          this.aiMessage.set('Không thể kết nối trợ lý AI. Vui lòng thử lại.');
        }
      });
  }

  breakdownWithAi() {
    const title = this.form.controls.title.value.trim();
    if (!title || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.http.post<any>('/api/v1/ai/task-breakdown', { title, description: this.form.controls.description.value, targetSubtaskCount: 4 })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.aiBusy.set(false);
          if (res?.subtasks?.length) {
            const list = res.subtasks.map((s: any) => `${s.orderIndex}. ${s.title}: ${s.description}`).join('\n');
            this.form.patchValue({ checklist: list });
            this.aiMessage.set(`AI đã phân rã thành ${res.subtasks.length} nhiệm vụ con: ${res.strategy}`);
          }
        },
        error: () => {
          this.aiBusy.set(false);
          this.aiMessage.set('Phân rã thất bại. Vui lòng kiểm tra tiêu đề.');
        }
      });
  }
  save() {
    if (this.busy() || this.done() || !this.session.hasPermission('tasks.create')) return;
    if (!this.uncertain()) {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return;
      }
      const value = this.form.getRawValue();
      const deadline = value.deadline ? new Date(value.deadline) : null;
      if (deadline && (!Number.isFinite(deadline.getTime()) || deadline.getTime() <= Date.now())) {
        this.error.set('Choose a future deadline in your local time.');
        return;
      }
      this.submitted = {
        title: value.title,
        description: value.description || null,
        priority: value.priority,
        deadline: deadline?.toISOString() ?? null,
        checklist: value.checklist
          .split('\n')
          .map((v) => v.trim())
          .filter(Boolean),
        ...(this.requestId() ? { requestId: this.requestId() } : {}),
      };
    }
    this.busy.set(true);
    this.error.set('');
    this.form.disable();
    this.http
      .post<{ taskId: string }>('/api/v1/tasks', this.submitted, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          if (!result?.taskId || !/^[0-9a-f-]{36}$/i.test(result.taskId)) {
            this.unknown();
            return;
          }
          this.done.set(true);
          void this.router.navigate(['/tasks', result.taskId]);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          if (failure.status === 422 && !this.uncertain()) {
            this.form.enable();
            this.key = crypto.randomUUID();
            this.error.set('Check the title, checklist and future deadline. No task was created.');
          } else if (failure.status === 403 || failure.status === 401) {
            this.done.set(true);
            this.error.set('You no longer have permission to create tasks.');
          } else {
            this.unknown();
          }
        },
      });
  }
  private unknown() {
    this.uncertain.set(true);
    this.error.set(
      'The outcome is uncertain. Retry this exact submission safely with the same key, or check your task list before starting again.',
    );
  }
}
