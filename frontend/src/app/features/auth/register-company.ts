import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';

interface SubmitRegistrationResponse {
  companyId: string;
  code: string;
  name: string;
  contactEmail: string;
  status: string;
  createdAt: string;
}

@Component({
  selector: 'bf-register-company',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  styles: [`
    .register-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
      background: radial-gradient(circle at 50% 20%, #0d1e38 0%, #050b14 100%);
      color: #f0f4fc;
      font-family: inherit;
    }
    .register-card {
      width: 100%;
      max-width: 520px;
      background: rgba(13, 27, 49, 0.85);
      backdrop-filter: blur(16px);
      border: 1px solid rgba(80, 140, 240, 0.25);
      border-radius: 16px;
      padding: 2.5rem;
      box-shadow: 0 16px 40px rgba(0, 0, 0, 0.45);
    }
    .card-header {
      margin-bottom: 2rem;
      text-align: center;
    }
    .brand-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.35rem 0.85rem;
      background: rgba(40, 100, 220, 0.15);
      border: 1px solid rgba(80, 140, 240, 0.3);
      border-radius: 20px;
      font-size: 0.85rem;
      color: #60a5fa;
      margin-bottom: 1rem;
      font-weight: 500;
    }
    h1 {
      font-size: 1.75rem;
      font-weight: 700;
      margin: 0 0 0.5rem;
      color: #ffffff;
      letter-spacing: -0.02em;
    }
    .subtitle {
      color: #94a3b8;
      font-size: 0.95rem;
      margin: 0;
      line-height: 1.5;
    }
    .form-grid {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    mat-form-field {
      width: 100%;
    }
    .submit-button {
      height: 48px;
      font-size: 1rem;
      font-weight: 600;
      border-radius: 10px;
      background: linear-gradient(135deg, #2563eb, #1d4ed8);
      color: #ffffff;
      margin-top: 0.5rem;
      transition: all 0.2s ease;
    }
    .submit-button:hover:not(:disabled) {
      background: linear-gradient(135deg, #3b82f6, #2563eb);
      box-shadow: 0 4px 16px rgba(37, 99, 235, 0.4);
    }
    .alert-error {
      padding: 0.85rem 1rem;
      background: rgba(239, 68, 68, 0.15);
      border: 1px solid rgba(239, 68, 68, 0.35);
      border-radius: 8px;
      color: #fca5a5;
      font-size: 0.9rem;
      margin-bottom: 1rem;
    }
    .success-container {
      text-align: center;
      padding: 1rem 0;
    }
    .success-icon {
      width: 64px;
      height: 64px;
      border-radius: 50%;
      background: rgba(34, 197, 94, 0.15);
      color: #4ade80;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 1.5rem;
      font-size: 2rem;
      border: 1px solid rgba(34, 197, 94, 0.3);
    }
    .details-box {
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(148, 163, 184, 0.15);
      border-radius: 10px;
      padding: 1.25rem;
      margin: 1.5rem 0;
      text-align: left;
      font-size: 0.9rem;
    }
    .details-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
      color: #94a3b8;
    }
    .details-row:last-child {
      margin-bottom: 0;
    }
    .details-val {
      color: #f1f5f9;
      font-weight: 500;
    }
    .badge-pending {
      display: inline-block;
      padding: 0.2rem 0.6rem;
      background: rgba(234, 179, 8, 0.15);
      color: #fde047;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .card-footer {
      margin-top: 2rem;
      text-align: center;
      font-size: 0.9rem;
      color: #94a3b8;
      border-top: 1px solid rgba(148, 163, 184, 0.1);
      padding-top: 1.25rem;
    }
    .card-footer a {
      color: #60a5fa;
      text-decoration: none;
      font-weight: 500;
    }
    .card-footer a:hover {
      text-decoration: underline;
    }
  `],
  template: `
    <div class="register-page">
      <div class="register-card">
        @if (!submittedResult()) {
          <div class="card-header">
            <div class="brand-badge">
              <span>●</span> BizFlow SaaS Platform
            </div>
            <h1>Register your Company</h1>
            <p class="subtitle">Submit your organization profile to establish an enterprise workspace.</p>
          </div>

          @if (error()) {
            <div class="alert-error" role="alert">
              {{ error() }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
            <mat-form-field appearance="outline">
              <mat-label>Company Code</mat-label>
              <input matInput formControlName="code" placeholder="e.g. ACME" maxlength="50" style="text-transform: uppercase;" />
              <mat-hint>Unique uppercase identifier (max 50 chars)</mat-hint>
              @if (form.controls.code.hasError('required')) {
                <mat-error>Company code is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Company Full Name</mat-label>
              <input matInput formControlName="name" placeholder="e.g. Acme Global Logistics Corp." maxlength="200" />
              @if (form.controls.name.hasError('required')) {
                <mat-error>Company name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Corporate Contact Email</mat-label>
              <input matInput type="email" formControlName="contactEmail" placeholder="contact@acme.com" maxlength="200" />
              @if (form.controls.contactEmail.hasError('required') || form.controls.contactEmail.hasError('email')) {
                <mat-error>A valid corporate email is required.</mat-error>
              }
            </mat-form-field>

            <button mat-flat-button class="submit-button" type="submit" [disabled]="submitting() || form.invalid">
              @if (submitting()) {
                <span>Submitting Registration…</span>
              } @else {
                <span>Submit Company Registration</span>
              }
            </button>
          </form>

          <div class="card-footer">
            Already have an account? <a routerLink="/login">Sign in here</a>
          </div>
        } @else {
          <div class="success-container">
            <div class="success-icon">✓</div>
            <h1>Registration Submitted!</h1>
            <p class="subtitle">
              Your company registration has been securely recorded. Our Platform Administrator will review and provision your enterprise tenant workspace.
            </p>

            <div class="details-box">
              <div class="details-row">
                <span>Company Code:</span>
                <span class="details-val">{{ submittedResult()?.code }}</span>
              </div>
              <div class="details-row">
                <span>Company Name:</span>
                <span class="details-val">{{ submittedResult()?.name }}</span>
              </div>
              <div class="details-row">
                <span>Contact Email:</span>
                <span class="details-val">{{ submittedResult()?.contactEmail }}</span>
              </div>
              <div class="details-row">
                <span>Status:</span>
                <span><span class="badge-pending">{{ submittedResult()?.status }}</span></span>
              </div>
            </div>

            <button mat-flat-button class="submit-button" style="width: 100%;" routerLink="/login">
              Return to Login
            </button>
          </div>
        }
      </div>
    </div>
  `
})
export class RegisterCompany {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    contactEmail: ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly submittedResult = signal<SubmitRegistrationResponse | null>(null);

  submit() {
    if (this.form.invalid) return;

    this.submitting.set(true);
    this.error.set(null);

    const payload = {
      code: this.form.getRawValue().code.trim().toUpperCase(),
      name: this.form.getRawValue().name.trim(),
      contactEmail: this.form.getRawValue().contactEmail.trim(),
    };

    this.http.post<SubmitRegistrationResponse>('/api/v1/company-registrations', payload).subscribe({
      next: (res) => {
        this.submittedResult.set(res);
        this.submitting.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        if (err.status === 409) {
          this.error.set('This company code or contact email is already registered in our system.');
        } else if (err.error?.message) {
          this.error.set(err.error.message);
        } else {
          this.error.set('Failed to submit company registration. Please check your information and try again.');
        }
      }
    });
  }
}
