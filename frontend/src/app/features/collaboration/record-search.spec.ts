import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { RecordSearch, RecordSearchPagedResult } from './record-search';

describe('RecordSearch component', () => {
  let fixture: ComponentFixture<RecordSearch>;
  let component: RecordSearch;
  let httpMock: HttpTestingController;

  const mockPagedResult: RecordSearchPagedResult = {
    items: [
      {
        id: '019f7f8a-0000-7000-8000-000000000010',
        recordType: 'TASK',
        title: 'Complete ISO 27001 Audit',
        description: 'Prepare deliverable evidence documents',
        status: 'COMPLETED',
        priority: 'HIGH',
        creatorOrRequesterId: '019f7f8a-0000-7000-8000-000000000001',
        creatorOrRequesterName: 'Alice Auditor',
        createdAt: '2026-10-07T10:00:00Z',
        updatedAt: '2026-10-07T12:00:00Z',
        deletedAt: null,
        isArchived: false,
      },
      {
        id: '019f7f8a-0000-7000-8000-000000000020',
        recordType: 'REQUEST',
        title: 'New Hardware Procurement',
        description: 'Order high-spec workstation for engineering',
        status: 'CLOSED',
        priority: 'MEDIUM',
        creatorOrRequesterId: '019f7f8a-0000-7000-8000-000000000002',
        creatorOrRequesterName: 'Bob Developer',
        createdAt: '2026-10-07T09:00:00Z',
        updatedAt: '2026-10-07T11:00:00Z',
        deletedAt: '2026-10-07T13:00:00Z',
        isArchived: true,
      },
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecordSearch],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantName: 'Acme Corp', tenantId: 'tenant-1' }),
            hasPermission: () => true,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RecordSearch);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads and displays search results on init', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush(mockPagedResult);

    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Search Records');
    expect(el.textContent).toContain('2 records found');
    expect(el.textContent).toContain('Complete ISO 27001 Audit');
    expect(el.textContent).toContain('New Hardware Procurement');
    expect(el.textContent).toContain('TASK');
    expect(el.textContent).toContain('REQUEST');
    expect(el.textContent).toContain('Archived');
  });

  it('applies text query and type filter', () => {
    fixture.detectChanges();

    const initReq = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    initReq.flush(mockPagedResult);
    fixture.detectChanges();

    component.filters.patchValue({
      query: 'Audit',
      type: 'TASK',
      status: 'COMPLETED',
      priority: 'HIGH',
      includeArchived: false,
    });

    component.applyFilters();
    fixture.detectChanges();

    // After navigate, the subscription triggers executeSearch with query params
    const searchReq = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    expect(searchReq.request.params.get('q')).toBe('Audit');
    expect(searchReq.request.params.get('type')).toBe('TASK');
    expect(searchReq.request.params.get('status')).toBe('COMPLETED');
    expect(searchReq.request.params.get('priority')).toBe('HIGH');
    searchReq.flush({
      items: [mockPagedResult.items[0]],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('1 record found');
    expect(el.textContent).toContain('Complete ISO 27001 Audit');
  });

  it('toggles includeArchived in search request', () => {
    fixture.detectChanges();

    const initReq = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    initReq.flush(mockPagedResult);
    fixture.detectChanges();

    component.filters.patchValue({
      includeArchived: true,
    });

    component.applyFilters();
    fixture.detectChanges();

    const searchReq = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    expect(searchReq.request.params.get('includeArchived')).toBe('true');
    searchReq.flush(mockPagedResult);
  });

  it('handles empty results state', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/v1/records/search');
    req.flush({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    });

    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No records matched your search criteria.');
  });
});
