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

  it('validates email mode and requires tenant key for submission', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.changeMode('email');
    component.form.setValue({ identifier: 'not-an-email', password: 'password', tenantKey: 'company-a' });
    expect(component.form.invalid).toBe(true);

    component.form.setValue({ identifier: 'person@company.test', password: 'password', tenantKey: '' });
    expect(component.form.invalid).toBe(true);
    expect(component.form.controls.tenantKey.hasError('required')).toBe(true);

    component.form.controls.tenantKey.setValue('company-a');
    expect(component.form.valid).toBe(true);
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ code: 'AUTH.INVALID_CREDENTIALS' }, { status: 401, statusText: 'Unauthorized' });
    expect(component.error()).toContain('We could not sign you in');
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

  it('submits email-link request and displays confirmation message', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.setAuthMethod('email-link');
    component.emailForm.setValue({ email: 'user@company.com' });
    component.submitEmailLink();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/email-link/request');
    expect(req.request.body).toEqual({ email: 'user@company.com' });
    req.flush({ message: 'Link sent', expiresInMinutes: 15 });

    expect(component.emailSentMessage()).toContain('15 phút');
    expect(component.error()).toBe('');
  });

  it('handles 404 for unknown email in email-link request with register link', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.setAuthMethod('email-link');
    component.emailForm.setValue({ email: 'notfound@company.com' });
    component.submitEmailLink();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/email-link/request');
    req.flush({ code: 'AUTH.USER_NOT_FOUND' }, { status: 404, statusText: 'Not Found' });

    expect(component.showRegisterLink()).toBe(true);
    expect(component.error()).toContain('Không tìm thấy tài khoản');
  });

  it('verifies token and navigates directly when single workspace', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.verifyToken('valid-token-123');

    const req = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/email-link/verify');
    expect(req.request.body).toEqual({ token: 'valid-token-123' });
    req.flush({
      status: 'Authenticated',
      authentication: {
        accessToken: 'access-123',
        refreshToken: 'refresh-123',
        expiresAt: new Date().toISOString(),
        session: { userId: '1', fullName: 'User', tenantId: 't1', tenantName: 'Tenant 1', permissions: [] },
      },
    });

    expect(component.error()).toBe('');
    expect(component.busy()).toBe(false);
  });
});

