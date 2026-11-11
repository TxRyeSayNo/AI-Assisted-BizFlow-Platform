import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import type { SlaHistoryRow } from './sla-version-history';

@Component({
  selector: 'bf-sla-version-detail',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title id="sla-detail-title">{{ 'slaDetail.title' | translate: 'SLA version' }} {{ data.versionNo }}</h2>
    <mat-dialog-content>
      <p>{{ 'sla.immutableSnapshot' | translate: 'Immutable snapshot' }} · {{ 'audit.readOnly' | translate: 'Read-only' }}</p>
      <dl>
        <dt>{{ 'slaDetail.versionId' | translate: 'Version ID' }}</dt>
        <dd>{{ data.slaVersionId }}</dd>
        <dt>{{ 'sla.col.target' | translate: 'Target' }}</dt>
        <dd>{{ data.targetMinutes }} {{ 'slaDetail.workingMinutes' | translate: 'working minutes' }}</dd>
        <dt>{{ 'sla.col.warning' | translate: 'Warning from start' }}</dt>
        <dd>{{ data.warningMinutes }} {{ 'slaDetail.workingMinutes' | translate: 'working minutes' }}</dd>
      </dl>
      <h3>{{ 'slaDetail.frozenCalendar' | translate: 'Frozen business calendar' }}</h3>
      <dl>
        <dt>{{ 'slaDetail.calendarId' | translate: 'Calendar ID' }}</dt>
        <dd>{{ data.calendar.calendarId }}</dd>
        <dt>{{ 'slaDetail.timeZone' | translate: 'Time zone' }}</dt>
        <dd>{{ data.calendar.timeZone }}</dd>
      </dl>
      <h4>{{ 'slaDetail.workingHours' | translate: 'Working hours' }}</h4>
      <dl>
        @for (day of days; track day.key) {
          <dt>{{ day.keyPrefix | translate: day.label }}</dt>
          <dd>
            @for (range of data.calendar.workingHours[day.key] ?? []; track $index) {
              <span class="interval">{{ range.start }}–{{ range.end }}</span>
            } @empty {
              {{ 'sla.noWorkingTime' | translate: 'No working time' }}
            }
          </dd>
        }
      </dl>
      <h4>{{ 'slaDetail.holidays' | translate: 'Holidays (local dates)' }}</h4>
      @if (data.calendar.holidays.length) {
        <ul>
          @for (date of data.calendar.holidays; track date) {
            <li>{{ date }}</li>
          }
        </ul>
      } @else {
        <p>{{ 'slaDetail.noHolidays' | translate: 'No holidays configured.' }}</p>
      }
      <h3>{{ 'slaDetail.escalationPolicy' | translate: 'Escalation policy' }}</h3>
      @for (level of data.escalationConfig?.levels ?? []; track level.level) {
        <h4>{{ 'slaDetail.level' | translate: 'Level' }} {{ level.level }} · {{ level.afterMinutes }} {{ 'sla.zeroBreachSuffix' | translate: 'working minutes after breach' }}</h4>
        <p>{{ 'slaDetail.recipients' | translate: 'Recipient user IDs' }}</p>
        <ul>
          @for (id of level.recipientUserIds; track id) {
            <li>{{ id }}</li>
          }
        </ul>
      } @empty {
        <p>{{ 'slaDetail.noEscalation' | translate: 'No escalation configured.' }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions
      ><button mat-stroked-button type="button" (click)="dialog.close()">
        {{ 'audit.closeDetails' | translate: 'Close details' }}
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
    { label: 'Monday', key: 'monday', keyPrefix: 'sla.dayMonday' },
    { label: 'Tuesday', key: 'tuesday', keyPrefix: 'sla.dayTuesday' },
    { label: 'Wednesday', key: 'wednesday', keyPrefix: 'sla.dayWednesday' },
    { label: 'Thursday', key: 'thursday', keyPrefix: 'sla.dayThursday' },
    { label: 'Friday', key: 'friday', keyPrefix: 'sla.dayFriday' },
    { label: 'Saturday', key: 'saturday', keyPrefix: 'sla.daySaturday' },
    { label: 'Sunday', key: 'sunday', keyPrefix: 'sla.daySunday' },
  ];
}
