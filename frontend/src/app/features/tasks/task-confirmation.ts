import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-task-confirmation',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title id="task-confirmation-title">Review task result</h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        Review submitted results and choose whether to confirm completion or return the task for
        rework.
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      <form [formGroup]="form">
        <mat-form-field appearance="outline">
          <mat-label>Decision</mat-label>
          <mat-select formControlName="decision">
            <mat-option value="CONFIRMED">Approve & Complete</mat-option>
            <mat-option value="REWORK">Request Rework</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label
            >Review note
            {{ form.controls.decision.value === 'REWORK' ? '(required)' : '(optional)' }}</mat-label
          >
          <textarea
            matInput
            rows="4"
            formControlName="note"
            placeholder="Provide feedback or reasons for rework..."
          ></textarea>
          @if (form.controls.note.hasError('required')) {
            <mat-error>A note is required when requesting rework.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Close</button>
      <button
        mat-flat-button
        type="button"
        [disabled]="busy() || terminal() || form.invalid"
        (click)="save()"
      >
        {{
          uncertain()
            ? 'Retry review decision'
            : form.controls.decision.value === 'REWORK'
              ? 'Send for rework'
              : 'Confirm completion'
        }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    'mat-form-field { width: 100%; } mat-dialog-actions { gap: 0.5rem; flex-wrap: wrap; } form { display: flex; flex-direction: column; gap: 0.5rem; }',
  ],
})
export class TaskConfirmationEditor {
  readonly data = inject<{ taskId: string; title: string }>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<TaskConfirmationEditor>);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly destroy = inject(DestroyRef);

  readonly form = new FormGroup({
    decision: new FormControl<'CONFIRMED' | 'REWORK'>('CONFIRMED', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true }),
  });

  readonly busy = signal(false);
  readonly terminal = signal(false);
  readonly uncertain = signal(false);
  readonly error = signal('');
  private key = crypto.randomUUID();
  private submitted: { decision: string; note: string | null } | undefined;

  constructor() {
    this.form.controls.decision.valueChanges
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe((decision) => {
        if (decision === 'REWORK') {
          this.form.controls.note.setValidators([Validators.required]);
        } else {
          this.form.controls.note.clearValidators();
        }
        this.form.controls.note.updateValueAndValidity();
      });
  }

  save() {
    if (
      this.busy() ||
      this.terminal() ||
      this.form.invalid ||
      !this.session.hasPermission('tasks.confirm')
    )
      return;
    if (!this.uncertain()) {
      this.submitted = {
        decision: this.form.controls.decision.value,
        note: this.form.controls.note.value.trim() || null,
      };
    }
    this.busy.set(true);
    this.dialog.disableClose = true;
    this.form.disable();
    this.error.set('');
    this.http
      .post<{
        taskId: string;
        confirmationId: string;
        decision: string;
        status: string;
        confirmedAt: string;
      }>(`/api/v1/tasks/${this.data.taskId}/confirmation`, this.submitted, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.dialog.disableClose = false;
          if (
            result?.taskId !== this.data.taskId ||
            !result.confirmationId ||
            (result.status !== 'CONFIRMED' &&
              result.status !== 'COMPLETED' &&
              result.status !== 'IN_PROGRESS')
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
            this.form.enable();
            this.key = crypto.randomUUID();
            this.error.set('Review your confirmation input and note, then try again.');
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
      'The outcome is uncertain. Retry the decision safely, or close and review the task.',
    );
  }
}
