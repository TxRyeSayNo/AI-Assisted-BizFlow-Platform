import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
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
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
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
 * Which existing endpoint a drop replays, keyed by `from -> to`. Only the two request transitions
 * that need no payload are offered: receiving a routed request and starting work on a received
 * one. Everything else a request can do — routing, rejecting, resolving, confirming, cancelling,
 * escalating — requires a target, a reason or a resolution that a dropped card cannot invent, so
 * those lanes refuse the drop and point the person at the request itself.
 */
const transitions: Record<string, { path: string; permission: string }> = {
  'ROUTED>RECEIVED': { path: 'receive', permission: 'requests.receive' },
  'RECEIVED>IN_PROGRESS': { path: 'start', permission: 'requests.process' },
};

/**
 * Board over the request list. Cards can be dragged across the two payload-free transitions
 * above; every other lane links to the request detail page, where the remaining commands live
 * exactly as they always have.
 */
@Component({
  selector: 'bf-request-board',
  imports: [
    RouterLink,
    DatePipe,
    DragDropModule,
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

  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);
  private moving = false;

  private readonly seenRequesters = signal<Map<string, string>>(new Map());
  readonly requesterOptions = computed(() => [...this.seenRequesters().entries()]);

  /** Lane ids stay stable so a lane can receive a card from any other lane. */
  readonly laneIds = Object.keys(states).map((status) => 'request-lane-' + status);

  /** True when a card in `from` could legitimately be dropped into `to`. */
  canMove(from: string, to: string): boolean {
    return transitions[from + '>' + to] !== undefined;
  }

  drop(event: CdkDragDrop<RequestBoardColumn>, target: string) {
    // The card is never moved optimistically: the board reloads once the transition really
    // succeeded, so a refused drop leaves the lane exactly as it was.
    if (event.previousContainer === event.container || this.moving) return;
    const card = event.item.data as RequestBoardCard;
    const plan = transitions[card.status + '>' + target];
    if (!plan) {
      this.toast.error(
        this.i18n.t(
          'requestBoard.invalidTransition',
          'Bước này cần thêm thông tin. Hãy mở yêu cầu để thực hiện.',
        ),
      );
      return;
    }
    if (!this.session.hasPermission(plan.permission)) {
      this.toast.error(
        this.i18n.t(
          'taskBoard.noPermission',
          'Bạn không có quyền thực hiện chuyển trạng thái này.',
        ),
      );
      return;
    }
    this.moving = true;
    this.http
      .post(
        `/api/v1/requests/${encodeURIComponent(card.requestId)}/${plan.path}`,
        {},
        // A retried transition after an uncertain response must not apply twice.
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.moving = false;
          this.load();
        },
        error: (failure: HttpErrorResponse) => {
          this.moving = false;
          // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
          const curated = failure?.status < 500 ? failure?.error?.message : null;
          this.toast.error(
            curated ?? this.i18n.t('requestBoard.errMove', 'Không chuyển được trạng thái yêu cầu.'),
          );
          this.load();
        },
      });
  }

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
