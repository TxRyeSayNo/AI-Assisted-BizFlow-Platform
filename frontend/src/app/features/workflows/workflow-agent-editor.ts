import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface WorkflowAgentPolicy {
  mode: 'REVIEW' | 'AUTO';
  instructions: string;
  inputFields: string[];
  includeDescription: boolean;
  includePreviousResults: boolean;
}

@Component({
  selector: 'bf-workflow-agent-editor',
  imports: [FormsModule],
  template: `
    <fieldset [disabled]="readonly">
      <legend>AI Agent cho bước xử lý</legend>
      <label
        ><input type="checkbox" [ngModel]="!!value" (ngModelChange)="toggle($event)" /> Bật AI xử lý
        bước này</label
      >
      @if (value; as agent) {
        <p>
          Người được phân công vẫn chịu trách nhiệm tiếp nhận khi AI cần hỗ trợ. AI không thay bước
          phê duyệt.
        </p>
        <label
          >Chế độ
          <select [ngModel]="agent.mode" (ngModelChange)="patch({ mode: $event })">
            <option value="REVIEW">AI chuẩn bị, người phụ trách kiểm tra</option>
            <option value="AUTO">AI tự hoàn tất bước xử lý</option>
          </select></label
        >
        @if (agent.mode === 'AUTO') {
          <p>
            Chỉ dùng cho xử lý nội bộ có thể kiểm chứng. AI không kiểm tra được tệp đính kèm hoặc hệ
            thống bên ngoài; thiếu dữ kiện sẽ chuyển người phụ trách.
          </p>
        }
        <label
          >Mục tiêu và đầu ra cần bàn giao
          <textarea
            rows="5"
            maxlength="4000"
            [ngModel]="agent.instructions"
            (ngModelChange)="patch({ instructions: $event })"
            placeholder="Ví dụ: đọc dự toán và nhu cầu, lập bảng tóm tắt cùng danh sách thông tin còn thiếu để người duyệt kiểm tra."
          ></textarea>
        </label>
        <p>Chọn trường biểu mẫu AI được đọc (tối đa 20):</p>
        @for (field of fields; track field.key) {
          <label
            ><input
              type="checkbox"
              [ngModel]="agent.inputFields.includes(field.key)"
              (ngModelChange)="fieldToggle(field.key, $event)"
            />
            {{ field.label }}</label
          >
        }
        <label
          ><input
            type="checkbox"
            [ngModel]="agent.includeDescription"
            (ngModelChange)="patch({ includeDescription: $event })"
          />
          Cho phép đọc mô tả yêu cầu</label
        >
        <label
          ><input
            type="checkbox"
            [ngModel]="agent.includePreviousResults"
            (ngModelChange)="patch({ includePreviousResults: $event })"
          />
          Đọc tóm tắt các bước AI đã hoàn tất trước đó</label
        >
      }
    </fieldset>
  `,
  styles: [
    `
      fieldset {
        display: grid;
        gap: 0.75rem;
        padding: 1rem;
        margin: 0.75rem 0;
        border: 1px solid #a5b4fc;
        border-radius: 0.5rem;
      }
      label {
        display: block;
      }
      textarea,
      select {
        width: 100%;
        padding: 0.5rem;
      }
      p {
        margin: 0;
        font-size: 0.875rem;
      }
    `,
  ],
})
export class WorkflowAgentEditor {
  @Input() value?: WorkflowAgentPolicy;
  @Input() fields: { key: string; label: string }[] = [];
  @Input() readonly = false;
  @Output() valueChange = new EventEmitter<WorkflowAgentPolicy | undefined>();
  toggle(enabled: boolean) {
    if (this.readonly) return;
    this.valueChange.emit(
      enabled
        ? {
            mode: 'REVIEW',
            instructions: '',
            inputFields: [],
            includeDescription: false,
            includePreviousResults: false,
          }
        : undefined,
    );
  }
  patch(update: Partial<WorkflowAgentPolicy>) {
    if (this.value && !this.readonly) this.valueChange.emit({ ...this.value, ...update });
  }
  fieldToggle(key: string, selected: boolean) {
    if (!this.value) return;
    const keys = this.value.inputFields.filter((f) => f !== key);
    if (selected && keys.length < 20) keys.push(key);
    this.patch({ inputFields: keys });
  }
}
