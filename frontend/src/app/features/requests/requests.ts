import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export const requestStates = {
  DRAFT: 'Draft',
  SUBMITTED: 'Submitted',
  ROUTED: 'Routed',
  RECEIVED: 'Received',
  IN_PROGRESS: 'In Progress',
  WAITING_FOR_INFORMATION: 'Waiting for Information',
  RESOLVED: 'Resolved',
  CONFIRMED: 'Confirmed',
  CLOSED: 'Closed',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
  OVERDUE: 'Overdue',
} as const;

export const requestPriorities = {
  LOW: 'Low',
  MEDIUM: 'Medium',
  HIGH: 'High',
  CRITICAL: 'Critical',
} as const;

export interface RequestRow {
  requestId: string;
  title: string;
  status: keyof typeof requestStates;
  priority: keyof typeof requestPriorities;
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  requesterId: string;
  requesterName: string;
  createdAt: string;
  updatedAt: string;
}

export interface RequestPage {
  items: RequestRow[];
  page: number;
  pageSize: number;
  total: number;
}

interface ServiceOption {
  serviceId: string;
  name: string;
}

@Component({
  selector: 'bf-requests',
  imports: [
    RouterLink,
    DatePipe,
    DecimalPipe,
    NgClass,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './requests.html',
  styleUrls: ['../organization/departments.scss', './requests.scss'],
})
export class Requests implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly forms = inject(FormBuilder).nonNullable;
  private pendingRequest?: Subscription;

  readonly session = inject(SessionService);
  readonly identity = this.session.identity;

  readonly stateOptions = Object.entries(requestStates);
  readonly priorityOptions = Object.entries(requestPriorities);
  readonly states = requestStates;
  readonly priorities = requestPriorities;

  readonly services = signal<ServiceOption[]>([]);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly result = signal<RequestPage | null>(null);
  readonly page = signal(1);

  readonly filters = this.forms.group({
    search: [''],
    status: [''],
    priority: [''],
    serviceId: [''],
  });

  ngOnInit() {
    this.loadServices();
    this.load(1);
  }

  loadServices() {
    this.http
      .get<{ items: { serviceId: string; name: string; status: string }[] }>('/api/v1/services', {
        params: new HttpParams().set('pageSize', '100'),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.services.set(
            (res.items || [])
              .filter((s) => s.status === 'ACTIVE')
              .map((s) => ({ serviceId: s.serviceId, name: s.name })),
          );
        },
        error: () => {
          // Non-critical; filter remains functional with empty options
        },
      });
  }

  apply() {
    this.load(1);
  }

  load(page: number) {
    this.pendingRequest?.unsubscribe();
    this.busy.set(true);
    this.error.set(false);
    this.denied.set(false);

    const filterVal = this.filters.getRawValue();
    let params = new HttpParams().set('page', page.toString()).set('pageSize', '25');

    if (filterVal.search.trim()) {
      params = params.set('search', filterVal.search.trim());
    }
    if (filterVal.status) {
      params = params.set('status', filterVal.status);
    }
    if (filterVal.priority) {
      params = params.set('priority', filterVal.priority);
    }
    if (filterVal.serviceId) {
      params = params.set('serviceId', filterVal.serviceId);
    }

    this.pendingRequest = this.http
      .get<RequestPage>('/api/v1/requests', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.busy.set(false);
          this.page.set(page);
          this.result.set(data);
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          if (err.status === 403 || err.status === 401) {
            this.denied.set(true);
          } else {
            this.error.set(true);
          }
        },
      });
  }

  statusClass(status: string): string {
    return 'status-' + status.toLowerCase().replace(/_/g, '-');
  }

  priorityClass(priority: string): string {
    return 'priority-' + priority.toLowerCase();
  }

  getStateName(status: string): string {
    return this.states[status as keyof typeof requestStates] ?? status;
  }

  getPriorityName(priority: string): string {
    return this.priorities[priority as keyof typeof requestPriorities] ?? priority;
  }
}
