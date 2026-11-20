import { NgTemplateOutlet } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';

import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import {
  DepartmentOption,
  ProjectDashboardPage,
  ProjectSummaryView,
  progressTone,
} from './dashboard-model';

/**
 * Project overview grouped into large and small projects. What a viewer sees is decided by the
 * backend from their live task-read scope: a company admin sees every project, a manager the
 * projects their departments work on, an employee only the projects they take part in.
 */
@Component({
  selector: 'bf-project-dashboard',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './project-dashboard.html',
  styleUrls: [
    '../organization/departments.scss',
    '../reports/reports.scss',
    './project-dashboard.scss',
  ],
})
export class ProjectDashboard implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly i18n = inject(I18nService);
  readonly session = inject(SessionService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<ProjectDashboardPage | null>(null);
  readonly departments = signal<DepartmentOption[]>([]);

  readonly filters = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    size: new FormControl('', { nonNullable: true }),
    departmentId: new FormControl('', { nonNullable: true }),
  });

  private activeSub?: Subscription;
  private page = 1;

  readonly large = computed(() =>
    (this.data()?.items ?? []).filter((project) => project.sizeCategory === 'LARGE'),
  );
  readonly small = computed(() =>
    (this.data()?.items ?? []).filter((project) => project.sizeCategory === 'SMALL'),
  );
  readonly isEmpty = computed(() => (this.data()?.items ?? []).length === 0);

  /** Department narrowing is only meaningful for someone who can read beyond their own work. */
  readonly canFilterByDepartment = computed(
    () =>
      this.session.hasPermission('tasks.read.tenant') ||
      this.session.hasPermission('tasks.read.managed'),
  );

  readonly isPersonalScope = computed(
    () =>
      !this.session.hasPermission('tasks.read.tenant') &&
      !this.session.hasPermission('tasks.read.managed'),
  );

  ngOnInit(): void {
    if (this.canFilterByDepartment()) this.loadDepartments();
    this.load();
  }

  applyFilters(): void {
    this.page = 1;
    this.load();
  }

  resetFilters(): void {
    this.filters.reset({ search: '', size: '', departmentId: '' });
    this.page = 1;
    this.load();
  }

  nextPage(): void {
    const current = this.data();
    if (!current) return;
    if (current.page * current.pageSize >= current.total) return;
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

  deadlineOf(project: ProjectSummaryView): string | null {
    return project.deadline;
  }

  private load(): void {
    this.activeSub?.unsubscribe();
    this.busy.set(true);
    this.error.set(null);

    const raw = this.filters.getRawValue();
    let params = new HttpParams().set('page', String(this.page)).set('pageSize', '25');
    if (raw.search.trim()) params = params.set('search', raw.search.trim());
    if (raw.size) params = params.set('size', raw.size);
    if (raw.departmentId) params = params.set('departmentId', raw.departmentId);

    this.activeSub = this.http
      .get<ProjectDashboardPage>('/api/v1/dashboard/projects', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.busy.set(false);
        },
        error: (err) => {
          this.error.set(
            err?.error?.message ??
              this.i18n.t('projectDashboard.errLoad', 'Không tải được danh sách dự án.'),
          );
          this.busy.set(false);
        },
      });
  }

  private loadDepartments(): void {
    this.http
      .get<{ items?: DepartmentOption[] }>('/api/v1/departments', {
        params: new HttpParams().set('pageSize', '100'),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => this.departments.set(res?.items ?? []),
        error: () => {
          // A missing department list only removes an optional filter.
        },
      });
  }
}
