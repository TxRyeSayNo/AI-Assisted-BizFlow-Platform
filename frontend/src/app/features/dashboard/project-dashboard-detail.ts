import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';

import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ProjectDashboardDetailView, progressTone, statusBadge } from './dashboard-model';

/**
 * One project: its work items, who holds each of them, how far each has got, and the weighted
 * responsibility split. The backend narrows the rows to the viewer's own task-read scope while
 * keeping the project totals truthful, so the numbers stay comparable between roles.
 */
@Component({
  selector: 'bf-project-dashboard-detail',
  standalone: true,
  imports: [DatePipe, MatButtonModule, RouterLink, TranslatePipe],
  templateUrl: './project-dashboard-detail.html',
  styleUrls: [
    '../organization/departments.scss',
    '../reports/reports.scss',
    './project-dashboard.scss',
  ],
})
export class ProjectDashboardDetail implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly session = inject(SessionService);

  readonly data = signal<ProjectDashboardDetailView | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  private id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  load(): void {
    if (!this.id) {
      this.error.set(this.i18n.t('projectDashboard.errMissingId', 'Thiếu mã dự án.'));
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.http
      .get<ProjectDashboardDetailView>(`/api/v1/dashboard/projects/${this.id}`)
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
                'projectDashboard.errDetail',
                'Dự án không tồn tại hoặc nằm ngoài phạm vi quyền truy cập của bạn.',
              ),
          );
          this.busy.set(false);
        },
      });
  }

  tone(percent: number): string {
    return progressTone(percent);
  }

  badge(status: string): string {
    return statusBadge(status);
  }

  participantLabel(fullName: string, userId: string | null): string {
    if (userId) return fullName;
    return this.i18n.t('projectDashboard.pendingClaim', 'Chưa có người nhận');
  }
}
