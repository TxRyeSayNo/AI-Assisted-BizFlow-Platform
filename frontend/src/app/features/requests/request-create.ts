import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

interface CategoryOption {
  serviceCategoryId: string;
  name: string;
  status: string;
}

interface ServiceOption {
  serviceId: string;
  name: string;
  status: string;
  categories: CategoryOption[];
}

@Component({
  selector: 'bf-request-create',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
  ],
  templateUrl: './request-create.html',
  styleUrls: ['../organization/departments.scss', '../tasks/task-editor.scss'],
})
export class RequestCreate implements OnInit {
  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);

  readonly busy = signal(false);
  readonly loadingServices = signal(true);
  readonly done = signal(false);
  readonly error = signal('');

  readonly services = signal<ServiceOption[]>([]);
  readonly availableCategories = signal<CategoryOption[]>([]);

  private key = crypto.randomUUID();

  readonly form = inject(FormBuilder).nonNullable.group({
    serviceId: ['', Validators.required],
    categoryId: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: [''],
    priority: ['MEDIUM', Validators.required],
    submitImmediately: [false],
  });

  ngOnInit() {
    this.loadCatalog();

    // When service changes, update available categories
    this.form.controls.serviceId.valueChanges
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe((selectedServiceId) => {
        const selected = this.services().find((s) => s.serviceId === selectedServiceId);
        const cats = (selected?.categories || []).filter((c) => c.status === 'ACTIVE');
        this.availableCategories.set(cats);

        // Reset category if not in available list
        const currentCat = this.form.controls.categoryId.value;
        if (!cats.some((c) => c.serviceCategoryId === currentCat)) {
          this.form.controls.categoryId.setValue(cats.length > 0 ? cats[0].serviceCategoryId : '');
        }
      });
  }

  loadCatalog() {
    this.loadingServices.set(true);
    this.http
      .get<{ items: ServiceOption[] }>('/api/v1/services', {
        params: { pageSize: '100' },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.loadingServices.set(false);
          const activeServices = (res.items || []).filter((s) => s.status === 'ACTIVE');
          this.services.set(activeServices);
          if (activeServices.length > 0 && !this.form.controls.serviceId.value) {
            this.form.controls.serviceId.setValue(activeServices[0].serviceId);
          }
        },
        error: () => {
          this.loadingServices.set(false);
          this.error.set('Could not load service catalog. Please refresh.');
        },
      });
  }

  save() {
    if (this.busy() || this.done() || !this.session.hasPermission('requests.create')) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      serviceId: value.serviceId,
      categoryId: value.categoryId,
      title: value.title.trim(),
      description: value.description.trim() || null,
      priority: value.priority,
      submitImmediately: value.submitImmediately,
    };

    this.busy.set(true);
    this.error.set('');
    this.form.disable();

    this.http
      .post<{ requestId: string }>('/api/v1/requests', payload, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          if (!result?.requestId || !/^[0-9a-f-]{36}$/i.test(result.requestId)) {
            this.unknown();
            return;
          }
          this.done.set(true);
          void this.router.navigate(['/requests', result.requestId]);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          if (failure.status === 422 || failure.status === 400) {
            this.form.enable();
            this.key = crypto.randomUUID();
            this.error.set(
              failure.error?.message || 'Check request inputs. No request was created.',
            );
          } else if (failure.status === 409) {
            this.form.enable();
            this.key = crypto.randomUUID();
            this.error.set('Request conflicting with a previous submission. Please retry.');
          } else if (failure.status === 403 || failure.status === 401) {
            this.done.set(true);
            this.error.set('You do not have permission to create requests.');
          } else {
            this.unknown();
          }
        },
      });
  }

  private unknown() {
    this.form.enable();
    this.key = crypto.randomUUID();
    this.error.set('Unexpected error creating request. Please try again.');
  }
}
