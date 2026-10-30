import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService, EmailLinkVerificationResult } from '../../core/auth/session';
import { FirebaseAuthService } from '../../core/auth/firebase-auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';
import { WorkspaceSelectorDialog } from './workspace-selector-dialog';

export interface ToastAlert {
  title: string;
  message: string;
}

@Component({
  selector: 'bf-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './login.html',
  styleUrls: ['./login.scss'],
})
export class Login implements OnInit, OnDestroy {
  private readonly forms = inject(FormBuilder);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly firebaseAuth = inject(FirebaseAuthService);
  private readonly toastService = inject(ToastService);

  readonly authMethod = signal<'password' | 'email-link'>('password');
  readonly mode = signal<'code' | 'email'>('code');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly emailSentMessage = signal('');
  readonly showRegisterLink = signal(false);
  readonly showPassword = signal(false);
  readonly rememberMe = signal(false);
  readonly isSuperAdmin = signal(false);

  readonly toast = signal<ToastAlert | null>(null);
  readonly toastFading = signal<boolean>(false);
  private toastTimer?: ReturnType<typeof setTimeout>;
  private toastFadeTimer?: ReturnType<typeof setTimeout>;

  readonly form = this.forms.nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
    tenantKey: [
      '',
      [
        Validators.required,
        Validators.maxLength(80),
        Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/),
      ],
    ],
  });

  readonly emailForm = this.forms.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  constructor() {
    this.form.controls.identifier.valueChanges.subscribe((value) => {
      const isSuper = this.checkSuperAdmin(value);
      this.isSuperAdmin.set(isSuper);
      this.updateTenantKeyValidators(isSuper);
    });

    effect(() => {
      const err = this.error();
      if (err) {
        this.toastService.error(err);
      }
    });

    effect(() => {
      const msg = this.emailSentMessage();
      if (msg) {
        this.toastService.success(msg);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    // Pre-warm workspace
    void import('./workspace');

    // Handle Firebase email link authentication if present in URL
    if (this.firebaseAuth.isConfigured() && this.firebaseAuth.isSignInLink()) {
      await this.handleFirebaseSignInLink();
      return;
    }

    const firebaseToken = this.route.snapshot.queryParamMap.get('firebase_token');
    if (firebaseToken) {
      this.verifyFirebaseToken(firebaseToken);
      return;
    }

    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) {
      this.verifyToken(token);
    }
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
    this.toastService.error(message, title);

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

  checkSuperAdmin(identifier: string): boolean {
    const raw = (identifier || '').trim().toLowerCase();
    return raw === 'superadmin' || raw === 'platform@bizflow.local';
  }

  updateTenantKeyValidators(isSuper: boolean): void {
    const tenantControl = this.form.controls.tenantKey;
    if (isSuper) {
      tenantControl.setValidators([
        Validators.maxLength(80),
        Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/),
      ]);
      if (tenantControl.hasError('required')) {
        tenantControl.setErrors(null);
      }
    } else {
      tenantControl.setValidators([
        Validators.required,
        Validators.maxLength(80),
        Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/),
      ]);
    }
    tenantControl.updateValueAndValidity({ emitEvent: false });
  }

  setAuthMethod(method: 'password' | 'email-link'): void {
    this.authMethod.set(method);
    this.error.set('');
    this.emailSentMessage.set('');
    this.showRegisterLink.set(false);
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

    const isSuper = this.checkSuperAdmin(this.form.controls.identifier.value);
    this.isSuperAdmin.set(isSuper);
    this.updateTenantKeyValidators(isSuper);
  }

  submit() {
    if (this.busy()) return;

    const isSuper = this.checkSuperAdmin(this.form.controls.identifier.value);
    this.isSuperAdmin.set(isSuper);
    this.updateTenantKeyValidators(isSuper);

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

    const tenantKey = isSuper && !values.tenantKey.trim() ? undefined : values.tenantKey.trim();

    this.session
      .login({
        identifier: values.identifier.trim(),
        password: values.password,
        tenantKey,
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

          // Seamless offline fallback for dev accounts
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

  async submitEmailLink(): Promise<void> {
    if (this.busy()) return;
    this.emailForm.markAllAsTouched();
    if (this.emailForm.invalid) return;

    this.busy.set(true);
    this.error.set('');
    this.emailSentMessage.set('');
    this.showRegisterLink.set(false);

    const email = this.emailForm.controls.email.value.trim();

    if (this.firebaseAuth.isConfigured()) {
      try {
        await this.firebaseAuth.sendSignInLink(email);
        this.busy.set(false);
        this.emailSentMessage.set(
          `Đã gửi liên kết đăng nhập tới ${email} qua Firebase. Vui lòng mở liên kết trong email để tiếp tục.`,
        );
      } catch {
        this.requestBackendEmailLink(email);
      }
    } else {
      this.requestBackendEmailLink(email);
    }
  }

  private requestBackendEmailLink(email: string): void {
    this.busy.set(true);
    this.session
      .requestEmailLink(email)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (res) => {
          this.emailSentMessage.set(
            `Đã gửi liên kết đăng nhập tới ${email} (hết hạn trong ${res.expiresInMinutes || 15} phút). Vui lòng kiểm tra hộp thư.`,
          );
        },
        error: (err: HttpErrorResponse) => {
          if (err.status === 404 || err.error?.code === 'AUTH.USER_NOT_FOUND') {
            this.error.set(
              'Không tìm thấy tài khoản cho email này. Bạn có thể đăng ký doanh nghiệp mới.',
            );
            this.showRegisterLink.set(true);
          } else if (err.status === 429) {
            this.error.set('Quá nhiều yêu cầu gửi liên kết. Vui lòng đợi trong giây lát.');
          } else {
            this.error.set('Không thể gửi liên kết đăng nhập. Vui lòng thử lại sau.');
          }
        },
      });
  }

  async handleFirebaseSignInLink(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.showRegisterLink.set(false);

    try {
      let email = this.firebaseAuth.getStoredEmail();
      if (!email) {
        email = this.route.snapshot.queryParamMap.get('email');
        if (!email && typeof window !== 'undefined') {
          email = window.prompt('Vui lòng xác nhận địa chỉ email của bạn để hoàn tất đăng nhập:');
        }
      }

      if (!email) {
        this.error.set('Cần cung cấp email để hoàn tất đăng nhập bằng liên kết.');
        this.busy.set(false);
        return;
      }

      const { idToken } = await this.firebaseAuth.completeSignInWithEmailLink(email);
      this.verifyFirebaseToken(idToken);
    } catch {
      this.busy.set(false);
      this.error.set(
        'Liên kết Firebase không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.',
      );
    }
  }

  verifyFirebaseToken(idToken: string): void {
    this.busy.set(true);
    this.error.set('');
    this.showRegisterLink.set(false);

    this.session
      .verifyFirebaseToken(idToken)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (res) => this.handleVerificationResult(res),
        error: (err: HttpErrorResponse) => {
          if (err.status === 401 || err.status === 403) {
            this.error.set('Mã xác thực Firebase không hợp lệ hoặc đã hết hạn.');
          } else {
            this.error.set('Không thể xác thực danh tính từ Firebase. Vui lòng thử lại sau.');
          }
        },
      });
  }

  verifyToken(token: string): void {
    this.busy.set(true);
    this.error.set('');
    this.showRegisterLink.set(false);

    this.session
      .verifyEmailLink(token)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (res) => this.handleVerificationResult(res),
        error: () => {
          this.error.set(
            'Liên kết đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.',
          );
        },
      });
  }

  private handleVerificationResult(res: EmailLinkVerificationResult): void {
    if (res.status === 'Authenticated') {
      void this.router.navigateByUrl('/workspace', { replaceUrl: true });
    } else if (res.status === 'RequiresWorkspaceSelection' && res.workspaces && res.ticket) {
      const dialogRef = this.dialog.open(WorkspaceSelectorDialog, {
        data: { workspaces: res.workspaces, ticket: res.ticket },
        disableClose: true,
      });
      dialogRef.afterClosed().subscribe((selectedKey) => {
        if (selectedKey) {
          this.busy.set(true);
          this.session
            .selectWorkspace(res.ticket!, selectedKey)
            .pipe(finalize(() => this.busy.set(false)))
            .subscribe({
              next: () => void this.router.navigateByUrl('/workspace', { replaceUrl: true }),
              error: () =>
                this.error.set('Không thể hoàn tất chọn không gian làm việc. Vui lòng thử lại.'),
            });
        }
      });
    } else if (res.status === 'NoWorkspace') {
      this.error.set(res.message || 'Không tìm thấy không gian làm việc nào cho email này.');
      this.showRegisterLink.set(true);
    }
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
          userId: '00000000-0000-0000-0000-000000000001',
          fullName: 'Platform Administrator',
          tenantId: '00000000-0000-0000-0000-000000000002',
          tenantName: 'BizFlow Corporation',
          permissions: [
            'tenants.read',
            'tenants.create',
            'tenants.update',
            'departments.read',
            'departments.create',
            'departments.update',
            'users.read',
            'users.create',
            'users.update',
            'roles.read',
            'roles.create',
            'roles.update',
            'roles.configure',
            'requests.read.tenant',
            'requests.read.managed',
            'requests.read.own',
            'requests.create',
            'requests.update',
            'requests.delete',
            'tasks.read.tenant',
            'tasks.read.managed',
            'tasks.read.own',
            'tasks.read.assigned',
            'tasks.create',
            'tasks.assign',
            'tasks.accept',
            'tasks.execute',
            'tasks.submit',
            'tasks.confirm',
            'sla.read',
            'sla.manage',
            'workflows.read',
            'workflows.manage',
            'audit.read',
            'analytics.read',
            'reports.export',
            'platform.company-registrations.read',
            'platform.company-registrations.review',
          ],
        },
      });
    } else {
      this.session.setSessionForTesting({
        accessToken: 'dev-employee-token',
        refreshToken: 'dev-employee-refresh',
        expiresAt: new Date(Date.now() + 86400000).toISOString(),
        session: {
          userId: '00000000-0000-0000-0000-000000000003',
          fullName: 'Regular Employee',
          tenantId: '00000000-0000-0000-0000-000000000002',
          tenantName: 'BizFlow Corporation',
          permissions: [
            'requests.read.own',
            'requests.create',
            'tasks.read.own',
            'tasks.read.assigned',
            'tasks.execute',
            'tasks.submit',
          ],
        },
      });
    }
    void this.router.navigateByUrl('/workspace', { replaceUrl: true });
  }
}
