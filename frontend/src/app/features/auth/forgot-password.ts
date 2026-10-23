import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

interface ToastAlert {
  title: string;
  message: string;
}

@Component({
  selector: 'bf-forgot-password',
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, RouterLink],
  styleUrl: './forgot-password.scss',
  template: `
    <!-- Top-Right Floating Pop-Up Alert (Fades out smoothly after 3s) -->
    @if (toast(); as currentToast) {
      <aside
        id="auth-top-toast"
        class="auth-top-toast"
        [class.toast-fade-out]="toastFading()"
        role="alert"
        aria-live="assertive"
      >
        <div class="toast-icon-wrap" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" class="toast-svg">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        </div>
        <div class="toast-content">
          <div class="toast-title">{{ currentToast.title }}</div>
          <div class="toast-desc">{{ currentToast.message }}</div>
        </div>
        <button type="button" class="toast-close-btn" (click)="dismissToast()" aria-label="Close notification">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>

        <!-- Precise SVG Rounded Perimeter Border Runner (3s full loop, perfectly matches corner radius) -->
        <svg class="toast-perimeter-svg" aria-hidden="true" width="100%" height="100%">
          <rect
            class="toast-perimeter-track"
            x="1.5"
            y="1.5"
            width="calc(100% - 3px)"
            height="calc(100% - 3px)"
            rx="14.5"
            ry="14.5"
            pathLength="100"
          />
          <rect
            class="toast-perimeter-runner"
            x="1.5"
            y="1.5"
            width="calc(100% - 3px)"
            height="calc(100% - 3px)"
            rx="14.5"
            ry="14.5"
            pathLength="100"
          />
        </svg>
      </aside>
    }

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
                <div class="eyebrow-badge">
                  <span class="eyebrow">Account recovery</span>
                </div>
                <h1 id="reset-title">Forgot your password?</h1>
                <p class="supporting-copy">
                  Enter your employee code or company email to receive reset instructions.
                </p>
              </div>

              @if (sent()) {
                <div class="confirmation-panel" role="status">
                  <div class="status-icon-wrap" aria-hidden="true">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="status-icon">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                  </div>
                  <p class="inline-alert success-message">
                    If a matching account can receive email, reset instructions will be sent. Include your
                    workspace key if your identifier is shared across companies.
                  </p>
                </div>
              } @else {
                <form [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="busy()">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>Employee code or company email</mat-label>
                    <input
                      matInput
                      formControlName="identifier"
                      autocomplete="username"
                      [readonly]="busy()"
                      placeholder="e.g. EMP001 or name@company.com"
                    />
                  </mat-form-field>

                  <mat-form-field appearance="outline" subscriptSizing="dynamic">
                    <mat-label>Workspace key</mat-label>
                    <input
                      matInput
                      formControlName="tenantKey"
                      autocomplete="organization"
                      [readonly]="busy()"
                      placeholder="e.g. bizflow-corp"
                    />
                  </mat-form-field>

                  <!-- Primary Action Button with Animated Rotating Laser Border -->
                  <div class="sign-in-btn-animated-border">
                    <button
                      mat-flat-button
                      id="send-reset-btn"
                      class="sign-in-button"
                      type="submit"
                      (click)="submit()"
                      [disabled]="busy()"
                    >
                      @if (busy()) {
                        <span>Sending…</span>
                      } @else {
                        <span>Send reset link</span>
                      }
                    </button>
                  </div>
                </form>
              }

              <div class="auth-card-footer">
                <p class="auth-help"><a routerLink="/login">Back to sign in</a></p>
              </div>
            </section>
          </div>

          <div class="auth-platform-tag">
            BizFlow Platform <span class="tag-sep">•</span> Internal work orchestration & accountability.
          </div>
        </div>
      </main>
    </div>
  `,
})
export class ForgotPassword implements OnDestroy {
  private readonly http = inject(HttpClient);
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');

  readonly toast = signal<ToastAlert | null>(null);
  readonly toastFading = signal<boolean>(false);
  private toastTimer?: ReturnType<typeof setTimeout>;
  private toastFadeTimer?: ReturnType<typeof setTimeout>;

  readonly form = inject(FormBuilder).nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(320)]],
    tenantKey: ['', [Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });

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

  submit() {
    if (this.busy()) return;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      const idCtrl = this.form.controls.identifier;
      const tenantCtrl = this.form.controls.tenantKey;

      if (idCtrl.invalid) {
        if (idCtrl.errors?.['required']) {
          this.showToast(
            'Missing account identifier',
            'Please enter your employee code or company email.',
          );
        } else {
          this.showToast(
            'Invalid account identifier',
            'Employee code or company email must be 320 characters or fewer.',
          );
        }
        return;
      }

      if (tenantCtrl.invalid) {
        this.showToast(
          'Invalid workspace key',
          'Workspace key may only contain lowercase letters, numbers, and hyphens (e.g. bizflow-corp).',
        );
        return;
      }

      this.showToast('Invalid input', 'Please review the required fields and try again.');
      return;
    }

    const values = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    this.dismissToast();

    this.http
      .post('/api/v1/auth/forgot-password', {
        identifier: values.identifier.trim(),
        ...(values.tenantKey.trim() ? { tenantKey: values.tenantKey.trim() } : {}),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.sent.set(true);
          this.dismissToast();
        },
        error: (failure: HttpErrorResponse) => {
          const title = failure.status === 429 ? 'Too many requests' : 'Service unavailable';
          const msg =
            failure.status === 429
              ? 'Too many requests. Please wait before trying again.'
              : 'Password reset is temporarily unavailable. Please try again or contact your administrator.';
          this.error.set(msg);
          this.showToast(title, msg);
        },
      });
  }
}
