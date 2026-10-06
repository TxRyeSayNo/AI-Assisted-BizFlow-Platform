import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'bf-feedback',
  imports: [MatButtonModule],
  template: `
    <section class="feedback" [attr.aria-busy]="state() === 'loading'" aria-live="polite">
      <span class="feedback-mark" aria-hidden="true">{{
        state() === 'error' ? '!' : state() === 'denied' ? '×' : '—'
      }}</span>
      <div>
        <h2>{{ title() }}</h2>
        <p>{{ message() }}</p>
        @if (retryable()) {
          <button mat-stroked-button type="button" (click)="retry.emit()">Try again</button>
        }
      </div>
    </section>
  `,
})
export class Feedback {
  readonly state = input.required<'loading' | 'empty' | 'error' | 'denied'>();
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly retryable = input(false);
  readonly retry = output<void>();
}
