import { DatePipe } from '@angular/common';
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

interface Registration {
  companyId: string;
  code: string;
  name: string;
  contactEmail: string;
  status: 'PENDING' | 'ACTIVE' | 'SUSPENDED' | 'INACTIVE';
  createdAt: string;
}
interface RegistrationPage {
  items: Registration[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-company-registrations',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  styleUrl: './company-registrations.scss',
  template: `
    <header class="public-header platform-header">
      <a class="wordmark" routerLink="/workspace">BizFlow</a>
      <nav aria-label="Platform navigation">
        <a routerLink="/workspace">Home</a>
        <a routerLink="/platform/companies" aria-current="page">Companies</a>
      </nav>
      <span>Platform administration</span>
    </header>
    <main id="main-content" class="page-content" tabindex="-1">
      <span class="eyebrow">Company onboarding</span>
      <h1>Company registrations</h1>
      <p>
        Review registration records across the platform. This view does not approve or provision a
        workspace.
      </p>
      <section aria-label="Registration queue" class="registry-panel" [attr.aria-busy]="busy()">
        <form [formGroup]="filters" (ngSubmit)="apply()" class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Company name or code</mat-label>
            <input matInput formControlName="search" maxlength="200" [readonly]="busy()" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Company status</mat-label>
            <mat-select formControlName="status">
              <mat-option value="">All statuses</mat-option>
              <mat-option value="PENDING">Pending</mat-option>
              <mat-option value="ACTIVE">Active</mat-option>
              <mat-option value="SUSPENDED">Suspended</mat-option>
              <mat-option value="INACTIVE">Inactive</mat-option>
            </mat-select>
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="busy()">Apply filters</button>
        </form>
        @if (busy()) {
          <p role="status" class="feedback">Loading company registrations…</p>
        } @else if (denied()) {
          <p role="alert" class="feedback">
            You no longer have permission to view company registrations.
          </p>
        } @else if (error()) {
          <div class="feedback">
            <p role="alert">Company registrations could not be loaded. Please try again.</p>
            <button mat-stroked-button type="button" (click)="load(page())">Retry</button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <p role="status" class="feedback">
              No company registrations match these filters. Try another name, code or status.
            </p>
          } @else {
            <p class="scroll-hint">
              Scroll the table horizontally to see contact, status and registration date.
            </p>
            <div
              class="table-scroll"
              tabindex="0"
              role="region"
              aria-label="Company registration records; scroll horizontally for all columns"
            >
              <table>
                <caption>
                  Company registration records
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Company</th>
                    <th scope="col">Contact</th>
                    <th scope="col">Status</th>
                    <th scope="col">Registered</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of data.items; track item.companyId) {
                    <tr>
                      <th scope="row">
                        {{ item.name }}<span class="company-code">{{ item.code }}</span>
                      </th>
                      <td>{{ item.contactEmail }}</td>
                      <td>
                        <span class="status-label">{{ labels[item.status] }}</span>
                      </td>
                      <td>
                        {{ item.createdAt | date: 'mediumDate' : 'UTC'
                        }}<span class="company-code"
                          >{{ item.createdAt | date: 'shortTime' : 'UTC' }} UTC</span
                        >
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
          <nav aria-label="Registration pages" class="pagination">
            <span role="status">{{ data.total }} records · Page {{ data.page }}</span>
            <button
              mat-stroked-button
              type="button"
              [disabled]="data.page <= 1"
              (click)="load(data.page - 1)"
            >
              Previous
            </button>
            <button
              mat-stroked-button
              type="button"
              [disabled]="data.page * data.pageSize >= data.total"
              (click)="load(data.page + 1)"
            >
              Next
            </button>
          </nav>
        }
      </section>
    </main>
  `,
})
export class CompanyRegistrations {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', status: 'PENDING' };
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<RegistrationPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly labels = {
    PENDING: 'Pending',
    ACTIVE: 'Active',
    SUSPENDED: 'Suspended',
    INACTIVE: 'Inactive',
  };

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
      .get<RegistrationPage>('/api/v1/platform/company-registrations', { params })
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
