import { ChangeDetectorRef, Component, forwardRef, inject, input } from '@angular/core';
import {
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import { DatePickerComponent } from './date-picker';

@Component({
  selector: 'bf-date-time-picker',
  standalone: true,
  imports: [DatePickerComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DateTimePickerComponent),
      multi: true,
    },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => DateTimePickerComponent), multi: true },
  ],
  template: `
    <fieldset [disabled]="disabled">
      <legend>{{ label() }}</legend>
      <div class="picker-fields">
        <bf-date-picker
          #dayPicker
          class="date-field"
          label="Ngày"
          [value]="day"
          [readonly]="disabled"
          (valueChange)="changeDay($event, dayPicker.invalid)"
          (touched)="touch()"
        />
        <label
          >Giờ
          <select (change)="change('hour', $any($event.target).value)" (blur)="touch()">
            <option value="" [selected]="!hour">Giờ</option>
            @for (value of hours; track value) {
              <option [value]="value" [selected]="value === hour">{{ value }}</option>
            }
          </select>
        </label>
        <span class="time-separator" aria-hidden="true">:</span>
        <label
          >Phút
          <select (change)="change('minute', $any($event.target).value)" (blur)="touch()">
            <option value="" [selected]="!minute">Phút</option>
            @for (value of minutes; track value) {
              <option [value]="value" [selected]="value === minute">{{ value }}</option>
            }
          </select>
        </label>
      </div>
      @if (incomplete) {
        <p class="picker-error" role="status">Vui lòng chọn đủ ngày, giờ và phút.</p>
      } @else {
        <p class="picker-hint">{{ hint() }}</p>
      }
      @if (day || hour || minute) {
        <button type="button" class="clear-date" (click)="clear()">Xóa thời gian</button>
      }
    </fieldset>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
      container-type: inline-size;
    }
    fieldset {
      margin: 0;
      padding: 12px;
      min-width: 0;
      border: 1px solid #8b9aaa;
      border-radius: 6px;
    }
    legend {
      padding: 0 5px;
      font-size: 14px;
      color: #334155;
    }
    .picker-fields {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 70px 8px 70px;
      gap: 8px;
      align-items: end;
    }
    label {
      display: grid;
      gap: 6px;
      font-size: 13px;
      color: #475569;
      min-width: 0;
    }
    @container (max-width: 360px) {
      .picker-fields {
        grid-template-columns: minmax(0, 1fr) 8px minmax(0, 1fr);
      }
      .date-field {
        grid-column: 1 / -1;
      }
    }
    input,
    select {
      box-sizing: border-box;
      width: 100%;
      min-width: 0;
      min-height: 44px;
      padding: 8px;
      border: 1px solid #c5d1df;
      border-radius: 6px;
      font: inherit;
      font-size: 16px;
      color: #0f172a;
      background: #fff;
    }
    input {
      cursor: pointer;
    }
    input:focus-visible,
    select:focus-visible,
    button:focus-visible {
      outline: 2px solid #2563eb;
      outline-offset: 2px;
    }
    .time-separator {
      padding-bottom: 12px;
      color: #64748b;
    }
    .picker-hint,
    .picker-error {
      margin: 8px 0 0;
      font-size: 12px;
      line-height: 1.5;
    }
    .picker-hint {
      color: #64748b;
    }
    .picker-error {
      color: #b91c1c;
    }
    .clear-date {
      margin-top: 6px;
      padding: 5px 0;
      border: 0;
      background: transparent;
      color: #1d4ed8;
      cursor: pointer;
      font: inherit;
      font-size: 13px;
    }
    fieldset:disabled {
      opacity: 0.65;
    }
    button:disabled {
      cursor: default;
    }
  `,
})
export class DateTimePickerComponent implements ControlValueAccessor, Validator {
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly label = input('Thời gian (tùy chọn)');
  readonly hint = input('Bấm vào ngày để mở lịch. Chọn giờ theo định dạng 24 giờ.');
  readonly hours = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0'));
  readonly minutes = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'));
  day = '';
  hour = '';
  minute = '';
  disabled = false;
  private invalidDay = false;
  private onChange = (_: string) => {};
  private onTouched = () => {};
  private onValidationChange = () => {};

  get incomplete(): boolean {
    return !!(this.day || this.hour || this.minute) && !(this.day && this.hour && this.minute);
  }

  writeValue(value: string | null): void {
    this.invalidDay = false;
    const [day = '', time = ''] = (value ?? '').split('T');
    const [hour = '', minute = ''] = time.split(':');
    this.day = day;
    this.hour = hour;
    this.minute = minute;
    this.changeDetector.markForCheck();
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  registerOnValidatorChange(fn: () => void): void {
    this.onValidationChange = fn;
  }
  setDisabledState(disabled: boolean): void {
    this.disabled = disabled;
    this.changeDetector.markForCheck();
  }
  validate(): ValidationErrors | null {
    if (this.invalidDay) return { invalidDate: true };
    if (this.incomplete) return { incompleteDateTime: true };
    return this.day && !/^\d{4}-\d{2}-\d{2}$/.test(this.day) ? { invalidDate: true } : null;
  }
  touch(): void {
    this.onTouched();
  }

  changeDay(value: string, invalid: boolean): void {
    this.invalidDay = invalid;
    this.change('day', value);
  }

  change(part: 'day' | 'hour' | 'minute', value: string): void {
    if (this.disabled) return;
    this[part] = value;
    // Partial values stay invalid rather than silently dropping an intended deadline.
    this.onChange(
      this.day || this.hour || this.minute ? `${this.day}T${this.hour}:${this.minute}` : '',
    );
    this.onValidationChange();
    this.onTouched();
  }
  clear(): void {
    if (this.disabled) return;
    this.day = this.hour = this.minute = '';
    this.invalidDay = false;
    this.onChange('');
    this.onValidationChange();
    this.onTouched();
  }
}
