import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { InboxUpdates } from '../../core/notifications/inbox-updates';

export interface NotificationRow {
  notificationId: string;
  type: string;
  objectType: string | null;
  objectId: string | null;
  title: string;
  content: string;
  readAt: string | null;
  sentAt: string | null;
}
export interface NotificationPage {
  items: NotificationRow[];
  page: number;
  pageSize: number;
  total: number;
  unreadCount: number;
}

@Component({
  selector: 'bf-notifications',
  providers: [InboxUpdates],
  imports: [RouterLink, DatePipe, MatButtonModule, MatCheckboxModule],
  templateUrl: './notifications.html',
  styleUrls: ['../organization/departments.scss', './notifications.scss'],
})
export class Notifications {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  readonly session = inject(SessionService);
  readonly updates = inject(InboxUpdates);
  private refreshTimer?: ReturnType<typeof setTimeout>;
  readonly result = signal<NotificationPage | null>(null);
  readonly unreadOnly = signal(false);
  readonly page = signal(1);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly reading = signal<string | null>(null);
  readonly receiptError = signal('');
  readonly receiptMessage = signal('');
  constructor() {
    this.updates.changes.pipe(takeUntilDestroyed(this.destroy)).subscribe((event) => {
      clearTimeout(this.refreshTimer);
      if (event === 'clear') {
        this.request?.unsubscribe();
        this.result.set(null);
        return;
      }
      // Coalesce bursts; a pending receipt already refreshes after success.
      this.refreshTimer = setTimeout(() => this.load(this.page()), 100);
    });
    this.destroy.onDestroy(() => clearTimeout(this.refreshTimer));
    this.load(1);
  }

  filter(unreadOnly: boolean) {
    if (this.reading()) return;
    this.unreadOnly.set(unreadOnly);
    this.load(1);
  }

  load(page: number) {
    if (this.reading()) return;
    this.request?.unsubscribe();
    this.page.set(page);
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', 25)
      .set('unreadOnly', this.unreadOnly());
    this.request = this.http
      .get<NotificationPage>('/api/v1/notifications', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          if (data.items.length === 0 && page > 1) {
            this.load(Math.max(1, Math.ceil(data.total / data.pageSize)));
            return;
          }
          this.result.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
        },
      });
  }

  markRead(row: NotificationRow) {
    if (this.reading() || this.busy() || row.readAt) return;
    this.reading.set(row.notificationId);
    this.receiptError.set('');
    this.receiptMessage.set('');
    this.http
      .patch<NotificationRow>(
        `/api/v1/notifications/${encodeURIComponent(row.notificationId)}/read`,
        {},
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.reading.set(null);
          this.receiptMessage.set('Notification marked read.');
          this.load(this.page());
        },
        error: (failure: HttpErrorResponse) => {
          this.reading.set(null);
          this.receiptError.set(
            failure.status === 403 || failure.status === 404
              ? 'This notification is no longer available to you. Refresh the inbox.'
              : 'The read receipt could not be confirmed. Refresh or try again.',
          );
          if (failure.status === 403 || failure.status === 404) this.load(this.page());
        },
      });
  }
}
