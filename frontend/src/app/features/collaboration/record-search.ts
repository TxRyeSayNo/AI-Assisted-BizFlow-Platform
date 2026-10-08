import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export interface RecordSearchResultItem {
  id: string;
  recordType: 'TASK' | 'REQUEST' | string;
  title: string;
  description: string | null;
  status: string;
  priority: string;
  creatorOrRequesterId: string | null;
  creatorOrRequesterName: string | null;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
  isArchived: boolean;
}

export interface RecordSearchPagedResult {
  items: RecordSearchResultItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

@Component({
  selector: 'bf-record-search',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './record-search.html',
  styleUrls: ['../organization/departments.scss', '../requests/requests.scss', './record-search.scss'],
})
export class RecordSearch implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  readonly session = inject(SessionService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly data = signal<RecordSearchPagedResult | null>(null);

  readonly filters = new FormGroup({
    query: new FormControl('', { nonNullable: true }),
    type: new FormControl('ALL', { nonNullable: true }),
    status: new FormControl('', { nonNullable: true }),
    priority: new FormControl('', { nonNullable: true }),
    includeArchived: new FormControl(false, { nonNullable: true }),
  });

  readonly typeOptions = [
    { value: 'ALL', label: 'All Record Types' },
    { value: 'TASK', label: 'Tasks' },
    { value: 'REQUEST', label: 'Requests' },
  ];

  readonly statusOptions = [
    { value: '', label: 'All Statuses' },
    { value: 'DRAFT', label: 'Draft' },
    { value: 'SUBMITTED', label: 'Submitted' },
    { value: 'ASSIGNED', label: 'Assigned' },
    { value: 'ACCEPTED', label: 'Accepted' },
    { value: 'IN_PROGRESS', label: 'In Progress' },
    { value: 'RESOLVED', label: 'Resolved' },
    { value: 'CONFIRMED', label: 'Confirmed' },
    { value: 'COMPLETED', label: 'Completed' },
    { value: 'CLOSED', label: 'Closed' },
    { value: 'REJECTED', label: 'Rejected' },
    { value: 'CANCELLED', label: 'Cancelled' },
    { value: 'OVERDUE', label: 'Overdue' },
  ];

  readonly priorityOptions = [
    { value: '', label: 'All Priorities' },
    { value: 'LOW', label: 'Low' },
    { value: 'MEDIUM', label: 'Medium' },
    { value: 'HIGH', label: 'High' },
    { value: 'CRITICAL', label: 'Critical' },
  ];

  currentPage = 1;
  readonly pageSize = 20;
  private searchSub?: Subscription;

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const q = params.get('q') ?? params.get('query') ?? '';
    const type = params.get('type') ?? 'ALL';
    const status = params.get('status') ?? '';
    const priority = params.get('priority') ?? '';
    const includeArchived = params.get('includeArchived') === 'true';
    const page = parseInt(params.get('page') ?? '1', 10);

    this.filters.patchValue(
      {
        query: q,
        type: type.toUpperCase(),
        status: status.toUpperCase(),
        priority: priority.toUpperCase(),
        includeArchived,
      },
      { emitEvent: false }
    );

    this.currentPage = isNaN(page) || page < 1 ? 1 : page;
    this.executeSearch();
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.syncUrl();
    this.executeSearch();
  }

  resetFilters(): void {
    this.filters.reset({
      query: '',
      type: 'ALL',
      status: '',
      priority: '',
      includeArchived: false,
    });
    this.currentPage = 1;
    this.syncUrl();
    this.executeSearch();
  }

  goToPage(page: number): void {
    if (page < 1) return;
    this.currentPage = page;
    this.syncUrl();
    this.executeSearch();
  }

  private syncUrl(): void {
    const raw = this.filters.getRawValue();
    const queryParams: Record<string, string | boolean | number | null> = {};

    if (raw.query.trim()) queryParams['q'] = raw.query.trim();
    if (raw.type && raw.type !== 'ALL') queryParams['type'] = raw.type;
    if (raw.status) queryParams['status'] = raw.status;
    if (raw.priority) queryParams['priority'] = raw.priority;
    if (raw.includeArchived) queryParams['includeArchived'] = true;
    if (this.currentPage > 1) queryParams['page'] = this.currentPage;

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      replaceUrl: true,
    });
  }

  executeSearch(): void {
    this.searchSub?.unsubscribe();
    this.busy.set(true);
    this.error.set(null);

    const raw = this.filters.getRawValue();
    let params = new HttpParams()
      .set('page', this.currentPage.toString())
      .set('pageSize', this.pageSize.toString());

    if (raw.query.trim()) {
      params = params.set('q', raw.query.trim());
    }
    if (raw.type && raw.type !== 'ALL') {
      params = params.set('type', raw.type);
    }
    if (raw.status) {
      params = params.set('status', raw.status);
    }
    if (raw.priority) {
      params = params.set('priority', raw.priority);
    }
    if (raw.includeArchived) {
      params = params.set('includeArchived', 'true');
    }

    this.searchSub = this.http
      .get<RecordSearchPagedResult>('/api/v1/records/search', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.data.set(result);
          this.busy.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load records.');
          this.busy.set(false);
        },
      });
  }
}
