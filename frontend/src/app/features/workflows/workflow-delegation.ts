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
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'bf-workflow-delegation',
  imports: [FormsModule],
  template: `
    <details>
      <summary>Ủy quyền xử lý bước</summary>
      <p>
        Mỗi người nhận đại diện một lượt chưa quyết định. Thời hạn tối đa 90 ngày; quản trị viên có
        thể thu hồi.
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      <label
        >Người được giao ban đầu
        <select [(ngModel)]="principalId" [disabled]="busy()">
          <option value="">Chọn người</option>
          @for (candidate of candidates; track candidate.userId) {
            @if (!candidate.decided) {
              <option [value]="candidate.userId">{{ candidate.name }}</option>
            }
          }
        </select>
      </label>
      <label
        >Người nhận ủy quyền
        <select [(ngModel)]="delegateId" [disabled]="busy() || !users().length">
          <option value="">Chọn người</option>
          @for (user of users(); track user.id) {
            @if (!isOriginal(user.id)) {
              <option [value]="user.id">{{ user.fullName }} ({{ user.employeeCode }})</option>
            }
          }
        </select>
      </label>
      <label
        >Hết hiệu lực (giờ tại máy của bạn)
        <input type="datetime-local" [(ngModel)]="expiresAt" [disabled]="busy()"
      /></label>
      <label
        >Lý do <textarea [(ngModel)]="reason" maxlength="1000" [disabled]="busy()"></textarea>
      </label>
      <button
        type="button"
        [disabled]="busy() || !principalId || !delegateId || !expiresAt || !reason.trim()"
        (click)="save()"
      >
        Xác nhận ủy quyền
      </button>
    </details>
  `,
  styles: [
    `
      details {
        border: 1px solid #cbd5e1;
        padding: 0.75rem;
        margin: 0.75rem 0;
      }
      label {
        display: grid;
        gap: 0.3rem;
        margin: 0.6rem 0;
      }
      input,
      select,
      textarea {
        padding: 0.5rem;
        max-width: 100%;
      }
    `,
  ],
})
export class WorkflowDelegation implements OnInit {
  @Input({ required: true }) requestId = '';
  @Input({ required: true }) stepId = '';
  @Input({ required: true }) revision = 0;
  @Input() candidates: { userId: string; name: string; decided: boolean }[] = [];
  @Output() updated = new EventEmitter<void>();
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly users = signal<{ id: string; fullName: string; employeeCode: string }[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);
  principalId = '';
  delegateId = '';
  expiresAt = '';
  reason = '';
  ngOnInit() {
    this.http
      .get<{ id: string; fullName: string; employeeCode: string }[]>(
        '/api/v1/workflows/lookup/users',
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (users) => this.users.set(users),
        error: () =>
          this.error.set('Không tải được danh sách người nhận. Hãy làm mới hồ sơ để thử lại.'),
      });
  }
  isOriginal(id: string) {
    return this.candidates.some((c) => c.userId === id);
  }
  save() {
    if (this.busy() || !this.principalId || !this.delegateId || !this.reason.trim()) return;
    const expiry = new Date(this.expiresAt);
    if (
      !Number.isFinite(expiry.getTime()) ||
      expiry <= new Date() ||
      expiry.getTime() > Date.now() + 90 * 86400000
    ) {
      this.error.set('Hạn ủy quyền phải trong tương lai và không quá 90 ngày.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.http
      .post(`/api/v1/requests/${this.requestId}/workflow/steps/${this.stepId}/delegations`, {
        revision: this.revision,
        principalId: this.principalId,
        delegateId: this.delegateId,
        expiresAt: expiry.toISOString(),
        reason: this.reason.trim(),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.reason = '';
          this.updated.emit();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(err.error?.message || 'Không thể ủy quyền. Vui lòng làm mới hồ sơ.');
          if (err.status === 409) this.updated.emit();
        },
      });
  }
}
