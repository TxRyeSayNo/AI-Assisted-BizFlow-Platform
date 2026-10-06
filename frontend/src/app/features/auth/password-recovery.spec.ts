import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { ForgotPassword } from './forgot-password';
import { ResetPassword } from './reset-password';

describe('password recovery', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: 'token=test-reset-token' } } },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('does not disclose account existence in the forgot-password confirmation', () => {
    const component = TestBed.createComponent(ForgotPassword).componentInstance;
    component.form.setValue({ identifier: ' EMP1 ', tenantKey: 'company-a' });
    component.submit();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/forgot-password');
    expect(request.request.body.identifier).toBe('EMP1');
    request.flush({ message: 'untrusted server text' }, { status: 202, statusText: 'Accepted' });
    expect(component.sent()).toBe(true);
    expect(component.error()).toBe('');
  });

  it('removes the token from history and prevents mismatched passwords from being sent', () => {
    const replace = vi.spyOn(TestBed.inject(Location), 'replaceState');
    const component = TestBed.createComponent(ResetPassword).componentInstance;
    expect(replace).toHaveBeenCalledWith('/reset-password');
    component.form.setValue({
      identifier: 'EMP1',
      tenantKey: 'company-a',
      newPassword: 'Long-password!123',
      confirmPassword: 'Different-password!123',
    });
    component.submit();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/auth/reset-password');
    expect(component.error()).toContain('do not match');
  });

  it('preserves password whitespace, clears it after failure, and rejects arbitrary error text', () => {
    const component = TestBed.createComponent(ResetPassword).componentInstance;
    const password = ' Long-password!123 ';
    component.form.setValue({
      identifier: ' EMP1 ',
      tenantKey: '',
      newPassword: password,
      confirmPassword: password,
    });
    component.submit();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/auth/reset-password');
    expect(request.request.body).toEqual({
      identifier: 'EMP1',
      resetToken: 'test-reset-token',
      newPassword: password,
    });
    request.flush({ message: 'internal-secret' }, { status: 401, statusText: 'Unauthorized' });
    expect(component.form.controls.newPassword.value).toBe('');
    expect(component.error()).toContain('invalid or expired');
    expect(component.error()).not.toContain('internal-secret');
  });
});
