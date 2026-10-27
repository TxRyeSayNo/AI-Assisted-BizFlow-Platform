import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { WorkspaceSelectorDialog } from './workspace-selector-dialog';

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
})
export class Login implements OnInit {
  private readonly forms = inject(FormBuilder);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  readonly authMethod = signal<'password' | 'email-link'>('password');
  readonly mode = signal<'code' | 'email'>('code');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly emailSentMessage = signal('');
  readonly showRegisterLink = signal(false);
  readonly showPassword = signal(false);

  readonly form = this.forms.nonNullable.group({
    identifier: ['', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
    tenantKey: ['', [Validators.required, Validators.maxLength(80), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
  });

  readonly emailForm = this.forms.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  ngOnInit(): void {
    // Eagerly pre-warm the workspace bundle while the user is typing credentials
    void import('./workspace');

    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) {
      this.verifyToken(token);
    }
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

  submitEmailLink(): void {
    if (this.busy()) return;
    this.emailForm.markAllAsTouched();
    if (this.emailForm.invalid) return;

    this.busy.set(true);
    this.error.set('');
    this.emailSentMessage.set('');
    this.showRegisterLink.set(false);

    const email = this.emailForm.controls.email.value.trim();
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
            this.error.set('Không tìm thấy tài khoản cho email này. Bạn có thể đăng ký doanh nghiệp mới.');
            this.showRegisterLink.set(true);
          } else if (err.status === 429) {
            this.error.set('Quá nhiều yêu cầu gửi liên kết. Vui lòng đợi trong giây lát.');
          } else {
            this.error.set('Không thể gửi liên kết đăng nhập. Vui lòng thử lại sau.');
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
        next: (res) => {
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
                    error: () => this.error.set('Không thể hoàn tất chọn không gian làm việc. Vui lòng thử lại.'),
                  });
              }
            });
          } else if (res.status === 'NoWorkspace') {
            this.error.set('Không tìm thấy không gian làm việc nào cho email này.');
            this.showRegisterLink.set(true);
          }
        },
        error: () => {
          this.error.set('Liên kết đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.');
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
