import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { MESSAGE_CONNECTION, MessageConnection } from '../../core/messaging/message-updates';
import { ToastService } from '../../core/toast/toast.service';
import { ConversationRow, MessageRow, Messages } from './messages';

describe('direct messages', () => {
  const conversations = '/api/v1/messages/conversations';
  let hints: Subject<string | undefined>;

  /** A stub transport, so no socket is opened and the test drives the hints itself. */
  const stubConnection = (): MessageConnection => {
    const handlers = new Map<string, (conversationId?: string) => void>();
    hints.subscribe((id) => handlers.get('ConversationChanged')?.(id));
    return {
      // Never resolves on purpose: a connect that completes would emit its own refresh hint and
      // reload the list a second time. Here the test drives every hint itself.
      start: () => new Promise<void>(() => undefined),
      stop: () => Promise.resolve(),
      on: (name, callback) => handlers.set(name, callback),
      onreconnecting: () => undefined,
      onreconnected: () => undefined,
      onclose: () => undefined,
    };
  };

  beforeEach(() => {
    hints = new Subject<string | undefined>();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            hasPermission: () => true,
            identity: () => ({ userId: 'me', tenantId: 'tenant-1', tenantName: 'Acme Corp' }),
            sessionGeneration: () => 1,
            realtimeAccessToken: () => Promise.resolve('token'),
          },
        },
        { provide: MESSAGE_CONNECTION, useValue: () => stubConnection() },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const enable = () => {
    TestBed.inject(FeatureFlagsService).load();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/features').flush({
      kanban: true,
      calendar: true,
      mentions: true,
      deadlineReminders: true,
      reportExport: true,
      labels: true,
      directMessages: true,
    });
  };

  const row = (over: Partial<ConversationRow> = {}): ConversationRow => ({
    conversationId: 'conv-1',
    otherUserId: 'user-2',
    otherUserName: 'Đồng nghiệp',
    otherUserEmployeeCode: 'EMP002',
    lastMessagePreview: 'Chào bạn',
    lastMessageSenderId: 'user-2',
    lastMessageAt: '2026-11-21T10:00:00Z',
    unreadCount: 2,
    ...over,
  });

  const message = (over: Partial<MessageRow> = {}): MessageRow => ({
    messageId: 'msg-1',
    conversationId: 'conv-1',
    senderId: 'user-2',
    senderName: 'Đồng nghiệp',
    body: 'Chào bạn',
    deleted: false,
    mine: false,
    createdAt: '2026-11-21T10:00:00Z',
    editedAt: null,
    ...over,
  });

  /** Drains the two requests the page makes on init. */
  const init = (items: ConversationRow[] = [row()]) => {
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Messages);
    fixture.detectChanges();
    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({ items, page: 1, pageSize: 50, total: items.length });
    http
      .expectOne((r) => r.url === '/api/v1/users')
      .flush({ items: [{ userId: 'user-3', fullName: 'Người mới', employeeCode: 'EMP003' }] });
    fixture.detectChanges();
    return fixture;
  };

  it('stays completely inert while the messaging feature is off', () => {
    const fixture = TestBed.createComponent(Messages);
    fixture.detectChanges();
    // No conversation read, no directory read: the page asks for nothing at all.
    TestBed.inject(HttpTestingController).verify();
    expect(fixture.nativeElement.textContent).toContain('not enabled on this deployment');
  });

  it('lists conversations with their unread counts and offers colleagues without one', () => {
    enable();
    const fixture = init();

    expect(fixture.nativeElement.textContent).toContain('Đồng nghiệp');
    expect(fixture.nativeElement.textContent).toContain('Chào bạn');
    expect(fixture.componentInstance.totalUnread()).toBe(2);
    // The colleague who already has a conversation is not offered again.
    expect(fixture.componentInstance.startable().map((p) => p.userId)).toEqual(['user-3']);
  });

  it('opens a thread oldest-first and marks it read', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();

    fixture.componentInstance.openConversation('conv-1');
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({
        conversationId: 'conv-1',
        // The API returns newest first.
        items: [
          message({ messageId: 'msg-2', body: 'Tin sau' }),
          message({ messageId: 'msg-1', body: 'Tin trước' }),
        ],
        page: 1,
        pageSize: 50,
        total: 2,
      });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    // A thread reads oldest to newest.
    expect(fixture.componentInstance.messages().map((m) => m.body)).toEqual([
      'Tin trước',
      'Tin sau',
    ]);
    expect(fixture.componentInstance.conversations()[0].unreadCount).toBe(0);
  });

  it('sends with an idempotency key and appends the stored message', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();
    fixture.componentInstance.openConversation('conv-1');
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({ conversationId: 'conv-1', items: [], page: 1, pageSize: 50, total: 0 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });

    fixture.componentInstance.draft = '  Xin chào  ';
    fixture.componentInstance.send();

    const sent = http.expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'POST');
    expect(sent.request.body.body).toBe('Xin chào');
    // A retried send must not be able to duplicate the message.
    expect(sent.request.headers.get('Idempotency-Key')).toBeTruthy();
    sent.flush(message({ messageId: 'msg-9', body: 'Xin chào', mine: true, senderId: 'me' }));

    // Sending refreshes the list so the preview and ordering stay right.
    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({ items: [row({ unreadCount: 0 })], page: 1, pageSize: 50, total: 1 });

    expect(fixture.componentInstance.messages().map((m) => m.messageId)).toEqual(['msg-9']);
    expect(fixture.componentInstance.draft).toBe('');
  });

  it('refetches rather than trusting the socket when a hint arrives', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();
    fixture.componentInstance.openConversation('conv-1');
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({ conversationId: 'conv-1', items: [], page: 1, pageSize: 50, total: 0 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });

    // A hint for the open conversation refreshes both the list and the thread.
    hints.next('conv-1');
    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({ items: [row()], page: 1, pageSize: 50, total: 1 });
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({ conversationId: 'conv-1', items: [message()], page: 1, pageSize: 50, total: 1 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });
    expect(fixture.componentInstance.messages().length).toBe(1);

    // A hint for a different conversation refreshes only the list.
    hints.next('conv-other');
    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({ items: [row()], page: 1, pageSize: 50, total: 1 });
    http.verify();
  });

  it('starts a conversation with a colleague and opens it', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init([]);

    fixture.componentInstance.startWith({
      userId: 'user-3',
      fullName: 'Người mới',
      employeeCode: 'EMP003',
    });
    const opened = http.expectOne((r) => r.url === conversations && r.method === 'POST');
    expect(opened.request.body.userId).toBe('user-3');
    opened.flush(row({ conversationId: 'conv-new', otherUserId: 'user-3', unreadCount: 0 }));

    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({
        items: [row({ conversationId: 'conv-new', otherUserId: 'user-3' })],
        page: 1,
        pageSize: 50,
        total: 1,
      });
    http
      .expectOne((r) => r.url === `${conversations}/conv-new` && r.method === 'GET')
      .flush({ conversationId: 'conv-new', items: [], page: 1, pageSize: 50, total: 0 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-new/read` })
      .flush(null, { status: 204, statusText: 'No Content' });

    expect(fixture.componentInstance.openId()).toBe('conv-new');
  });

  it('deletes only its own message and keeps the row without its words', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();
    fixture.componentInstance.openConversation('conv-1');
    const own = message({ messageId: 'msg-mine', body: 'Nhầm rồi', mine: true, senderId: 'me' });
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({ conversationId: 'conv-1', items: [own], page: 1, pageSize: 50, total: 1 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });

    // Someone else's message is never deletable from here.
    fixture.componentInstance.remove(message({ mine: false }));
    http.verify();

    fixture.componentInstance.remove(own);
    http
      .expectOne({ method: 'DELETE', url: `${conversations}/conv-1/msg-mine` })
      .flush({ ...own, body: '', deleted: true });
    http
      .expectOne((r) => r.url === conversations && r.method === 'GET')
      .flush({ items: [row()], page: 1, pageSize: 50, total: 1 });
    fixture.detectChanges();

    expect(fixture.componentInstance.messages()[0].deleted).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('This message was deleted');
    expect(fixture.nativeElement.textContent).not.toContain('Nhầm rồi');
  });

  it('reports a refused send with the server wording and a failure with its own', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();
    fixture.componentInstance.openConversation('conv-1');
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush({ conversationId: 'conv-1', items: [], page: 1, pageSize: 50, total: 0 });
    http
      .expectOne({ method: 'PUT', url: `${conversations}/conv-1/read` })
      .flush(null, { status: 204, statusText: 'No Content' });

    fixture.componentInstance.draft = 'thử';
    fixture.componentInstance.send();
    http
      .expectOne((r) => r.method === 'POST' && r.url === `${conversations}/conv-1`)
      .flush(
        { message: 'Tin nhắn quá dài.' },
        { status: 422, statusText: 'Unprocessable Content' },
      );
    expect(TestBed.inject(ToastService).toasts()[0].message).toContain('quá dài');

    TestBed.inject(ToastService).clear();
    fixture.componentInstance.send();
    http
      .expectOne((r) => r.method === 'POST' && r.url === `${conversations}/conv-1`)
      .flush({ message: 'private detail' }, { status: 503, statusText: 'Unavailable' });
    // A 5xx body never reaches the screen.
    expect(TestBed.inject(ToastService).toasts()[0].message).not.toContain('private detail');
    expect(fixture.componentInstance.sending()).toBe(false);
  });

  it('closes a thread that has become unreachable', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = init();
    fixture.componentInstance.openConversation('conv-1');
    http
      .expectOne((r) => r.url === `${conversations}/conv-1` && r.method === 'GET')
      .flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.openId()).toBeNull();
    expect(fixture.componentInstance.messages()).toEqual([]);
  });
});
