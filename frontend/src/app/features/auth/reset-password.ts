import { Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-reset-password',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, RouterLink],
  template: `
    <div class="auth-page">
      <header class="public-header"><a class="wordmark" routerLink="/login">BizFlow</a></header>
      <main id="main-content" class="auth-content" tabindex="-1">
        <section class="auth-intro">
          <span class="eyebrow">Account security</span>
          <h1>Choose a new password.</h1>
          <p>
            Reset links are short-lived and single-use. Changing your password signs out all
            existing sessions.
          </p>
        </section>
        <section class="auth-card" aria-labelledby="reset-title">
          <h2 id="reset-title">Reset your password</h2>
          @if (complete()) {
            <p class="inline-alert" role="status">
              Your password has been changed. Sign in with your new password.
            </p>
          } @else if (!hasToken()) {
            <p class="inline-alert" role="alert">
              Open the reset link from your email, or request a new link.
            </p>
          } @else {
            <p class="supporting-copy">
              Use at least 12 characters and four distinct characters. Your company may require a
              stronger password.
            </p>
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
              <mat-form-field appearance="outline" subscriptSizing="dynamic"
                ><mat-label>Workspace key</mat-label>
                <input
                  matInput
                  formControlName="tenantKey"
                  autocomplete="organization"
                  [readonly]="busy()"
                />
                <mat-hint>Optional; the reset link is bound to your account.</mat-hint
                ><mat-error>Use a valid company workspace key.</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline"
                ><mat-label>New password</mat-label>
                <input
                  matInput
                  type="password"
                  formControlName="newPassword"
                  autocomplete="new-password"
                  [readonly]="busy()"
                />
                <mat-error>Use 12–1024 characters.</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline"
                ><mat-label>Confirm new password</mat-label>
                <input
                  matInput
                  type="password"
                  formControlName="confirmPassword"
                  autocomplete="new-password"
                  [readonly]="busy()"
                />
                <mat-error>Confirm your new password.</mat-error></mat-form-field
              >
              @if (error()) {
                <p class="inline-alert" role="alert">{{ error() }}</p>
              }
              <button mat-flat-button class="sign-in-button" type="submit" [disabled]="busy()">
                {{ busy() ? 'Changing password…' : 'Change password' }}
              </button>
            </form>
          }
          <p class="auth-help">
            <a routerLink="/forgot-password">Request a new reset link</a> ·
            <a routerLink="/login">Back to sign in</a>
          </p>
        </section>
      </main>
    </div>
  `,
})
export class ResetPassword {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private token =
    new URLSearchParams(inject(ActivatedRoute).snapshot.fragment ?? '').get('token') ?? '';
  readonly hasToken = signal(this.token.length > 0 && this.token.length <= 4096);
  readonly busy = signal(false);
  readonly complete = signal(false);
  readonly error = signal('');
  readonly form = inject(FormBuilder).nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(320)]],
    tenantKey: ['', [Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
    newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(1024)]],
    confirmPassword: ['', [Validators.required, Validators.maxLength(1024)]],
  });
  constructor() {
    // Fragments never reach the server; remove the token from visible browser history immediately.
    inject(Location).replaceState('/reset-password');
  }
  submit() {
    this.form.markAllAsTouched();
    if (this.busy() || this.form.invalid || !this.hasToken()) return;
    const values = this.form.getRawValue();
    if (values.newPassword !== values.confirmPassword) {
      this.error.set('The new passwords do not match.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.http
      .post('/api/v1/auth/reset-password', {
        identifier: values.identifier.trim(),
        resetToken: this.token,
        newPassword: values.newPassword,
        ...(values.tenantKey.trim() ? { tenantKey: values.tenantKey.trim() } : {}),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.token = '';
          this.complete.set(true);
          this.form.reset();
          this.session.clear();
        },
        error: (failure: HttpErrorResponse) => {
          this.form.controls.newPassword.reset();
          this.form.controls.confirmPassword.reset();
          this.error.set(
            failure.status === 401
              ? 'This link is invalid or expired. Request a new reset link.'
              : failure.error?.code === 'AUTH.PASSWORD_POLICY'
                ? 'Choose a stronger password that meets your company password policy.'
                : failure.status === 429
                  ? 'Too many requests. Please wait before trying again.'
                  : 'Password reset is temporarily unavailable. Please try again shortly.',
          );
        },
      });
  }
}
