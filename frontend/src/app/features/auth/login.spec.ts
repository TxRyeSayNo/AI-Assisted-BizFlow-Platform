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

  it('allows Super Admin to login without entering a tenant key', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.form.controls.identifier.setValue('SUPERADMIN');
    expect(component.isSuperAdmin()).toBe(true);
    expect(component.form.controls.tenantKey.hasError('required')).toBe(false);

    component.form.controls.password.setValue('Password123!');
    component.form.controls.tenantKey.setValue('');
    expect(component.form.valid).toBe(true);

    component.submit();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/login');
    expect(request.request.body).toEqual({
      identifier: 'SUPERADMIN',
      password: 'Password123!',
    });
    request.flush({
      accessToken: 'token',
      refreshToken: 'refresh',
      expiresAt: new Date().toISOString(),
      session: {
        userId: 'super-1',
        fullName: 'Platform Administrator',
        tenantId: null,
        tenantName: null,
        permissions: ['platform.company-registrations.read'],
      },
    });
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

  it('validates email mode, requires tenant key, and handles TENANT_CONTEXT_REQUIRED', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.changeMode('email');
    component.form.setValue({ identifier: 'not-an-email', password: 'password', tenantKey: '' });
    expect(component.form.invalid).toBe(true);
    expect(component.form.controls.tenantKey.hasError('required')).toBe(true);

    component.form.controls.identifier.setValue('person@company.test');
    component.form.controls.tenantKey.setValue('company-a');
    component.submit();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/auth/login')
      .flush({ code: 'AUTH.TENANT_CONTEXT_REQUIRED' }, { status: 409, statusText: 'Conflict' });
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
        session: {
          userId: '1',
          fullName: 'User',
          tenantId: 't1',
          tenantName: 'Tenant 1',
          permissions: [],
        },
      },
    });

    expect(component.error()).toBe('');
    expect(component.busy()).toBe(false);
  });

  it('verifies firebase id token and authenticates user', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.verifyFirebaseToken('firebase-token-abc');

    const req = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/auth/email-link/verify-firebase',
    );
    expect(req.request.body).toEqual({ idToken: 'firebase-token-abc' });
    req.flush({
      status: 'Authenticated',
      authentication: {
        accessToken: 'access-bizflow',
        refreshToken: 'refresh-bizflow',
        expiresAt: new Date().toISOString(),
        session: {
          userId: '2',
          fullName: 'Firebase User',
          tenantId: 't2',
          tenantName: 'Tenant 2',
          permissions: [],
        },
      },
    });

    expect(component.error()).toBe('');
    expect(component.busy()).toBe(false);
  });

  it('handles invalid firebase id token error', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.verifyFirebaseToken('invalid-token');

    const req = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/auth/email-link/verify-firebase',
    );
    req.flush({ message: 'Invalid token' }, { status: 401, statusText: 'Unauthorized' });

    expect(component.error()).toContain('Mã xác thực Firebase không hợp lệ');
    expect(component.busy()).toBe(false);
  });

  it('handles NoWorkspace in firebase token verification', () => {
    const component = TestBed.createComponent(Login).componentInstance;
    component.verifyFirebaseToken('no-ws-token');

    const req = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/auth/email-link/verify-firebase',
    );
    req.flush({
      status: 'NoWorkspace',
      message: 'Không tìm thấy không gian làm việc.',
    });

    expect(component.showRegisterLink()).toBe(true);
    expect(component.error()).toContain('Không tìm thấy không gian làm việc');
  });
});
