import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

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
interface TaskRow {
  taskId: string;
  title: string;
  status: keyof typeof states;
  priority: keyof typeof priorities;
  deadline: string | null;
  assignedUserId: string | null;
  assignedUserName: string | null;
  assignedDepartmentId: string | null;
  assignedDepartmentName: string | null;
  requestId: string | null;
}
interface TaskPage {
  items: TaskRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-tasks',
  imports: [
    RouterLink,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './tasks.html',
  styleUrls: ['../organization/departments.scss', './tasks.scss'],
})
export class Tasks {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', status: '', priority: '' };
  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<TaskPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly states = states;
  readonly priorities = priorities;
  readonly stateOptions = Object.entries(states);
  readonly priorityOptions = Object.entries(priorities);
  constructor() {
    this.load(1);
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
    for (const [key, value] of Object.entries(this.applied)) {
      if (value.trim()) params = params.set(key, value.trim());
    }
    this.request = this.http
      .get<TaskPage>('/api/v1/tasks', { params })
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
