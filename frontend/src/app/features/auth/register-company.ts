import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';

interface SubmitRegistrationResponse {
  companyId: string;
  code: string;
  name: string;
  taxId: string;
  contactPersonName: string;
  contactEmail: string;
  contactPhone: string;
  status: string;
  createdAt: string;
  evidenceDocumentKey?: string | null;
  evidenceDocumentFileName?: string | null;
}

interface CheckCodeResponse {
  available: boolean;
  code: string;
  message?: string;
}

interface GenerateCodeResponse {
  code: string;
}

interface UploadDocumentResponse {
  documentKey: string;
  fileName: string;
  sizeBytes: number;
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
      padding: 2.5rem 1rem;
      background: radial-gradient(circle at 50% 20%, #0d1e38 0%, #050b14 100%);
      color: #f0f4fc;
      font-family: inherit;
    }
    .register-card {
      width: 100%;
      max-width: 600px;
      background: rgba(13, 27, 49, 0.88);
      backdrop-filter: blur(18px);
      border: 1px solid rgba(80, 140, 240, 0.25);
      border-radius: 18px;
      padding: 2.5rem;
      box-shadow: 0 16px 44px rgba(0, 0, 0, 0.5);
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
      margin-bottom: 0.85rem;
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
    .field-row-split {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }
    @media (max-width: 580px) {
      .field-row-split {
        grid-template-columns: 1fr;
      }
    }
    mat-form-field {
      width: 100%;
    }
    .code-action-row {
      display: flex;
      gap: 0.5rem;
      align-items: flex-start;
    }
    .code-action-row mat-form-field {
      flex: 1;
    }
    .btn-random-code {
      height: 56px;
      margin-top: 0;
      border: 1px solid rgba(80, 140, 240, 0.35);
      color: #93c5fd;
      border-radius: 10px;
      white-space: nowrap;
      font-weight: 500;
      transition: all 0.2s ease;
    }
    .btn-random-code:hover:not(:disabled) {
      background: rgba(40, 100, 220, 0.2);
      border-color: #60a5fa;
    }
    .code-feedback {
      font-size: 0.82rem;
      margin-top: 0.25rem;
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }
    .code-available {
      color: #4ade80;
    }
    .code-unavailable {
      color: #f87171;
    }
    .code-checking {
      color: #93c5fd;
    }
    .document-upload-box {
      border: 2px dashed rgba(80, 140, 240, 0.35);
      border-radius: 12px;
      padding: 1.25rem;
      text-align: center;
      background: rgba(15, 23, 42, 0.45);
      transition: border-color 0.2s ease;
    }
    .document-upload-box:hover {
      border-color: #60a5fa;
    }
    .upload-label {
      font-size: 0.95rem;
      font-weight: 600;
      color: #e2e8f0;
      margin-bottom: 0.35rem;
      display: block;
    }
    .upload-hint {
      font-size: 0.8rem;
      color: #94a3b8;
      margin-bottom: 0.85rem;
    }
    .file-input-hidden {
      display: none;
    }
    .uploaded-file-chip {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: rgba(30, 58, 138, 0.35);
      border: 1px solid rgba(96, 165, 250, 0.4);
      border-radius: 8px;
      padding: 0.6rem 0.9rem;
      margin-top: 0.75rem;
      font-size: 0.88rem;
      color: #bfdbfe;
    }
    .uploaded-file-chip button {
      background: none;
      border: none;
      color: #f87171;
      cursor: pointer;
      font-weight: bold;
      padding: 0.2rem 0.5rem;
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
            <p class="subtitle">Provide your corporate credentials to establish an enterprise workspace.</p>
          </div>

          @if (error()) {
            <div class="alert-error" role="alert">
              {{ error() }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
            <!-- 1. Tax ID & 2. Company Name -->
            <div class="field-row-split">
              <mat-form-field appearance="outline">
                <mat-label>Tax ID (Mã số thuế)</mat-label>
                <input matInput formControlName="taxId" placeholder="e.g. 0101234567" maxlength="50" required />
                @if (form.controls.taxId.hasError('required')) {
                  <mat-error>Tax ID is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Company Full Name</mat-label>
                <input matInput formControlName="name" placeholder="Acme Logistics Corp." maxlength="200" required />
                @if (form.controls.name.hasError('required')) {
                  <mat-error>Company name is required.</mat-error>
                }
              </mat-form-field>
            </div>

            <!-- 3. Workspace Code with Random Generation & Real-time Check -->
            <div>
              <div class="code-action-row">
                <mat-form-field appearance="outline">
                  <mat-label>Workspace Code (Mã Workspace)</mat-label>
                  <input matInput formControlName="code" (input)="onCodeInput($event)" placeholder="e.g. acme-corp" maxlength="50" required />
                  <mat-hint>Unique URL slug (3-50 chars: letters, numbers, hyphens)</mat-hint>
                  @if (form.controls.code.hasError('required')) {
                    <mat-error>Workspace code is required.</mat-error>
                  }
                  @if (form.controls.code.hasError('pattern')) {
                    <mat-error>Use lowercase letters, numbers, and hyphens.</mat-error>
                  }
                </mat-form-field>

                <button type="button" mat-stroked-button class="btn-random-code" [disabled]="generatingCode()" (click)="generateRandomCode()">
                  {{ generatingCode() ? 'Generating…' : 'Generate Code' }}
                </button>
              </div>

              @if (codeChecking()) {
                <div class="code-feedback code-checking">
                  <span>Checking code availability…</span>
                </div>
              } @else if (codeAvailable() === true) {
                <div class="code-feedback code-available">
                  <span>✓ Code is available!</span>
                </div>
              } @else if (codeAvailable() === false) {
                <div class="code-feedback code-unavailable">
                  <span>✕ {{ codeMessage() || 'Code is already in use.' }}</span>
                </div>
              }
            </div>

            <!-- 4. Contact Person Name & 5. Contact Phone -->
            <div class="field-row-split">
              <mat-form-field appearance="outline">
                <mat-label>Authorized Representative</mat-label>
                <input matInput formControlName="contactPersonName" placeholder="Nguyen Van A" maxlength="150" required />
                @if (form.controls.contactPersonName.hasError('required')) {
                  <mat-error>Contact person name is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Contact Phone (Số điện thoại)</mat-label>
                <input matInput formControlName="contactPhone" placeholder="0901234567" maxlength="30" required />
                @if (form.controls.contactPhone.hasError('required')) {
                  <mat-error>Contact phone number is required.</mat-error>
                }
              </mat-form-field>
            </div>

            <!-- 6. Corporate Contact Email -->
            <mat-form-field appearance="outline">
              <mat-label>Corporate Contact Email</mat-label>
              <input matInput type="email" formControlName="contactEmail" placeholder="contact@acme.com" maxlength="320" required />
              @if (form.controls.contactEmail.hasError('required') || form.controls.contactEmail.hasError('email')) {
                <mat-error>A valid corporate email is required.</mat-error>
              }
            </mat-form-field>

            <!-- Legal Evidence Document Upload (MinIO / S3) -->
            <div class="document-upload-box">
              <span class="upload-label">Legal Registration Proof (Hồ sơ pháp lý)</span>
              <p class="upload-hint">Upload business license or incorporation certificate (PDF, PNG, JPG, up to 10 MB).</p>

              @if (!uploadedDocumentKey()) {
                <input #fileInput type="file" class="file-input-hidden" (change)="onFileSelected($event)" accept=".pdf,.png,.jpg,.jpeg" />
                <button type="button" mat-stroked-button color="primary" [disabled]="uploadingDocument()" (click)="fileInput.click()">
                  {{ uploadingDocument() ? 'Uploading document…' : 'Select Evidence Document' }}
                </button>
              } @else {
                <div class="uploaded-file-chip">
                  <span>📄 {{ uploadedFileName() }}</span>
                  <button type="button" (click)="clearDocument()">✕ Remove</button>
                </div>
              }

              @if (uploadError()) {
                <div class="code-feedback code-unavailable" style="justify-content: center; margin-top: 0.5rem;">
                  <span>✕ {{ uploadError() }}</span>
                </div>
              }
            </div>

            <button mat-flat-button class="submit-button" type="submit" [disabled]="submitting() || form.invalid || codeAvailable() === false">
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
              Your company registration has been securely recorded. Our Platform Administrator will review your dossier and provision your primary enterprise tenant workspace.
            </p>

            <div class="details-box">
              <div class="details-row">
                <span>Company Code:</span>
                <span class="details-val">{{ submittedResult()?.code }}</span>
              </div>
              <div class="details-row">
                <span>Tax ID:</span>
                <span class="details-val">{{ submittedResult()?.taxId }}</span>
              </div>
              <div class="details-row">
                <span>Company Name:</span>
                <span class="details-val">{{ submittedResult()?.name }}</span>
              </div>
              <div class="details-row">
                <span>Representative:</span>
                <span class="details-val">{{ submittedResult()?.contactPersonName }}</span>
              </div>
              <div class="details-row">
                <span>Contact Email:</span>
                <span class="details-val">{{ submittedResult()?.contactEmail }}</span>
              </div>
              <div class="details-row">
                <span>Contact Phone:</span>
                <span class="details-val">{{ submittedResult()?.contactPhone }}</span>
              </div>
              <div class="details-row">
                <span>Evidence File:</span>
                <span class="details-val">{{ submittedResult()?.evidenceDocumentFileName || 'None uploaded' }}</span>
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
export class RegisterCompany implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    taxId: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/i)]],
    contactPersonName: ['', [Validators.required, Validators.maxLength(150)]],
    contactEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    contactPhone: ['', [Validators.required, Validators.maxLength(30)]],
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly submittedResult = signal<SubmitRegistrationResponse | null>(null);

  readonly codeChecking = signal(false);
  readonly codeAvailable = signal<boolean | null>(null);
  readonly codeMessage = signal<string>('');
  readonly generatingCode = signal(false);

  readonly uploadingDocument = signal(false);
  readonly uploadedDocumentKey = signal<string | null>(null);
  readonly uploadedFileName = signal<string | null>(null);
  readonly uploadError = signal<string | null>(null);

  private readonly codeSubject = new Subject<string>();
  private codeSub?: Subscription;

  ngOnInit(): void {
    this.codeSub = this.codeSubject
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        filter(code => code.trim().length >= 3),
        switchMap(code => {
          this.codeChecking.set(true);
          return this.http.get<CheckCodeResponse>(`/api/v1/tenants/check-code?code=${encodeURIComponent(code.trim().toLowerCase())}`);
        })
      )
      .subscribe({
        next: (res) => {
          this.codeChecking.set(false);
          this.codeAvailable.set(res.available);
          this.codeMessage.set(res.message || '');
        },
        error: () => {
          this.codeChecking.set(false);
          this.codeAvailable.set(null);
        }
      });
  }

  ngOnDestroy(): void {
    this.codeSub?.unsubscribe();
  }

  onCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value.trim().toLowerCase();
    this.codeAvailable.set(null);
    this.codeMessage.set('');
    if (value.length >= 3) {
      this.codeSubject.next(value);
    }
  }

  generateRandomCode(): void {
    if (this.generatingCode()) return;
    this.generatingCode.set(true);
    const companyName = this.form.controls.name.value;
    this.http.post<GenerateCodeResponse>('/api/v1/tenants/generate-code', { baseName: companyName }).subscribe({
      next: (res) => {
        this.generatingCode.set(false);
        this.form.controls.code.setValue(res.code);
        this.codeAvailable.set(true);
        this.codeMessage.set('');
      },
      error: () => {
        this.generatingCode.set(false);
        const fallback = 'bizflow-' + Math.floor(1000 + Math.random() * 9000);
        this.form.controls.code.setValue(fallback);
        this.codeAvailable.set(true);
      }
    });
  }

  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const files = target.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    const allowed = ['.pdf', '.png', '.jpg', '.jpeg'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!allowed.includes(ext)) {
      this.uploadError.set('Allowed file types: PDF, PNG, JPG, JPEG.');
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      this.uploadError.set('File size exceeds the 10 MB limit.');
      return;
    }

    this.uploadingDocument.set(true);
    this.uploadError.set(null);

    const formData = new FormData();
    formData.append('file', file, file.name);

    this.http.post<UploadDocumentResponse>('/api/v1/company-registrations/upload-document', formData).subscribe({
      next: (res) => {
        this.uploadingDocument.set(false);
        this.uploadedDocumentKey.set(res.documentKey);
        this.uploadedFileName.set(res.fileName);
      },
      error: (err: HttpErrorResponse) => {
        this.uploadingDocument.set(false);
        this.uploadError.set(err.error?.message || 'Failed to upload document. Please try again.');
      }
    });
  }

  clearDocument(): void {
    this.uploadedDocumentKey.set(null);
    this.uploadedFileName.set(null);
    this.uploadError.set(null);
  }

  submit() {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.codeAvailable() === false) return;

    this.submitting.set(true);
    this.error.set(null);

    const raw = this.form.getRawValue();
    const payload = {
      code: raw.code.trim().toUpperCase(),
      name: raw.name.trim(),
      taxId: raw.taxId.trim(),
      contactPersonName: raw.contactPersonName.trim(),
      contactEmail: raw.contactEmail.trim(),
      contactPhone: raw.contactPhone.trim(),
      evidenceDocumentKey: this.uploadedDocumentKey(),
      evidenceDocumentFileName: this.uploadedFileName(),
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
