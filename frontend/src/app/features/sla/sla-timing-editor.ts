import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { SessionService } from '../../core/auth/session';
import type { SlaHistoryRow } from './sla-version-history';

export interface SlaTimingData {
  profileId: string;
  profileName: string;
  source: SlaHistoryRow;
}
export interface SlaTimingOutcome {
  versionNo?: number;
  reviewHistory: boolean;
}
interface CreatedVersion {
  slaVersionId: string;
  slaProfileId: string;
  versionNo: number;
  targetMinutes: number;
  warningMinutes: number;
  calendarId: string;
}

@Component({
  selector: 'bf-sla-timing-editor',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './sla-timing-editor.html',
  styleUrl: './sla-timing-editor.scss',
})
export class SlaTimingEditor {
  readonly data = structuredClone(inject<SlaTimingData>(MAT_DIALOG_DATA));
  readonly session = inject(SessionService);
  readonly dialog = inject<MatDialogRef<SlaTimingEditor, SlaTimingOutcome>>(MatDialogRef);
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly busy = signal(false);
  readonly uncertain = signal(false);
  readonly terminal = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group(
    {
      targetMinutes: [
        this.data.source.targetMinutes,
        [Validators.required, Validators.min(1), Validators.max(2147483647)],
      ],
      warningMinutes: [
        this.data.source.warningMinutes,
        [Validators.required, Validators.min(0), Validators.max(2147483646)],
      ],
    },
    {
      validators: (control: AbstractControl) => {
        const target = control.get('targetMinutes')?.value;
        const warning = control.get('warningMinutes')?.value;
        return Number.isInteger(target) &&
          Number.isInteger(warning) &&
          target > 0 &&
          warning >= 0 &&
          warning < target
          ? null
          : { timing: true };
      },
    },
  );

  save() {
    if (this.busy() || this.uncertain() || this.terminal()) return;
    if (!this.session.hasPermission('sla.configure')) {
      this.terminal.set(true);
      this.error.set('You no longer have permission to configure SLA versions.');
      return;
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const timing = this.form.getRawValue();
    const payload = {
      ...timing,
      calendarId: this.data.source.calendar.calendarId,
      escalationConfig: this.data.source.escalationConfig,
    };
    this.busy.set(true);
    this.error.set(null);
    this.dialog.disableClose = true;
    this.http
      .post<CreatedVersion>(
        `/api/v1/sla-profiles/${encodeURIComponent(this.data.profileId)}/versions`,
        payload,
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (created) => {
          this.busy.set(false);
          if (
            !created ||
            created.slaProfileId !== this.data.profileId ||
            !created.slaVersionId ||
            !Number.isInteger(created.versionNo) ||
            created.versionNo <= this.data.source.versionNo ||
            created.targetMinutes !== timing.targetMinutes ||
            created.warningMinutes !== timing.warningMinutes ||
            created.calendarId !== payload.calendarId
          ) {
            this.unknownOutcome();
            return;
          }
          this.terminal.set(true); // Keep submission disabled throughout the closing animation.
          this.dialog.disableClose = false;
          this.dialog.close({ versionNo: created.versionNo, reviewHistory: true });
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          if (![400, 401, 403, 404, 409, 422].includes(failure.status)) {
            this.unknownOutcome();
            return;
          }
          this.dialog.disableClose = false;
          this.terminal.set([401, 403, 404].includes(failure.status));
          this.error.set(
            failure.status === 401 || failure.status === 403
              ? 'You no longer have permission to configure SLA versions.'
              : failure.status === 404
                ? 'The profile or calendar is no longer available.'
                : failure.error?.code === 'SLA.INVALID_ESCALATION_TARGET'
                  ? 'The preserved escalation policy has an inactive or unavailable recipient. Its configuration must be corrected before this version can be saved.'
                  : 'The version was not saved. Check the timing values and selected calendar or policy before trying again.',
          );
        },
      });
  }
  private unknownOutcome() {
    this.uncertain.set(true);
    this.dialog.disableClose = true;
    this.error.set(
      'The save outcome is unknown. A version may have been created. Review saved history before making another version; this form will not submit again.',
    );
  }
  close() {
    if (this.busy()) return;
    this.dialog.close(this.uncertain() ? { reviewHistory: true } : undefined);
  }
}
