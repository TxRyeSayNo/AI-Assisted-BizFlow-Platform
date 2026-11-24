import {
  ChangeDetectorRef,
  Component,
  Injectable,
  effect,
  forwardRef,
  inject,
  input,
  output,
  viewChild,
  ElementRef,
} from '@angular/core';
import {
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import {
  DateAdapter,
  MAT_DATE_FORMATS,
  MAT_DATE_LOCALE,
  NativeDateAdapter,
} from '@angular/material/core';
import { MatDatepickerIntl, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

@Injectable()
export class VietnameseDateAdapter extends NativeDateAdapter {
  override getFirstDayOfWeek(): number {
    return 1;
  }
  override parse(value: unknown): Date | null {
    if (typeof value !== 'string') return value instanceof Date ? value : null;
    if (!value.trim()) return null;
    const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(value.trim());
    if (!match) return this.invalid();
    try {
      return this.createDate(+match[3], +match[2] - 1, +match[1]);
    } catch {
      return this.invalid();
    }
  }
  override format(date: Date, displayFormat: object): string {
    if (displayFormat === DATE_INPUT)
      return `${String(date.getDate()).padStart(2, '0')}/${String(date.getMonth() + 1).padStart(2, '0')}/${date.getFullYear()}`;
    return super.format(date, displayFormat);
  }
}
const DATE_INPUT = { day: '2-digit', month: '2-digit', year: 'numeric' } as const;
function vietnameseCalendarLabels(): MatDatepickerIntl {
  return Object.assign(new MatDatepickerIntl(), {
    calendarLabel: 'Lịch chọn ngày',
    openCalendarLabel: 'Mở lịch',
    closeCalendarLabel: 'Đóng lịch',
    prevMonthLabel: 'Tháng trước',
    nextMonthLabel: 'Tháng sau',
    prevYearLabel: 'Năm trước',
    nextYearLabel: 'Năm sau',
    prevMultiYearLabel: '24 năm trước',
    nextMultiYearLabel: '24 năm sau',
    switchToMonthViewLabel: 'Chọn ngày',
    switchToMultiYearViewLabel: 'Chọn tháng và năm',
    startDateLabel: 'Ngày bắt đầu',
    endDateLabel: 'Ngày kết thúc',
    comparisonDateLabel: 'Khoảng ngày so sánh',
    formatYearRangeLabel: (start: string, end: string) => `Từ năm ${start} đến năm ${end}`,
  });
}

@Component({
  selector: 'bf-date-picker',
  imports: [MatDatepickerModule, MatFormFieldModule, MatInputModule],
  providers: [
    { provide: MAT_DATE_LOCALE, useValue: 'vi-VN' },
    { provide: DateAdapter, useClass: VietnameseDateAdapter },
    {
      provide: MAT_DATE_FORMATS,
      useValue: {
        parse: { dateInput: DATE_INPUT },
        display: {
          dateInput: DATE_INPUT,
          monthYearLabel: { month: 'long', year: 'numeric' },
          dateA11yLabel: { day: 'numeric', month: 'long', year: 'numeric' },
          monthYearA11yLabel: { month: 'long', year: 'numeric' },
        },
      },
    },
    { provide: MatDatepickerIntl, useFactory: vietnameseCalendarLabels },
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DatePickerComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => DatePickerComponent), multi: true },
  ],
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" floatLabel="always">
      <mat-label>{{ label() }}</mat-label>
      <input
        #dateInput
        matInput
        [matDatepicker]="calendar"
        [value]="date"
        placeholder="dd/mm/yyyy"
        [disabled]="disabled"
        [readonly]="readonly()"
        (click)="open(calendar)"
        (input)="type($any($event.target).value)"
        [attr.aria-invalid]="invalid"
        (dateInput)="change($event.value, $any($event.targetElement).value)"
        (blur)="touch()"
      />
      <mat-datepicker-toggle matIconSuffix [for]="calendar" [disabled]="disabled || readonly()" />
      <mat-datepicker #calendar [disabled]="disabled || readonly()" />
      @if (invalid) {
        <mat-hint>Ngày không hợp lệ. Nhập theo dạng dd/mm/yyyy.</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    mat-form-field {
      width: 100%;
    }
    input {
      cursor: pointer;
    }
  `,
})
export class DatePickerComponent implements ControlValueAccessor, Validator {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly adapter = inject(DateAdapter<Date>);
  private readonly dateInput = viewChild<ElementRef<HTMLInputElement>>('dateInput');
  readonly label = input('Ngày');
  readonly readonly = input(false);
  readonly value = input<string>();
  readonly valueChange = output<string>();
  readonly touched = output<void>();
  date: Date | null = null;
  disabled = false;
  invalid = false;
  private onChange = (_: string) => {};
  private onTouched = () => {};
  private onValidationChange = () => {};
  private currentValue = '';
  constructor() {
    effect(() => {
      const value = this.value();
      if (value !== undefined && value !== this.currentValue) this.writeValue(value);
    });
  }
  writeValue(value: string | null): void {
    this.currentValue = value ?? '';
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
    this.date = match ? new Date(+match[1], +match[2] - 1, +match[3]) : null;
    const input = this.dateInput()?.nativeElement;
    if (input) input.value = this.date ? this.adapter.format(this.date, DATE_INPUT) : '';
    this.invalid = false;
    this.cd.markForCheck();
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
  setDisabledState(value: boolean): void {
    this.disabled = value;
    this.cd.markForCheck();
  }
  validate(): ValidationErrors | null {
    return this.invalid ? { invalidDate: true } : null;
  }
  touch(): void {
    this.onTouched();
    this.touched.emit();
  }
  open(calendar: { open(): void }): void {
    if (!this.disabled && !this.readonly()) calendar.open();
  }
  type(text: string): void {
    const parsed = this.adapter.parse(text, DATE_INPUT);
    this.change(parsed && this.adapter.isValid(parsed) ? parsed : null, text);
  }
  change(value: Date | null, text: string): void {
    if (this.disabled || this.readonly()) return;
    if (value || !text.trim()) this.date = value;
    this.invalid = !value && !!text.trim();
    // Keep invalid input non-empty so a partially entered deadline cannot be silently cleared.
    const result = value
      ? `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
      : this.invalid
        ? text
        : '';
    this.currentValue = result;
    this.onChange(result);
    this.valueChange.emit(result);
    this.onValidationChange();
    this.touch();
  }
}
