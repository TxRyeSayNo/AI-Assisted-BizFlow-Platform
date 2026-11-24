import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePickerComponent, VietnameseDateAdapter } from './date-picker';
import { DateAdapter } from '@angular/material/core';
import { By } from '@angular/platform-browser';

@Component({
  imports: [DatePickerComponent, ReactiveFormsModule],
  template: '<bf-date-picker [formControl]="date" [readonly]="locked()" />',
})
class Host {
  readonly locked = signal(false);
  readonly date = new FormControl('2026-11-25', { nonNullable: true });
}

describe('Vietnamese date picker', () => {
  it('restores editing and the calendar toggle after readonly and disabled states', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    fixture.componentInstance.locked.set(true);
    fixture.changeDetectorRef.markForCheck();
    await fixture.whenStable();
    expect(input.readOnly).toBe(true);
    fixture.componentInstance.locked.set(false);
    fixture.changeDetectorRef.markForCheck();
    await fixture.whenStable();
    expect(input.readOnly).toBe(false);
    fixture.componentInstance.date.disable();
    await fixture.whenStable();
    expect(input.disabled).toBe(true);
    fixture.componentInstance.date.enable();
    await fixture.whenStable();
    expect(input.disabled).toBe(false);
  });
  it('displays day/month/year and emits ISO date-only without timezone conversion', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.value).toBe('25/11/2026');
    input.value = '01/02/2027';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.date.value).toBe('2027-02-01');
    expect(fixture.componentInstance.date.valid).toBe(true);
    input.value = '31/02/2027';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.date.hasError('invalidDate')).toBe(true);
    input.value = '';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.date.value).toBe('');
    expect(fixture.componentInstance.date.valid).toBe(true);
  });

  it('uses Vietnamese month names and Monday-first weeks regardless of browser locale', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const picker = fixture.debugElement.query(By.directive(DatePickerComponent));
    const adapter = picker.injector.get(DateAdapter) as VietnameseDateAdapter;
    expect(adapter.getFirstDayOfWeek()).toBe(1);
    expect(adapter.getMonthNames('long')[10].toLocaleLowerCase('vi')).toBe('tháng 11');
    expect(adapter.isValid(adapter.parse('11/25/2026')!)).toBe(false);
    expect(adapter.isValid(adapter.parse('29/02/2028')!)).toBe(true);
    expect(adapter.isValid(adapter.parse('29/02/2027')!)).toBe(false);
  });
});
