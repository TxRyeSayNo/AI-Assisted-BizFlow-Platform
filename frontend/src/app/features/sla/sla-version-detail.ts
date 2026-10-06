import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import type { SlaHistoryRow } from './sla-version-history';

@Component({
  selector: 'bf-sla-version-detail',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title id="sla-detail-title">SLA version {{ data.versionNo }}</h2>
    <mat-dialog-content>
      <p>Immutable snapshot · Read-only</p>
      <dl>
        <dt>Version ID</dt>
        <dd>{{ data.slaVersionId }}</dd>
        <dt>Target</dt>
        <dd>{{ data.targetMinutes }} working minutes</dd>
        <dt>Warning from start</dt>
        <dd>{{ data.warningMinutes }} working minutes</dd>
      </dl>
      <h3>Frozen business calendar</h3>
      <dl>
        <dt>Calendar ID</dt>
        <dd>{{ data.calendar.calendarId }}</dd>
        <dt>Time zone</dt>
        <dd>{{ data.calendar.timeZone }}</dd>
      </dl>
      <h4>Working hours</h4>
      <dl>
        @for (day of days; track day.key) {
          <dt>{{ day.label }}</dt>
          <dd>
            @for (range of data.calendar.workingHours[day.key] ?? []; track $index) {
              <span class="interval">{{ range.start }}–{{ range.end }}</span>
            } @empty {
              No working time
            }
          </dd>
        }
      </dl>
      <h4>Holidays (local dates)</h4>
      @if (data.calendar.holidays.length) {
        <ul>
          @for (date of data.calendar.holidays; track date) {
            <li>{{ date }}</li>
          }
        </ul>
      } @else {
        <p>No holidays configured.</p>
      }
      <h3>Escalation policy</h3>
      @for (level of data.escalationConfig?.levels ?? []; track level.level) {
        <h4>Level {{ level.level }} · {{ level.afterMinutes }} working minutes after breach</h4>
        <p>Recipient user IDs</p>
        <ul>
          @for (id of level.recipientUserIds; track id) {
            <li>{{ id }}</li>
          }
        </ul>
      } @empty {
        <p>No escalation configured.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions
      ><button mat-stroked-button type="button" (click)="dialog.close()">
        Close details
      </button></mat-dialog-actions
    >
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    mat-dialog-content {
      flex: 1;
      max-height: none;
    }
    dt {
      font-weight: bold;
      margin-top: var(--tp-space-3);
    }
    dd {
      margin: 0;
    }
    dd,
    li,
    h4 {
      overflow-wrap: anywhere;
    }
    .interval {
      display: block;
      font-family: var(--tp-font-mono);
    }
  `,
})
export class SlaVersionDetail {
  readonly data = inject<SlaHistoryRow>(MAT_DIALOG_DATA);
  readonly dialog = inject(MatDialogRef<SlaVersionDetail>);
  readonly days = [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ].map((label) => ({ label, key: label.toLowerCase() }));
}
