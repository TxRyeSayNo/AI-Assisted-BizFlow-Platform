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
      {{ data.resume ? 'Tiếp tục thực hiện' : 'Bắt đầu thực hiện' }}
    </h2>
    <mat-dialog-content>
      <p>{{ data.title }}</p>
      <p>
        {{
          data.resume
            ? 'Tiếp tục xử lý nhiệm vụ này. Thời hạn và lịch sử quá hạn vẫn được giữ nguyên.'
            : 'Chuyển nhiệm vụ đã tiếp nhận sang trạng thái đang thực hiện. Thao tác này chưa nộp kết quả hoặc đóng nhiệm vụ.'
        }}
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" [disabled]="busy()" (click)="close()">Đóng</button>
      <button mat-flat-button type="button" [disabled]="busy() || terminal()" (click)="save()">
        {{ uncertain() ? 'Thử lại thao tác' : data.resume ? 'Xác nhận tiếp tục' : 'Xác nhận bắt đầu' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: ['mat-dialog-actions { gap: 0.75rem; flex-wrap: wrap; }'],
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
              'Nhiệm vụ, phân công hoặc quyền truy cập vừa thay đổi. Vui lòng đóng và kiểm tra lại.',
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
