import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface ReminderMark {
  id: string;
  taskId: string;
  userId: string;
  leadTimeMinutes: number;
  remindAt: string;
  triggerType: string;
  status: string;
  createdAt: string;
}

/** The marks the server documents, in the order a person reads them. */
const marks: { minutes: number; trigger: string; labelKey: string; fallback: string }[] = [
  { minutes: 1440, trigger: '24h', labelKey: 'reminders.mark24h', fallback: '24 hours before' },
  { minutes: 240, trigger: '4h', labelKey: 'reminders.mark4h', fallback: '4 hours before' },
  { minutes: 60, trigger: '1h', labelKey: 'reminders.mark1h', fallback: '1 hour before' },
  { minutes: 0, trigger: 'Overdue', labelKey: 'reminders.markOverdue', fallback: 'When overdue' },
];

/**
 * Per-task reminder marks for the signed-in person (F4). It reads nothing until the deadline
 * reminder feature is known to be on, so pages whose existing specs assert that no unexpected
 * request was made stay unaffected, and a deployment with the flag off looks untouched.
 *
 * Browser push registration is deliberately absent: it needs a service worker, and PWA work is
 * outside this scope. The server still escalates to web push for subscriptions it already holds.
 */
@Component({
  selector: 'bf-task-reminders',
  imports: [MatButtonModule, MatCheckboxModule, TranslatePipe],
  template: `
    @if (features.deadlineReminders() && available()) {
      <section class="task-reminders" [attr.aria-busy]="busy()">
        <h3>{{ 'reminders.title' | translate: 'Deadline reminders' }}</h3>
        <p class="reminders-hint">
          {{
            'reminders.hint'
              | translate
                : 'You will be reminded in the app, and by push or email when you are away.'
          }}
        </p>
        <div class="reminders-marks">
          @for (mark of marks; track mark.trigger) {
            <mat-checkbox
              [checked]="selected().has(mark.trigger)"
              [disabled]="busy()"
              (change)="toggle(mark.trigger)"
            >
              {{ mark.labelKey | translate: mark.fallback }}
            </mat-checkbox>
          }
        </div>
        <button mat-stroked-button type="button" [disabled]="busy()" (click)="save()">
          {{ 'common.save' | translate: 'Save' }}
        </button>
      </section>
    }
  `,
  styles: [
    `
      .task-reminders {
        margin: 0.75rem 0;
      }
      .reminders-hint {
        font-size: 0.8125rem;
        opacity: 0.75;
        margin: 0 0 0.35rem;
      }
      .reminders-marks {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        margin-bottom: 0.5rem;
      }
    `,
  ],
})
export class TaskReminders implements OnInit {
  @Input({ required: true }) taskId!: string;
  /** Reminders only make sense for work that carries a deadline. */
  @Input() hasDeadline = true;

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly features = inject(FeatureFlagsService);
  readonly marks = marks;
  readonly selected = signal<Set<string>>(new Set());
  readonly busy = signal(false);
  readonly available = signal(false);
  /** The rows the server currently holds, so unchecking can cancel the right ones. */
  private current: ReminderMark[] = [];

  ngOnInit() {
    if (!this.features.deadlineReminders() || !this.taskId || !this.hasDeadline) return;
    this.http
      .get<ReminderMark[]>('/api/v1/notifications/reminders/config', {
        params: { taskId: this.taskId },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (items) => {
          this.current = items ?? [];
          this.selected.set(
            new Set(this.current.filter((m) => m.status === 'Scheduled').map((m) => m.triggerType)),
          );
          this.available.set(true);
        },
        // A task out of scope, or a disabled feature, simply shows nothing.
        error: () => this.available.set(false),
      });
  }

  toggle(trigger: string) {
    this.selected.update((prev) => {
      const next = new Set(prev);
      if (next.has(trigger)) next.delete(trigger);
      else next.add(trigger);
      return next;
    });
  }

  save() {
    if (this.busy()) return;
    const chosen = marks.filter((mark) => this.selected().has(mark.trigger));
    // An empty list means "use the defaults" to the configuration endpoint, so clearing every box
    // has to cancel the existing marks instead of posting nothing.
    const toCancel = this.current.filter(
      (mark) => mark.status === 'Scheduled' && !this.selected().has(mark.triggerType),
    );
    this.busy.set(true);
    if (chosen.length === 0) {
      this.cancelThen(toCancel, () => {
        this.current = this.current.map((mark) =>
          toCancel.some((c) => c.id === mark.id) ? { ...mark, status: 'Cancelled' } : mark,
        );
        this.selected.set(new Set());
        this.done();
      });
      return;
    }
    this.http
      .post<ReminderMark[]>('/api/v1/notifications/reminders/config', {
        taskId: this.taskId,
        leadTimeMinutes: chosen.map((mark) => mark.minutes),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (items) => {
          const saved = items ?? [];
          this.cancelThen(toCancel, () => {
            this.current = saved;
            this.selected.set(new Set(saved.map((m) => m.triggerType)));
            this.done();
          });
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  /** Cancels each mark in turn, then runs the continuation. */
  private cancelThen(toCancel: ReminderMark[], then: () => void) {
    const [head, ...rest] = toCancel;
    if (!head) {
      then();
      return;
    }
    this.http
      .delete(`/api/v1/notifications/reminders/${encodeURIComponent(head.id)}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => this.cancelThen(rest, then),
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  private done() {
    this.busy.set(false);
    this.toast.success(this.i18n.t('reminders.saved', 'Đã lưu nhắc hạn.'));
  }

  private failed(failure: HttpErrorResponse) {
    this.busy.set(false);
    // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
    const curated = failure?.status < 500 ? failure?.error?.message : null;
    this.toast.error(curated ?? this.i18n.t('reminders.errSave', 'Không lưu được nhắc hạn.'));
  }
}
