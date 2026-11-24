import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Location } from '@angular/common';
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

  it('opens a signed correction link, preserves company identity and submits the token with corrections', () => {
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: { snapshot: { fragment: 'resubmit=signed-link' } },
    });
    const replace = vi.spyOn(TestBed.inject(Location), 'replaceState');
    const fixture = TestBed.createComponent(RegisterCompany);
    const component = fixture.componentInstance;
    component.ngOnInit();
    const http = TestBed.inject(HttpTestingController);
    const resolve = http.expectOne('/api/v1/company-registrations/resubmission');
    expect(resolve.request.body).toEqual({ token: 'signed-link' });
    resolve.flush({
      companyId: 'company',
      code: 'ACME',
      name: 'Acme',
      taxId: '0101234567',
      contactPersonName: 'Owner',
      contactEmail: 'owner@example.test',
      contactPhone: '0901234567',
      status: 'INACTIVE',
      evidenceDocumentKey: 'old-document',
      evidenceDocumentFileName: 'old.pdf',
    });
    expect(replace).toHaveBeenCalledWith('/register-company');
    expect(component.form.controls.code.disabled).toBe(true);
    expect(component.form.controls.contactEmail.disabled).toBe(true);
    component.form.controls.contactPhone.setValue('0912345678');
    component.submit();
    const request = http.expectOne('/api/v1/company-registrations');
    expect(request.request.body.resubmissionToken).toBe('signed-link');
    expect(request.request.body.code).toBe('ACME');
    expect(request.request.body.contactPhone).toBe('0912345678');
    expect(request.request.body.evidenceDocumentKey).toBe('old-document');
    request.flush({ status: 'PENDING' });
    expect(component.submittedResult()?.status).toBe('PENDING');
  });

  it('does not submit when the correction link is invalid or consumed', () => {
    TestBed.overrideProvider(ActivatedRoute, {
      useValue: { snapshot: { fragment: 'resubmit=expired' } },
    });
    const component = TestBed.createComponent(RegisterCompany).componentInstance;
    component.ngOnInit();
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne('/api/v1/company-registrations/resubmission')
      .flush({}, { status: 404, statusText: 'Not Found' });
    component.submit();
    http.expectNone('/api/v1/company-registrations');
    expect(component.form.disabled).toBe(true);
    expect(component.error()).toContain('Liên kết bổ sung');
  });

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
