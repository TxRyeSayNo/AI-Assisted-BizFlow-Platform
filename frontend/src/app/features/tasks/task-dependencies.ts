import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, Input, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

export interface DependencyLink {
  taskId: string;
  title: string;
  status: string;
  settled: boolean;
  deadline: string | null;
}

export interface TaskDependencyView {
  taskId: string;
  dependsOn: DependencyLink[];
  blocks: DependencyLink[];
  blockedBy: number;
}

interface TaskCandidate {
  taskId: string;
  title: string;
  status: string;
}

/**
 * What this task waits for, and what waits on it (F7).
 *
 * The links are advisory on the server too: an open predecessor is shown as a warning and never
 * stops the task being worked on. Inert until the TaskDependencies feature is known to be on, so
 * pages whose existing specs assert that no unexpected request was made stay unaffected.
 */
@Component({
  selector: 'bf-task-dependencies',
  imports: [DatePipe, MatButtonModule, ReactiveFormsModule, RouterLink, TranslatePipe],
  template: `
    @if (features.taskDependencies() && available()) {
      <section class="task-dependencies">
        <h3>{{ 'dependencies.title' | translate: 'Dependencies' }}</h3>

        @if (view()?.blockedBy) {
          <p class="dependency-warning" role="status">
            {{
              'dependencies.warning'
                | translate: 'This task is waiting for work that has not finished yet.'
            }}
          </p>
        }

        <h4>{{ 'dependencies.waitsFor' | translate: 'Waits for' }}</h4>
        @if (view()?.dependsOn?.length) {
          <ul class="dependency-list">
            @for (link of view()!.dependsOn; track link.taskId) {
              <li [class.dependency-open]="!link.settled">
                <a [routerLink]="['/tasks', link.taskId]">{{ link.title }}</a>
                <span class="dependency-status">{{ label(link.status) }}</span>
                @if (link.deadline) {
                  <time [attr.datetime]="link.deadline">{{ link.deadline | date: 'short' }}</time>
                }
                <button
                  type="button"
                  class="dependency-remove"
                  [disabled]="busy()"
                  [attr.aria-label]="
                    ('dependencies.remove' | translate: 'Remove dependency') + ': ' + link.title
                  "
                  (click)="remove(link.taskId)"
                >
                  ×
                </button>
              </li>
            }
          </ul>
        } @else {
          <p class="dependency-none">
            {{ 'dependencies.none' | translate: 'Nothing to wait for' }}
          </p>
        }

        <h4>{{ 'dependencies.blocks' | translate: 'Waited on by' }}</h4>
        @if (view()?.blocks?.length) {
          <ul class="dependency-list">
            @for (link of view()!.blocks; track link.taskId) {
              <li>
                <a [routerLink]="['/tasks', link.taskId]">{{ link.title }}</a>
                <span class="dependency-status">{{ label(link.status) }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="dependency-none">
            {{ 'dependencies.noBlocks' | translate: 'No other task is waiting' }}
          </p>
        }

        <div class="dependency-search">
          <label [attr.for]="'dependency-search-' + objectId">
            {{ 'dependencies.add' | translate: 'Add a task this one waits for' }}
          </label>
          <input
            [id]="'dependency-search-' + objectId"
            type="search"
            [formControl]="search"
            [attr.placeholder]="'dependencies.searchHint' | translate: 'Search by title'"
          />
          <button mat-stroked-button type="button" [disabled]="busy()" (click)="find()">
            {{ 'dependencies.search' | translate: 'Search' }}
          </button>
          @if (candidates().length > 0) {
            <ul class="dependency-candidates">
              @for (candidate of candidates(); track candidate.taskId) {
                <li>
                  <button type="button" [disabled]="busy()" (click)="add(candidate.taskId)">
                    {{ candidate.title }}
                  </button>
                </li>
              }
            </ul>
          } @else if (searched()) {
            <p class="dependency-none">
              {{ 'dependencies.noMatches' | translate: 'No matching task' }}
            </p>
          }
        </div>
      </section>
    }
  `,
  styleUrls: ['./task-dependencies.scss'],
})
export class TaskDependencies implements OnInit {
  @Input({ required: true }) objectId!: string;

  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly features = inject(FeatureFlagsService);
  readonly view = signal<TaskDependencyView | null>(null);
  readonly candidates = signal<TaskCandidate[]>([]);
  readonly searched = signal(false);
  readonly busy = signal(false);
  readonly available = signal(false);
  readonly search = new FormControl('', { nonNullable: true });

  private get url() {
    return `/api/v1/tasks/${encodeURIComponent(this.objectId)}/dependencies`;
  }

  ngOnInit() {
    if (!this.features.taskDependencies() || !this.objectId) return;
    this.http
      .get<TaskDependencyView>(this.url)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (view) => {
          this.view.set(view ?? null);
          this.available.set(true);
        },
        // A task out of scope, or a disabled feature, simply shows nothing.
        error: () => this.available.set(false),
      });
  }

  /** Looks for a predecessor through the ordinary task list, which is already scope-limited. */
  find() {
    const term = this.search.value.trim();
    this.http
      .get<{ items: TaskCandidate[] }>('/api/v1/tasks', {
        params: { page: 1, pageSize: 10, ...(term ? { search: term } : {}) },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (page) => {
          const linked = new Set([
            this.objectId,
            ...(this.view()?.dependsOn ?? []).map((link) => link.taskId),
          ]);
          this.candidates.set((page?.items ?? []).filter((item) => !linked.has(item.taskId)));
          this.searched.set(true);
        },
        error: () => {
          this.candidates.set([]);
          this.searched.set(true);
        },
      });
  }

  add(dependsOnTaskId: string) {
    this.send(this.http.post<TaskDependencyView>(this.url, { dependsOnTaskId }));
  }

  remove(dependsOnTaskId: string) {
    this.send(
      this.http.delete<TaskDependencyView>(`${this.url}/${encodeURIComponent(dependsOnTaskId)}`),
    );
  }

  private send(request: ReturnType<HttpClient['post']>) {
    if (this.busy()) return;
    this.busy.set(true);
    request.pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (view) => {
        this.view.set((view as TaskDependencyView) ?? null);
        this.candidates.set([]);
        this.searched.set(false);
        this.busy.set(false);
      },
      error: (failure: HttpErrorResponse) => {
        this.busy.set(false);
        // Only a 4xx carries a curated application fault message; a 5xx body is never shown.
        const curated = failure?.status < 500 ? failure?.error?.message : null;
        this.toast.error(
          curated ?? this.i18n.t('dependencies.errUpdate', 'Không cập nhật được phụ thuộc.'),
        );
      },
    });
  }

  /** Task states are stable codes such as IN_PROGRESS; show them readably. */
  label(status: string): string {
    return status.replace(/_/g, ' ').toLowerCase();
  }
}
