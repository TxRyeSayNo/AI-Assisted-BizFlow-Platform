import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../../core/auth/session';

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
export class Login {
  private readonly forms = inject(FormBuilder);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  readonly mode = signal<'code' | 'email'>('code');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly showPassword = signal(false);
  readonly form = this.forms.nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
    tenantKey: ['', [Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });

  changeMode(mode: 'code' | 'email') {
    this.mode.set(mode);
    this.form.controls.identifier.setValidators(
      mode === 'email'
        ? [Validators.required, Validators.email, Validators.maxLength(320)]
        : [Validators.required, Validators.maxLength(50)],
    );
    this.form.controls.identifier.updateValueAndValidity();
    this.error.set('');
  }

  submit() {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const values = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    this.session
      .login({
        identifier: values.identifier.trim(),
        password: values.password,
        ...(values.tenantKey.trim() ? { tenantKey: values.tenantKey.trim() } : {}),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigateByUrl('/workspace');
        },
        error: (failure: HttpErrorResponse) => {
          // Error text is locally allowlisted; do not echo arbitrary provider/server payloads.
          if (failure.error?.code === 'AUTH.TENANT_CONTEXT_REQUIRED') {
            this.error.set(
              'Enter your company workspace key to continue. Your company administrator can provide it.',
            );
            this.form.controls.tenantKey.addValidators(Validators.required);
            this.form.controls.tenantKey.updateValueAndValidity();
          } else if (failure.status === 401 || failure.status === 403) {
            this.error.set(
              'We could not sign you in. Check your credentials and workspace, or contact your administrator.',
            );
          } else if (failure.status === 429) {
            this.error.set('Too many sign-in attempts. Please wait before trying again.');
          } else {
            this.error.set('Sign-in is temporarily unavailable. Please try again shortly.');
          }
          this.form.controls.password.reset();
        },
      });
  }
}
