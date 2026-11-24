import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ServiceRequestForm } from './service-request-form';

describe('service-specific request form', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('requires typed input and rejects out-of-range amounts before submitting', () => {
    const fixture = TestBed.createComponent(ServiceRequestForm);
    fixture.componentRef.setInput('serviceId', 'service');
    const validity = vi.fn();
    fixture.componentInstance.validChanged.subscribe(validity);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/services/service/form')
      .flush([
        { key: 'amount', label: 'Dự toán', type: 'number', required: true, min: 0, max: 1000 },
      ]);
    expect(validity).toHaveBeenLastCalledWith(false);
    fixture.componentInstance.update('amount', -1);
    expect(validity).toHaveBeenLastCalledWith(false);
    fixture.componentInstance.update('amount', 125);
    expect(validity).toHaveBeenLastCalledWith(true);
  });
  it('does not silently permit submission when the schema cannot be loaded', () => {
    const fixture = TestBed.createComponent(ServiceRequestForm);
    fixture.componentRef.setInput('serviceId', 'service');
    const validity = vi.fn();
    fixture.componentInstance.validChanged.subscribe(validity);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/services/service/form')
      .flush({}, { status: 503, statusText: 'Unavailable' });
    expect(validity).toHaveBeenLastCalledWith(false);
    expect(fixture.componentInstance.error()).not.toBe('');
  });
});
