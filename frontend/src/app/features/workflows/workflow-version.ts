import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe, JsonPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import type { WorkflowRow } from './workflows';

export interface WorkflowVersionView {
  workflowId: string;
  name: string;
  businessType: string;
  versionId: string;
  versionNo: number;
  status: string;
  publishedAt: string | null;
  definition: Record<string, unknown>;
  steps: {
    stepId: string;
    stepCode: string;
    name: string;
    type: string;
    orderNo: number;
    config: Record<string, unknown>;
  }[];
  transitions: {
    transitionId: string;
    fromState: string;
    toState: string;
    guard: Record<string, unknown> | null;
  }[];
}
@Component({
  selector: 'bf-workflow-version',
  imports: [RouterLink, MatButtonModule, DatePipe, JsonPipe],
  templateUrl: './workflow-version.html',
  styleUrls: ['../organization/departments.scss', './workflows.scss'],
})
export class WorkflowVersion {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private creation?: Subscription;
  readonly session = inject(SessionService);
  readonly version = signal<WorkflowVersionView | null>(null);
  readonly busy = signal(false);
  readonly failure = signal('');
  readonly retryable = signal(false);
  readonly creating = signal(false);
  readonly created = signal<WorkflowRow | null>(null);
  readonly createFailure = signal('');
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => this.load());
  }
  load() {
    this.request?.unsubscribe();
    this.creation?.unsubscribe();
    this.creating.set(false);
    this.created.set(null);
    this.createFailure.set('');
    this.version.set(null);
    this.busy.set(true);
    this.failure.set('');
    this.retryable.set(false);
    const workflowId = this.route.snapshot.paramMap.get('id');
    const versionId = this.route.snapshot.paramMap.get('v');
    if (!workflowId || !versionId) {
      this.busy.set(false);
      this.failure.set('This workflow version was not found.');
      return;
    }
    this.request = this.http
      .get<WorkflowVersionView>(`/api/v1/workflow-versions/${encodeURIComponent(versionId)}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (version) => {
          this.busy.set(false);
          if (version.workflowId !== workflowId) {
            this.failure.set('This workflow version was not found.');
            return;
          }
          this.version.set(version);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.retryable.set(failure.status !== 403 && failure.status !== 404);
          this.failure.set(
            failure.status === 403
              ? 'You no longer have access to this workflow version.'
              : failure.status === 404
                ? 'This workflow version was not found.'
                : 'The workflow version could not be loaded. Please try again.',
          );
        },
      });
  }

  createNextVersion() {
    const current = this.version();
    if (
      !current ||
      this.creating() ||
      this.created() ||
      !this.session.hasPermission('workflows.configure')
    )
      return;
    this.creating.set(true);
    this.createFailure.set('');
    this.creation = this.http
      .post<WorkflowRow>(`/api/v1/workflows/${encodeURIComponent(current.workflowId)}/versions`, {})
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (created) => {
          this.creating.set(false);
          this.created.set(created);
        },
        error: (failure: HttpErrorResponse) => {
          this.creating.set(false);
          this.createFailure.set(
            failure.status === 403
              ? 'You no longer have permission to create workflow versions.'
              : failure.status === 404
                ? 'This workflow was not found.'
                : failure.status === 409
                  ? 'A new version could not be created. Return to the workflow library to check its current state.'
                  : 'Creation could not be confirmed. Check the workflow library before trying again; a draft may have been created.',
          );
        },
      });
  }
}
