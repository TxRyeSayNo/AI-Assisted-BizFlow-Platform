import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { DateTimePickerComponent } from './date-time-picker';

@Component({
  imports: [DateTimePickerComponent, ReactiveFormsModule],
  template: '<bf-date-time-picker [formControl]="deadline" />',
})
class ReactiveHost {
  readonly deadline = new FormControl('', { nonNullable: true });
}
@Component({
  imports: [DateTimePickerComponent, FormsModule],
  template: '<bf-date-time-picker [(ngModel)]="deadline" />',
})
class TemplateHost {
  deadline = '';
}
describe('shared date and time selectors', () => {
  it('keeps invalid typed dates invalid until corrected or cleared', async () => {
    const fixture = TestBed.createComponent(ReactiveHost);
    fixture.detectChanges();
    fixture.componentInstance.deadline.setValue('2099-11-25T09:05');
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    enter(input, '31/02/2099');
    await fixture.whenStable();
    expect(fixture.componentInstance.deadline.hasError('invalidDate')).toBe(true);
    expect(input.value).toBe('31/02/2099');
    enter(input, '2099-11-25');
    await fixture.whenStable();
    expect(fixture.componentInstance.deadline.hasError('invalidDate')).toBe(true);
    fixture.nativeElement.querySelector('.clear-date').click();
    await fixture.whenStable();
    expect(fixture.componentInstance.deadline.value).toBe('');
    expect(fixture.componentInstance.deadline.valid).toBe(true);
  });
  function enter(element: HTMLInputElement | HTMLSelectElement, value: string) {
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  }

  it('requires every selected part, emits local minute precision and clears an optional deadline', async () => {
    const fixture = TestBed.createComponent(ReactiveHost);
    fixture.detectChanges();
    const control = fixture.componentInstance.deadline;
    const date = fixture.nativeElement.querySelector('input');
    const [hours, minutes] = fixture.nativeElement.querySelectorAll('select');
    expect(control.valid).toBe(true);
    enter(date, '25/11/2099');
    await fixture.whenStable();
    expect(control.hasError('incompleteDateTime')).toBe(true);
    enter(hours, '09');
    expect(control.invalid).toBe(true);
    enter(minutes, '00');
    fixture.detectChanges();
    expect(control.valid).toBe(true);
    expect(control.value).toBe('2099-11-25T09:00');
    expect(hours.options).toHaveLength(25);
    expect(minutes.options).toHaveLength(61);
    fixture.nativeElement.querySelector('.clear-date').click();
    expect(control.value).toBe('');
    expect(control.valid).toBe(true);
  });

  it('restores saved values, handles midnight and respects disabled state', async () => {
    const fixture = TestBed.createComponent(ReactiveHost);
    fixture.componentInstance.deadline.setValue('2099-01-01T00:05');
    fixture.detectChanges();
    await fixture.whenStable();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    const selects = fixture.nativeElement.querySelectorAll('select');
    expect(input.value).toBe('01/01/2099');
    expect(selects[0].value).toBe('00');
    expect(selects[1].value).toBe('05');
    input.dispatchEvent(new Event('blur'));
    expect(fixture.componentInstance.deadline.touched).toBe(true);
    fixture.componentInstance.deadline.disable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('fieldset').disabled).toBe(true);
    enter(selects[1], '15');
    expect(fixture.componentInstance.deadline.value).toBe('2099-01-01T00:05');
  });

  it('updates the rendered disabled state and values after asynchronous form changes', async () => {
    const fixture = TestBed.createComponent(ReactiveHost);
    fixture.detectChanges();
    fixture.componentInstance.deadline.disable();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('fieldset').disabled).toBe(true);
    fixture.componentInstance.deadline.enable();
    fixture.componentInstance.deadline.setValue('2099-11-25T10:30');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('fieldset').disabled).toBe(false);
    expect(fixture.nativeElement.querySelector('input').value).toBe('25/11/2099');
    expect(fixture.nativeElement.querySelector('select').value).toBe('10');
  });

  it('updates template-driven forms used by the request task dialog', async () => {
    const fixture = TestBed.createComponent(TemplateHost);
    fixture.detectChanges();
    await fixture.whenStable();
    enter(fixture.nativeElement.querySelector('input'), '31/12/2099');
    await fixture.whenStable();
    const [hours, minutes] = fixture.nativeElement.querySelectorAll('select');
    enter(hours, '23');
    enter(minutes, '59');
    await fixture.whenStable();
    expect(fixture.componentInstance.deadline).toBe('2099-12-31T23:59');
  });
});
