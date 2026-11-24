import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

export interface SavedFilterView {
  savedFilterId: string;
  scope: string;
  name: string;
  query: string;
  createdAt: string;
  updatedAt: string | null;
}

/**
 * One person's saved list filters (F12).
 *
 * These belong to whoever saved them and to nobody else — the server scopes every read and write to
 * the signed-in user, so there is no shared state here. Applying one only re-runs the list with its
 * query, and the list endpoint re-validates every value and re-applies its read policy, so a saved
 * filter can never widen what its owner may see.
 *
 * Inert until the SavedFilters feature is known to be on, so pages whose existing specs assert that
 * no unexpected request was made stay unaffected.
 */
@Component({
  selector: 'bf-saved-filters',
  imports: [MatButtonModule, ReactiveFormsModule, TranslatePipe],
  template: `
    @if (features.savedFilters() && available()) {
      <section class="saved-filters">
        <span class="saved-filters-title">{{ 'filters.saved' | translate: 'Saved filters' }}:</span>
        @for (filter of items(); track filter.savedFilterId) {
          <span class="saved-filter-chip">
            <button type="button" [disabled]="busy()" (click)="apply.emit(filter.query)">
              {{ filter.name }}
            </button>
            <button
              type="button"
              class="saved-filter-remove"
              [disabled]="busy()"
              [attr.aria-label]="
                ('filters.remove' | translate: 'Delete saved filter') + ': ' + filter.name
              "
              (click)="remove(filter)"
            >
              ×
            </button>
          </span>
        }
        @if (items().length === 0) {
          <span class="saved-filters-none">{{ 'filters.none' | translate: 'None yet' }}</span>
        }
        <input
          type="text"
          [formControl]="name"
          maxlength="80"
          [attr.placeholder]="'filters.nameHint' | translate: 'Name this filter'"
          [attr.aria-label]="'filters.nameHint' | translate: 'Name this filter'"
        />
        <button mat-stroked-button type="button" [disabled]="busy()" (click)="save()">
          {{ 'filters.save' | translate: 'Save current' }}
        </button>
      </section>
    }
  `,
  styleUrls: ['./saved-filters.scss'],
})
export class SavedFilters implements OnInit {
  /** TASK or REQUEST: which list these filters belong to. */
  @Input() scope: 'TASK' | 'REQUEST' = 'TASK';

  /** The query string of the filters currently applied on the page, without a leading "?". */
  @Input() currentQuery = '';

  /** Emits the saved query string when one is chosen, for the page to apply. */
  @Output() readonly apply = new EventEmitter<string>();

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly features = inject(FeatureFlagsService);
  readonly items = signal<SavedFilterView[]>([]);
  readonly busy = signal(false);
  readonly available = signal(false);
  readonly name = new FormControl('', { nonNullable: true });

  ngOnInit() {
    if (!this.features.savedFilters()) return;
    this.load();
  }

  private load() {
    this.http
      .get<SavedFilterView[]>('/api/v1/saved-filters', { params: { scope: this.scope } })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (items) => {
          this.items.set(items ?? []);
          this.available.set(true);
        },
        // A disabled feature, or no workspace, simply shows nothing.
        error: () => this.available.set(false),
      });
  }

  save() {
    const name = this.name.value.trim();
    if (this.busy() || !name) return;
    this.busy.set(true);
    this.http
      .post<SavedFilterView>('/api/v1/saved-filters', {
        scope: this.scope,
        name,
        query: this.currentQuery,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.name.setValue('');
          this.toast.success(this.i18n.t('filters.savedOk', 'Đã lưu bộ lọc.'));
          this.load();
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  remove(filter: SavedFilterView) {
    if (this.busy()) return;
    this.busy.set(true);
    this.http
      .delete(`/api/v1/saved-filters/${encodeURIComponent(filter.savedFilterId)}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.items.update((all) => all.filter((f) => f.savedFilterId !== filter.savedFilterId));
        },
        error: (failure: HttpErrorResponse) => this.failed(failure),
      });
  }

  private failed(failure: HttpErrorResponse) {
    this.busy.set(false);
    // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
    const curated = failure?.status < 500 ? failure?.error?.message : null;
    this.toast.error(curated ?? this.i18n.t('filters.errSave', 'Không lưu được bộ lọc.'));
  }
}
