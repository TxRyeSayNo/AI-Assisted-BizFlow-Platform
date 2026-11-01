import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SessionService } from '../auth/session';

export interface NotificationCountResult {
  total: number;
  unreadCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class NotificationCountService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);

  readonly unreadCount = signal<number>(0);
  readonly totalCount = signal<number>(0);

  refresh(): void {
    if (!this.session.signedIn()) return;
    this.http.get<any>('/api/v1/notifications?pageSize=1').subscribe({
      next: (res) => {
        if (typeof res?.unreadCount === 'number') {
          this.unreadCount.set(res.unreadCount);
        }
        if (typeof res?.total === 'number') {
          this.totalCount.set(res.total);
        }
      },
      error: () => {},
    });
  }

  decrement(): void {
    const cur = this.unreadCount();
    if (cur > 0) {
      this.unreadCount.set(cur - 1);
    }
  }
}

