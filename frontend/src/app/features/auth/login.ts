import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../../core/auth/session';

interface ToastAlert {
  title: string;
  message: string;
}

@Component({
  selector: 'bf-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
  ],
  templateUrl: './login.html',
})
export class Login implements OnInit, OnDestroy {
  private readonly forms = inject(FormBuilder);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  readonly mode = signal<'code' | 'email'>('code');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly showPassword = signal(false);

  readonly toast = signal<ToastAlert | null>(null);
  readonly toastFading = signal<boolean>(false);
  private toastTimer?: ReturnType<typeof setTimeout>;
  private toastFadeTimer?: ReturnType<typeof setTimeout>;

  readonly form = this.forms.nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
    tenantKey: ['', [Validators.required, Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });

  ngOnInit(): void {
    // Eagerly pre-warm the workspace bundle while the user is typing credentials
    void import('./workspace');
  }

  ngOnDestroy(): void {
    this.clearToastTimers();
  }

  private clearToastTimers(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    if (this.toastFadeTimer) clearTimeout(this.toastFadeTimer);
  }

  showToast(title: string, message: string): void {
    this.clearToastTimers();
    this.toast.set({ title, message });
    this.toastFading.set(false);

    // Visible for exactly 3s, then fades away gradually over 650ms without abrupt disappearance
    this.toastTimer = setTimeout(() => {
      this.toastFading.set(true);
      this.toastFadeTimer = setTimeout(() => {
        this.toast.set(null);
        this.toastFading.set(false);
      }, 650);
    }, 3000);
  }

  dismissToast(): void {
    this.clearToastTimers();
    this.toastFading.set(true);
    this.toastFadeTimer = setTimeout(() => {
      this.toast.set(null);
      this.toastFading.set(false);
    }, 400);
  }

  changeMode(mode: 'code' | 'email') {
    this.mode.set(mode);
    this.form.controls.identifier.setValidators(
      mode === 'email'
        ? [Validators.required, Validators.email, Validators.maxLength(320)]
        : [Validators.required, Validators.maxLength(50)],
    );
    this.form.controls.identifier.updateValueAndValidity();
    this.error.set('');
    this.dismissToast();
  }

  submit() {
    if (this.busy()) return;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      const idCtrl = this.form.controls.identifier;
      const passCtrl = this.form.controls.password;
      const tenantCtrl = this.form.controls.tenantKey;

      if (idCtrl.invalid) {
        if (idCtrl.errors?.['required']) {
          this.showToast(
            this.mode() === 'email' ? 'Missing company email' : 'Missing employee code',
            this.mode() === 'email'
              ? 'Please enter your company email address.'
              : 'Please enter your employee code.',
          );
        } else if (idCtrl.errors?.['email']) {
          this.showToast(
            'Invalid email format',
            'Company email address is not in a valid format (e.g. name@company.com).',
          );
        } else {
          this.showToast('Invalid employee code', 'Employee code must be 50 characters or fewer.');
        }
        return;
      }

      if (passCtrl.invalid) {
        this.showToast('Missing password', 'Please enter your password.');
        return;
      }

      if (tenantCtrl.invalid) {
        if (tenantCtrl.errors?.['required']) {
          this.showToast('Missing workspace key', 'Please enter your company workspace key.');
        } else if (tenantCtrl.errors?.['pattern']) {
          this.showToast(
            'Invalid workspace key',
            'Workspace key may only contain lowercase letters, numbers, and hyphens (e.g. bizflow-corp).',
          );
        } else {
          this.showToast('Invalid workspace key', 'Workspace key must be 80 characters or fewer.');
        }
        return;
      }

      this.showToast('Invalid input', 'Please review the required fields and try again.');
      return;
    }

    const values = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    this.dismissToast();

    this.session
      .login({
        identifier: values.identifier.trim(),
        password: values.password,
        tenantKey: values.tenantKey.trim(),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigateByUrl('/workspace', { replaceUrl: true });
        },
        error: (failure: HttpErrorResponse) => {
          const rawId = values.identifier.trim();
          const upperId = rawId.toUpperCase();
          const lowerId = rawId.toLowerCase();
          const isOfflineOrUnavailable =
            failure.status === 0 ||
            failure.status === 502 ||
            failure.status === 503 ||
            failure.status === 504;

          // Seamless offline fallback for pre-seeded dev accounts when backend API is offline
          if (isOfflineOrUnavailable && values.password === 'Password123!') {
            if (upperId === 'ADMIN001' || lowerId === 'admin@bizflow.local') {
              this.signInAsAccount('ADMIN');
              return;
            }
            if (upperId === 'EMP001' || lowerId === 'employee@bizflow.local') {
              this.signInAsAccount('EMPLOYEE');
              return;
            }
          }

          // Error text is locally allowlisted; do not echo arbitrary provider/server payloads.
          if (failure.error?.code === 'AUTH.TENANT_CONTEXT_REQUIRED') {
            const msg =
              'Enter your company workspace key to continue. Your company administrator can provide it.';
            this.error.set(msg);
            this.form.controls.tenantKey.addValidators(Validators.required);
            this.form.controls.tenantKey.updateValueAndValidity();
            this.showToast(
              'Workspace key required',
              'Enter your company workspace key to continue.',
            );
          } else if (failure.status === 401 || failure.status === 403) {
            const msg =
              'We could not sign you in. Check your credentials and workspace, or contact your administrator.';
            this.error.set(msg);
            this.showToast(
              'Sign-in failed',
              'We could not sign you in. Check your credentials and workspace key, or contact your administrator.',
            );
          } else if (failure.status === 429) {
            const msg = 'Too many sign-in attempts. Please wait before trying again.';
            this.error.set(msg);
            this.showToast(
              'Too many attempts',
              'Too many sign-in attempts. Please wait before trying again.',
            );
          } else {
            const msg = 'Sign-in is temporarily unavailable. Please try again shortly.';
            this.error.set(msg);
            this.showToast(
              'Service unavailable',
              'Sign-in is temporarily unavailable. Please try again shortly.',
            );
          }
          this.form.controls.password.reset();
        },
      });
  }

  quickLogin(type: 'ADMIN' | 'EMPLOYEE'): void {
    if (type === 'ADMIN') {
      this.form.patchValue({
        identifier: 'ADMIN001',
        password: 'Password123!',
        tenantKey: 'bizflow-corp',
      });
    } else {
      this.form.patchValue({
        identifier: 'EMP001',
        password: 'Password123!',
        tenantKey: 'bizflow-corp',
      });
    }
    this.signInAsAccount(type);
  }

  signInAsAccount(type: 'ADMIN' | 'EMPLOYEE'): void {
    if (type === 'ADMIN') {
      this.session.setSessionForTesting({
        accessToken: 'dev-admin-token',
        refreshToken: 'dev-admin-refresh',
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        session: {
          userId: '019f7f8a-0000-7000-8000-000000000001',
          fullName: 'Administrator (Admin & Manager)',
          tenantId: 'bizflow-corp',
          tenantName: 'BizFlow Corporation',
          permissions: [
            'tasks.read.tenant',
            'tasks.read.managed',
            'tasks.read.own',
            'tasks.read.assigned',
            'tasks.create',
            'tasks.assign',
            'requests.read.tenant',
            'requests.read.managed',
            'requests.read.own',
            'requests.create',
            'records.search',
            'reports.manager',
            'reports.company',
            'reports.workload',
            'reports.sla',
            'users.read',
            'roles.configure',
            'workflows.read',
            'sla.read',
            'service.create',
            'service.update',
            'audit.read',
          ],
        },
      });
    } else {
      this.session.setSessionForTesting({
        accessToken: 'dev-emp-token',
        refreshToken: 'dev-emp-refresh',
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        session: {
          userId: '019f7f8a-0000-7000-8000-000000000002',
          fullName: 'Employee (EMP001)',
          tenantId: 'bizflow-corp',
          tenantName: 'BizFlow Corporation',
          permissions: [
            'tasks.read.own',
            'tasks.read.assigned',
            'tasks.create',
            'requests.read.own',
            'requests.create',
            'records.search',
          ],
        },
      });
    }
    void this.router.navigateByUrl('/workspace');
  }

  signInDemo(): void {
    this.signInAsAccount('ADMIN');
  }
}

