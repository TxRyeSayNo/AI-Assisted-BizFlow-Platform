import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface ApprovalMatrix {
  amountField: string;
  tiers: { upperLimit: number | null; roleId: string; policy: 'SINGLE' | 'ANY' | 'ALL' }[];
}
@Component({
  selector: 'bf-workflow-approval-matrix',
  imports: [FormsModule],
  template: ` <fieldset [disabled]="readonly">
    <legend>Ma trận hạn mức phê duyệt</legend>
    <p>
      Các mức tăng dần, tính cả giá trị bằng hạn mức. Mức cuối nhận mọi giá trị cao hơn. Chọn cùng
      đơn vị tiền tệ cho trường giá trị.
    </p>
    <label
      >Trường giá trị (mã trường số bắt buộc)
      <select [ngModel]="matrix.amountField" (ngModelChange)="amount($event)">
        <option value="">Chọn trường</option>
        @for (field of fields; track field.key) {
          @if (field.type === 'number' && field.required) {
            <option [value]="field.key">{{ field.label }} ({{ field.key }})</option>
          }
        }
      </select>
    </label>
    @for (tier of matrix.tiers; track $index; let i = $index; let last = $last) {
      <div class="tier">
        @if (last) {
          <p>Mức cuối: không giới hạn</p>
        } @else {
          <label
            >Đến giá trị
            <input
              type="number"
              min="0"
              [ngModel]="tier.upperLimit"
              (ngModelChange)="edit(i, 'upperLimit', $event)"
          /></label>
        }
        <label
          >Vai trò duyệt
          <select [ngModel]="tier.roleId" (ngModelChange)="edit(i, 'roleId', $event)">
            <option value="">Chọn vai trò</option>
            @for (role of roles; track role.id) {
              <option [value]="role.id">{{ role.name }}</option>
            }
          </select></label
        >
        <label
          >Chính sách
          <select [ngModel]="tier.policy" (ngModelChange)="edit(i, 'policy', $event)">
            <option value="SINGLE">Một người duyệt</option>
            <option value="ANY">Bất kỳ ai trong nhóm</option>
            <option value="ALL">Tất cả người trong nhóm</option>
          </select></label
        >
        @if (!last) {
          <button type="button" (click)="remove(i)">Xóa mức</button>
        }
      </div>
    }
    <button type="button" [disabled]="matrix.tiers.length >= 20" (click)="add()">
      Thêm mức giới hạn
    </button>
  </fieldset>`,
  styles: [
    `
      fieldset {
        border: 1px solid #cbd5e1;
        padding: 0.75rem;
        margin: 0.75rem 0;
      }
      label {
        display: grid;
        gap: 0.3rem;
        margin: 0.5rem 0;
      }
      .tier {
        border-top: 1px solid #cbd5e1;
        padding: 0.5rem 0;
      }
      select,
      input {
        padding: 0.5rem;
        width: 100%;
        box-sizing: border-box;
      }
    `,
  ],
})
export class WorkflowApprovalMatrixEditor {
  @Input() matrix: ApprovalMatrix = {
    amountField: '',
    tiers: [{ upperLimit: null, roleId: '', policy: 'SINGLE' }],
  };
  @Input() roles: { id: string; name: string }[] = [];
  @Input() fields: { key: string; label: string; type: string; required: boolean }[] = [];
  @Input() readonly = false;
  @Output() changed = new EventEmitter<ApprovalMatrix>();
  amount(value: string) {
    this.changed.emit({ ...this.matrix, amountField: value });
  }
  edit(index: number, key: string, value: unknown) {
    this.changed.emit({
      ...this.matrix,
      tiers: this.matrix.tiers.map((t, i) => (i === index ? { ...t, [key]: value } : t)),
    });
  }
  remove(index: number) {
    this.changed.emit({ ...this.matrix, tiers: this.matrix.tiers.filter((_, i) => i !== index) });
  }
  add() {
    const tiers = [...this.matrix.tiers];
    tiers.splice(tiers.length - 1, 0, { upperLimit: 0, roleId: '', policy: 'SINGLE' });
    this.changed.emit({ ...this.matrix, tiers });
  }
}
