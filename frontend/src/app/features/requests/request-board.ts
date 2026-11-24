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
  SUBMITTED: 'Submitted',
  ROUTED: 'Routed',
  RECEIVED: 'Received',
  IN_PROGRESS: 'In progress',
  RESOLVED: 'Resolved',
  CONFIRMED: 'Confirmed',
  CLOSED: 'Closed',
  WAITING_FOR_INFORMATION: 'Waiting for information',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
  OVERDUE: 'Overdue',
};
const priorities = { LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', CRITICAL: 'Critical' };

export interface RequestBoardCard {
  requestId: string;
  title: string;
  status: keyof typeof states;
  priority: keyof typeof priorities;
  serviceId: string;
  serviceName: string | null;
  categoryId: string;
  categoryName: string | null;
  requesterId: string;
  requesterName: string | null;
  createdAt: string;
  updatedAt: string;
}
export interface RequestBoardColumn {
  status: keyof typeof states;
  items: RequestBoardCard[];
  total: number;
}
export interface RequestBoardView {
  columns: RequestBoardColumn[];
  columnSize: number;
}

/**
 * Read-only board over the request list. A request advances through nine separate commands, most
 * of which require a payload a dropped card cannot supply, so cards are not draggable here: each
 * one links to the request detail page where those commands already live.
 */
@Component({
  selector: 'bf-request-board',
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
  templateUrl: './request-board.html',
  styleUrls: ['../organization/departments.scss', '../tasks/task-board.scss'],
})
export class RequestBoard {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', priority: '', requesterId: '' };

  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<RequestBoardView | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly states = states;
  readonly priorities = priorities;
  readonly priorityOptions = Object.entries(priorities);

  private readonly seenRequesters = signal<Map<string, string>>(new Map());
  readonly requesterOptions = computed(() => [...this.seenRequesters().entries()]);

  constructor() {
    this.load();
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load();
  }

  reset() {
    this.filters.reset();
    this.apply();
  }

  load() {
    this.request?.unsubscribe();
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    let params = new HttpParams();
    for (const [key, value] of Object.entries(this.applied)) {
      if (value.trim()) params = params.set(key, value.trim());
    }
    this.request = this.http
      .get<RequestBoardView>('/api/v1/requests/board', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.remember(data);
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

  private remember(view: RequestBoardView) {
    const requesters = new Map(this.seenRequesters());
    for (const column of view.columns ?? []) {
      for (const card of column.items ?? []) {
        if (card.requesterId) {
          requesters.set(card.requesterId, card.requesterName ?? card.requesterId);
        }
      }
    }
    this.seenRequesters.set(requesters);
  }
}
