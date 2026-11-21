import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
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

  it('renders one lane per request state and never offers a drag handle', () => {
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
    // Read-only board: no drag handles anywhere.
    expect(fixture.nativeElement.querySelector('.card-drag-handle')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.board-card.is-readonly').length).toBe(2);
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
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
