import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TeamDashboardView, progressTone } from './dashboard-model';

/**
 * Subordinate management. The department choices come from the scope the server returns, and the
 * server refuses a department outside the caller's own management scope, so narrowing the filter
 * is a convenience rather than the access control itself.
 */
@Component({
  selector: 'bf-team-dashboard',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './team-dashboard.html',
  styleUrls: [
    '../organization/departments.scss',
    '../reports/reports.scss',
    './project-dashboard.scss',
  ],
})
export class TeamDashboard implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly session = inject(SessionService);

  readonly data = signal<TeamDashboardView | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly filters = new FormGroup({
    departmentId: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.busy.set(true);
    this.error.set(null);
    const departmentId = this.filters.getRawValue().departmentId;
    let params = new HttpParams();
    if (departmentId) params = params.set('departmentId', departmentId);

    this.http
      .get<TeamDashboardView>('/api/v1/dashboard/team', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.busy.set(false);
        },
        error: (err) => {
          this.error.set(
            err?.error?.message ??
              this.i18n.t(
                'team.errLoad',
                'Không tải được bảng cấp dưới, hoặc phòng ban nằm ngoài phạm vi quản lý của bạn.',
              ),
          );
          this.busy.set(false);
        },
      });
  }

  resetFilter(): void {
    this.filters.reset({ departmentId: '' });
    this.load();
  }

  tone(percent: number): string {
    return progressTone(percent);
  }
}
