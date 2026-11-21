import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface WatchStatus {
  objectType: string;
  objectId: string;
  watching: boolean;
  watcherCount: number;
}

/**
 * Follow / unfollow button for a task or a request (F3).
 *
 * It reads nothing until the Mentions feature is known to be on, which keeps it inert on pages
 * whose existing specs assert that no unexpected request was made, and makes a deployment with the
 * flag off look exactly like one where the feature never shipped.
 */
@Component({
  selector: 'bf-work-item-watch',
  imports: [MatButtonModule, TranslatePipe],
  template: `
    @if (features.mentions() && status(); as current) {
      <button
        mat-stroked-button
        type="button"
        class="watch-toggle"
        [disabled]="busy()"
        [attr.aria-pressed]="current.watching"
        (click)="toggle()"
      >
        @if (current.watching) {
          <span>{{ 'watch.unfollow' | translate: 'Unfollow' }}</span>
        } @else {
          <span>{{ 'watch.follow' | translate: 'Follow' }}</span>
        }
        @if (current.watcherCount > 0) {
          <span class="watch-count">({{ current.watcherCount }})</span>
        }
      </button>
    }
  `,
  styles: [
    '.watch-count { margin-left: 0.35rem; opacity: 0.75; font-variant-numeric: tabular-nums }',
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

  private get url() {
    return `/api/v1/work-items/${this.objectType}/${encodeURIComponent(this.objectId)}/watch`;
  }

  ngOnInit() {
    if (!this.features.mentions() || !this.objectId) return;
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
