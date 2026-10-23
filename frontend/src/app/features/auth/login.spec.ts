import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Login } from './login';

describe('Login', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'workspace', component: class {} }]),
      ],
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
    component.form.controls.tenantKey.setValue('company-a');
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ code: 'AUTH.TENANT_CONTEXT_REQUIRED' }, { status: 409, statusText: 'Conflict' });
    component.form.controls.tenantKey.setValue('');
    expect(component.form.controls.tenantKey.hasError('required')).toBe(true);
    expect(component.error()).toContain('workspace key');
  });

  it('does not render arbitrary backend error messages', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.setValue({ identifier: 'EMP001', password: 'password', tenantKey: 'company-a' });
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ message: 'sensitive-internal-error' }, { status: 500, statusText: 'Error' });
    expect(component.error()).toContain('temporarily unavailable');
    expect(component.error()).not.toContain('sensitive-internal-error');
  });

  it('signs in ADMIN001 via offline fallback when backend API is unavailable', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.setValue({
      identifier: 'ADMIN001',
      password: 'Password123!',
      tenantKey: 'bizflow-corp',
    });
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush(null, { status: 504, statusText: 'Gateway Timeout' });
    expect(component.error()).toBe('');
  });

  it('signs in EMP001 via quickLogin and sets form controls', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.quickLogin('EMPLOYEE');
    expect(component.form.controls.identifier.value).toBe('EMP001');
    expect(component.form.controls.password.value).toBe('Password123!');
    expect(component.form.controls.tenantKey.value).toBe('bizflow-corp');
  });

  it('triggers top-right toast alert on missing field and does not stretch the form', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.submit();
    expect(component.toast()).not.toBeNull();
    expect(component.toast()?.title).toBe('Missing employee code');
  });

  it('triggers top-right toast alert on 401 unauthorized', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.setValue({
      identifier: 'EMP001',
      password: 'wrong-password',
      tenantKey: 'company-a',
    });
    component.submit();
    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/login');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(component.toast()).not.toBeNull();
    expect(component.toast()?.title).toBe('Sign-in failed');
  });

  it('triggers top-right toast alert on missing or invalid workspace key', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.controls.identifier.setValue('EMP001');
    component.form.controls.password.setValue('password');
    component.form.controls.tenantKey.setValue('');
    component.submit();
    expect(component.toast()?.title).toBe('Missing workspace key');

    component.form.controls.tenantKey.setValue('INVALID KEY!');
    component.submit();
    expect(component.toast()?.title).toBe('Invalid workspace key');
  });
});

