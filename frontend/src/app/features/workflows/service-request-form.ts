import {
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  inject,
  signal,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';

export interface ServiceFormField {
  key: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'select';
  required: boolean;
  min?: number;
  max?: number;
  options?: string[];
}
@Component({
  selector: 'bf-service-request-form',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (error()) {
      <p role="alert">{{ error() }}</p>
      <button type="button" (click)="ngOnChanges()">Tải lại biểu mẫu</button>
    }
    @if (fields().length) {
      <fieldset>
        <legend>Thông tin dịch vụ</legend>
        @for (field of fields(); track field.key) {
          <label [for]="'service-field-' + field.key"
            >{{ field.label }}{{ field.required ? ' *' : '' }}</label
          >
          @if (field.type === 'select') {
            <select
              [id]="'service-field-' + field.key"
              [ngModel]="values[field.key]"
              (ngModelChange)="update(field.key, $event)"
              [ngModelOptions]="{ standalone: true }"
              [required]="field.required"
            >
              <option value="">Chọn giá trị</option>
              @for (option of field.options || []; track option) {
                <option [value]="option">{{ option }}</option>
              }
            </select>
          } @else {
            <input
              [id]="'service-field-' + field.key"
              [type]="field.type"
              [ngModel]="values[field.key]"
              (ngModelChange)="update(field.key, $event)"
              [ngModelOptions]="{ standalone: true }"
              [required]="field.required"
              [min]="field.min ?? null"
              [max]="field.max ?? null"
              maxlength="4000"
            />
          }
        }
      </fieldset>
    }
  `,
  styles: [
    `
      fieldset {
        display: grid;
        gap: 0.6rem;
        margin: 1rem 0;
        border: 1px solid #cbd5e1;
        padding: 1rem;
        border-radius: 8px;
      }
      input,
      select {
        padding: 0.7rem;
        max-width: 100%;
      }
    `,
  ],
})
export class ServiceRequestForm implements OnChanges {
  @Input() serviceId = '';
  @Output() valueChanged = new EventEmitter<Record<string, string | number>>();
  @Output() validChanged = new EventEmitter<boolean>();
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  readonly fields = signal<ServiceFormField[]>([]);
  readonly error = signal('');
  values: Record<string, string | number> = {};
  ngOnChanges() {
    this.request?.unsubscribe();
    this.values = {};
    this.fields.set([]);
    this.error.set('');
    this.valueChanged.emit({});
    this.validChanged.emit(!this.serviceId);
    if (!this.serviceId) return;
    this.request = this.http
      .get<ServiceFormField[]>(`/api/v1/services/${this.serviceId}/form`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (fields) => {
          this.fields.set(fields);
          this.emit();
        },
        error: () => {
          this.error.set('Không tải được biểu mẫu dịch vụ. Vui lòng thử lại trước khi gửi.');
          this.validChanged.emit(false);
        },
      });
  }
  update(key: string, value: string | number | null) {
    if (value === null || value === '') delete this.values[key];
    else this.values[key] = value;
    this.emit();
  }
  private emit() {
    this.valueChanged.emit({ ...this.values });
    this.validChanged.emit(
      this.fields().every((f) => {
        const value = this.values[f.key];
        if (value === undefined || value === '') return !f.required;
        return (
          f.type !== 'number' ||
          (typeof value === 'number' &&
            Number.isFinite(value) &&
            (f.min == null || value >= f.min) &&
            (f.max == null || value <= f.max))
        );
      }),
    );
  }
}
