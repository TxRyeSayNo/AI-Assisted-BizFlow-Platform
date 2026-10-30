import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

@Component({
  selector: 'bf-forgot-password',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslatePipe,
  ],
  styleUrl: './forgot-password.scss',
  template: `
    <div class="auth-page light-ocean-theme">
      <!-- Ambient Light Ocean Animated Atmosphere (CSS-Only, No Image) -->
      <div class="light-ocean-ambient" aria-hidden="true">
        <div class="ambient-glow glow-1"></div>
        <div class="ambient-glow glow-2"></div>
        <div class="ambient-glow glow-3"></div>
      </div>

      <!-- Centered Frosted Glass Panel with Animated Border -->
      <main id="main-content" class="auth-centered-container" tabindex="-1">
        <div class="form-container">
          <!-- Animated Luminous Outer Border Wrapper (Rotating Conic Laser Sweep) -->
          <div class="animated-border-wrapper">
            <section class="auth-card" aria-labelledby="reset-title">
              <!-- Refined BizFlow Brand Identity -->
              <div class="auth-card-header">
                <div class="brand-badge-compact">
                  <span class="brand-mark" aria-hidden="true"></span>
                  <span class="brand-text">BizFlow</span>
                  <span class="brand-dot" aria-hidden="true"></span>
                </div>
                <div class="header-titles">
                  <span class="eyebrow">{{
                    'forgot.eyebrow' | translate: 'Account recovery'
                  }}</span>
                  <h1 id="reset-title">
                    {{ 'forgot.title' | translate: 'Forgot your password?' }}
                  </h1>
                </div>
                <p class="intro-desc">
                  {{
                    'forgot.subtitle'
                      | translate
                        : "We'll send a reset link to your company email. If you do not have access to that email, contact your company administrator."
                  }}
                </p>
              </div>

              @if (sent()) {
                <div class="confirmation-panel" role="status">
                  <div class="status-icon-wrap" aria-hidden="true">
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                      class="status-icon"
                    >
                      <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
                      />
                    </svg>
                  </div>
                  <p class="inline-alert success-message">
                    {{
                      'forgot.successMessage'
                        | translate
                          : 'If a matching account can receive email, reset instructions will be sent. Include your workspace key if your identifier is shared across companies.'
                    }}
                  </p>
                </div>
              } @else {
                <p class="supporting-copy">
                  {{
                    'forgot.supportingCopy'
                      | translate: 'Enter your employee code or company email.'
                  }}
                </p>
                <form [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="busy()">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>{{
                      'forgot.identifier' | translate: 'Employee code or company email'
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="identifier"
                      autocomplete="username"
                      [readonly]="busy()"
                    />
                    <mat-error>{{
                      'forgot.identifierReq'
                        | translate: 'Enter your employee code or company email.'
                    }}</mat-error>
                  </mat-form-field>

                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>{{
                      'forgot.workspaceKey' | translate: 'Workspace key'
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="tenantKey"
                      autocomplete="organization"
                      [readonly]="busy()"
                    />
                    <mat-hint>{{
                      'forgot.workspaceKeyHint' | translate: 'Use your company key if you know it.'
                    }}</mat-hint>
                    <mat-error>{{
                      'forgot.workspaceKeyInvalid'
                        | translate: 'Use a valid company workspace key.'
                    }}</mat-error>
                  </mat-form-field>

                  <!-- Accessible alert announcement without layout displacement -->
                  @if (error()) {
                    <div class="sr-only" role="alert" aria-live="assertive">{{ error() }}</div>
                  }

                  <!-- Primary Action Button with Animated Rotating Laser Border -->
                  <div class="sign-in-btn-animated-border">
                    <button
                      mat-flat-button
                      class="sign-in-button"
                      type="submit"
                      [disabled]="busy()"
                    >
                      @if (busy()) {
                        <span>{{ 'forgot.submitting' | translate: 'Sending…' }}</span>
                      } @else {
                        <span>{{ 'forgot.submit' | translate: 'Send reset link' }}</span>
                      }
                    </button>
                  </div>
                </form>
              }

              <div class="auth-card-footer">
                <p class="auth-help">
                  <a routerLink="/login">{{
                    'auth.backToSignIn' | translate: 'Back to sign in'
                  }}</a>
                </p>
              </div>
            </section>
          </div>
        </div>
      </main>
    </div>
  `,
})
export class ForgotPassword {
  private readonly http = inject(HttpClient);
  private readonly i18n = inject(I18nService);
  private readonly toast = inject(ToastService);

  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');
  readonly form = inject(FormBuilder).nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(320)]],
    tenantKey: ['', [Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });

  constructor() {
    effect(() => {
      const err = this.error();
      if (err) {
        this.toast.error(err);
      }
    });
  }

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
        next: () => {
          this.sent.set(true);
          this.toast.success(
            this.i18n.t(
              'forgot.successToast',
              'Đã gửi liên kết khôi phục mật khẩu. Vui lòng kiểm tra email của bạn.',
            ),
          );
        },
        error: (failure: HttpErrorResponse) =>
          this.error.set(
            failure.status === 429
              ? this.i18n.t(
                  'forgot.errTooMany',
                  'Too many requests. Please wait before trying again.',
                )
              : this.i18n.t(
                  'forgot.errUnavailable',
                  'Password reset is temporarily unavailable. Please try again or contact your administrator.',
                ),
          ),
      });
  }
}
