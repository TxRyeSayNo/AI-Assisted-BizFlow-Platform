import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CompanyRegistrations } from './company-registrations';

describe('platform registration queue', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne(
      (r) => r.url === '/api/v1/platform/company-registrations',
    );

  it('renders loading and useful empty states', () => {
    const fixture = TestBed.createComponent(CompanyRegistrations);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading company registrations');
    const request = pending();
    expect(request.request.params.get('status')).toBe('PENDING');
    request.flush({ items: [], total: 0, page: 1, pageSize: 25 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No company registrations match');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    expect(buttons.find((b) => b.textContent?.includes('Next'))?.disabled).toBe(true);
  });

  it('clears stale company data and shows permission denial without provider text', () => {
    const fixture = TestBed.createComponent(CompanyRegistrations);
    pending().flush({
      items: [
        {
          companyId: 'one',
          name: 'Sensitive company',
          code: 'ONE',
          contactEmail: 'contact@example.test',
          status: 'PENDING',
          createdAt: '2026-09-30T00:00:00Z',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 25,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sensitive company');
    fixture.componentInstance.load(1);
    pending().flush({ message: 'internal-secret' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have permission');
    expect(fixture.nativeElement.textContent).not.toContain('Sensitive company');
    expect(fixture.nativeElement.textContent).not.toContain('internal-secret');
  });

  it('retries a failed page and resets pagination when filters are applied', () => {
    const component = TestBed.createComponent(CompanyRegistrations).componentInstance;
    pending().flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.error()).toBe(true);
    component.load(2);
    expect(pending().request.params.get('page')).toBe('2');
    // Starting a new filter cancels the pending old page, preventing stale results from replacing it.
    component.filters.setValue({ status: 'ACTIVE', search: ' Example ' });
    component.apply();
    const request = pending();
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('search')).toBe('Example');
    expect(request.request.params.get('status')).toBe('ACTIVE');
    request.flush({ items: [], total: 0, page: 1, pageSize: 25 });
    expect(component.error()).toBe(false);
  });
});
