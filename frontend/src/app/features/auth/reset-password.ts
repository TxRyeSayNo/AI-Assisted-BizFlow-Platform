import { Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

@Component({
  selector: 'bf-reset-password',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslatePipe,
  ],
  template: `
    <div class="auth-page">
      <header class="public-header">
        <a class="wordmark" routerLink="/login">
          <span class="brand-mark" aria-hidden="true"></span> BizFlow
        </a>
      </header>
      <main id="main-content" class="auth-content" tabindex="-1">
        <section class="auth-intro">
          <span class="eyebrow">{{ 'reset.eyebrow' | translate: 'Bảo mật Tài khoản' }}</span>
          <h1>{{ 'reset.title' | translate: 'Đặt lại mật khẩu mới' }}</h1>
          <p>
            {{
              'reset.subtitle'
                | translate
                  : 'Liên kết đặt lại có thời hạn ngắn và chỉ dùng một lần. Đổi mật khẩu sẽ đăng xuất khỏi tất cả phiên đăng nhập hiện tại.'
            }}
          </p>
        </section>
        <section class="auth-card" aria-labelledby="reset-title">
          <h2 id="reset-title">{{ 'reset.title' | translate: 'Đặt lại mật khẩu mới' }}</h2>
          @if (complete()) {
            <p class="inline-alert" role="status">
              {{
                'reset.success'
                  | translate: 'Mật khẩu của bạn đã được thay đổi. Hãy đăng nhập bằng mật khẩu mới.'
              }}
            </p>
          } @else if (!hasToken()) {
            <p class="inline-alert" role="alert">
              {{
                'reset.noToken'
                  | translate: 'Vui lòng mở liên kết đặt lại từ email, hoặc yêu cầu liên kết mới.'
              }}
            </p>
          } @else {
            <p class="supporting-copy">
              {{
                'reset.passwordReqs'
                  | translate
                    : 'Sử dụng ít nhất 12 ký tự. Công ty của bạn có thể yêu cầu mật khẩu mạnh hơn.'
              }}
            </p>
            <form [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="busy()">
              <mat-form-field appearance="outline"
                ><mat-label>{{
                  'forgot.identifier' | translate: 'Mã nhân viên hoặc email công ty'
                }}</mat-label>
                <input
                  matInput
                  formControlName="identifier"
                  autocomplete="username"
                  [readonly]="busy()"
                />
                <mat-error>{{
                  'forgot.identifierReq' | translate: 'Vui lòng nhập mã nhân viên hoặc email công ty.'
                }}</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline" subscriptSizing="dynamic"
                ><mat-label>{{
                  'forgot.workspaceKey' | translate: 'Mã không gian làm việc'
                }}</mat-label>
                <input
                  matInput
                  formControlName="tenantKey"
                  autocomplete="organization"
                  [readonly]="busy()"
                />
                <mat-hint>{{
                  'forgot.workspaceKeyHint'
                    | translate: 'Không bắt buộc; liên kết khôi phục đã gắn với tài khoản của bạn.'
                }}</mat-hint
                ><mat-error>{{
                  'forgot.workspaceKeyInvalid' | translate: 'Vui lòng nhập mã không gian làm việc hợp lệ.'
                }}</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline"
                ><mat-label>{{ 'reset.newPassword' | translate: 'Mật khẩu mới' }}</mat-label>
                <input
                  matInput
                  type="password"
                  formControlName="newPassword"
                  autocomplete="new-password"
                  [readonly]="busy()"
                />
                <mat-error>{{
                  'reset.passwordLength' | translate: 'Sử dụng từ 12 đến 1024 ký tự.'
                }}</mat-error></mat-form-field
              >
              <mat-form-field appearance="outline"
                ><mat-label>{{
                  'reset.confirmPassword' | translate: 'Xác nhận mật khẩu mới'
                }}</mat-label>
                <input
                  matInput
                  type="password"
                  formControlName="confirmPassword"
                  autocomplete="new-password"
                  [readonly]="busy()"
                />
                <mat-error>{{
                  'reset.confirmPasswordReq' | translate: 'Vui lòng xác nhận mật khẩu mới của bạn.'
                }}</mat-error></mat-form-field
              >
              <!-- Accessible alert announcement without layout displacement -->
              @if (error()) {
                <div class="sr-only" role="alert" aria-live="assertive">{{ error() }}</div>
              }
              <button mat-flat-button class="sign-in-button" type="submit" [disabled]="busy()">
                {{
                  busy()
                    ? ('reset.submitting' | translate: 'Đang cập nhật…')
                    : ('reset.submit' | translate: 'Cập nhật mật khẩu')
                }}
              </button>
            </form>
          }
          <p class="auth-help">
            <a routerLink="/forgot-password">{{
              'reset.requestNewLink' | translate: 'Yêu cầu liên kết đặt lại mới'
            }}</a>
            ·
            <a routerLink="/login">{{ 'reset.backToSignIn' | translate: 'Quay lại đăng nhập' }}</a>
          </p>
        </section>
      </main>
    </div>
  `,
})
export class ResetPassword {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly i18n = inject(I18nService);
  private readonly toast = inject(ToastService);
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

    effect(() => {
      const err = this.error();
      if (err) {
        this.toast.error(err);
      }
    });
  }
  submit() {
    this.form.markAllAsTouched();
    if (this.busy() || this.form.invalid || !this.hasToken()) return;
    const values = this.form.getRawValue();
    if (values.newPassword !== values.confirmPassword) {
      this.error.set(this.i18n.t('reset.passwordMismatch', 'The new passwords do not match.'));
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
              ? this.i18n.t(
                  'reset.errInvalidLink',
                  'This link is invalid or expired. Request a new reset link.',
                )
              : failure.error?.code === 'AUTH.PASSWORD_POLICY'
                ? this.i18n.t(
                    'reset.errPasswordPolicy',
                    'Choose a stronger password that meets your company password policy.',
                  )
                : failure.status === 429
                  ? this.i18n.t(
                      'reset.errTooMany',
                      'Too many requests. Please wait before trying again.',
                    )
                  : this.i18n.t(
                      'reset.errUnavailable',
                      'Password reset is temporarily unavailable. Please try again shortly.',
                    ),
          );
        },
      });
  }
}
