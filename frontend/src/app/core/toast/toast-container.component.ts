import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from './toast.service';
import { ToastItem } from './toast.types';
import { I18nService } from '../i18n/i18n.service';
import { TranslatePipe } from '../i18n/translate.pipe';

@Component({
  selector: 'bf-toast-container',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  styles: [
    `
      :host {
        display: contents;
      }

      .toast-viewport {
        position: fixed;
        top: 1.25rem;
        right: 1.25rem;
        z-index: 999999;
        display: flex;
        flex-direction: column;
        gap: 0.65rem;
        max-width: 420px;
        width: calc(100vw - 2.5rem);
        pointer-events: none;
      }

      @media (max-width: 640px) {
        .toast-viewport {
          top: 0.85rem;
          left: 1rem;
          right: 1rem;
          width: auto;
          max-width: 100%;
        }
      }

      .toast-card {
        pointer-events: auto;
        position: relative;
        display: flex;
        align-items: flex-start;
        gap: 0.75rem;
        padding: 0.85rem 1rem;
        background: rgba(255, 255, 255, 0.98);
        backdrop-filter: blur(16px);
        -webkit-backdrop-filter: blur(16px);
        border-radius: 12px;
        border: 1px solid #e2e8f0;
        box-shadow:
          0 10px 25px -4px rgba(11, 42, 74, 0.12),
          0 4px 10px -2px rgba(11, 42, 74, 0.06);
        overflow: hidden;
        animation: toast-slide-in 240ms cubic-bezier(0.16, 1, 0.3, 1) forwards;
        transform-origin: top right;
      }

      .toast-card.leaving {
        animation: toast-slide-out 200ms ease-in forwards;
      }

      @keyframes toast-slide-in {
        from {
          opacity: 0;
          transform: translateY(-12px) scale(0.96);
        }
        to {
          opacity: 1;
          transform: translateY(0) scale(1);
        }
      }

      @keyframes toast-slide-out {
        from {
          opacity: 1;
          transform: translateY(0) scale(1);
        }
        to {
          opacity: 0;
          transform: translateY(-12px) scale(0.94);
        }
      }

      .toast-type-error {
        border-left: 4px solid #ef4444;
        background: #fffafa;
      }
      .toast-type-success {
        border-left: 4px solid #10b981;
        background: #f8fefb;
      }
      .toast-type-warning {
        border-left: 4px solid #f59e0b;
        background: #fffdf7;
      }
      .toast-type-info {
        border-left: 4px solid #3b82f6;
        background: #f7faff;
      }

      .toast-icon {
        flex-shrink: 0;
        width: 22px;
        height: 22px;
        margin-top: 1px;
        display: flex;
        align-items: center;
        justify-content: center;
      }

      .toast-icon svg {
        width: 20px;
        height: 20px;
      }

      .toast-type-error .toast-icon {
        color: #ef4444;
      }
      .toast-type-success .toast-icon {
        color: #10b981;
      }
      .toast-type-warning .toast-icon {
        color: #f59e0b;
      }
      .toast-type-info .toast-icon {
        color: #3b82f6;
      }

      .toast-content {
        flex: 1;
        min-width: 0;
      }

      .toast-header {
        font-size: 0.78rem;
        font-weight: 700;
        letter-spacing: 0.03em;
        text-transform: uppercase;
        margin-bottom: 0.2rem;
      }

      .toast-type-error .toast-header {
        color: #b91c1c;
      }
      .toast-type-success .toast-header {
        color: #047857;
      }
      .toast-type-warning .toast-header {
        color: #b45309;
      }
      .toast-type-info .toast-header {
        color: #1d4ed8;
      }

      .toast-message {
        font-size: 0.88rem;
        line-height: 1.4;
        color: #1e293b;
        word-break: break-word;
      }

      .toast-close-btn {
        flex-shrink: 0;
        background: transparent;
        border: none;
        color: #94a3b8;
        cursor: pointer;
        padding: 2px 4px;
        border-radius: 6px;
        display: flex;
        align-items: center;
        justify-content: center;
        transition:
          background-color 140ms ease,
          color 140ms ease;
      }

      .toast-close-btn:hover {
        background: #f1f5f9;
        color: #0f172a;
      }

      .toast-progress {
        position: absolute;
        bottom: 0;
        left: 0;
        right: 0;
        height: 2.5px;
        background: rgba(0, 0, 0, 0.05);
      }

      .toast-progress-bar {
        height: 100%;
        width: 100%;
        transform-origin: left;
        animation: progress-drain linear forwards;
      }

      .toast-type-error .toast-progress-bar {
        background: #ef4444;
      }
      .toast-type-success .toast-progress-bar {
        background: #10b981;
      }
      .toast-type-warning .toast-progress-bar {
        background: #f59e0b;
      }
      .toast-type-info .toast-progress-bar {
        background: #3b82f6;
      }

      @keyframes progress-drain {
        from {
          transform: scaleX(1);
        }
        to {
          transform: scaleX(0);
        }
      }
    `,
  ],
  template: `
    <div class="toast-viewport" aria-live="polite" aria-atomic="true">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="toast-card toast-type-{{ toast.type }}"
          [class.leaving]="toast.leaving"
          [attr.role]="toast.type === 'error' || toast.type === 'warning' ? 'alert' : 'status'"
        >
          <!-- Type Icon -->
          <div class="toast-icon" aria-hidden="true">
            @switch (toast.type) {
              @case ('error') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              }
              @case ('success') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                  <polyline points="22 4 12 14.01 9 11.01" />
                </svg>
              }
              @case ('warning') {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path
                    d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
                  />
                  <line x1="12" y1="9" x2="12" y2="13" />
                  <line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
              }
              @default {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="16" x2="12" y2="12" />
                  <line x1="12" y1="8" x2="12.01" y2="8" />
                </svg>
              }
            }
          </div>

          <!-- Body -->
          <div class="toast-content">
            <div class="toast-header">
              {{ toast.title || getDefaultTitle(toast.type) }}
            </div>
            <div class="toast-message">{{ toast.message }}</div>
          </div>

          <!-- Close button -->
          <button
            type="button"
            class="toast-close-btn"
            [attr.aria-label]="'toast.close' | translate: 'Close'"
            (click)="toastService.dismiss(toast.id)"
          >
            <svg
              viewBox="0 0 24 24"
              width="16"
              height="16"
              fill="none"
              stroke="currentColor"
              stroke-width="2.2"
            >
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>

          <!-- Auto-drain progress indicator -->
          @if (toast.duration > 0) {
            <div class="toast-progress" aria-hidden="true">
              <div
                class="toast-progress-bar"
                [style.animationDuration.ms]="toast.duration"
              ></div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  readonly toastService = inject(ToastService);
  private readonly i18n = inject(I18nService);

  getDefaultTitle(type: ToastItem['type']): string {
    switch (type) {
      case 'error':
        return this.i18n.t('toast.error', 'Lỗi');
      case 'success':
        return this.i18n.t('toast.success', 'Thành công');
      case 'warning':
        return this.i18n.t('toast.warning', 'Cảnh báo');
      case 'info':
      default:
        return this.i18n.t('toast.info', 'Thông báo');
    }
  }
}

