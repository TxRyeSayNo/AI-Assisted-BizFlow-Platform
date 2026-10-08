import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Requests, RequestRow } from './requests';

describe('scoped request list', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const pendingRequests = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/requests');
  const pendingServices = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/services');

  const empty = { items: [], total: 0, page: 1, pageSize: 25 };
  const mockRow: RequestRow = {
    requestId: '01a12000-0000-7000-8000-000000000099',
    title: 'Workstation Setup',
    status: 'IN_PROGRESS',
    priority: 'HIGH',
    serviceId: '01a12000-0000-7000-8000-000000000001',
    serviceName: 'IT Support',
    categoryId: '01a12000-0000-7000-8000-000000000002',
    categoryName: 'Hardware',
    requesterId: '01a12000-0000-7000-8000-000000000003',
    requesterName: 'Alice Developer',
    createdAt: '2026-10-07T12:00:00Z',
    updatedAt: '2026-10-07T12:00:00Z',
  };

  it('loads requests and services catalog on init and renders empty state', () => {
    const fixture = TestBed.createComponent(Requests);
    fixture.detectChanges();

    const reqCall = pendingRequests();
    expect(reqCall.request.params.get('page')).toBe('1');
    expect(reqCall.request.params.get('pageSize')).toBe('25');

    const svcCall = pendingServices();
    svcCall.flush({ items: [{ serviceId: 's1', name: 'IT Support', status: 'ACTIVE' }] });
    reqCall.flush(empty);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No requests match your access scope');
  });

  it('renders request rows with status badges and metadata', () => {
    const fixture = TestBed.createComponent(Requests);
    fixture.detectChanges();

    pendingServices().flush({ items: [] });
    pendingRequests().flush({ items: [mockRow], total: 1, page: 1, pageSize: 25 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Workstation Setup');
    expect(fixture.nativeElement.textContent).toContain('IT Support');
    expect(fixture.nativeElement.textContent).toContain('Hardware');
    expect(fixture.nativeElement.textContent).toContain('Alice Developer');
    expect(fixture.nativeElement.textContent).toContain('In Progress');
    expect(fixture.nativeElement.textContent).toContain('High');
  });

  it('applies filters and preserves query parameters', () => {
    const fixture = TestBed.createComponent(Requests);
    fixture.detectChanges();

    pendingServices().flush({ items: [] });
    pendingRequests().flush(empty);

    fixture.componentInstance.filters.setValue({
      search: 'Monitor',
      status: 'SUBMITTED',
      priority: 'CRITICAL',
      serviceId: 's1',
    });
    fixture.componentInstance.apply();

    const filteredCall = pendingRequests();
    expect(filteredCall.request.params.get('search')).toBe('Monitor');
    expect(filteredCall.request.params.get('status')).toBe('SUBMITTED');
    expect(filteredCall.request.params.get('priority')).toBe('CRITICAL');
    expect(filteredCall.request.params.get('serviceId')).toBe('s1');
    filteredCall.flush(empty);
  });

  it('handles authorization denial gracefully without revealing private info', () => {
    const fixture = TestBed.createComponent(Requests);
    fixture.detectChanges();

    pendingServices().flush({ items: [] });
    pendingRequests().flush(
      { message: 'Internal confidential info' },
      { status: 403, statusText: 'Forbidden' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'You do not have permission to view requests in this scope.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('Internal confidential info');
  });
});
