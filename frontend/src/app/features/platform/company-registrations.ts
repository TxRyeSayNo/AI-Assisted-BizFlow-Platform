import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface Registration {
  companyId: string;
  code: string;
  name: string;
  taxId?: string;
  contactPersonName?: string;
  contactEmail: string;
  contactPhone?: string;
  status: 'PENDING' | 'ACTIVE' | 'SUSPENDED' | 'INACTIVE';
  createdAt: string;
  evidenceDocumentKey?: string | null;
  evidenceDocumentFileName?: string | null;
  evidenceDocumentDownloadUrl?: string | null;
}

interface RegistrationPage {
  items: Registration[];
  page: number;
  pageSize: number;
  total: number;
}

@Component({
  selector: 'bf-company-registrations',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  styleUrl: './company-registrations.scss',
  template: `
    <header class="public-header platform-header">
      <a class="wordmark" routerLink="/workspace">
        <span class="brand-mark" aria-hidden="true"></span> BizFlow
      </a>
      <nav aria-label="Platform navigation">
        <a routerLink="/workspace">{{ 'platform.home' | translate: 'Home' }}</a>
        <a routerLink="/platform/companies" aria-current="page">{{
          'platform.companies' | translate: 'Companies'
        }}</a>
        <a routerLink="/platform/audit">{{ 'platform.auditLogs' | translate: 'Audit Logs' }}</a>
      </nav>
      <div style="display: flex; align-items: center; gap: 0.75rem;">
        <span>{{ 'platform.admin' | translate: 'Platform Administration' }}</span>
      </div>
    </header>

    <main id="main-content" class="page-content" tabindex="-1">
      <h1>{{ 'platform.title' | translate: 'Quản lý Đăng ký & Cung cấp Doanh nghiệp' }}</h1>
      <p>
        {{
          'platform.subtitle'
            | translate
              : 'Review incoming organization registrations, approve enterprise tenant provisioning, or manage subscription statuses.'
        }}
      </p>

      @if (actionSuccess()) {
        <div class="banner-success" role="status">
          {{ actionSuccess() }}
          <button mat-button type="button" (click)="actionSuccess.set(null)">✕</button>
        </div>
      }

      @if (actionError()) {
        <div class="banner-error" role="alert">
          {{ actionError() }}
          <button mat-button type="button" (click)="actionError.set(null)">✕</button>
        </div>
      }

      <section aria-label="Registration queue" class="registry-panel" [attr.aria-busy]="busy()">
        <form [formGroup]="filters" (ngSubmit)="apply()" class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'platform.searchLabel' | translate: 'Company name or code' }}</mat-label>
            <input matInput formControlName="search" maxlength="200" [readonly]="busy()" />
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'platform.statusLabel' | translate: 'Company status' }}</mat-label>
            <mat-select formControlName="status">
              <mat-option value="">{{
                'departments.allStatuses' | translate: 'All statuses'
              }}</mat-option>
              <mat-option value="PENDING">{{
                'platform.pending' | translate: 'Pending'
              }}</mat-option>
              <mat-option value="ACTIVE">{{
                'departments.active' | translate: 'Active'
              }}</mat-option>
              <mat-option value="SUSPENDED">{{
                'platform.suspended' | translate: 'Suspended'
              }}</mat-option>
              <mat-option value="INACTIVE">{{
                'departments.inactive' | translate: 'Inactive'
              }}</mat-option>
            </mat-select>
          </mat-form-field>
          <button mat-flat-button type="submit" [disabled]="busy()">
            {{ 'departments.applyFilters' | translate: 'Apply filters' }}
          </button>
        </form>

        @if (busy()) {
          <p role="status" class="feedback">
            {{ 'platform.loading' | translate: 'Loading company registrations…' }}
          </p>
        } @else if (denied()) {
          <p role="alert" class="feedback">
            {{
              'platform.denied'
                | translate: 'You no longer have permission to view company registrations.'
            }}
          </p>
        } @else if (error()) {
          <div class="feedback">
            <p role="alert">
              {{
                'platform.error'
                  | translate: 'Company registrations could not be loaded. Please try again.'
              }}
            </p>
            <button mat-stroked-button type="button" (click)="load(page())">
              {{ 'common.retry' | translate: 'Retry' }}
            </button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <p role="status" class="feedback">
              {{ 'platform.empty' | translate: 'No company registrations match these filters.' }}
            </p>
          } @else {
            <div
              class="table-scroll"
              tabindex="0"
              role="region"
              aria-label="Company registration records"
            >
              <table>
                <caption>
                  Company registration queue
                </caption>
                <thead>
                  <tr>
                    <th scope="col">{{ 'platform.col.company' | translate: 'Company' }}</th>
                    <th scope="col">{{ 'platform.col.contact' | translate: 'Contact' }}</th>
                    <th scope="col">{{ 'common.status' | translate: 'Status' }}</th>
                    <th scope="col">{{ 'platform.col.registered' | translate: 'Registered' }}</th>
                    <th scope="col" style="text-align: right;">
                      {{ 'common.actions' | translate: 'Actions' }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of data.items; track item.companyId) {
                    <tr>
                      <th scope="row">
                        <strong>{{ item.name }}</strong>
                        <span class="company-code">{{ item.code }}</span>
                        @if (item.taxId) {
                          <span class="company-code" style="color: #145da0; font-weight: 600;"
                            >MST: {{ item.taxId }}</span
                          >
                        }
                      </th>
                      <td>
                        <div>{{ item.contactEmail }}</div>
                        @if (item.contactPersonName || item.contactPhone) {
                          <div style="font-size: 0.8rem; color: #52657c;">
                            {{ item.contactPersonName }}
                            {{ item.contactPhone ? '· ' + item.contactPhone : '' }}
                          </div>
                        }
                        @if (item.evidenceDocumentDownloadUrl) {
                          <div style="margin-top: 0.35rem;">
                            <a
                              [href]="item.evidenceDocumentDownloadUrl"
                              target="_blank"
                              rel="noopener"
                              style="font-size: 0.8rem; color: #2563eb; font-weight: 600; text-decoration: underline;"
                            >
                              📄 {{ item.evidenceDocumentFileName || ('platform.viewLicense' | translate: 'Xem giấy phép kinh doanh') }}
                            </a>
                          </div>
                        }
                      </td>
                      <td>
                        <span class="status-label status-{{ item.status.toLowerCase() }}">{{
                          labels[item.status]
                        }}</span>
                      </td>
                      <td>
                        {{ item.createdAt | date: 'mediumDate' : 'UTC' }}
                        <span class="company-code"
                          >{{ item.createdAt | date: 'shortTime' : 'UTC' }} UTC</span
                        >
                      </td>
                      <td style="text-align: right;">
                        <div class="action-buttons">
                          @if (item.status === 'PENDING') {
                            <button
                              mat-flat-button
                              color="primary"
                              class="btn-approve"
                              (click)="openApproveModal(item)"
                            >
                              {{ 'platform.approve' | translate: 'Approve' }}
                            </button>
                            <button
                              mat-stroked-button
                              color="warn"
                              class="btn-reject"
                              (click)="openRejectModal(item)"
                            >
                              {{ 'platform.reject' | translate: 'Reject' }}
                            </button>
                          } @else if (item.status === 'ACTIVE') {
                            <button
                              mat-stroked-button
                              color="warn"
                              class="btn-suspend"
                              (click)="updateStatus(item, 'SUSPENDED')"
                            >
                              {{ 'platform.suspend' | translate: 'Suspend' }}
                            </button>
                          } @else if (item.status === 'SUSPENDED') {
                            <button
                              mat-stroked-button
                              color="primary"
                              class="btn-activate"
                              (click)="updateStatus(item, 'ACTIVE')"
                            >
                              {{ 'platform.activate' | translate: 'Activate' }}
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <nav aria-label="Registration pages" class="pagination">
            <span role="status"
              >{{ data.total }} {{ 'platform.records' | translate: 'records' }} ·
              {{ 'common.page' | translate: 'Page' }} {{ data.page }}</span
            >
            <button
              mat-stroked-button
              type="button"
              [disabled]="data.page <= 1"
              (click)="load(data.page - 1)"
            >
              {{ 'common.previous' | translate: 'Previous' }}
            </button>
            <button
              mat-stroked-button
              type="button"
              [disabled]="data.page * data.pageSize >= data.total"
              (click)="load(data.page + 1)"
            >
              {{ 'common.next' | translate: 'Next' }}
            </button>
          </nav>
        }
      </section>

      <!-- Approve Modal Overlay -->
      @if (approvingItem(); as item) {
        <div class="modal-backdrop">
          <div class="modal-card">
            <h2>{{ 'platform.approveModalTitle' | translate: 'Phê duyệt & Cấp Workspace' }}</h2>
            <p class="modal-desc">
              {{ 'platform.approveModalDescBefore' | translate: 'Phê duyệt doanh nghiệp' }} <strong>{{ item.name }}</strong> {{ 'platform.approveModalDescAfter' | translate: 'sẽ tự động khởi tạo không gian làm việc (tenant) và tài khoản Quản trị viên Doanh nghiệp.' }}

            </p>

            <form [formGroup]="approveForm" (ngSubmit)="confirmApprove(item)" class="modal-form">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.tenantKeyLabel' | translate: 'Mã Không gian làm việc (Tenant Key)' }}</mat-label>
                <input matInput formControlName="tenantKey" placeholder="vd: acme-corp" />
                <mat-hint>{{ 'platform.tenantKeyHint' | translate: 'Chữ thường không dấu, số và dấu gạch nối (BR-023)' }}</mat-hint>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.adminFullNameLabel' | translate: 'Họ và tên Quản trị viên' }}</mat-label>
                <input matInput formControlName="adminFullName" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.adminEmailLabel' | translate: 'Email Quản trị viên Doanh nghiệp' }}</mat-label>
                <input matInput type="email" formControlName="adminEmail" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.adminCodeLabel' | translate: 'Mã Nhân viên Quản trị' }}</mat-label>
                <input matInput formControlName="adminEmployeeCode" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.adminPasswordLabel' | translate: 'Mật khẩu Khởi tạo Ban đầu' }}</mat-label>
                <input matInput formControlName="adminPassword" type="password" />
              </mat-form-field>

              <div class="modal-actions">
                <button
                  mat-button
                  type="button"
                  (click)="approvingItem.set(null)"
                  [disabled]="actionBusy()"
                >
                  {{ 'common.cancel' | translate: 'Hủy' }}
                </button>
                <button
                  mat-flat-button
                  color="primary"
                  type="submit"
                  [disabled]="actionBusy() || approveForm.invalid"
                >
                  @if (actionBusy()) {
                    {{ 'platform.provisioning' | translate: 'Đang khởi tạo…' }}
                  } @else {
                    {{ 'platform.confirmProvision' | translate: 'Xác nhận & Cấp Workspace' }}
                  }
                </button>
              </div>
            </form>
          </div>
        </div>
      }

      <!-- Reject Modal Overlay -->
      @if (rejectingItem(); as item) {
        <div class="modal-backdrop">
          <div class="modal-card">
            <h2>{{ 'platform.rejectModalTitle' | translate: 'Từ chối Hồ sơ Doanh nghiệp' }}</h2>
            <p class="modal-desc">
              {{ 'platform.rejectModalDescBefore' | translate: 'Vui lòng cung cấp lý do từ chối hồ sơ đăng ký của' }}
              <strong>{{ item.name }}</strong> {{ 'platform.rejectModalDescAfter' | translate: '(bắt buộc theo quy định BR-022).' }}
            </p>

            <form [formGroup]="rejectForm" (ngSubmit)="confirmReject(item)" class="modal-form">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'platform.rejectReasonLabel' | translate: 'Lý do từ chối' }}</mat-label>
                <textarea
                  matInput
                  formControlName="reason"
                  rows="4"
                  placeholder="Nêu rõ lý do từ chối hồ sơ này (tối thiểu 5 ký tự)…"
                ></textarea>
                @if (
                  rejectForm.controls.reason.hasError('required') ||
                  rejectForm.controls.reason.hasError('minlength')
                ) {
                  <mat-error>{{ 'platform.rejectReasonMin' | translate: 'Cần nhập lý do từ chối tối thiểu 5 ký tự.' }}</mat-error>
                }
              </mat-form-field>

              <div class="modal-actions">
                <button
                  mat-button
                  type="button"
                  (click)="rejectingItem.set(null)"
                  [disabled]="actionBusy()"
                >
                  {{ 'common.cancel' | translate: 'Hủy' }}
                </button>
                <button
                  mat-flat-button
                  color="warn"
                  type="submit"
                  [disabled]="actionBusy() || rejectForm.invalid"
                >
                  @if (actionBusy()) {
                    {{ 'platform.processing' | translate: 'Đang xử lý…' }}
                  } @else {
                    {{ 'platform.confirmReject' | translate: 'Từ chối Hồ sơ' }}
                  }
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </main>
  `,
})
export class CompanyRegistrations {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { search: '', status: 'PENDING' };

  readonly filters = this.fb.nonNullable.group(this.applied);
  readonly result = signal<RegistrationPage | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);

  readonly approvingItem = signal<Registration | null>(null);
  readonly rejectingItem = signal<Registration | null>(null);
  readonly actionBusy = signal(false);
  readonly actionSuccess = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  readonly approveForm = this.fb.nonNullable.group({
    tenantKey: ['', [Validators.required, Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)]],
    adminEmployeeCode: ['ADMIN01', [Validators.required, Validators.maxLength(50)]],
    adminEmail: ['', [Validators.required, Validators.email]],
    adminFullName: ['', [Validators.required, Validators.maxLength(200)]],
    adminPassword: ['Password123!', [Validators.required, Validators.minLength(8)]],
    timeZone: ['UTC'],
  });

  readonly rejectForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(1000)]],
  });

  readonly labels = {
    PENDING: 'Chờ duyệt',
    ACTIVE: 'Hoạt động',
    SUSPENDED: 'Tạm khóa',
    INACTIVE: 'Ngừng hoạt động',
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

  openApproveModal(item: Registration) {
    const slug = item.code
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '');
    this.approveForm.patchValue({
      tenantKey: slug || 'tenant',
      adminEmployeeCode: 'ADMIN01',
      adminEmail: item.contactEmail,
      adminFullName: item.contactPersonName || `${item.name} Administrator`,
      adminPassword: 'Password123!',
      timeZone: 'UTC',
    });
    this.approvingItem.set(item);
  }

  confirmApprove(item: Registration) {
    if (this.approveForm.invalid) return;
    this.actionBusy.set(true);
    this.actionError.set(null);

    this.http
      .post(
        `/api/v1/platform/company-registrations/${item.companyId}/approve`,
        this.approveForm.getRawValue(),
      )
      .subscribe({
        next: () => {
          this.actionBusy.set(false);
          this.approvingItem.set(null);
          this.actionSuccess.set(`Company ${item.name} approved and provisioned successfully!`);
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.actionBusy.set(false);
          this.actionError.set(err.error?.message || 'Failed to approve company registration.');
        },
      });
  }

  openRejectModal(item: Registration) {
    this.rejectForm.reset();
    this.rejectingItem.set(item);
  }

  confirmReject(item: Registration) {
    if (this.rejectForm.invalid) return;
    this.actionBusy.set(true);
    this.actionError.set(null);

    this.http
      .post(
        `/api/v1/platform/company-registrations/${item.companyId}/reject`,
        this.rejectForm.getRawValue(),
      )
      .subscribe({
        next: () => {
          this.actionBusy.set(false);
          this.rejectingItem.set(null);
          this.actionSuccess.set(`Company ${item.name} registration rejected.`);
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.actionBusy.set(false);
          this.actionError.set(err.error?.message || 'Failed to reject company registration.');
        },
      });
  }

  updateStatus(item: Registration, status: 'ACTIVE' | 'SUSPENDED' | 'INACTIVE') {
    this.actionError.set(null);
    this.http
      .patch(`/api/v1/platform/company-registrations/${item.companyId}/status`, { status })
      .subscribe({
        next: () => {
          this.actionSuccess.set(`Company ${item.name} status updated to ${status}.`);
          this.load(this.page());
        },
        error: (err: HttpErrorResponse) => {
          this.actionError.set(err.error?.message || 'Failed to update company status.');
        },
      });
  }
}
