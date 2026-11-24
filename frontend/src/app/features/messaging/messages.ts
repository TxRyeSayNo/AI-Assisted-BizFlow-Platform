import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { MessageUpdates } from '../../core/messaging/message-updates';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface ConversationRow {
  conversationId: string;
  otherUserId: string;
  otherUserName: string;
  otherUserEmployeeCode: string;
  lastMessagePreview: string | null;
  lastMessageSenderId: string | null;
  lastMessageAt: string | null;
  unreadCount: number;
}
export interface ConversationPage {
  items: ConversationRow[];
  page: number;
  pageSize: number;
  total: number;
}
export interface MessageRow {
  messageId: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  body: string;
  deleted: boolean;
  mine: boolean;
  createdAt: string;
  editedAt: string | null;
}
export interface MessagePage {
  conversationId: string;
  items: MessageRow[];
  page: number;
  pageSize: number;
  total: number;
}
interface Colleague {
  userId: string;
  fullName: string;
  employeeCode: string;
}

/**
 * Direct messages between colleagues (F13).
 *
 * The socket only ever tells this page that a conversation changed; the words always come from an
 * authenticated HTTP read, so a revoked session cannot be shown message content. MessageUpdates is
 * provided here rather than at the root, so leaving the page closes the transport.
 */
@Component({
  selector: 'bf-messages',
  providers: [MessageUpdates],
  imports: [
    RouterLink,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
  ],
  templateUrl: './messages.html',
  styleUrls: ['../organization/departments.scss', './messages.scss'],
})
export class Messages {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);
  private readonly realtime = inject(MessageUpdates);
  private listRequest?: Subscription;
  private threadRequest?: Subscription;

  readonly session = inject(SessionService);
  readonly features = inject(FeatureFlagsService);
  readonly identity = this.session.identity;
  readonly status = this.realtime.status;

  readonly conversations = signal<ConversationRow[]>([]);
  readonly messages = signal<MessageRow[]>([]);
  readonly openId = signal<string | null>(null);
  readonly busy = signal(false);
  readonly sending = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly colleagues = signal<Colleague[]>([]);
  readonly search = signal('');

  draft = '';

  readonly open = computed(() => {
    const id = this.openId();
    return id ? (this.conversations().find((c) => c.conversationId === id) ?? null) : null;
  });

  /** Colleagues with no conversation yet, narrowed by the search box. */
  readonly startable = computed(() => {
    const needle = this.search().trim().toLowerCase();
    const existing = new Set(this.conversations().map((c) => c.otherUserId));
    return this.colleagues()
      .filter((person) => !existing.has(person.userId))
      .filter(
        (person) =>
          needle.length === 0 ||
          person.fullName.toLowerCase().includes(needle) ||
          person.employeeCode.toLowerCase().includes(needle),
      )
      .slice(0, 10);
  });

  readonly totalUnread = computed(() =>
    this.conversations().reduce((sum, row) => sum + (row.unreadCount ?? 0), 0),
  );

  constructor() {
    if (!this.features.directMessages()) return;
    this.loadConversations();
    this.loadColleagues();
    // A hint names the conversation that changed: refresh the open thread, or just the list.
    this.realtime.changes.pipe(takeUntilDestroyed(this.destroy)).subscribe((event) => {
      if (event.kind !== 'refresh') return;
      this.loadConversations();
      if (event.conversationId && event.conversationId === this.openId())
        this.loadThread(event.conversationId);
    });
  }

  loadConversations() {
    this.listRequest?.unsubscribe();
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    this.listRequest = this.http
      .get<ConversationPage>('/api/v1/messages/conversations', {
        params: new HttpParams().set('page', 1).set('pageSize', 50),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (page) => {
          this.conversations.set(page?.items ?? []);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.conversations.set([]);
          this.denied.set(failure.status === 403);
          this.unavailable.set(failure.status === 404);
          this.error.set(![403, 404].includes(failure.status));
          this.busy.set(false);
        },
      });
  }

  openConversation(conversationId: string) {
    this.openId.set(conversationId);
    this.draft = '';
    this.loadThread(conversationId);
  }

  /** Starts, or re-opens, the conversation with a colleague. */
  startWith(person: Colleague) {
    if (this.sending()) return;
    this.sending.set(true);
    this.http
      .post<ConversationRow>('/api/v1/messages/conversations', { userId: person.userId })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.sending.set(false);
          this.search.set('');
          this.loadConversations();
          if (row?.conversationId) this.openConversation(row.conversationId);
        },
        error: (failure: HttpErrorResponse) => this.failed(failure, 'messages.errOpen'),
      });
  }

  loadThread(conversationId: string) {
    this.threadRequest?.unsubscribe();
    this.threadRequest = this.http
      .get<MessagePage>(`/api/v1/messages/conversations/${encodeURIComponent(conversationId)}`, {
        params: new HttpParams().set('page', 1).set('pageSize', 50),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (page) => {
          // The API returns newest first; a thread reads oldest to newest.
          this.messages.set([...(page?.items ?? [])].reverse());
          this.markRead(conversationId);
        },
        error: (failure: HttpErrorResponse) => {
          this.messages.set([]);
          if (failure.status === 404) this.openId.set(null);
        },
      });
  }

  send() {
    const body = this.draft.trim();
    const conversationId = this.openId();
    if (!body || !conversationId || this.sending()) return;
    this.sending.set(true);
    this.http
      .post<MessageRow>(
        `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}`,
        { body },
        // A retried send after an uncertain response must not duplicate the message.
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (message) => {
          this.sending.set(false);
          this.draft = '';
          if (message) this.messages.update((prev) => [...prev, message]);
          this.loadConversations();
        },
        error: (failure: HttpErrorResponse) => this.failed(failure, 'messages.errSend'),
      });
  }

  remove(message: MessageRow) {
    const conversationId = this.openId();
    if (!conversationId || !message.mine || message.deleted || this.sending()) return;
    this.sending.set(true);
    this.http
      .delete<MessageRow>(
        `/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/${encodeURIComponent(message.messageId)}`,
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (updated) => {
          this.sending.set(false);
          this.messages.update((prev) =>
            prev.map((item) => (item.messageId === message.messageId ? (updated ?? item) : item)),
          );
          this.loadConversations();
        },
        error: (failure: HttpErrorResponse) => this.failed(failure, 'messages.errDelete'),
      });
  }

  private markRead(conversationId: string) {
    this.http
      .put(`/api/v1/messages/conversations/${encodeURIComponent(conversationId)}/read`, {})
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () =>
          this.conversations.update((prev) =>
            prev.map((row) =>
              row.conversationId === conversationId ? { ...row, unreadCount: 0 } : row,
            ),
          ),
        // A read marker that does not stick is not worth interrupting anyone over.
        error: () => undefined,
      });
  }

  private loadColleagues() {
    this.http
      .get<{ items: { userId: string; fullName: string; employeeCode: string }[] }>(
        '/api/v1/users',
        {
          params: new HttpParams().set('pageSize', 100),
        },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (page) =>
          this.colleagues.set(
            (page?.items ?? [])
              .filter((person) => person.userId && person.userId !== this.identity()?.userId)
              .map((person) => ({
                userId: person.userId,
                fullName: person.fullName ?? person.employeeCode,
                employeeCode: person.employeeCode ?? '',
              })),
          ),
        error: () => this.colleagues.set([]),
      });
  }

  private failed(failure: HttpErrorResponse, key: string) {
    this.sending.set(false);
    // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
    const curated = failure?.status < 500 ? failure?.error?.message : null;
    this.toast.error(curated ?? this.i18n.t(key, 'Không gửi được tin nhắn.'));
  }
}
