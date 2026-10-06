import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';

interface CategoryRow {
  serviceCategoryId: string;
  code: string;
  name: string;
  status: 'ACTIVE' | 'INACTIVE';
}
export interface ServiceRow {
  serviceId: string;
  code: string;
  name: string;
  description: string | null;
  status: 'DRAFT' | 'ACTIVE' | 'INACTIVE';
  categories: CategoryRow[];
  activeWorkflowVersionId: string | null;
  activeSlaVersionId: string | null;
  eTag: string;
}
interface ServicePage {
  items: ServiceRow[];
  page: number;
  pageSize: number;
  total: number;
}
@Component({
  selector: 'bf-services',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './services.html',
  styleUrls: ['../organization/departments.scss', './services.scss'],
})
export class Services {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly forms = inject(FormBuilder).nonNullable;
  private request?: Subscription;
  private applied = { search: '', status: '' };
  readonly session = inject(SessionService);
  readonly filters = this.forms.group(this.applied);
  readonly categories = this.forms.array<ReturnType<Services['categoryForm']>>([]);
  readonly form = this.forms.group({
    code: ['', [Validators.required, Validators.maxLength(80)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    active: [true],
    categories: this.categories,
  });
  readonly result = signal<ServicePage | null>(null);
  readonly created = signal<ServiceRow | null>(null);
  readonly busy = signal(false);
  readonly saving = signal(false);
  readonly denied = signal(false);
  readonly error = signal(false);
  readonly mutationError = signal('');
  readonly uncertain = signal(false);
  readonly page = signal(1);
  readonly labels = { DRAFT: 'Draft', ACTIVE: 'Active', INACTIVE: 'Inactive' };
  readonly existingCategoryForm = this.categoryForm();
  readonly selectedService = signal<ServiceRow | null>(null);
  readonly categorySaving = signal(false);
  readonly categoryNeedsReload = signal(false);
  readonly categoryError = signal('');
  readonly categoryCreated = signal('');
  private categoryDraftServiceId?: string;
  readonly metadataForm = this.forms.group({
    code: ['', [Validators.required, Validators.maxLength(80)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    active: [true],
  });
  readonly editingService = signal<ServiceRow | null>(null);
  readonly metadataSaving = signal(false);
  readonly metadataNeedsReload = signal(false);
  readonly metadataError = signal('');
  readonly metadataSaved = signal('');
  readonly mutating = computed(
    () => this.saving() || this.categorySaving() || this.metadataSaving(),
  );
  readonly needsReconciliation = computed(
    () => this.uncertain() || this.categoryNeedsReload() || this.metadataNeedsReload(),
  );
  private metadataDraftServiceId?: string;
  constructor() {
    this.load(1);
  }
  private categoryForm() {
    return this.forms.group({
      code: ['', [Validators.required, Validators.maxLength(80)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      active: [true],
    });
  }
  addCategory() {
    if (!this.mutating()) this.categories.push(this.categoryForm());
  }
  removeCategory(index: number) {
    if (!this.mutating()) this.categories.removeAt(index);
  }
  apply() {
    if (!this.mutating()) {
      this.applied = this.filters.getRawValue();
      this.load(1);
    }
  }
  load(page: number) {
    if (this.mutating()) return;
    this.request?.unsubscribe();
    this.selectedService.set(null);
    this.editingService.set(null);
    this.result.set(null);
    this.busy.set(true);
    this.denied.set(false);
    this.error.set(false);
    this.page.set(page);
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    for (const [key, value] of Object.entries(this.applied))
      if (value.trim()) params = params.set(key, value.trim());
    this.request = this.http
      .get<ServicePage>('/api/v1/services', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.busy.set(false);
          this.uncertain.set(false);
          this.categoryNeedsReload.set(false);
          this.metadataNeedsReload.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.denied.set(failure.status === 403);
          this.error.set(failure.status !== 403);
          this.busy.set(false);
          this.created.set(null);
          this.categoryCreated.set('');
          this.metadataSaved.set('');
        },
      });
  }
  create() {
    this.form.markAllAsTouched();
    if (
      !this.session.hasPermission('service.create') ||
      this.denied() ||
      this.busy() ||
      this.mutating() ||
      this.needsReconciliation() ||
      this.form.invalid
    )
      return;
    const raw = this.form.getRawValue();
    const input = {
      code: raw.code.trim(),
      name: raw.name.trim(),
      description: raw.description.trim() || null,
      active: raw.active,
      categories: raw.categories.map((c) => ({
        code: c.code.trim(),
        name: c.name.trim(),
        active: c.active,
      })),
    };
    if (!input.code || !input.name || input.categories.some((c) => !c.code || !c.name)) {
      this.mutationError.set('Enter a code and name for the service and each category.');
      return;
    }
    if (new Set(input.categories.map((c) => c.code)).size !== input.categories.length) {
      this.mutationError.set('Category codes must be unique within the service.');
      return;
    }
    this.saving.set(true);
    this.mutationError.set('');
    this.created.set(null);
    this.http
      .post<ServiceRow>('/api/v1/services', input)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.saving.set(false);
          this.created.set(row);
          this.categories.clear();
          this.form.reset({ code: '', name: '', description: '', active: true });
          this.applied = { search: row.code, status: '' };
          this.filters.setValue(this.applied);
          this.load(1);
        },
        error: (failure: HttpErrorResponse) => {
          this.saving.set(false);
          if (failure.status === 403) {
            this.denied.set(true);
            this.result.set(null);
            this.selectedService.set(null);
            this.editingService.set(null);
            return;
          }
          const known = failure.status === 422 || failure.status === 409;
          this.uncertain.set(!known);
          this.mutationError.set(
            failure.status === 409
              ? 'A service with this code already exists. Choose another code.'
              : failure.status === 422
                ? 'Check the service and category values: codes up to 80 characters, names up to 200 characters, and plain text.'
                : 'Creation could not be confirmed. Reload the catalog and check for your service before trying again.',
          );
        },
      });
  }
  beginCategory(row: ServiceRow) {
    if (
      !this.session.hasPermission('service.create') ||
      this.busy() ||
      this.mutating() ||
      this.needsReconciliation() ||
      !this.result()?.items.includes(row)
    )
      return;
    if (this.categoryDraftServiceId !== row.serviceId)
      this.existingCategoryForm.reset({ code: '', name: '', active: true });
    this.categoryDraftServiceId = row.serviceId;
    this.selectedService.set(row);
    this.editingService.set(null);
    this.categoryError.set('');
    this.categoryCreated.set('');
  }
  cancelCategory() {
    if (!this.categorySaving()) this.selectedService.set(null);
  }
  saveCategory() {
    this.existingCategoryForm.markAllAsTouched();
    const service = this.selectedService();
    if (
      !service ||
      !this.session.hasPermission('service.create') ||
      this.denied() ||
      this.busy() ||
      this.mutating() ||
      this.needsReconciliation() ||
      this.existingCategoryForm.invalid
    )
      return;
    const raw = this.existingCategoryForm.getRawValue();
    const input = { code: raw.code.trim(), name: raw.name.trim(), active: raw.active };
    if (!input.code || !input.name) {
      this.categoryError.set('Enter a code and name for the category.');
      return;
    }
    this.categorySaving.set(true);
    this.categoryError.set('');
    this.categoryCreated.set('');
    this.http
      .post<ServiceRow>(`/api/v1/services/${service.serviceId}/categories`, input, {
        headers: { 'If-Match': service.eTag },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (updated) => {
          this.categorySaving.set(false);
          if (updated.serviceId !== service.serviceId || !updated.eTag) {
            this.categoryNeedsReload.set(true);
            this.categoryError.set(
              'Saving could not be confirmed. Reload the catalog, check its categories, and select the service again before retrying.',
            );
            return;
          }
          this.result.update((data) =>
            data
              ? {
                  ...data,
                  items: data.items.map((row) =>
                    row.serviceId === updated.serviceId ? updated : row,
                  ),
                }
              : data,
          );
          this.categoryCreated.set(`Category added: ${input.code} to ${service.code}.`);
          this.selectedService.set(null);
          this.categoryDraftServiceId = undefined;
          this.existingCategoryForm.reset({ code: '', name: '', active: true });
        },
        error: (failure: HttpErrorResponse) => {
          this.categorySaving.set(false);
          if (failure.status === 403) {
            this.result.set(null);
            this.selectedService.set(null);
            this.editingService.set(null);
            this.denied.set(true);
            return;
          }
          const duplicate =
            failure.status === 409 && failure.error?.code === 'SERVICE.CATEGORY_CODE_EXISTS';
          const stale =
            failure.status === 409 && failure.error?.code === 'SERVICE.VERSION_CONFLICT';
          this.categoryNeedsReload.set(!duplicate && failure.status !== 422);
          this.categoryError.set(
            duplicate
              ? 'A category with this code already exists in the service. Choose another code.'
              : failure.status === 422
                ? 'Check the category values: code up to 80 characters, name up to 200 characters, and plain text.'
                : stale
                  ? 'The service changed. Reload the catalog, check its categories, and select the service again before retrying.'
                  : 'Saving could not be confirmed. Reload the catalog, check its categories, and select the service again before retrying.',
          );
        },
      });
  }
  beginMetadata(row: ServiceRow) {
    if (
      !this.session.hasPermission('service.update') ||
      this.busy() ||
      this.mutating() ||
      this.needsReconciliation() ||
      !this.result()?.items.includes(row)
    )
      return;
    if (this.metadataDraftServiceId !== row.serviceId)
      this.metadataForm.reset({
        code: row.code,
        name: row.name,
        description: row.description ?? '',
        active: row.status === 'ACTIVE',
      });
    this.metadataDraftServiceId = row.serviceId;
    this.editingService.set(row);
    this.selectedService.set(null);
    this.metadataError.set('');
    this.metadataSaved.set('');
  }
  cancelMetadata() {
    if (!this.mutating()) {
      this.editingService.set(null);
      this.metadataDraftServiceId = undefined;
    }
  }
  saveMetadata() {
    this.metadataForm.markAllAsTouched();
    const service = this.editingService();
    if (
      !service ||
      !this.session.hasPermission('service.update') ||
      this.denied() ||
      this.busy() ||
      this.mutating() ||
      this.needsReconciliation() ||
      this.metadataForm.invalid
    )
      return;
    const raw = this.metadataForm.getRawValue();
    const input = {
      code: raw.code.trim(),
      name: raw.name.trim(),
      description: raw.description.trim() || null,
      active: raw.active,
    };
    if (!input.code || !input.name) {
      this.metadataError.set('Enter a code and name for the service.');
      return;
    }
    this.metadataSaving.set(true);
    this.metadataError.set('');
    this.metadataSaved.set('');
    this.http
      .put<ServiceRow>(`/api/v1/services/${service.serviceId}`, input, {
        headers: { 'If-Match': service.eTag },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.metadataSaving.set(false);
          if (row.serviceId !== service.serviceId || !row.eTag) {
            this.metadataNeedsReload.set(true);
            this.metadataError.set(
              'Saving could not be confirmed. Reload the catalog, check the service, and select it again before retrying.',
            );
            return;
          }
          this.metadataSaved.set(`Service updated: ${row.code} — ${row.name}.`);
          this.created.set(null);
          this.categoryCreated.set('');
          this.metadataDraftServiceId = undefined;
          this.applied = { search: row.code, status: '' };
          this.filters.setValue(this.applied);
          this.load(1);
        },
        error: (failure: HttpErrorResponse) => {
          this.metadataSaving.set(false);
          if (failure.status === 403) {
            this.result.set(null);
            this.editingService.set(null);
            this.selectedService.set(null);
            this.denied.set(true);
            return;
          }
          const duplicate =
            failure.status === 409 && failure.error?.code === 'SERVICE.DUPLICATE_CODE';
          const stale =
            failure.status === 409 && failure.error?.code === 'SERVICE.VERSION_CONFLICT';
          this.metadataNeedsReload.set(!duplicate && failure.status !== 422);
          this.metadataError.set(
            duplicate
              ? 'A service with this code already exists. Choose another code.'
              : failure.status === 422
                ? 'Check the service values: code up to 80 characters, name up to 200 characters, and plain text.'
                : stale
                  ? 'The service changed. Reload the catalog, check its current values, and select it again before retrying.'
                  : 'Saving could not be confirmed. Reload the catalog, check the service, and select it again before retrying.',
          );
        },
      });
  }
}
