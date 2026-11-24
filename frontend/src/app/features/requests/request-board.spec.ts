import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';
import { RequestBoard, RequestBoardCard, RequestBoardView } from './request-board';

describe('request board', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: { hasPermission: () => true, identity: () => ({ tenantName: 'Acme Corp' }) },
        },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/requests/board');

  const card = (over: Partial<RequestBoardCard> = {}): RequestBoardCard => ({
    requestId: 'req-1',
    title: 'Hồ sơ nháp A',
    status: 'DRAFT',
    priority: 'HIGH',
    serviceId: 'srv-1',
    serviceName: 'Dịch vụ hạ tầng',
    categoryId: 'cat-1',
    categoryName: 'Máy chủ',
    requesterId: 'usr-1',
    requesterName: 'Nguyễn A',
    createdAt: '2026-11-01T00:00:00Z',
    updatedAt: '2026-11-02T00:00:00Z',
    ...over,
  });

  const view = (items: RequestBoardCard[]): RequestBoardView => ({
    columnSize: 50,
    columns: [
      'DRAFT',
      'SUBMITTED',
      'ROUTED',
      'RECEIVED',
      'IN_PROGRESS',
      'RESOLVED',
      'CONFIRMED',
      'CLOSED',
      'WAITING_FOR_INFORMATION',
      'REJECTED',
      'CANCELLED',
      'OVERDUE',
    ].map((status) => ({
      status: status as RequestBoardCard['status'],
      items: items.filter((item) => item.status === status),
      total: items.filter((item) => item.status === status).length,
    })),
  });

  // A drop event carries only what the component reads from it.
  const dropEvent = (moved: RequestBoardCard) =>
    ({
      previousContainer: { id: 'from' },
      container: { id: 'to' },
      item: { data: moved },
    }) as never;

  it('renders one lane per request state with a drag handle on every card', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    fixture.detectChanges();
    const request = pending();
    expect(request.request.params.keys()).toEqual([]);
    request.flush(
      view([card(), card({ requestId: 'req-2', status: 'SUBMITTED', title: 'Đã gửi' })]),
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.board-lane').length).toBe(12);
    expect(fixture.nativeElement.textContent).toContain('Hồ sơ nháp A');
    expect(fixture.nativeElement.textContent).toContain('Đã gửi');
    expect(fixture.nativeElement.textContent).toContain('Dịch vụ hạ tầng');
    expect(fixture.nativeElement.querySelectorAll('.card-drag-handle').length).toBe(2);
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
  });

  it('replays only the two request transitions that need no extra information', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    pending().flush(view([]));

    // These two are payload-free, so a dropped card can complete them on its own.
    expect(fixture.componentInstance.canMove('ROUTED', 'RECEIVED')).toBe(true);
    expect(fixture.componentInstance.canMove('RECEIVED', 'IN_PROGRESS')).toBe(true);
    // Everything else needs a target, a reason or a resolution.
    for (const [from, to] of [
      ['SUBMITTED', 'ROUTED'],
      ['IN_PROGRESS', 'RESOLVED'],
      ['RESOLVED', 'CONFIRMED'],
      ['SUBMITTED', 'REJECTED'],
      ['ROUTED', 'CANCELLED'],
      ['CONFIRMED', 'CLOSED'],
      ['RECEIVED', 'OVERDUE'],
    ] as [RequestBoardCard['status'], string][])
      expect(fixture.componentInstance.canMove(from, to)).toBe(false);
  });

  it('posts the matching endpoint on an allowed drop and reloads the board', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    pending().flush(view([card({ status: 'ROUTED' })]));

    fixture.componentInstance.drop(dropEvent(card({ status: 'ROUTED' })), 'RECEIVED');

    const posted = TestBed.inject(HttpTestingController).expectOne(
      (r) => r.method === 'POST' && r.url === '/api/v1/requests/req-1/receive',
    );
    // A retried transition after an uncertain response must not apply twice.
    expect(posted.request.headers.get('Idempotency-Key')).toBeTruthy();
    posted.flush({});
    pending().flush(view([card({ status: 'RECEIVED' })]));

    expect(TestBed.inject(ToastService).toasts()).toEqual([]);
  });

  it('refuses a drop that would need information the card cannot supply', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    pending().flush(view([card({ status: 'IN_PROGRESS' })]));

    fixture.componentInstance.drop(dropEvent(card({ status: 'IN_PROGRESS' })), 'RESOLVED');

    // Nothing is sent, and the person is told to open the request instead.
    TestBed.inject(HttpTestingController).verify();
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    expect(toasts[0].message).toContain('mở yêu cầu');
  });

  it('sends applied filters and keeps requester choices found on the board', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    pending().flush(view([card({ requesterId: 'usr-9', requesterName: 'Trần B' })]));
    fixture.detectChanges();
    expect(fixture.componentInstance.requesterOptions()).toEqual([['usr-9', 'Trần B']]);

    fixture.componentInstance.filters.setValue({
      search: ' nháp ',
      priority: 'HIGH',
      requesterId: 'usr-9',
    });
    fixture.componentInstance.apply();

    const filtered = pending();
    expect(filtered.request.params.get('search')).toBe('nháp');
    expect(filtered.request.params.get('priority')).toBe('HIGH');
    expect(filtered.request.params.get('requesterId')).toBe('usr-9');
    filtered.flush(view([]));
    expect(fixture.componentInstance.requesterOptions()).toEqual([['usr-9', 'Trần B']]);
  });

  it('separates a disabled feature from a denial and from a transient failure', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    pending().flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(fixture.componentInstance.unavailable()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('not enabled on this deployment');

    fixture.componentInstance.load();
    pending().flush({ message: 'private detail' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.componentInstance.denied()).toBe(true);
    expect(fixture.nativeElement.textContent).not.toContain('private detail');

    fixture.componentInstance.load();
    pending().flush({ message: 'private detail' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.componentInstance.error()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('could not be loaded');
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
  });

  it('cancels an outdated board read when the view is destroyed', () => {
    const fixture = TestBed.createComponent(RequestBoard);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
