import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { MyWorkDashboardView, progressTone, statusBadge } from './dashboard-model';

/**
 * The employee dashboard: only the caller's own work items and their own progress, plus the
 * shared headline figures of the projects they take part in. The endpoint takes no user
 * identifier, so this page can never be pointed at a colleague.
 */
@Component({
  selector: 'bf-my-work-dashboard',
  standalone: true,
  imports: [DatePipe, MatButtonModule, RouterLink, TranslatePipe],
  templateUrl: './my-work-dashboard.html',
  styleUrls: [
    '../organization/departments.scss',
    '../reports/reports.scss',
    './project-dashboard.scss',
  ],
})
export class MyWorkDashboard implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly session = inject(SessionService);

  readonly data = signal<MyWorkDashboardView | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  private page = 1;

  readonly canSeeTeam = computed(
    () =>
      this.session.hasPermission('tasks.read.tenant') ||
      this.session.hasPermission('tasks.read.managed'),
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.busy.set(true);
    this.error.set(null);
    const params = new HttpParams().set('page', String(this.page)).set('pageSize', '25');
    this.http
      .get<MyWorkDashboardView>('/api/v1/dashboard/my-work', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.busy.set(false);
        },
        error: (err) => {
          this.error.set(
            err?.error?.message ??
              this.i18n.t('myWork.errLoad', 'Không tải được bảng công việc của bạn.'),
          );
          this.busy.set(false);
        },
      });
  }

  nextPage(): void {
    const current = this.data();
    if (!current || current.page * current.pageSize >= current.total) return;
    this.page = current.page + 1;
    this.load();
  }

  previousPage(): void {
    const current = this.data();
    if (!current || current.page <= 1) return;
    this.page = current.page - 1;
    this.load();
  }

  tone(percent: number): string {
    return progressTone(percent);
  }

  badge(status: string): string {
    return statusBadge(status);
  }
}
