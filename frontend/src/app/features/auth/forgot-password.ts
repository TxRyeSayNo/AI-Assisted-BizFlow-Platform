import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

@Component({
  selector: 'bf-forgot-password',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, RouterLink],
  template: `
    <div class="auth-page">
      <header class="public-header"><a class="wordmark" routerLink="/login">BizFlow</a></header>
      <main id="main-content" class="auth-content" tabindex="-1">
        <section class="auth-intro">
          <span class="eyebrow">Account recovery</span>
          <h1>Get back to your work.</h1>
          <p>
            We'll send a reset link to your company email. If you do not have access to that email,
            contact your company administrator.
          </p>
        </section>
        <section class="auth-card" aria-labelledby="reset-title">
          <h2 id="reset-title">Forgot your password?</h2>
          @if (sent()) {
            <p class="inline-alert" role="status">
              If a matching account can receive email, reset instructions will be sent. Include your
              workspace key if your identifier is shared across companies.
            </p>
          } @else {
            <p class="supporting-copy">Enter your employee code or company email.</p>
            <form [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="busy()">
              <mat-form-field appearance="outline"
                ><mat-label>Employee code or company email</mat-label>
                <input
                  matInput
                  formControlName="identifier"
                  autocomplete="username"
                  [readonly]="busy()"
                />
                <mat-error>Enter your employee code or company email.</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline"
                ><mat-label>Workspace key</mat-label>
                <input
                  matInput
                  formControlName="tenantKey"
                  autocomplete="organization"
                  [readonly]="busy()"
                />
                <mat-hint>Use your company key if you know it.</mat-hint
                ><mat-error>Use a valid company workspace key.</mat-error></mat-form-field
              >
              @if (error()) {
                <p class="inline-alert" role="alert">{{ error() }}</p>
              }
              <button mat-flat-button class="sign-in-button" type="submit" [disabled]="busy()">
                {{ busy() ? 'Sending…' : 'Send reset link' }}
              </button>
            </form>
          }
          <p class="auth-help"><a routerLink="/login">Back to sign in</a></p>
        </section>
      </main>
    </div>
  `,
})
export class ForgotPassword {
  private readonly http = inject(HttpClient);
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');
  readonly form = inject(FormBuilder).nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(320)]],
    tenantKey: ['', [Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });
  submit() {
    this.form.markAllAsTouched();
    if (this.busy() || this.form.invalid) return;
    const values = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    this.http
      .post('/api/v1/auth/forgot-password', {
        identifier: values.identifier.trim(),
        ...(values.tenantKey.trim() ? { tenantKey: values.tenantKey.trim() } : {}),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => this.sent.set(true),
        error: (failure: HttpErrorResponse) =>
          this.error.set(
            failure.status === 429
              ? 'Too many requests. Please wait before trying again.'
              : 'Password reset is temporarily unavailable. Please try again or contact your administrator.',
          ),
      });
  }
}
