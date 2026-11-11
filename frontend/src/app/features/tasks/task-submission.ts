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
    <h2 mat-dialog-title id="task-submission-title">Nộp kết quả nhiệm vụ</h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        Gửi kết quả hoàn thành để người quản lý xem xét, xác nhận hoàn tất hoặc yêu cầu chỉnh sửa bổ sung.
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      <mat-form-field appearance="outline">
        <mat-label>Tóm tắt kết quả / sản phẩm bàn giao (tùy chọn)</mat-label>
        <textarea
          matInput
          rows="4"
          [formControl]="content"
          placeholder="Mô tả kết quả đã hoàn thành hoặc dẫn chiếu tài liệu bàn giao..."
        ></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Đóng</button>
      <button mat-flat-button type="button" [disabled]="busy() || terminal()" (click)="save()">
        {{ uncertain() ? 'Thử lại nộp kết quả' : 'Nộp để phê duyệt' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: ['mat-form-field { width: 100%; } mat-dialog-actions { gap: 0.75rem; flex-wrap: wrap; }'],
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
            this.error.set('Vui lòng kiểm tra lại nội dung nộp kết quả và thử lại.');
          } else if ([401, 403, 404, 409].includes(failure.status)) {
            this.terminal.set(true);
            this.error.set(
              'Trạng thái nhiệm vụ, phân công hoặc quyền hạn vừa thay đổi. Vui lòng đóng và kiểm tra lại.',
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
