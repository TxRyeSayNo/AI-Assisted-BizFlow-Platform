import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export interface GoogleAccountItem {
  name: string;
  email: string;
  role: string;
  avatarChar: string;
  avatarBg: string;
}

@Component({
  selector: 'bf-google-account-chooser-dialog',
  imports: [MatDialogModule, MatButtonModule, FormsModule],
  template: `
    <div class="google-chooser-container">
      <!-- Google Header -->
      <div class="google-header">
        <svg class="google-g-logo" viewBox="0 0 24 24" width="24" height="24">
          <path
            fill="#4285F4"
            d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
          />
          <path
            fill="#34A853"
            d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
          />
          <path
            fill="#FBBC05"
            d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"
          />
          <path
            fill="#EA4335"
            d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"
          />
        </svg>
        <span class="google-header-text">Đăng nhập bằng Google</span>
      </div>

      <!-- Main Headline -->
      <h2 class="chooser-headline">Chọn tài khoản</h2>
      <p class="chooser-subhead">
        Tiếp tục tới <span class="app-brand-target">BizFlow Corporation</span>
      </p>

      <!-- Account List -->
      <div class="accounts-list" role="list">
        @for (acc of accounts; track acc.email) {
          <button
            type="button"
            class="account-row-btn"
            (click)="selectAccount(acc.email)"
            [disabled]="loadingEmail() !== null"
          >
            <div class="avatar-circle" [style.background-color]="acc.avatarBg">
              {{ acc.avatarChar }}
            </div>
            <div class="account-meta">
              <div class="account-name-line">
                <span class="account-name">{{ acc.name }}</span>
                <span class="role-pill">{{ acc.role }}</span>
              </div>
              <span class="account-email">{{ acc.email }}</span>
            </div>
            @if (loadingEmail() === acc.email) {
              <div class="row-spinner" aria-label="Đang đăng nhập..."></div>
            }
          </button>
        }

        <div class="divider-line"></div>

        <!-- Use another account option -->
        @if (!showCustomInput()) {
          <button
            type="button"
            class="account-row-btn other-account-btn"
            (click)="showCustomInput.set(true)"
            [disabled]="loadingEmail() !== null"
          >
            <div class="avatar-circle custom-avatar">
              <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                <path
                  d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"
                />
              </svg>
            </div>
            <div class="account-meta">
              <span class="account-name">Sử dụng một tài khoản khác</span>
            </div>
          </button>
        } @else {
          <div class="custom-input-box">
            <input
              type="email"
              [(ngModel)]="customEmail"
              placeholder="Nhập địa chỉ email Google..."
              class="custom-email-field"
              (keyup.enter)="submitCustomEmail()"
              [disabled]="loadingEmail() !== null"
            />
            <div class="custom-btn-actions">
              <button
                type="button"
                class="btn-text-cancel"
                (click)="showCustomInput.set(false)"
              >
                Hủy
              </button>
              <button
                type="button"
                class="btn-submit-custom"
                (click)="submitCustomEmail()"
                [disabled]="!customEmail.trim() || loadingEmail() !== null"
              >
                Tiếp tục
              </button>
            </div>
          </div>
        }
      </div>

      <!-- Dialog Footer -->
      <div class="chooser-footer">
        <button
          type="button"
          class="btn-dismiss-dialog"
          (click)="dialogRef.close(null)"
          [disabled]="loadingEmail() !== null"
        >
          Đóng cửa sổ
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      .google-chooser-container {
        background-color: #1a1a1a;
        color: #e8eaed;
        font-family: 'Roboto', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
        padding: 24px 28px;
        min-width: 380px;
        max-width: 440px;
        border-radius: 16px;
        box-shadow: 0 16px 40px rgba(0, 0, 0, 0.6);
      }

      .google-header {
        display: flex;
        align-items: center;
        gap: 10px;
        margin-bottom: 20px;
      }

      .google-header-text {
        font-size: 0.9375rem;
        font-weight: 500;
        color: #ffffff;
        letter-spacing: -0.2px;
      }

      .chooser-headline {
        font-size: 1.5rem;
        font-weight: 500;
        color: #ffffff;
        margin: 0 0 6px 0;
        line-height: 1.25;
      }

      .chooser-subhead {
        font-size: 0.9375rem;
        color: #9aa0a6;
        margin: 0 0 20px 0;
      }

      .app-brand-target {
        color: #8ab4f8;
        font-weight: 500;
      }

      .accounts-list {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }

      .account-row-btn {
        display: flex;
        align-items: center;
        width: 100%;
        padding: 12px 14px;
        background: transparent;
        border: none;
        border-radius: 10px;
        cursor: pointer;
        text-align: left;
        color: inherit;
        transition: background-color 0.15s ease;
      }

      .account-row-btn:hover:not(:disabled) {
        background-color: rgba(255, 255, 255, 0.08);
      }

      .account-row-btn:active:not(:disabled) {
        background-color: rgba(255, 255, 255, 0.14);
      }

      .avatar-circle {
        width: 38px;
        height: 38px;
        border-radius: 50%;
        color: #ffffff;
        font-size: 1.05rem;
        font-weight: 600;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        margin-right: 14px;
      }

      .custom-avatar {
        background-color: #303134;
        color: #9aa0a6;
      }

      .account-meta {
        display: flex;
        flex-direction: column;
        flex: 1;
        min-width: 0;
      }

      .account-name-line {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .account-name {
        font-size: 0.9375rem;
        font-weight: 500;
        color: #e8eaed;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .role-pill {
        font-size: 0.725rem;
        padding: 2px 7px;
        border-radius: 6px;
        background-color: rgba(138, 180, 248, 0.15);
        color: #8ab4f8;
        font-weight: 500;
        white-space: nowrap;
      }

      .account-email {
        font-size: 0.8125rem;
        color: #9aa0a6;
        margin-top: 2px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .divider-line {
        height: 1px;
        background-color: #303134;
        margin: 10px 0;
      }

      .custom-input-box {
        padding: 8px 12px;
        display: flex;
        flex-direction: column;
        gap: 10px;
        background-color: #242526;
        border-radius: 10px;
        border: 1px solid #3c4043;
      }

      .custom-email-field {
        width: 100%;
        background-color: #1a1a1a;
        border: 1px solid #5f6368;
        border-radius: 8px;
        padding: 10px 12px;
        color: #ffffff;
        font-size: 0.875rem;
        outline: none;
        box-sizing: border-box;
      }

      .custom-email-field:focus {
        border-color: #8ab4f8;
      }

      .custom-btn-actions {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
      }

      .btn-text-cancel {
        background: transparent;
        border: none;
        color: #9aa0a6;
        font-size: 0.8125rem;
        cursor: pointer;
        padding: 6px 12px;
      }

      .btn-submit-custom {
        background-color: #8ab4f8;
        border: none;
        border-radius: 6px;
        color: #202124;
        font-weight: 600;
        font-size: 0.8125rem;
        padding: 6px 14px;
        cursor: pointer;
      }

      .chooser-footer {
        margin-top: 22px;
        display: flex;
        justify-content: flex-end;
      }

      .btn-dismiss-dialog {
        background: transparent;
        border: 1px solid #3c4043;
        border-radius: 8px;
        color: #9aa0a6;
        font-size: 0.8125rem;
        padding: 8px 16px;
        cursor: pointer;
        transition: all 0.15s ease;
      }

      .btn-dismiss-dialog:hover {
        background-color: rgba(255, 255, 255, 0.05);
        color: #ffffff;
      }

      .row-spinner {
        width: 18px;
        height: 18px;
        border: 2px solid rgba(138, 180, 248, 0.3);
        border-top-color: #8ab4f8;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
        margin-left: 10px;
        flex-shrink: 0;
      }

      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class GoogleAccountChooserDialog {
  readonly dialogRef = inject(MatDialogRef<GoogleAccountChooserDialog>);
  readonly loadingEmail = signal<string | null>(null);
  readonly showCustomInput = signal(false);
  customEmail = '';

  // Accounts directly extracted from the user's screenshot (Image 3)
  readonly accounts: GoogleAccountItem[] = [
    {
      name: 'Đức Truyền Phạm',
      email: 'hoilamchi6c@gmail.com',
      role: 'CEO',
      avatarChar: 'Đ',
      avatarBg: '#0b57d0', // Google dark blue
    },
    {
      name: 'Truyền Phạm',
      email: 'truyenc2006@gmail.com',
      role: 'CTO',
      avatarChar: 'T',
      avatarBg: '#0f9d58', // Google green
    },
    {
      name: 'Quoc Dung Le',
      email: 'quocdung.le2008@gmail.com',
      role: 'Sales Head',
      avatarChar: 'Q',
      avatarBg: '#7b1fa2', // Google purple
    },
    {
      name: 'Uyen Nhi',
      email: 'uyennhi292006@gmail.com',
      role: 'MKT Head',
      avatarChar: 'U',
      avatarBg: '#e65100', // Google warm amber
    },
    {
      name: 'Truyền Phạm',
      email: 'truyenpd155@gmail.com',
      role: 'Dev',
      avatarChar: 'T',
      avatarBg: '#00796b', // Google teal
    },
  ];

  selectAccount(email: string): void {
    this.loadingEmail.set(email);
    this.dialogRef.close({ email });
  }

  submitCustomEmail(): void {
    const trimmed = this.customEmail.trim();
    if (trimmed && trimmed.includes('@')) {
      this.selectAccount(trimmed);
    }
  }
}

