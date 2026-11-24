import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface ActivityRow {
  auditLogId: string;
  action: string;
  actorType: string;
  actorId: string | null;
  actorName: string | null;
  createdAt: string;
}
export interface ActivityPage {
  objectType: string;
  objectId: string;
  items: ActivityRow[];
  page: number;
  pageSize: number;
  total: number;
}

/**
 * The activity feed inside a task or request (F11). Read-only, and inert until the ActivityFeed
 * feature is known to be on, so pages whose existing specs assert that no unexpected request was
 * made stay unaffected.
 */
@Component({
  selector: 'bf-work-item-activity',
  imports: [DatePipe, MatButtonModule, TranslatePipe],
  template: `
    @if (features.activityFeed() && available()) {
      <section class="work-item-activity">
        <h3>{{ 'activity.title' | translate: 'Activity' }}</h3>
        @if (items().length === 0) {
          <p class="activity-empty">
            {{ 'activity.empty' | translate: 'No activity recorded yet.' }}
          </p>
        } @else {
          <ol class="activity-list">
            @for (entry of items(); track entry.auditLogId) {
              <li>
                <span class="activity-action">{{ label(entry.action) }}</span>
                <span class="activity-meta">
                  {{ entry.actorName ?? ('activity.system' | translate: 'System') }}
                  ·
                  <time [attr.datetime]="entry.createdAt">{{
                    entry.createdAt | date: 'short'
                  }}</time>
                </span>
              </li>
            }
          </ol>
          @if (items().length < total()) {
            <button mat-stroked-button type="button" [disabled]="busy()" (click)="more()">
              {{ 'activity.more' | translate: 'Show more' }}
            </button>
          }
        }
      </section>
    }
  `,
  styleUrls: ['./work-item-activity.scss'],
})
export class WorkItemActivity implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private page = 1;

  readonly features = inject(FeatureFlagsService);
  readonly items = signal<ActivityRow[]>([]);
  readonly total = signal(0);
  readonly busy = signal(false);
  readonly available = signal(false);

  ngOnInit() {
    if (!this.features.activityFeed() || !this.objectId) return;
    this.load();
  }

  more() {
    this.page += 1;
    this.load();
  }

  private load() {
    this.busy.set(true);
    this.http
      .get<ActivityPage>(
        `/api/v1/work-items/${this.objectType}/${encodeURIComponent(this.objectId)}/activity`,
        { params: { page: this.page, pageSize: 10 } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (page) => {
          this.busy.set(false);
          this.total.set(page?.total ?? 0);
          // Later pages append, so "show more" grows the list rather than replacing it.
          this.items.update((prev) =>
            this.page === 1 ? (page?.items ?? []) : [...prev, ...(page?.items ?? [])],
          );
          this.available.set(true);
        },
        error: () => {
          this.busy.set(false);
          // An item out of scope, or a disabled feature, simply shows nothing.
          if (this.page === 1) this.available.set(false);
        },
      });
  }

  /**
   * Audit actions are stable codes such as TASK_STARTED. Translate them when a key exists and
   * otherwise show the code, which is still more useful than hiding the event.
   */
  label(action: string): string {
    return action.replace(/_/g, ' ').toLowerCase();
  }
}
