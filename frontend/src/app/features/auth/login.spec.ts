import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Login } from './login';

describe('Login', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('does not send an invalid form', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.submit();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/auth/login');
  });

  it('preserves password whitespace and sends an explicit tenant key', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.setValue({
      identifier: ' EMP001 ',
      password: ' password ',
      tenantKey: 'company-a',
    });
    component.submit();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/login');
    expect(request.request.body).toEqual({
      identifier: 'EMP001',
      password: ' password ',
      tenantKey: 'company-a',
    });
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(component.form.controls.password.value).toBe('');
    expect(component.busy()).toBe(false);
  });

  it('validates email mode and requires tenant key when the API requests context', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.changeMode('email');
    component.form.setValue({ identifier: 'not-an-email', password: 'password', tenantKey: '' });
    expect(component.form.invalid).toBe(true);
    component.form.controls.identifier.setValue('person@company.test');
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ code: 'AUTH.TENANT_CONTEXT_REQUIRED' }, { status: 409, statusText: 'Conflict' });
    expect(component.form.controls.tenantKey.hasError('required')).toBe(true);
    expect(component.error()).toContain('workspace key');
  });

  it('does not render arbitrary backend error messages', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.setValue({ identifier: 'EMP001', password: 'password', tenantKey: '' });
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ message: 'sensitive-internal-error' }, { status: 500, statusText: 'Error' });
    expect(component.error()).toContain('temporarily unavailable');
    expect(component.error()).not.toContain('sensitive-internal-error');
  });
});
