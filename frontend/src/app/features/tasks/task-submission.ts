import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-task-submission',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title id="task-submission-title">Submit task result</h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        Submit your completed deliverables for review. Once submitted, a manager will review and
        confirm your results or request rework.
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      <mat-form-field appearance="outline">
        <mat-label>Result summary / Deliverables (optional)</mat-label>
        <textarea
          matInput
          rows="4"
          [formControl]="content"
          placeholder="Describe the completed work or attach deliverable references..."
        ></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Close</button>
      <button mat-flat-button type="button" [disabled]="busy() || terminal()" (click)="save()">
        {{ uncertain() ? 'Retry submission' : 'Submit for review' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: ['mat-form-field { width: 100%; } mat-dialog-actions { gap: 0.5rem; flex-wrap: wrap; }'],
})
export class TaskSubmissionEditor {
  readonly data = inject<{ taskId: string; title: string }>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<TaskSubmissionEditor>);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly destroy = inject(DestroyRef);
  readonly content = new FormControl('', { nonNullable: true });
  readonly busy = signal(false);
  readonly terminal = signal(false);
  readonly uncertain = signal(false);
  readonly error = signal('');
  private key = crypto.randomUUID();
  private submitted: { content: string | null } | undefined;

  save() {
    if (this.busy() || this.terminal() || !this.session.hasPermission('tasks.submit')) return;
    if (!this.uncertain()) this.submitted = { content: this.content.value || null };
    this.busy.set(true);
    this.dialog.disableClose = true;
    this.content.disable();
    this.error.set('');
    this.http
      .post<{
        taskId: string;
        taskResultId: string;
        revisionNo: number;
        status: string;
        submittedAt: string;
      }>(`/api/v1/tasks/${this.data.taskId}/result`, this.submitted, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (
            result?.taskId !== this.data.taskId ||
            !result.taskResultId ||
            result.status !== 'SUBMITTED'
          ) {
            this.unknown();
            return;
          }
          this.terminal.set(true);
          this.dialog.close(true);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (!this.uncertain() && failure.status === 422) {
            this.content.enable();
            this.key = crypto.randomUUID();
            this.error.set('Review your submission content and try again.');
          } else if ([401, 403, 404, 409].includes(failure.status)) {
            this.terminal.set(true);
            this.error.set(
              'The task status, assignment, or your access changed. Close and review the current task.',
            );
          } else this.unknown();
        },
      });
  }

  close() {
    if (!this.busy()) this.dialog.close(this.uncertain() || this.terminal());
  }

  private unknown() {
    this.uncertain.set(true);
    this.error.set(
      'The outcome is uncertain. Retry the submission safely, or close and review the task.',
    );
  }
}
