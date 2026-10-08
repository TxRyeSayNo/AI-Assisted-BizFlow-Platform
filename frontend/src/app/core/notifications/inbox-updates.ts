import { Injectable, InjectionToken, computed, effect, inject, signal } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { SessionService } from '../auth/session';

export interface InboxConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(name: string, callback: () => void): void;
  onreconnecting(callback: () => void): void;
  onreconnected(callback: () => void): void;
  onclose(callback: () => void): void;
}
export const INBOX_CONNECTION = new InjectionToken<
  (token: () => Promise<string>) => InboxConnection
>('recipient inbox connection', {
  providedIn: 'root',
  factory: () => (token) =>
    new HubConnectionBuilder()
      .withUrl('/api/v1/realtime/notifications', { accessTokenFactory: token })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 5000 })
      // Transport errors may contain credential-bearing URLs. Never log them.
      .configureLogging(LogLevel.None)
      .build(),
});

// Screen-scoped: navigation destroys the effect and stops its transport.
@Injectable()
export class InboxUpdates {
  private readonly session = inject(SessionService);
  private readonly factory = inject(INBOX_CONNECTION);
  private readonly scope = computed(() => {
    const identity = this.session.identity();
    return identity?.tenantId
      ? `${identity.tenantId}/${identity.userId}/${this.session.sessionGeneration()}`
      : null;
  });
  private readonly events = new Subject<'refresh' | 'clear'>();
  readonly changes = this.events.asObservable();
  readonly status = signal<'offline' | 'connecting' | 'live' | 'reconnecting'>('offline');

  constructor() {
    effect((cleanup) => {
      if (!this.scope()) {
        this.status.set('offline');
        return;
      }
      const generation = this.session.sessionGeneration();
      let stopped = false;
      let retry: ReturnType<typeof setTimeout> | undefined;
      const current = () => !stopped && generation === this.session.sessionGeneration();
      const connection = this.factory(async () => {
        if (!current()) throw new Error('Session changed.');
        const token = await this.session.realtimeAccessToken();
        if (!current()) throw new Error('Session changed.');
        return token;
      });
      const connected = () => {
        if (!current()) return;
        this.status.set('live');
        this.events.next('refresh');
      };
      const start = async () => {
        if (!current()) return;
        this.status.set('connecting');
        try {
          await connection.start();
          connected();
        } catch {
          if (!current()) return;
          this.status.set('offline');
          clearTimeout(retry);
          retry = setTimeout(() => void start(), 5000);
        }
      };
      connection.on('InboxChanged', () => {
        if (current()) this.events.next('refresh');
      });
      connection.on('InboxReady', connected);
      connection.onreconnecting(() => {
        if (current()) this.status.set('reconnecting');
      });
      connection.onreconnected(connected);
      connection.onclose(() => {
        if (!current()) return;
        this.status.set('offline');
        clearTimeout(retry);
        retry = setTimeout(() => void start(), 5000);
      });
      cleanup(() => {
        stopped = true;
        clearTimeout(retry);
        this.status.set('offline');
        this.events.next('clear');
        void connection.stop().catch(() => undefined);
      });
      void start();
    });
  }
}
