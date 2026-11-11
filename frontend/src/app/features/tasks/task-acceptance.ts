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
  selector: 'bf-task-acceptance',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title id="task-acceptance-title">Tiếp nhận nhiệm vụ</h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        Xác nhận bạn sẽ tiếp nhận và thực hiện nhiệm vụ này. Nếu nhiệm vụ đang thuộc hàng đợi phòng
        ban, thao tác sẽ gán trực tiếp cho bạn. Hệ thống sẽ kiểm tra lại phân công và quyền hạn hiện tại.
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      <mat-form-field appearance="outline">
        <mat-label>Ghi chú tiếp nhận (tùy chọn)</mat-label>
        <textarea matInput rows="4" [formControl]="note"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Đóng</button>
      <button mat-flat-button type="button" [disabled]="busy() || terminal()" (click)="save()">
        {{ uncertain() ? 'Thử lại tiếp nhận' : 'Xác nhận tiếp nhận' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: ['mat-form-field { width: 100%; } mat-dialog-actions { gap: 0.75rem; flex-wrap: wrap; }'],
})
export class TaskAcceptanceEditor {
  readonly data = inject<{ taskId: string; title: string }>(MAT_DIALOG_DATA);
  private readonly dialog = inject(MatDialogRef<TaskAcceptanceEditor>);
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly destroy = inject(DestroyRef);
  readonly note = new FormControl('', { nonNullable: true });
  readonly busy = signal(false);
  readonly terminal = signal(false);
  readonly uncertain = signal(false);
  readonly error = signal('');
  private key = crypto.randomUUID();
  private submitted: { note: string | null } | undefined;
  save() {
    if (this.busy() || this.terminal() || !this.session.hasPermission('tasks.accept')) return;
    if (!this.uncertain()) this.submitted = { note: this.note.value || null };
    this.busy.set(true);
    this.dialog.disableClose = true;
    this.note.disable();
    this.error.set('');
    this.http
      .post<{ taskId: string; assignmentId: string; confirmationId: string; status: string }>(
        `/api/v1/tasks/${this.data.taskId}/accept`,
        this.submitted,
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
            !result.confirmationId ||
            result.status !== 'ACCEPTED'
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
            this.note.enable();
            this.key = crypto.randomUUID();
            this.error.set('Vui lòng kiểm tra lại ghi chú tiếp nhận và thử lại.');
          } else if ([401, 403, 404, 409].includes(failure.status)) {
            this.terminal.set(true);
            this.error.set(
              'Phân công hoặc quyền truy cập vừa thay đổi. Vui lòng đóng hộp thoại và kiểm tra lại nhiệm vụ.',
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
      'Chưa nhận được phản hồi xác nhận từ máy chủ. Bạn có thể thử lại an toàn hoặc đóng để kiểm tra nhiệm vụ.',
    );
  }
}
