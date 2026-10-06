import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { AuditDetail } from './audit-detail';
import { AuditPage, AuditRow } from './audit-model';

@Component({
  selector: 'bf-audit',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './audit.html',
  styleUrls: ['../organization/departments.scss', './audit.scss'],
})
export class Audit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
  private request?: Subscription;
  private detail?: MatDialogRef<AuditDetail>;
  private applied: Record<string, string> = {};
  readonly session = inject(SessionService);
  readonly platform = inject(ActivatedRoute).snapshot.data['plane'] === 'platform';
  readonly filters = inject(FormBuilder).nonNullable.group({
    action: '',
    actorType: '',
    actorId: '',
    objectType: '',
    objectId: '',
    fromDate: '',
    throughDate: '',
  });
  readonly result = signal<AuditPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly validation = signal('');
  readonly page = signal(1);
  readonly actorLabels = { USER: 'User', SYSTEM: 'System', AI_AGENT: 'AI agent' };

  constructor() {
    this.destroy.onDestroy(() => this.detail?.close());
    this.load(1);
  }

  apply() {
    const values = this.filters.getRawValue();
    const idPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (
      [values.actorId, values.objectId].some(
        (id) =>
          id.trim() &&
          (!idPattern.test(id.trim()) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id.trim())),
      )
    ) {
      this.validation.set('Enter a complete actor or object UUID, or leave the field blank.');
      return;
    }
    const from = this.date(values.fromDate, false);
    const until = this.date(values.throughDate, true);
    if (from === null || until === null || (from && until && from >= until)) {
      this.validation.set('Choose valid UTC dates with the start on or before the through date.');
      return;
    }
    this.validation.set('');
    this.applied = {
      action: values.action.trim(),
      actorType: values.actorType,
      actorId: values.actorId.trim(),
      objectType: values.objectType.trim(),
      objectId: values.objectId.trim(),
      from,
      until,
    };
    this.load(1);
  }

  private date(value: string, nextDay: boolean): string | null {
    if (!value) return '';
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
    const date = new Date(value + 'T00:00:00.000Z');
    if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== value) return null;
    if (nextDay) date.setUTCDate(date.getUTCDate() + 1);
    return date.toISOString();
  }

  load(page: number) {
    this.request?.unsubscribe();
    this.detail?.close();
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    for (const [key, value] of Object.entries(this.applied))
      if (value) params = params.set(key, value);
    this.request = this.http
      .get<AuditPage>('/api/v1/audit-logs', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
        },
      });
  }

  open(row: AuditRow) {
    this.detail?.close();
    this.detail = this.dialog.open(AuditDetail, {
      data: { event: row, scopeLabel: this.platform ? 'Platform event' : 'Workspace event' },
      ariaLabelledBy: 'audit-detail-title',
      autoFocus: 'first-heading',
      restoreFocus: true,
      width: '44rem',
      maxWidth: '100vw',
      height: '100dvh',
      position: { right: '0', top: '0' },
    });
  }
}
