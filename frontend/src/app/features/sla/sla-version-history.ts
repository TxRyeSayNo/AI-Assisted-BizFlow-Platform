import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { SlaVersionDetail } from './sla-version-detail';
import { SlaTimingEditor, SlaTimingOutcome } from './sla-timing-editor';

export interface SlaHistoryRow {
  slaVersionId: string;
  versionNo: number;
  targetMinutes: number;
  warningMinutes: number;
  escalationConfig: {
    levels: { level: number; afterMinutes: number; recipientUserIds: string[] }[];
  } | null;
  calendar: {
    calendarId: string;
    timeZone: string;
    workingHours: Record<string, { start: string; end: string }[]>;
    holidays: string[];
  };
}
export interface SlaHistoryPage {
  slaProfileId: string;
  profileName: string;
  items: SlaHistoryRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-sla-version-history',
  imports: [RouterLink, MatButtonModule],
  templateUrl: './sla-version-history.html',
  styleUrls: ['../organization/departments.scss', './sla-version-history.scss'],
})
export class SlaVersionHistory {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private request?: Subscription;
  private detail?: MatDialogRef<SlaVersionDetail>;
  private editor?: MatDialogRef<SlaTimingEditor, SlaTimingOutcome>;
  private profileId = '';
  readonly session = inject(SessionService);
  readonly result = signal<SlaHistoryPage | null>(null);
  readonly busy = signal(false);
  readonly error = signal<'denied' | 'missing' | 'failed' | null>(null);
  readonly page = signal(1);
  readonly notice = signal<string | null>(null);
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe((params) => {
      this.profileId = params.get('id') ?? '';
      this.notice.set(null);
      this.load(1);
    });
    this.destroy.onDestroy(() => {
      this.detail?.close();
      this.editor?.close();
    });
  }
  load(page: number) {
    this.request?.unsubscribe();
    this.detail?.close();
    this.editor?.close();
    this.result.set(null);
    this.error.set(null);
    this.page.set(page);
    if (!this.profileId) {
      this.busy.set(false);
      this.error.set('missing');
      return;
    }
    this.busy.set(true);
    const id = this.profileId;
    const params = new HttpParams().set('page', page).set('pageSize', 25);
    this.request = this.http
      .get<SlaHistoryPage>(`/api/v1/sla-profiles/${encodeURIComponent(id)}/versions`, { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.busy.set(false);
          if (data.slaProfileId !== id) {
            this.error.set('missing');
            return;
          }
          this.result.set(data);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(
            failure.status === 403 ? 'denied' : failure.status === 404 ? 'missing' : 'failed',
          );
        },
      });
  }
  inspect(row: SlaHistoryRow) {
    if (!this.result()?.items.includes(row)) return;
    this.detail?.close();
    this.detail = this.dialog.open(SlaVersionDetail, {
      data: row,
      ariaLabelledBy: 'sla-detail-title',
      autoFocus: 'first-heading',
      restoreFocus: true,
      width: '44rem',
      maxWidth: '100vw',
      height: '100dvh',
      position: { right: '0', top: '0' },
    });
  }
  createTimingVersion(row: SlaHistoryRow) {
    const current = this.result();
    if (!current?.items.includes(row) || !this.session.hasPermission('sla.configure')) return;
    this.detail?.close();
    this.editor?.close();
    this.notice.set(null);
    const profileId = this.profileId;
    this.editor = this.dialog.open(SlaTimingEditor, {
      data: { profileId, profileName: current.profileName, source: row },
      ariaLabelledBy: 'sla-timing-title',
      autoFocus: 'first-heading',
      restoreFocus: true,
      width: '44rem',
      maxWidth: '100vw',
      height: '100dvh',
      position: { right: '0', top: '0' },
    });
    this.editor
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe((outcome) => {
        if (this.profileId !== profileId || !outcome?.reviewHistory) return;
        this.notice.set(
          outcome.versionNo
            ? `SLA version ${outcome.versionNo} created. Earlier snapshots are unchanged.`
            : 'Review saved versions before creating another snapshot.',
        );
        this.load(1);
      });
  }
}
