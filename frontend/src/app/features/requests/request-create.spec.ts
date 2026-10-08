import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { RequestCreate } from './request-create';

describe('request creation component', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'requests/:id', component: RequestCreate }]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
      ],
    }),
  );

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const mockServices = [
    {
      serviceId: 'srv-1',
      name: 'IT Services',
      status: 'ACTIVE',
      categories: [
        { serviceCategoryId: 'cat-1', name: 'Hardware', status: 'ACTIVE' },
        { serviceCategoryId: 'cat-2', name: 'Software', status: 'ACTIVE' },
        { serviceCategoryId: 'cat-3', name: 'Legacy Deprecated', status: 'INACTIVE' },
      ],
    },
    {
      serviceId: 'srv-2',
      name: 'HR Requests',
      status: 'ACTIVE',
      categories: [{ serviceCategoryId: 'cat-4', name: 'Benefits', status: 'ACTIVE' }],
    },
  ];

  it('loads active services and populates active categories dynamically', () => {
    const fixture = TestBed.createComponent(RequestCreate);
    fixture.detectChanges();

    const catalogReq = TestBed.inject(HttpTestingController).expectOne(
      (r) => r.url === '/api/v1/services',
    );
    catalogReq.flush({ items: mockServices });
    fixture.detectChanges();

    expect(fixture.componentInstance.services().length).toBe(2);
    expect(fixture.componentInstance.availableCategories().length).toBe(2); // Only ACTIVE categories
    expect(fixture.componentInstance.form.controls.serviceId.value).toBe('srv-1');
    expect(fixture.componentInstance.form.controls.categoryId.value).toBe('cat-1');

    // Switch service to HR
    fixture.componentInstance.form.controls.serviceId.setValue('srv-2');
    expect(fixture.componentInstance.availableCategories().length).toBe(1);
    expect(fixture.componentInstance.form.controls.categoryId.value).toBe('cat-4');
  });

  it('submits valid request with idempotency key and navigates to detail', () => {
    const fixture = TestBed.createComponent(RequestCreate);
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/v1/services')
      .flush({ items: mockServices });

    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.form.patchValue({
      title: 'Need new laptop monitor',
      description: 'Dual monitor desk setup',
      priority: 'HIGH',
      submitImmediately: true,
    });

    fixture.componentInstance.save();

    const postReq = TestBed.inject(HttpTestingController).expectOne('/api/v1/requests');
    expect(postReq.request.headers.has('Idempotency-Key')).toBe(true);
    expect(postReq.request.body).toEqual({
      serviceId: 'srv-1',
      categoryId: 'cat-1',
      title: 'Need new laptop monitor',
      description: 'Dual monitor desk setup',
      priority: 'HIGH',
      submitImmediately: true,
    });

    postReq.flush({ requestId: '01a12000-0000-7000-8000-000000000010' });
    expect(navSpy).toHaveBeenCalledWith(['/requests', '01a12000-0000-7000-8000-000000000010']);
    expect(fixture.componentInstance.done()).toBe(true);
  });

  it('handles validation error and allows user to retry with new idempotency key', () => {
    const fixture = TestBed.createComponent(RequestCreate);
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/v1/services')
      .flush({ items: mockServices });

    fixture.componentInstance.form.patchValue({
      title: 'Valid title',
    });

    fixture.componentInstance.save();

    const postReq = TestBed.inject(HttpTestingController).expectOne('/api/v1/requests');
    const firstKey = postReq.request.headers.get('Idempotency-Key');

    postReq.flush(
      { message: 'Category is inactive' },
      { status: 422, statusText: 'Unprocessable' },
    );
    fixture.detectChanges();

    expect(fixture.componentInstance.form.enabled).toBe(true);
    expect(fixture.componentInstance.error()).toBe('Category is inactive');

    // Retry
    fixture.componentInstance.save();
    const retryReq = TestBed.inject(HttpTestingController).expectOne('/api/v1/requests');
    const secondKey = retryReq.request.headers.get('Idempotency-Key');
    expect(secondKey).not.toBe(firstKey);
    retryReq.flush({ requestId: '01a12000-0000-7000-8000-000000000020' });
  });

  it('prevents submission when permission is revoked', () => {
    const fixture = TestBed.createComponent(RequestCreate);
    vi.spyOn(fixture.componentInstance.session, 'hasPermission').mockReturnValue(false);
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/v1/services')
      .flush({ items: mockServices });

    fixture.componentInstance.form.patchValue({ title: 'New Request' });
    fixture.componentInstance.save();

    TestBed.inject(HttpTestingController).expectNone('/api/v1/requests');
  });
});
