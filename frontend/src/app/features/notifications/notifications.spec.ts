import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Notifications, NotificationRow } from './notifications';

describe('recipient notification inbox', () => {
  const row: NotificationRow = {
    notificationId: 'notice',
    type: 'TaskAssigned',
    objectType: null,
    objectId: null,
    title: '<script>untrusted</script>',
    content: '<img src=x onerror=alert(1)>',
    readAt: null,
    sentAt: null,
  };
  const data = { items: [row], page: 1, pageSize: 25, total: 1, unreadCount: 1 };
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const list = () =>
    TestBed.inject(HttpTestingController).expectOne(
      (request) => request.url === '/api/v1/notifications',
    );

  it('renders loading, escaped event text and unread count without inventing email or creation timestamps', () => {
    const fixture = TestBed.createComponent(Notifications);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading notifications');
    list().flush(data);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(row.title);
    expect(fixture.nativeElement.textContent).toContain(row.content);
    expect(fixture.nativeElement.querySelector('script,img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('1 unread notification');
  });
  it('marks read once while pending and refreshes authoritative counts with an empty command', () => {
    const fixture = TestBed.createComponent(Notifications);
    list().flush(data);
    fixture.componentInstance.markRead(row);
    fixture.componentInstance.markRead(row);
    const receipt = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/notifications/notice/read',
    );
    expect(receipt.request.method).toBe('PATCH');
    expect(receipt.request.body).toEqual({});
    receipt.flush({ ...row, readAt: '2026-10-02T00:00:00Z' });
    list().flush({ ...data, items: [{ ...row, readAt: '2026-10-02T00:00:00Z' }], unreadCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Notification marked read.');
    expect(fixture.nativeElement.textContent).toContain('0 unread notifications');
  });
  it('uses unread filtering, cancels stale reads and recovers an empty last page after marking read', () => {
    const fixture = TestBed.createComponent(Notifications);
    const stale = list();
    fixture.componentInstance.filter(true);
    expect(stale.cancelled).toBe(true);
    const filtered = list();
    expect(filtered.request.params.get('unreadOnly')).toBe('true');
    filtered.flush(data);
    fixture.componentInstance.load(2);
    list().flush({ ...data, page: 2, total: 26 });
    fixture.componentInstance.markRead(row);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/notifications/notice/read')
      .flush({ ...row, readAt: '2026-10-02T00:00:00Z' });
    list().flush({ ...data, items: [], page: 2, total: 25, unreadCount: 25 });
    const recovered = list();
    expect(recovered.request.params.get('page')).toBe('1');
    recovered.flush({ ...data, total: 25, unreadCount: 25 });
    expect(fixture.componentInstance.page()).toBe(1);
  });
  it('handles empty, failure and denial while clearing stale private content', () => {
    const fixture = TestBed.createComponent(Notifications);
    list().flush({ ...data, items: [], total: 0, unreadCount: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No notifications yet.');
    fixture.componentInstance.load(1);
    list().flush(data);
    fixture.componentInstance.load(1);
    list().flush({ message: 'private-error' }, { status: 403, statusText: 'Denied' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('untrusted');
    expect(fixture.nativeElement.textContent).not.toContain('private-error');
    fixture.componentInstance.load(1);
    list().flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('could not be loaded');
  });
  it('does not optimistically report a failed receipt as read', () => {
    const fixture = TestBed.createComponent(Notifications);
    list().flush(data);
    fixture.componentInstance.markRead(row);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/notifications/notice/read')
      .flush({ message: 'private-error' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.componentInstance.result()?.items[0].readAt).toBeNull();
    expect(fixture.componentInstance.reading()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('receipt could not be confirmed');
    expect(fixture.nativeElement.textContent).not.toContain('private-error');
  });
});
