import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const states = {
  DRAFT: 'Draft',
  ASSIGNED: 'Assigned',
  ACCEPTED: 'Accepted',
  IN_PROGRESS: 'In progress',
  SUBMITTED: 'Submitted',
  CONFIRMED: 'Confirmed',
  COMPLETED: 'Completed',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
  OVERDUE: 'Overdue',
};
const priorities = { LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', CRITICAL: 'Critical' };

export interface CalendarTask {
  taskId: string;
  title: string;
  status: keyof typeof states;
  priority: keyof typeof priorities;
  deadline: string | null;
  assignedUserName: string | null;
  assignedDepartmentName: string | null;
}
export interface CalendarView {
  items: CalendarTask[];
  from: string;
  to: string;
  limit: number;
  truncated: boolean;
}
export interface CalendarCell {
  key: string;
  day: number;
  date: Date;
  inFocus: boolean;
  isToday: boolean;
  items: CalendarTask[];
}

/** Local calendar-day key. Deadlines are grouped in the viewer's own time zone. */
function dayKey(value: Date): string {
  const month = `${value.getMonth() + 1}`.padStart(2, '0');
  const day = `${value.getDate()}`.padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

/** Monday-first week start, matching the Vietnamese calendar convention. */
function startOfWeek(value: Date): Date {
  const copy = new Date(value.getFullYear(), value.getMonth(), value.getDate());
  const weekday = (copy.getDay() + 6) % 7;
  copy.setDate(copy.getDate() - weekday);
  return copy;
}

/**
 * Read-only deadline calendar (F2). It shows one month or one week at a time and links each entry
 * to the task detail page; it never changes a task.
 */
@Component({
  selector: 'bf-task-calendar',
  imports: [
    RouterLink,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  templateUrl: './task-calendar.html',
  styleUrls: ['../organization/departments.scss', './task-calendar.scss'],
})
export class TaskCalendar {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { status: '', priority: '' };

  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<CalendarView | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly mode = signal<'month' | 'week'>('month');
  readonly anchor = signal<Date>(new Date());
  readonly states = states;
  readonly priorities = priorities;
  readonly stateOptions = Object.entries(states);
  readonly priorityOptions = Object.entries(priorities);
  readonly weekdayLabels = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

  /** Inclusive grid bounds: whole weeks for a month, the single week otherwise. */
  readonly window = computed(() => {
    const anchor = this.anchor();
    if (this.mode() === 'week') {
      const start = startOfWeek(anchor);
      const end = new Date(start);
      end.setDate(end.getDate() + 6);
      return { start, end };
    }
    const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
    const last = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
    const start = startOfWeek(first);
    const end = startOfWeek(last);
    end.setDate(end.getDate() + 6);
    return { start, end };
  });

  readonly cells = computed<CalendarCell[]>(() => {
    const { start, end } = this.window();
    const focusMonth = this.anchor().getMonth();
    const today = dayKey(new Date());
    const grouped = new Map<string, CalendarTask[]>();
    for (const item of this.result()?.items ?? []) {
      if (!item.deadline) continue;
      const parsed = new Date(item.deadline);
      if (Number.isNaN(parsed.getTime())) continue;
      const key = dayKey(parsed);
      const bucket = grouped.get(key);
      if (bucket) bucket.push(item);
      else grouped.set(key, [item]);
    }
    const cells: CalendarCell[] = [];
    for (const cursor = new Date(start); cursor <= end; cursor.setDate(cursor.getDate() + 1)) {
      const date = new Date(cursor);
      const key = dayKey(date);
      cells.push({
        key,
        day: date.getDate(),
        date,
        inFocus: this.mode() === 'week' || date.getMonth() === focusMonth,
        isToday: key === today,
        items: grouped.get(key) ?? [],
      });
    }
    return cells;
  });

  readonly heading = computed(() => {
    const { start, end } = this.window();
    return this.mode() === 'week'
      ? `${start.toLocaleDateString('vi-VN')} → ${end.toLocaleDateString('vi-VN')}`
      : `Tháng ${this.anchor().getMonth() + 1} năm ${this.anchor().getFullYear()}`;
  });

  constructor() {
    this.load();
  }

  setMode(mode: 'month' | 'week') {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    this.load();
  }

  step(direction: -1 | 1) {
    const anchor = this.anchor();
    const next =
      this.mode() === 'week'
        ? new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate() + 7 * direction)
        : new Date(anchor.getFullYear(), anchor.getMonth() + direction, 1);
    this.anchor.set(next);
    this.load();
  }

  today() {
    this.anchor.set(new Date());
    this.load();
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load();
  }

  load() {
    this.request?.unsubscribe();
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    const { start, end } = this.window();
    // The end of the grid is inclusive, so the window runs to the last instant of that day.
    const to = new Date(end.getFullYear(), end.getMonth(), end.getDate(), 23, 59, 59, 999);
    let params = new HttpParams().set('from', start.toISOString()).set('to', to.toISOString());
    for (const [key, value] of Object.entries(this.applied)) {
      if (value.trim()) params = params.set(key, value.trim());
    }
    this.request = this.http
      .get<CalendarView>('/api/v1/tasks/calendar', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.result.set(null);
          this.denied.set(failure.status === 403);
          this.unavailable.set(failure.status === 404);
          this.error.set(![403, 404].includes(failure.status));
          this.busy.set(false);
        },
      });
  }
}
