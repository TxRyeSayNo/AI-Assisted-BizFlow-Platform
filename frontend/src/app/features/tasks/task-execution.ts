import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-task-execution',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title id="task-execution-title">
      {{ data.resume ? 'Resume work' : 'Start work' }}
    </h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        {{
          data.resume
            ? 'Resume this task in progress. Its deadline and overdue history remain unchanged.'
            : 'Mark your accepted task as in progress. This does not submit results or complete the task.'
        }}
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Close</button>
      <button mat-flat-button type="button" [disabled]="busy() || terminal()" (click)="save()">
        {{ uncertain() ? 'Retry same action' : data.resume ? 'Confirm resume' : 'Confirm start' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: ['mat-dialog-actions { gap: 0.5rem; flex-wrap: wrap; }'],
})
export class TaskExecutionAction {
  readonly data = inject<{ taskId: string; title: string; resume: boolean }>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<TaskExecutionAction>);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly destroy = inject(DestroyRef);
  private readonly key = crypto.randomUUID();
  readonly busy = signal(false);
  readonly uncertain = signal(false);
  readonly terminal = signal(false);
  readonly error = signal('');
  save() {
    if (this.busy() || this.terminal() || !this.session.hasPermission('tasks.execute')) return;
    this.busy.set(true);
    this.error.set('');
    this.dialog.disableClose = true;
    this.http
      .post<{ taskId: string; assignmentId: string; status: string }>(
        `/api/v1/tasks/${this.data.taskId}/start`,
        {},
        { headers: { 'Idempotency-Key': this.key } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (
            result?.taskId !== this.data.taskId ||
            !result.assignmentId ||
            result.status !== 'IN_PROGRESS'
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
          if ([401, 403, 404, 409, 422].includes(failure.status)) {
            this.terminal.set(true);
            this.error.set(
              'The task, assignment or your access changed. Close and review the current task.',
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
      'The outcome is uncertain. Retry the same action safely, or close and review the task.',
    );
  }
}
