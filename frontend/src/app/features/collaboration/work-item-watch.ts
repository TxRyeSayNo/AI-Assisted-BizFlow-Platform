import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface WatcherParticipant {
  userId: string;
  fullName: string;
  employeeCode: string;
  departmentName?: string | null;
}

export interface WatchStatus {
  objectType: string;
  objectId: string;
  watching: boolean;
  watcherCount: number;
  watchers?: WatcherParticipant[];
}

/**
 * Watcher management and follow / unfollow control for a task or a request (F3 - Requirement 3).
 *
 * It reads nothing until the Mentions feature is known to be on, which keeps it inert on pages
 * whose existing specs assert that no unexpected request was made, and automatically loads via
 * signal effect once the feature flag resolves.
 */
@Component({
  selector: 'bf-work-item-watch',
  imports: [MatButtonModule, TranslatePipe],
  template: `
    @if (features.mentions() && status(); as current) {
      <section class="watcher-panel directory-panel" aria-label="Người theo dõi công việc">
        <div class="watcher-header-row">
          <div class="watcher-info">
            <span class="watcher-icon" aria-hidden="true">👁️</span>
            <div>
              <h3 class="watcher-title">
                {{ 'watch.panelTitle' | translate: 'Theo dõi công việc (Watchers)' }}
                <span class="watcher-badge">{{ current.watcherCount }}</span>
              </h3>
              <p class="watcher-subtitle">
                {{
                  current.watching
                    ? ('watch.followingHint'
                      | translate: 'Bạn đang theo dõi mục này và sẽ nhận thông báo khi có cập nhật tiến độ, trạng thái hoặc thảo luận mới.')
                    : ('watch.notFollowingHint'
                      | translate: 'Nhấn Theo dõi để nhận thông báo tự động khi có thay đổi quan trọng trên mục này.')
                }}
              </p>
            </div>
          </div>

          <div class="watcher-actions">
            <button
              type="button"
              class="btn-watch-action"
              [class.is-following]="current.watching"
              [disabled]="busy()"
              [attr.aria-pressed]="current.watching"
              (click)="toggle()"
            >
              @if (current.watching) {
                <span class="watch-icon">✓</span>
                <span>{{ 'watch.unfollow' | translate: 'Đang theo dõi' }}</span>
              } @else {
                <span class="watch-icon">+</span>
                <span>{{ 'watch.follow' | translate: 'Theo dõi' }}</span>
              }
              @if (current.watcherCount > 0) {
                <span class="watch-count">({{ current.watcherCount }})</span>
              }
            </button>
          </div>
        </div>

        @if (current.watchers && current.watchers.length > 0) {
          <div class="watcher-list" aria-label="Danh sách người đang theo dõi">
            <span class="watcher-list-label">Đang theo dõi:</span>
            @for (person of current.watchers; track person.userId) {
              <span class="watcher-chip" [title]="(person.employeeCode || '') + (person.departmentName ? ' · ' + person.departmentName : '')">
                <span class="watcher-avatar">{{ (person.fullName || 'U').charAt(0).toUpperCase() }}</span>
                <span class="watcher-name">{{ person.fullName }}</span>
                @if (person.employeeCode) {
                  <span class="watcher-code">{{ person.employeeCode }}</span>
                }
                @if (person.departmentName) {
                  <span class="watcher-dept">· {{ person.departmentName }}</span>
                }
              </span>
            }
          </div>
        }
      </section>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .watcher-panel {
        margin-top: 1.25rem;
        margin-bottom: 1.25rem;
        padding: 1rem 1.35rem;
        background: #ffffff;
        border: 1px solid #dbe4f0;
        border-radius: 12px;
      }
      .watcher-header-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 1rem;
      }
      .watcher-info {
        display: flex;
        align-items: flex-start;
        gap: 0.75rem;
      }
      .watcher-icon {
        font-size: 1.25rem;
        line-height: 1.4;
      }
      .watcher-title {
        margin: 0;
        font-size: 0.95rem;
        font-weight: 700;
        color: #0f172a;
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .watcher-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-width: 1.4rem;
        height: 1.4rem;
        padding: 0 0.45rem;
        border-radius: 9999px;
        font-size: 0.75rem;
        font-weight: 700;
        background: #eff6ff;
        color: #1d4ed8;
        border: 1px solid #bfdbfe;
      }
      .watcher-subtitle {
        margin: 0.2rem 0 0;
        font-size: 0.8125rem;
        color: #64748b;
      }
      .watcher-actions {
        display: flex;
        align-items: center;
      }
      .btn-watch-action {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        padding: 0.55rem 1.25rem;
        min-height: 2.5rem;
        font-size: 0.875rem;
        font-weight: 650;
        border-radius: 10px;
        cursor: pointer;
        transition: all 160ms cubic-bezier(0.16, 1, 0.3, 1);
        white-space: nowrap;
        background: linear-gradient(135deg, #0e44b8 0%, #1e6beb 52%, #2563eb 100%);
        color: #ffffff;
        border: 1px solid #1e40af;
        box-shadow: 0 3px 10px rgba(37, 99, 235, 0.28);

        &:hover:not(:disabled) {
          transform: translateY(-1px);
          box-shadow: 0 6px 18px rgba(37, 99, 235, 0.4);
        }

        &:active:not(:disabled) {
          transform: translateY(0);
        }

        &:disabled {
          opacity: 0.55;
          cursor: not-allowed;
          transform: none;
        }

        &.is-following {
          background: #f8fafc;
          color: #1e293b;
          border: 1px solid #cbd5e1;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);

          &:hover:not(:disabled) {
            background: #fef2f2;
            border-color: #fca5a5;
            color: #dc2626;
            box-shadow: 0 3px 10px rgba(220, 38, 38, 0.16);
          }
        }

        .watch-icon {
          font-weight: 700;
          font-size: 0.95rem;
        }

        .watch-count {
          opacity: 0.9;
          font-variant-numeric: tabular-nums;
          font-size: 0.8rem;
          margin-left: 0.15rem;
        }
      }
      .watcher-list {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.5rem;
        margin-top: 0.85rem;
        padding-top: 0.75rem;
        border-top: 1px solid #f1f5f9;
      }
      .watcher-list-label {
        font-size: 0.78rem;
        font-weight: 600;
        color: #64748b;
        margin-right: 0.25rem;
      }
      .watcher-chip {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.22rem 0.65rem 0.22rem 0.3rem;
        border-radius: 9999px;
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        font-size: 0.78rem;
        color: #1e293b;
      }
      .watcher-avatar {
        width: 1.35rem;
        height: 1.35rem;
        border-radius: 50%;
        background: #1e40af;
        color: #ffffff;
        font-size: 0.7rem;
        font-weight: 700;
        display: inline-flex;
        align-items: center;
        justify-content: center;
      }
      .watcher-name {
        font-weight: 600;
      }
      .watcher-code {
        font-family: var(--tp-font-mono, monospace);
        font-size: 0.72rem;
        color: #475569;
        background: #e2e8f0;
        padding: 0 4px;
        border-radius: 4px;
      }
      .watcher-dept {
        font-size: 0.72rem;
        color: #64748b;
      }
    `,
  ],
})
export class WorkItemWatch implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly features = inject(FeatureFlagsService);
  readonly status = signal<WatchStatus | null>(null);
  readonly busy = signal(false);
  private loadedForKey = '';

  constructor() {
    effect(() => {
      if (this.features.mentions() && this.objectId && this.objectType) {
        this.loadStatus();
      }
    });
  }

  private get url() {
    return `/api/v1/work-items/${this.objectType}/${encodeURIComponent(this.objectId)}/watch`;
  }

  ngOnInit() {
    this.features.load();
    if (this.features.mentions() && this.objectId) {
      this.loadStatus();
    }
  }

  private loadStatus() {
    const key = `${this.objectType}:${this.objectId}`;
    if (this.loadedForKey === key) return;
    this.loadedForKey = key;
    this.http
      .get<WatchStatus>(this.url)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (status) => this.status.set(status),
        // A missing or out-of-scope item simply shows no button.
        error: () => this.status.set(null),
      });
  }

  toggle() {
    const current = this.status();
    if (!current || this.busy()) return;
    this.busy.set(true);
    const request = current.watching
      ? this.http.delete<WatchStatus>(this.url)
      : this.http.put<WatchStatus>(this.url, {});
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (status) => {
        this.status.set(status);
        this.busy.set(false);
      },
      error: (failure: HttpErrorResponse) => {
        this.busy.set(false);
        // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
        const curated = failure?.status < 500 ? failure?.error?.message : null;
        this.toast.error(
          curated ?? this.i18n.t('watch.errToggle', 'Không cập nhật được trạng thái theo dõi.'),
        );
      },
    });
  }
}
