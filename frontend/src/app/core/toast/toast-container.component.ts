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
        top: 1.5rem;
        right: 1.5rem;
        z-index: 9999999;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        width: 400px;
        max-width: calc(100vw - 2rem);
        pointer-events: none;
      }

      @media (max-width: 640px) {
        .toast-viewport {
          top: 1rem;
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
        gap: 0.85rem;
        width: 100% !important;
        min-width: 0 !important;
        box-sizing: border-box;
        padding: 1rem 1.15rem;
        background: #ffffff !important;
        border-radius: 14px !important;
        border: 1.5px solid #e2e8f0;
        box-shadow:
          0 16px 36px -6px rgba(15, 23, 42, 0.14),
          0 6px 16px -3px rgba(15, 23, 42, 0.08);
        overflow: hidden;
        animation: toast-slide-in 240ms cubic-bezier(0.16, 1, 0.3, 1) forwards;
        transform-origin: top right;
        transition: opacity 250ms ease, transform 250ms ease;
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
        border: 1.5px solid #fca5a5 !important;
        background: #ffffff !important;
        box-shadow:
          0 20px 40px -8px rgba(239, 68, 68, 0.16),
          0 10px 20px -4px rgba(15, 23, 42, 0.08),
          0 0 0 1px rgba(239, 68, 68, 0.06) !important;
      }
      .toast-type-success {
        border: 1.5px solid #a7f3d0 !important;
        background: #ffffff !important;
        box-shadow:
          0 20px 40px -8px rgba(16, 185, 129, 0.16),
          0 10px 20px -4px rgba(15, 23, 42, 0.08) !important;
      }
      .toast-type-warning {
        border: 1.5px solid #fde68a !important;
        background: #ffffff !important;
        box-shadow:
          0 20px 40px -8px rgba(245, 158, 11, 0.16),
          0 10px 20px -4px rgba(15, 23, 42, 0.08) !important;
      }
      .toast-type-info {
        border: 1.5px solid #bfdbfe !important;
        background: #ffffff !important;
        box-shadow:
          0 20px 40px -8px rgba(37, 99, 235, 0.16),
          0 10px 20px -4px rgba(15, 23, 42, 0.08) !important;
      }

      .toast-icon {
        flex-shrink: 0;
        width: 36px;
        height: 36px;
        border-radius: 10px;
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
        background: #fee2e2;
        color: #ef4444;
      }
      .toast-type-success .toast-icon {
        background: #d1fae5;
        color: #059669;
      }
      .toast-type-warning .toast-icon {
        background: #fef3c7;
        color: #d97706;
      }
      .toast-type-info .toast-icon {
        background: #dbeafe;
        color: #2563eb;
      }

      .toast-content {
        flex: 1 1 auto;
        min-width: 0;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        white-space: normal;
        word-break: normal;
        overflow-wrap: break-word;
      }

      .toast-header {
        font-size: 0.88rem;
        font-weight: 700;
        letter-spacing: -0.01em;
        margin-bottom: 0;
        white-space: normal;
      }

      .toast-type-error .toast-header {
        color: #991b1b;
      }
      .toast-type-success .toast-header {
        color: #065f46;
      }
      .toast-type-warning .toast-header {
        color: #92400e;
      }
      .toast-type-info .toast-header {
        color: #1e40af;
      }

      .toast-message {
        font-size: 0.8125rem;
        line-height: 1.45;
        color: #475569;
        word-break: break-word;
      }

      .toast-close-btn {
        flex-shrink: 0;
        background: transparent;
        border: none;
        color: #94a3b8;
        cursor: pointer;
        padding: 5px;
        border-radius: 8px;
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
        height: 3.5px;
        background: rgba(0, 0, 0, 0.04);
        border-radius: 0 0 14px 14px;
        overflow: hidden;
      }

      .toast-progress-bar {
        height: 100%;
        width: 100%;
        transform-origin: left;
        animation: progress-drain linear forwards;
      }

      .toast-type-error .toast-progress-bar {
        background: linear-gradient(90deg, #ef4444, #f87171);
      }
      .toast-type-success .toast-progress-bar {
        background: linear-gradient(90deg, #10b981, #34d399);
      }
      .toast-type-warning .toast-progress-bar {
        background: linear-gradient(90deg, #f59e0b, #fbbf24);
      }
      .toast-type-info .toast-progress-bar {
        background: linear-gradient(90deg, #3b82f6, #60a5fa);
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

