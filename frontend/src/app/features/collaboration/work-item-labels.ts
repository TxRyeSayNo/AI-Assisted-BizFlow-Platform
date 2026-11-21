import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface LabelView {
  labelId: string;
  name: string;
  colour: string;
  archived: boolean;
  usageCount: number;
}

export interface WorkItemLabelsView {
  objectType: string;
  objectId: string;
  labels: LabelView[];
}

/**
 * The labels carried by one task or request (F6). Inert until the Labels feature is known to be
 * on, so pages whose existing specs assert that no unexpected request was made stay unaffected.
 */
@Component({
  selector: 'bf-work-item-labels',
  imports: [MatButtonModule, TranslatePipe],
  template: `
    @if (features.labels() && available()) {
      <section class="work-item-labels">
        <h3>{{ 'labels.onItem' | translate: 'Labels' }}</h3>
        <div class="label-chips">
          @for (label of attached(); track label.labelId) {
            <span class="label-chip" [style.border-color]="label.colour">
              <span class="label-dot" [style.background]="label.colour" aria-hidden="true"></span>
              {{ label.name }}
              <button
                type="button"
                class="label-remove"
                [disabled]="busy()"
                [attr.aria-label]="
                  ('labels.detach' | translate: 'Remove label') + ': ' + label.name
                "
                (click)="detach(label.labelId)"
              >
                ×
              </button>
            </span>
          }
          @if (attached().length === 0) {
            <span class="label-none">{{ 'labels.none' | translate: 'No labels yet' }}</span>
          }
        </div>
        @if (addable().length > 0) {
          <div class="label-picker">
            <span class="label-picker-title">{{ 'labels.add' | translate: 'Add a label' }}:</span>
            @for (label of addable(); track label.labelId) {
              <button
                type="button"
                class="label-chip label-chip-add"
                [style.border-color]="label.colour"
                [disabled]="busy()"
                (click)="attach(label.labelId)"
              >
                <span class="label-dot" [style.background]="label.colour" aria-hidden="true"></span>
                {{ label.name }}
              </button>
            }
          </div>
        }
      </section>
    }
  `,
  styleUrls: ['./work-item-labels.scss'],
})
export class WorkItemLabels implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly session = inject(SessionService);
  readonly features = inject(FeatureFlagsService);
  readonly attached = signal<LabelView[]>([]);
  readonly catalogue = signal<LabelView[]>([]);
  readonly busy = signal(false);
  readonly available = signal(false);

  /** Catalogue entries not already on the item, and never a retired one. */
  readonly addable = computed(() => {
    const on = new Set(this.attached().map((label) => label.labelId));
    return this.catalogue().filter((label) => !label.archived && !on.has(label.labelId));
  });

  private get url() {
    return `/api/v1/work-items/${this.objectType}/${encodeURIComponent(this.objectId)}/labels`;
  }

  ngOnInit() {
    if (!this.features.labels() || !this.objectId) return;
    this.http
      .get<WorkItemLabelsView>(this.url)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (view) => {
          this.attached.set(view?.labels ?? []);
          this.available.set(true);
          this.loadCatalogue();
        },
        // An item out of scope, or a disabled feature, simply shows nothing.
        error: () => this.available.set(false),
      });
  }

  attach(labelId: string) {
    this.send(this.http.put<WorkItemLabelsView>(`${this.url}/${encodeURIComponent(labelId)}`, {}));
  }

  detach(labelId: string) {
    this.send(this.http.delete<WorkItemLabelsView>(`${this.url}/${encodeURIComponent(labelId)}`));
  }

  private send(request: ReturnType<HttpClient['put']>) {
    if (this.busy()) return;
    this.busy.set(true);
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (view) => {
        this.attached.set((view as WorkItemLabelsView)?.labels ?? []);
        this.busy.set(false);
      },
      error: (failure: HttpErrorResponse) => {
        this.busy.set(false);
        // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
        const curated = failure?.status < 500 ? failure?.error?.message : null;
        this.toast.error(curated ?? this.i18n.t('labels.errUpdate', 'Không cập nhật được nhãn.'));
      },
    });
  }

  private loadCatalogue() {
    this.http
      .get<LabelView[]>('/api/v1/labels')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (items) => this.catalogue.set(items ?? []),
        error: () => this.catalogue.set([]),
      });
  }
}
