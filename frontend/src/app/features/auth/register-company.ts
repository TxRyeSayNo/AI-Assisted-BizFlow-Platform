import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

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
    TranslatePipe,
  ],
  styles: [
    `
      :host {
        display: block;
      }
      .register-page {
        min-height: 100dvh;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 2.5rem 1.25rem;
        background:
          radial-gradient(circle at 15% 15%, rgba(142, 197, 255, 0.28) 0%, transparent 45%),
          radial-gradient(circle at 85% 85%, rgba(37, 99, 235, 0.12) 0%, transparent 50%),
          linear-gradient(145deg, #eef5fc 0%, #f5f9ff 50%, #e6f0fd 100%);
        color: #0b223d;
        font-family: var(
          --bf-font-sans,
          'Plus Jakarta Sans',
          -apple-system,
          BlinkMacSystemFont,
          sans-serif
        );
      }
      .register-card {
        width: 100%;
        max-width: 620px;
        background: rgba(255, 255, 255, 0.96);
        backdrop-filter: blur(16px);
        -webkit-backdrop-filter: blur(16px);
        border-radius: 20px;
        padding: 2.35rem 2.25rem;
        box-shadow:
          0 20px 44px -12px rgba(7, 27, 51, 0.1),
          0 4px 16px -2px rgba(20, 93, 160, 0.06),
          0 0 0 1px rgba(191, 219, 254, 0.75);
        position: relative;
        overflow: hidden;
        animation: register-card-enter 280ms cubic-bezier(0.16, 1, 0.3, 1);
      }
      .register-card::before {
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        height: 4px;
        background: linear-gradient(90deg, #0b2a4a 0%, #145da0 45%, #2563eb 100%);
      }
      @keyframes register-card-enter {
        from {
          opacity: 0;
          transform: translateY(10px) scale(0.985);
        }
        to {
          opacity: 1;
          transform: translateY(0) scale(1);
        }
      }
      .card-header {
        margin-bottom: 1.75rem;
        text-align: center;
      }
      .brand-badge {
        display: inline-flex;
        align-items: center;
        gap: 0.45rem;
        padding: 0.3rem 0.85rem;
        background: #eaf4ff;
        border: 1px solid #bfdbfe;
        border-radius: 9999px;
        font-size: 0.8rem;
        color: #145da0;
        margin-bottom: 0.75rem;
        font-weight: 700;
        letter-spacing: 0.02em;
      }
      h1 {
        font-size: 1.65rem;
        font-weight: 800;
        margin: 0 0 0.4rem;
        color: #071b33;
        letter-spacing: -0.025em;
      }
      .subtitle {
        color: #52657c;
        font-size: 0.9rem;
        margin: 0;
        line-height: 1.5;
      }
      .form-grid {
        display: flex;
        flex-direction: column;
        gap: 1.1rem;
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
        .register-card {
          padding: 1.75rem 1.35rem;
        }
      }
      mat-form-field {
        width: 100%;
        --mdc-outlined-text-field-container-shape: 12px;
        --mdc-outlined-text-field-focus-outline-color: #2563eb;
        --mdc-outlined-text-field-hover-outline-color: #60a5fa;
        --mdc-outlined-text-field-outline-color: #cbd5e1;
        --mdc-outlined-text-field-label-text-color: #475569;
        --mdc-outlined-text-field-focus-label-text-color: #145da0;
        --mdc-outlined-text-field-input-text-color: #071b33;
      }
      .code-action-row {
        display: flex;
        gap: 0.6rem;
        align-items: flex-start;
      }
      .code-action-row mat-form-field {
        flex: 1;
      }
      .btn-random-code {
        height: 56px;
        margin-top: 0;
        border: 1px solid #bfdbfe !important;
        background: #f0f7ff !important;
        color: #145da0 !important;
        border-radius: 12px !important;
        white-space: nowrap;
        font-weight: 600;
        transition:
          background-color 160ms ease,
          border-color 160ms ease,
          transform 140ms cubic-bezier(0.16, 1, 0.3, 1);
      }
      .btn-random-code:hover:not(:disabled) {
        background: #dbeafe !important;
        border-color: #60a5fa !important;
      }
      .btn-random-code:active:not(:disabled) {
        transform: scale(0.98);
      }
      .code-feedback {
        font-size: 0.82rem;
        font-weight: 600;
        margin-top: 0.35rem;
        display: flex;
        align-items: center;
        gap: 0.35rem;
      }
      .code-available {
        color: #059669;
      }
      .code-unavailable {
        color: #dc2626;
      }
      .code-checking {
        color: #2563eb;
      }
      .document-upload-box {
        border: 1.5px dashed #93c5fd;
        border-radius: 14px;
        padding: 1.25rem;
        text-align: center;
        background: #f8fbff;
        transition:
          border-color 180ms ease,
          background-color 180ms ease;
      }
      .document-upload-box:hover {
        border-color: #2563eb;
        background: #f0f7ff;
      }
      .upload-label {
        font-size: 0.92rem;
        font-weight: 700;
        color: #0b2a4a;
        margin-bottom: 0.25rem;
        display: block;
      }
      .upload-hint {
        font-size: 0.8rem;
        color: #52657c;
        margin: 0 0 0.85rem;
      }
      .file-input-hidden {
        display: none;
      }
      .uploaded-file-chip {
        display: flex;
        align-items: center;
        justify-content: space-between;
        background: #eaf4ff;
        border: 1px solid #93c5fd;
        border-radius: 10px;
        padding: 0.6rem 0.9rem;
        margin-top: 0.65rem;
        font-size: 0.86rem;
        font-weight: 600;
        color: #0b2a4a;
      }
      .uploaded-file-chip button {
        background: none;
        border: none;
        color: #dc2626;
        cursor: pointer;
        font-weight: 700;
        padding: 0.2rem 0.5rem;
        border-radius: 6px;
      }
      .uploaded-file-chip button:hover {
        background: #fee2e2;
      }
      .submit-button {
        height: 48px !important;
        font-size: 0.95rem !important;
        font-weight: 700 !important;
        border-radius: 12px !important;
        background: linear-gradient(135deg, #0b2a4a 0%, #145da0 50%, #2563eb 100%) !important;
        color: #ffffff !important;
        margin-top: 0.35rem;
        box-shadow:
          0 6px 18px -3px rgba(37, 99, 235, 0.35),
          0 2px 6px rgba(11, 42, 74, 0.14);
        transition:
          transform 160ms cubic-bezier(0.16, 1, 0.3, 1),
          box-shadow 180ms cubic-bezier(0.16, 1, 0.3, 1),
          filter 180ms ease;
      }
      .submit-button:hover:not(:disabled) {
        filter: brightness(1.06);
        transform: translateY(-1px);
        box-shadow: 0 10px 24px -4px rgba(37, 99, 235, 0.45);
      }
      .submit-button:active:not(:disabled) {
        transform: scale(0.98);
      }
      .submit-button:disabled {
        opacity: 0.55;
        box-shadow: none;
      }
      .alert-error {
        padding: 0.8rem 1rem;
        background: #fef2f2;
        border: 1px solid #fecaca;
        border-left: 3px solid #dc2626;
        border-radius: 10px;
        color: #991b1b;
        font-size: 0.86rem;
        font-weight: 500;
        margin-bottom: 1rem;
      }
      .success-container {
        text-align: center;
        padding: 0.75rem 0;
      }
      .success-icon {
        width: 64px;
        height: 64px;
        border-radius: 50%;
        background: #ecfdf5;
        color: #059669;
        display: flex;
        align-items: center;
        justify-content: center;
        margin: 0 auto 1.25rem;
        font-size: 1.85rem;
        font-weight: 800;
        border: 1px solid #a7f3d0;
        box-shadow: 0 6px 16px rgba(5, 150, 105, 0.15);
      }
      .details-box {
        background: #f8fbff;
        border: 1px solid #dbe7f5;
        border-radius: 12px;
        padding: 1.25rem;
        margin: 1.5rem 0;
        text-align: left;
        font-size: 0.88rem;
      }
      .details-row {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 0.55rem;
        color: #52657c;
      }
      .details-row:last-child {
        margin-bottom: 0;
      }
      .details-val {
        color: #071b33;
        font-weight: 600;
        text-align: right;
      }
      .badge-pending {
        display: inline-block;
        padding: 0.2rem 0.65rem;
        background: #fef3c7;
        border: 1px solid #fde68a;
        color: #92400e;
        border-radius: 9999px;
        font-size: 0.75rem;
        font-weight: 700;
      }
      .card-footer {
        margin-top: 1.65rem;
        text-align: center;
        font-size: 0.86rem;
        color: #52657c;
        border-top: 1px solid #e8f0fa;
        padding-top: 1.15rem;
      }
      .card-footer a {
        color: #145da0;
        text-decoration: none;
        font-weight: 700;
      }
      .card-footer a:hover {
        color: #2563eb;
        text-decoration: underline;
      }
    `,
  ],
  template: `
    <div class="register-page">
      <div class="register-card">
        @if (!submittedResult()) {
          <div class="card-header">
            <div class="brand-badge" style="margin-bottom: 0.75rem;">
              <span>●</span> BizFlow SaaS Platform
            </div>
            <h1>{{ 'register.title' | translate: 'Register your company' }}</h1>
            <p class="subtitle">
              {{
                'register.subtitle'
                  | translate
                    : 'Provision a dedicated BizFlow workspace for your organization with an initial administrator account.'
              }}
            </p>
          </div>

          <!-- Accessible alert announcement without layout displacement -->
          @if (error()) {
            <div class="sr-only" role="alert" aria-live="assertive">
              {{ error() }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid">
            <!-- 1. Tax ID & 2. Company Name -->
            <div class="field-row-split">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'register.taxId' | translate: 'Tax ID' }}</mat-label>
                <input
                  matInput
                  formControlName="taxId"
                  [placeholder]="'register.taxIdPlaceholder' | translate: 'e.g. 0101234567'"
                  maxlength="50"
                  required
                />
                @if (form.controls.taxId.hasError('required')) {
                  <mat-error>{{
                    'register.taxIdReq' | translate: 'Tax ID is required.'
                  }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'register.companyName' | translate: 'Company name' }}</mat-label>
                <input
                  matInput
                  formControlName="name"
                  [placeholder]="'register.companyNamePlaceholder' | translate: 'Acme Logistics Corp.'"
                  maxlength="200"
                  required
                />
                @if (form.controls.name.hasError('required')) {
                  <mat-error>{{
                    'register.companyNameReq' | translate: 'Company name is required.'
                  }}</mat-error>
                }
              </mat-form-field>
            </div>

            <!-- 3. Workspace Code with Random Generation & Real-time Check -->
            <div>
              <div class="code-action-row">
                <mat-form-field appearance="outline">
                  <mat-label>{{
                    'register.workspaceCode' | translate: 'Workspace Code'
                  }}</mat-label>
                  <input
                    matInput
                    formControlName="code"
                    (input)="onCodeInput($event)"
                    [placeholder]="'register.workspaceCodePlaceholder' | translate: 'e.g. acme-corp'"
                    maxlength="50"
                    required
                  />
                  <mat-hint>{{
                    'register.workspaceCodeHint'
                      | translate: 'Unique URL slug (3-50 chars: letters, numbers, hyphens)'
                  }}</mat-hint>
                  @if (form.controls.code.hasError('required')) {
                    <mat-error>{{
                      'register.workspaceCodeReq' | translate: 'Workspace code is required.'
                    }}</mat-error>
                  }
                  @if (form.controls.code.hasError('pattern')) {
                    <mat-error>{{
                      'register.workspaceCodePattern'
                        | translate: 'Use lowercase letters, numbers, and hyphens.'
                    }}</mat-error>
                  }
                </mat-form-field>

                <button
                  type="button"
                  mat-stroked-button
                  class="btn-random-code"
                  [disabled]="generatingCode()"
                  (click)="generateRandomCode()"
                >
                  {{
                    generatingCode()
                      ? ('register.generating' | translate: 'Generating…')
                      : ('register.generateCode' | translate: 'Generate Code')
                  }}
                </button>
              </div>

              @if (codeChecking()) {
                <div class="code-feedback code-checking">
                  <span>{{
                    'register.checkingCode' | translate: 'Checking code availability…'
                  }}</span>
                </div>
              } @else if (codeAvailable() === true) {
                <div class="code-feedback code-available">
                  <span>✓ {{ 'register.codeAvailable' | translate: 'Code is available!' }}</span>
                </div>
              } @else if (codeAvailable() === false) {
                <div class="code-feedback code-unavailable">
                  <span
                    >✕
                    {{
                      codeMessage() || ('register.codeTaken' | translate: 'Code is already in use.')
                    }}</span
                  >
                </div>
              }
            </div>

            <!-- 4. Contact Person Name & 5. Contact Phone -->
            <div class="field-row-split">
              <mat-form-field appearance="outline">
                <mat-label>{{
                  'register.contactPerson' | translate: 'Authorized Representative'
                }}</mat-label>
                <input
                  matInput
                  formControlName="contactPersonName"
                  [placeholder]="'register.contactPersonPlaceholder' | translate: 'Nguyen Van A'"
                  maxlength="150"
                  required
                />
                @if (form.controls.contactPersonName.hasError('required')) {
                  <mat-error>{{
                    'register.contactPersonReq' | translate: 'Contact person name is required.'
                  }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  'register.contactPhone' | translate: 'Contact Phone'
                }}</mat-label>
                <input
                  matInput
                  formControlName="contactPhone"
                  [placeholder]="'register.contactPhonePlaceholder' | translate: '0901234567'"
                  maxlength="30"
                  required
                />
                @if (form.controls.contactPhone.hasError('required')) {
                  <mat-error>{{
                    'register.contactPhoneReq' | translate: 'Contact phone number is required.'
                  }}</mat-error>
                }
              </mat-form-field>
            </div>

            <!-- 6. Corporate Contact Email -->
            <mat-form-field appearance="outline">
              <mat-label>{{
                'register.contactEmail' | translate: 'Corporate Contact Email'
              }}</mat-label>
              <input
                matInput
                type="email"
                formControlName="contactEmail"
                [placeholder]="'register.contactEmailPlaceholder' | translate: 'contact@acme.com'"
                maxlength="320"
                required
              />
              @if (
                form.controls.contactEmail.hasError('required') ||
                form.controls.contactEmail.hasError('email')
              ) {
                <mat-error>{{
                  'register.contactEmailReq' | translate: 'A valid corporate email is required.'
                }}</mat-error>
              }
            </mat-form-field>

            <!-- Legal Evidence Document Upload (MinIO / S3) -->
            <div class="document-upload-box">
              <span class="upload-label">{{
                'register.evidenceLabel' | translate: 'Legal Registration Proof'
              }}</span>
              <p class="upload-hint">
                {{
                  'register.evidenceHint'
                    | translate
                      : 'Upload business license or incorporation certificate (PDF, PNG, JPG, up to 10 MB).'
                }}
              </p>

              @if (!uploadedDocumentKey()) {
                <input
                  #fileInput
                  type="file"
                  class="file-input-hidden"
                  (change)="onFileSelected($event)"
                  accept=".pdf,.png,.jpg,.jpeg"
                />
                <button
                  type="button"
                  mat-stroked-button
                  color="primary"
                  [disabled]="uploadingDocument()"
                  (click)="fileInput.click()"
                >
                  {{
                    uploadingDocument()
                      ? ('register.uploading' | translate: 'Uploading document…')
                      : ('register.chooseFile' | translate: 'Select Evidence Document')
                  }}
                </button>
              } @else {
                <div class="uploaded-file-chip">
                  <span>📄 {{ uploadedFileName() }}</span>
                  <button type="button" (click)="clearDocument()">
                    ✕ {{ 'register.removeFile' | translate: 'Remove' }}
                  </button>
                </div>
              }

              <!-- Accessible upload error announcement without layout displacement -->
              @if (uploadError()) {
                <div class="sr-only" role="alert" aria-live="assertive">
                  {{ uploadError() }}
                </div>
              }
            </div>

            <button
              mat-flat-button
              class="submit-button"
              type="submit"
              [disabled]="submitting() || form.invalid || codeAvailable() === false"
            >
              @if (submitting()) {
                <span>{{ 'register.submitting' | translate: 'Submitting registration…' }}</span>
              } @else {
                <span>{{ 'register.submit' | translate: 'Register company workspace' }}</span>
              }
            </button>
          </form>

          <div class="card-footer">
            {{ 'register.alreadyHaveWorkspace' | translate: 'Already have a workspace?' }}
            <a routerLink="/login">{{ 'auth.signIn' | translate: 'Sign in' }}</a>
          </div>
        } @else {
          <div class="success-container">
            <div class="success-icon">✓</div>
            <h1>{{ 'register.successTitle' | translate: 'Registration Submitted!' }}</h1>
            <p class="subtitle">
              {{
                'register.successSubtitle'
                  | translate
                    : 'Your company registration has been securely recorded. Our Platform Administrator will review your dossier and provision your primary enterprise tenant workspace.'
              }}
            </p>

            <div class="details-box">
              <div class="details-row">
                <span>{{ 'register.companyCodeLabel' | translate: 'Company Code:' }}</span>
                <span class="details-val">{{ submittedResult()?.code }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.taxIdLabel' | translate: 'Tax ID:' }}</span>
                <span class="details-val">{{ submittedResult()?.taxId }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.companyNameLabel' | translate: 'Company Name:' }}</span>
                <span class="details-val">{{ submittedResult()?.name }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.representativeLabel' | translate: 'Representative:' }}</span>
                <span class="details-val">{{ submittedResult()?.contactPersonName }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.contactEmailLabel' | translate: 'Contact Email:' }}</span>
                <span class="details-val">{{ submittedResult()?.contactEmail }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.contactPhoneLabel' | translate: 'Contact Phone:' }}</span>
                <span class="details-val">{{ submittedResult()?.contactPhone }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.evidenceFileLabel' | translate: 'Evidence File:' }}</span>
                <span class="details-val">{{
                  submittedResult()?.evidenceDocumentFileName ||
                    ('register.noneUploaded' | translate: 'None uploaded')
                }}</span>
              </div>
              <div class="details-row">
                <span>{{ 'register.statusLabel' | translate: 'Status:' }}</span>
                <span
                  ><span class="badge-pending">{{ submittedResult()?.status }}</span></span
                >
              </div>
            </div>

            <button mat-flat-button class="submit-button" style="width: 100%;" routerLink="/login">
              {{ 'register.returnToLogin' | translate: 'Return to Login' }}
            </button>
          </div>
        }
      </div>
    </div>
  `,
})
export class RegisterCompany implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(I18nService);
  private readonly toast = inject(ToastService);

  constructor() {
    effect(() => {
      const err = this.error();
      if (err) {
        this.toast.error(err);
      }
    });

    effect(() => {
      const uploadErr = this.uploadError();
      if (uploadErr) {
        this.toast.error(uploadErr);
      }
    });
  }

  readonly form = this.fb.nonNullable.group({
    taxId: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: [
      '',
      [
        Validators.required,
        Validators.maxLength(50),
        Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/i),
      ],
    ],
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
        filter((code) => code.trim().length >= 3),
        switchMap((code) => {
          this.codeChecking.set(true);
          return this.http.get<CheckCodeResponse>(
            `/api/v1/tenants/check-code?code=${encodeURIComponent(code.trim().toLowerCase())}`,
          );
        }),
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
        },
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
    this.http
      .post<GenerateCodeResponse>('/api/v1/tenants/generate-code', { baseName: companyName })
      .subscribe({
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
        },
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
      this.uploadError.set(
        this.i18n.t('register.errFileType', 'Allowed file types: PDF, PNG, JPG, JPEG.'),
      );
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      this.uploadError.set(
        this.i18n.t('register.errFileSize', 'File size exceeds the 10 MB limit.'),
      );
      return;
    }

    this.uploadingDocument.set(true);
    this.uploadError.set(null);

    const formData = new FormData();
    formData.append('file', file, file.name);

    this.http
      .post<UploadDocumentResponse>('/api/v1/company-registrations/upload-document', formData)
      .subscribe({
        next: (res) => {
          this.uploadingDocument.set(false);
          this.uploadedDocumentKey.set(res.documentKey);
          this.uploadedFileName.set(res.fileName);
        },
        error: (err: HttpErrorResponse) => {
          this.uploadingDocument.set(false);
          this.uploadError.set(
            err.error?.message ||
              this.i18n.t(
                'register.uploadFailed',
                'Failed to upload document. Please try again.',
              ),
          );
        },
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
          this.error.set(
            this.i18n.t(
              'register.errDuplicate',
              'This company code or contact email is already registered in our system.',
            ),
          );
        } else if (err.error?.message) {
          this.error.set(err.error.message);
        } else {
          this.error.set(
            this.i18n.t(
              'register.errFailed',
              'Failed to submit company registration. Please check your information and try again.',
            ),
          );
        }
      },
    });
  }
}
