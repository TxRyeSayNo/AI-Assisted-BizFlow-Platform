import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

interface UserRow {
  userId: string;
  employeeCode: string;
  fullName: string;
  email: string;
  departmentId: string | null;
  departmentName: string | null;
  status: 'ACTIVE' | 'INACTIVE' | 'LOCKED';
}
interface UserPage {
  items: UserRow[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-users',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './users.html',
  styleUrls: ['./departments.scss', './users.scss'],
})
export class Users {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', status: '' };
  readonly identity = inject(SessionService).identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<UserPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly labels = { ACTIVE: 'Active', INACTIVE: 'Inactive', LOCKED: 'Locked' };

  constructor() {
    this.load(1);
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load(1);
  }

  load(page: number) {
    this.request?.unsubscribe();
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (this.applied.status) params = params.set('status', this.applied.status);
    if (this.applied.search.trim()) params = params.set('search', this.applied.search.trim());
    this.request = this.http
      .get<UserPage>('/api/v1/users', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
          this.busy.set(false);
        },
      });
  }
}
