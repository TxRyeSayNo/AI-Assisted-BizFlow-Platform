import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ServiceFormField } from './service-request-form';

@Component({
  selector: 'bf-workflow-form-editor',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section aria-label="Biểu mẫu dịch vụ">
      <h2>Thông tin cần nhập khi gửi yêu cầu</h2>
      <p>
        Mã trường được dùng trong điều kiện rẽ nhánh. Mỗi phiên bản quy trình giữ biểu mẫu riêng.
      </p>
      @for (field of fields; track $index; let i = $index) {
        <fieldset [disabled]="readonly">
          <legend>Trường {{ i + 1 }}</legend>
          <label
            >Mã trường
            <input
              [ngModel]="field.key"
              (ngModelChange)="edit(i, 'key', $event)"
              pattern="[a-zA-Z0-9_]+"
              maxlength="50"
          /></label>
          <label
            >Tên hiển thị
            <input
              [ngModel]="field.label"
              (ngModelChange)="edit(i, 'label', $event)"
              maxlength="200"
          /></label>
          <label
            >Kiểu dữ liệu
            <select [ngModel]="field.type" (ngModelChange)="edit(i, 'type', $event)">
              <option value="text">Văn bản</option>
              <option value="number">Số</option>
              <option value="date">Ngày</option>
              <option value="select">Danh sách chọn</option>
            </select></label
          >
          <label
            ><input
              type="checkbox"
              [ngModel]="field.required"
              (ngModelChange)="edit(i, 'required', $event)"
            />
            Bắt buộc</label
          >
          @if (field.type === 'number') {
            <label
              >Tối thiểu
              <input type="number" [ngModel]="field.min" (ngModelChange)="edit(i, 'min', $event)"
            /></label>
            <label
              >Tối đa
              <input type="number" [ngModel]="field.max" (ngModelChange)="edit(i, 'max', $event)"
            /></label>
          }
          @if (field.type === 'select') {
            <label
              >Lựa chọn (phân cách bằng dấu phẩy)
              <input
                [ngModel]="(field.options || []).join(', ')"
                (ngModelChange)="options(i, $event)"
            /></label>
          }
          @if (!readonly) {
            <button type="button" (click)="remove(i)">Xóa trường</button>
          }
        </fieldset>
      }
      @if (!readonly) {
        <button type="button" [disabled]="fields.length >= 50" (click)="add()">
          Thêm trường thông tin
        </button>
      }
    </section>
  `,
  styles: [
    `
      section {
        padding: 1rem;
        border: 1px solid #cbd5e1;
        border-radius: 10px;
        margin: 1rem 0;
      }
      fieldset {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
        margin: 0.75rem 0;
      }
      label {
        display: grid;
        gap: 0.3rem;
      }
      input,
      select {
        padding: 0.5rem;
        max-width: 100%;
      }
    `,
  ],
})
export class WorkflowFormEditor {
  @Input() fields: ServiceFormField[] = [];
  @Input() readonly = false;
  @Output() fieldsChange = new EventEmitter<ServiceFormField[]>();
  edit(index: number, key: string, value: unknown) {
    this.fieldsChange.emit(this.fields.map((f, i) => (i === index ? { ...f, [key]: value } : f)));
  }
  options(index: number, text: string) {
    this.edit(
      index,
      'options',
      text
        .split(',')
        .map((x) => x.trim())
        .filter(Boolean),
    );
  }
  remove(index: number) {
    this.fieldsChange.emit(this.fields.filter((_, i) => i !== index));
  }
  add() {
    this.fieldsChange.emit([
      ...this.fields,
      { key: `field_${Date.now()}`, label: 'Thông tin mới', type: 'text', required: true },
    ]);
  }
}
