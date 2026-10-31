import { DatePipe, JsonPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { AuditRow } from './audit-model';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'bf-audit-detail',
  imports: [DatePipe, JsonPipe, MatButtonModule, MatDialogModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title id="audit-detail-title">
      {{ 'audit.detailTitle' | translate: 'Audit event details' }}
    </h2>
    <mat-dialog-content>
      <p>{{ data.scopeLabel }} · {{ 'audit.readOnly' | translate: 'Read-only' }}</p>
      <h3>{{ data.event.action }}</h3>
      <dl>
        <dt>{{ 'audit.eventId' | translate: 'Event ID' }}</dt>
        <dd>{{ data.event.auditLogId }}</dd>
        <dt>{{ 'audit.timeUtc' | translate: 'Time (UTC)' }}</dt>
        <dd>{{ data.event.createdAt | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }}</dd>
        <dt>{{ 'audit.actor' | translate: 'Actor' }}</dt>
        <dd>{{ data.event.actorType }} · {{ data.event.actorId ?? 'No actor ID recorded' }}</dd>
        <dt>{{ 'audit.object' | translate: 'Object' }}</dt>
        <dd>
          {{ data.event.objectType ?? 'No object type recorded' }} ·
          {{ data.event.objectId ?? 'No object ID recorded' }}
        </dd>
      </dl>
      <h3>{{ 'audit.before' | translate: 'Before' }}</h3>
      @if (data.event.before !== null) {
        <pre>{{ data.event.before | json }}</pre>
      } @else {
        <p>{{ 'audit.noBefore' | translate: 'No previous values recorded.' }}</p>
      }
      <h3>{{ 'audit.after' | translate: 'After' }}</h3>
      @if (data.event.after !== null) {
        <pre>{{ data.event.after | json }}</pre>
      } @else {
        <p>{{ 'audit.noAfter' | translate: 'No changed values recorded.' }}</p>
      }
      <h3>{{ 'audit.metadata' | translate: 'Metadata' }}</h3>
      @if (data.event.metadata !== null) {
        <pre>{{ data.event.metadata | json }}</pre>
      } @else {
        <p>{{ 'audit.noMetadata' | translate: 'No metadata recorded.' }}</p>
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
    h3,
    dd {
      overflow-wrap: anywhere;
    }
    dt {
      font-weight: bold;
      margin-top: var(--tp-space-3);
    }
    dd {
      margin: 0;
      font-family: var(--tp-font-mono);
      font-size: var(--tp-small-size);
    }
    pre {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      padding: var(--tp-space-3);
      background: var(--tp-surf-2);
      font-family: var(--tp-font-mono);
      font-size: var(--tp-small-size);
    }
  `,
})
export class AuditDetail {
  readonly data = inject<{ event: AuditRow; scopeLabel: string }>(MAT_DIALOG_DATA);
  readonly dialog = inject(MatDialogRef<AuditDetail>);
}
