import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RegisterCompany } from './register-company';

describe('RegisterCompany', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [RegisterCompany],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: class {} }]),
      ],
    }),
  );

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('initializes form with 6 mandatory fields and rejects submission when empty', () => {
    const fixture = TestBed.createComponent(RegisterCompany);
    const component = fixture.componentInstance;
    expect(component.form.invalid).toBe(true);
    expect(component.form.controls.taxId.hasError('required')).toBe(true);
    expect(component.form.controls.name.hasError('required')).toBe(true);
    expect(component.form.controls.code.hasError('required')).toBe(true);
    expect(component.form.controls.contactPersonName.hasError('required')).toBe(true);
    expect(component.form.controls.contactEmail.hasError('required')).toBe(true);
    expect(component.form.controls.contactPhone.hasError('required')).toBe(true);

    component.submit();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/company-registrations');
  });

  it('submits all 6 fields and evidence document metadata when form is valid', () => {
    const fixture = TestBed.createComponent(RegisterCompany);
    const component = fixture.componentInstance;

    component.form.setValue({
      taxId: '0101234567',
      name: 'Acme Logistics Corp.',
      code: 'acme-logistics',
      contactPersonName: 'Nguyen Van A',
      contactEmail: 'contact@acme.test',
      contactPhone: '0901234567',
    });

    component.submit();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/company-registrations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      code: 'ACME-LOGISTICS',
      name: 'Acme Logistics Corp.',
      taxId: '0101234567',
      contactPersonName: 'Nguyen Van A',
      contactEmail: 'contact@acme.test',
      contactPhone: '0901234567',
      evidenceDocumentKey: null,
      evidenceDocumentFileName: null,
    });

    req.flush({
      companyId: '019f7a64-0000-7000-8000-000000000001',
      code: 'ACME-LOGISTICS',
      name: 'Acme Logistics Corp.',
      taxId: '0101234567',
      contactPersonName: 'Nguyen Van A',
      contactEmail: 'contact@acme.test',
      contactPhone: '0901234567',
      status: 'PENDING',
      createdAt: '2026-10-21T00:00:00Z',
    });

    expect(component.submittedResult()?.status).toBe('PENDING');
    expect(component.submitting()).toBe(false);
  });

  it('calls generate-code endpoint and populates the workspace code field', () => {
    const fixture = TestBed.createComponent(RegisterCompany);
    const component = fixture.componentInstance;

    component.form.controls.name.setValue('Global Transport');
    component.generateRandomCode();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/tenants/generate-code');
    expect(req.request.body).toEqual({ baseName: 'Global Transport' });
    req.flush({ code: 'global-transport-4821' });

    expect(component.form.controls.code.value).toBe('global-transport-4821');
    expect(component.codeAvailable()).toBe(true);
  });
});
