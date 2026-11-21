import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';

export interface FeatureFlags {
  kanban: boolean;
  calendar: boolean;
  mentions: boolean;
  deadlineReminders: boolean;
  reportExport: boolean;
  labels: boolean;
}

const allOff: FeatureFlags = {
  kanban: false,
  calendar: false,
  mentions: false,
  deadlineReminders: false,
  reportExport: false,
  labels: false,
};

/**
 * Deployment switches reported by `GET /api/v1/features`.
 *
 * Every flag starts off, so a client that has not loaded them yet — or cannot, because the call
 * failed or nobody is signed in — renders exactly what it rendered before the feature existed.
 * The flags hide entry points only: the server refuses a disabled feature on its own, and it
 * remains the sole authority on permissions.
 */
@Injectable({ providedIn: 'root' })
export class FeatureFlagsService {
  private readonly http = inject(HttpClient);
  private readonly state = signal<FeatureFlags>(allOff);
  private requested = false;

  readonly flags = this.state.asReadonly();
  readonly kanban = computed(() => this.state().kanban);
  readonly calendar = computed(() => this.state().calendar);
  readonly mentions = computed(() => this.state().mentions);
  readonly deadlineReminders = computed(() => this.state().deadlineReminders);
  readonly reportExport = computed(() => this.state().reportExport);
  readonly labels = computed(() => this.state().labels);

  /** Idempotent: repeated calls from the shell effect issue at most one in-flight request. */
  load(): void {
    if (this.requested) return;
    this.requested = true;
    this.http.get<Partial<FeatureFlags>>('/api/v1/features').subscribe({
      next: (flags) =>
        this.state.set({
          kanban: flags?.kanban === true,
          calendar: flags?.calendar === true,
          mentions: flags?.mentions === true,
          deadlineReminders: flags?.deadlineReminders === true,
          reportExport: flags?.reportExport === true,
          labels: flags?.labels === true,
        }),
      error: () => {
        // Allow a later attempt, and stay on the pre-feature behaviour until one succeeds.
        this.requested = false;
        this.state.set(allOff);
      },
    });
  }
}
