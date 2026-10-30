import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface ExtractedActionItem {
  orderIndex: number;
  title: string;
  description: string;
  suggestedDepartment?: string | null;
  suggestedPriority?: string | null;
  suggestedDurationDays?: number | null;
}

export interface DocumentSummarizeResult {
  sourceTitle: string;
  sourceType: string;
  mode: string;
  executiveSummary: string;
  keyPoints: string[];
  actionItems: ExtractedActionItem[];
  risksAndCompliance: string[];
  wordCount: number;
  confidence: number;
  tokensUsed?: number | null;
  summarizedAt: string;
}

export interface CreatedSummaryTaskResult {
  taskId: string;
  title: string;
  priority: string;
  departmentId?: string | null;
  status: string;
}

export interface CreateTasksFromSummaryResponse {
  totalCreated: number;
  createdTasks: CreatedSummaryTaskResult[];
  createdAt: string;
}

@Component({
  selector: 'bf-document-summarizer-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  template: `
    <div *ngIf="isOpen" class="dialog-backdrop" (click)="onBackdropClick($event)">
      <div class="dialog-card" role="dialog" aria-modal="true" aria-labelledby="summarizer-title">
        <!-- Dialog Header -->
        <div class="dialog-header">
          <div class="header-branding">
            <span class="ai-spark" aria-hidden="true">📑</span>
            <div>
              <h2 id="summarizer-title" class="dialog-title">
                {{ 'ai.summarizeTitle' | translate: 'AI Document Intelligence & Summarizer' }}
              </h2>
              <p class="dialog-subtitle">
                {{
                  'ai.summarizeSubtitle'
                    | translate
                      : 'Extract executive insights, actionable tasks, and compliance alerts'
                }}
              </p>
            </div>
          </div>
          <button type="button" class="btn-close" (click)="closeDialog()" aria-label="Close dialog">
            ✕
          </button>
        </div>

        <div class="dialog-body">
          <!-- Source Indicator -->
          <div *ngIf="fileName" class="source-banner">
            <span class="source-icon">📎</span>
            <div class="source-info">
              <span class="source-label">{{
                'attachments.file' | translate: 'Tài liệu nguồn:'
              }}</span>
              <strong class="source-name">{{ fileName }}</strong>
            </div>
          </div>

          <!-- Free text input if no attachment or editing raw text -->
          <div *ngIf="!attachmentId" class="input-group">
            <label class="input-label" for="raw-document-text">
              {{
                'ai.documentTextInput'
                  | translate: 'Dán nội dung tài liệu hoặc hợp đồng cần tóm tắt:'
              }}
            </label>
            <textarea
              id="raw-document-text"
              class="form-textarea"
              rows="5"
              [(ngModel)]="rawText"
              [placeholder]="
                'ai.documentPlaceholder'
                  | translate: 'Nhập biên bản họp, hợp đồng dịch vụ, chỉ thị hoặc quy định nội bộ…'
              "
              [disabled]="loading()"
            ></textarea>
          </div>

          <!-- Controls: Mode and Language Selection -->
          <div class="controls-row">
            <div class="mode-selector">
              <span class="controls-label">{{ 'ai.mode' | translate: 'Chế độ tóm tắt:' }}</span>
              <div class="mode-chips">
                <button
                  type="button"
                  class="mode-chip"
                  [class.active]="selectedMode === 'COMPREHENSIVE'"
                  (click)="selectedMode = 'COMPREHENSIVE'"
                >
                  📋 {{ 'ai.modeComprehensive' | translate: 'Toàn diện' }}
                </button>
                <button
                  type="button"
                  class="mode-chip"
                  [class.active]="selectedMode === 'EXECUTIVE'"
                  (click)="selectedMode = 'EXECUTIVE'"
                >
                  📑 {{ 'ai.modeExecutive' | translate: 'Tóm tắt điều hành' }}
                </button>
                <button
                  type="button"
                  class="mode-chip"
                  [class.active]="selectedMode === 'ACTION_ITEMS'"
                  (click)="selectedMode = 'ACTION_ITEMS'"
                >
                  ⚡ {{ 'ai.modeActionItems' | translate: 'Việc cần làm' }}
                </button>
                <button
                  type="button"
                  class="mode-chip"
                  [class.active]="selectedMode === 'RISKS_COMPLIANCE'"
                  (click)="selectedMode = 'RISKS_COMPLIANCE'"
                >
                  ⚠️ {{ 'ai.modeRisksCompliance' | translate: 'Rủi ro & Tuân thủ' }}
                </button>
              </div>
            </div>

            <div class="language-selector">
              <span class="controls-label">{{ 'common.language' | translate: 'Ngôn ngữ:' }}</span>
              <select class="form-select" [(ngModel)]="selectedLang" [disabled]="loading()">
                <option value="vi">Tiếng Việt</option>
                <option value="en">English</option>
                <option value="ja">日本語</option>
              </select>
            </div>
          </div>

          <!-- Action trigger -->
          <div class="action-row">
            <button
              type="button"
              class="btn-primary-ai"
              (click)="summarize()"
              [disabled]="loading() || (!attachmentId && !rawText.trim())"
            >
              <span *ngIf="loading()" class="spinner" aria-hidden="true"></span>
              <span>{{
                loading()
                  ? ('ai.summarizing' | translate: 'AI đang phân tích tài liệu…')
                  : ('ai.startSummarize' | translate: '✨ Bắt đầu tóm tắt với AI')
              }}</span>
            </button>
          </div>

          <!-- Error Alert -->
          <div *ngIf="errorMessage()" class="alert-error" role="alert">
            {{ errorMessage() }}
          </div>

          <!-- Result Area -->
          <div *ngIf="summaryResult()" class="results-container">
            <!-- Confidence & Word Count Stats -->
            <div class="result-stats-bar">
              <span class="stat-pill">
                {{ 'ai.wordCount' | translate: 'Số từ:' }}
                <strong>{{ summaryResult()!.wordCount }}</strong>
              </span>
              <span class="stat-pill">
                {{ 'ai.confidence' | translate: 'Độ tin cậy:' }}
                <strong>{{ summaryResult()!.confidence * 100 | number: '1.0-0' }}%</strong>
              </span>
              <span *ngIf="summaryResult()!.tokensUsed" class="stat-pill">
                {{ 'ai.tokens' | translate: 'Tokens:' }}
                <strong>{{ summaryResult()!.tokensUsed }}</strong>
              </span>
            </div>

            <!-- Executive Summary -->
            <div *ngIf="summaryResult()!.executiveSummary" class="result-card executive-card">
              <h3 class="result-card-title">
                <span>📑</span> {{ 'ai.executiveSummary' | translate: 'Tóm tắt điều hành' }}
              </h3>
              <p class="summary-text">{{ summaryResult()!.executiveSummary }}</p>
            </div>

            <!-- Key Points -->
            <div *ngIf="summaryResult()!.keyPoints?.length" class="result-card key-points-card">
              <h3 class="result-card-title">
                <span>🎯</span> {{ 'ai.keyPoints' | translate: 'Điểm then chốt' }}
              </h3>
              <ul class="key-points-list">
                <li *ngFor="let point of summaryResult()!.keyPoints">{{ point }}</li>
              </ul>
            </div>

            <!-- Risks and Compliance -->
            <div *ngIf="summaryResult()!.risksAndCompliance?.length" class="result-card risks-card">
              <h3 class="result-card-title">
                <span>⚠️</span> {{ 'ai.risksCompliance' | translate: 'Cảnh báo Rủi ro & Tuân thủ' }}
              </h3>
              <ul class="risks-list">
                <li *ngFor="let risk of summaryResult()!.risksAndCompliance">{{ risk }}</li>
              </ul>
            </div>

            <!-- Extracted Action Items -->
            <div *ngIf="summaryResult()!.actionItems?.length" class="result-card actions-card">
              <div class="actions-header">
                <h3 class="result-card-title">
                  <span>⚡</span>
                  {{ 'ai.actionItems' | translate: 'Danh sách Việc cần làm đã trích xuất' }}
                  <span class="items-count">({{ summaryResult()!.actionItems.length }})</span>
                </h3>
                <button type="button" class="btn-toggle-all" (click)="toggleSelectAll()">
                  {{
                    allSelected()
                      ? ('ai.deselectAll' | translate: 'Bỏ chọn tất cả')
                      : ('ai.selectAll' | translate: 'Chọn tất cả')
                  }}
                </button>
              </div>

              <div class="action-items-list">
                <div
                  *ngFor="let item of summaryResult()!.actionItems"
                  class="action-item-row"
                  [class.selected]="selectedActionIndexes.has(item.orderIndex)"
                >
                  <label class="action-checkbox-wrap">
                    <input
                      type="checkbox"
                      [checked]="selectedActionIndexes.has(item.orderIndex)"
                      (change)="toggleActionSelection(item.orderIndex)"
                    />
                  </label>
                  <div class="action-content">
                    <div class="action-meta-row">
                      <span class="action-index">#{{ item.orderIndex }}</span>
                      <strong class="action-title">{{ item.title }}</strong>
                      <span *ngIf="item.suggestedDepartment" class="badge-dept">{{
                        item.suggestedDepartment
                      }}</span>
                      <span
                        class="badge-priority"
                        [class]="'pri-' + (item.suggestedPriority || 'medium').toLowerCase()"
                      >
                        {{ item.suggestedPriority || 'Medium' }}
                      </span>
                      <span *ngIf="item.suggestedDurationDays" class="badge-days">
                        ⏱ {{ item.suggestedDurationDays }} {{ 'common.days' | translate: 'ngày' }}
                      </span>
                    </div>
                    <p class="action-desc">{{ item.description }}</p>
                  </div>
                </div>
              </div>

              <!-- Create Tasks Button -->
              <div class="create-tasks-bar">
                <button
                  type="button"
                  class="btn-create-tasks"
                  (click)="createTasksFromSelected()"
                  [disabled]="selectedActionIndexes.size === 0 || creatingTasks()"
                >
                  <span *ngIf="creatingTasks()" class="spinner-small" aria-hidden="true"></span>
                  <span>{{
                    creatingTasks()
                      ? ('ai.creatingTasks' | translate: 'Đang tạo nhiệm vụ…')
                      : ('ai.createTasksFromSelected'
                        | translate
                          : 'Tạo nhiệm vụ từ ' + selectedActionIndexes.size + ' việc đã chọn')
                  }}</span>
                </button>
              </div>

              <!-- Created Tasks Success Feedback -->
              <div *ngIf="createdTasksSuccess()" class="tasks-success-banner">
                <span class="success-icon">✓</span>
                <div>
                  <strong>{{
                    'ai.tasksCreatedSuccess' | translate: 'Đã tạo nhiệm vụ thành công!'
                  }}</strong>
                  <p class="success-detail">
                    {{ createdTasksResult()?.totalCreated }}
                    {{
                      'ai.tasksSpawned'
                        | translate: 'nhiệm vụ mới đã được lưu vào hệ thống ở trạng thái DRAFT.'
                    }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .dialog-backdrop {
        position: fixed;
        inset: 0;
        z-index: 1200;
        background-color: rgba(15, 23, 42, 0.65);
        backdrop-filter: blur(4px);
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1.5rem;
      }
      .dialog-card {
        background: var(--surface-card, #ffffff);
        color: var(--text-color, #1e293b);
        border: 1px solid var(--surface-border, #e2e8f0);
        border-radius: 16px;
        width: 100%;
        max-width: 820px;
        max-height: 90vh;
        display: flex;
        flex-direction: column;
        box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
        overflow: hidden;
      }
      .dialog-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1.25rem 1.5rem;
        border-bottom: 1px solid var(--surface-border, #e2e8f0);
        background: linear-gradient(
          135deg,
          rgba(37, 99, 235, 0.05) 0%,
          rgba(139, 92, 246, 0.05) 100%
        );
      }
      .header-branding {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }
      .ai-spark {
        font-size: 1.75rem;
      }
      .dialog-title {
        font-size: 1.2rem;
        font-weight: 700;
        margin: 0;
        color: var(--text-color, #0f172a);
      }
      .dialog-subtitle {
        font-size: 0.85rem;
        color: var(--text-secondary, #64748b);
        margin: 0.15rem 0 0 0;
      }
      .btn-close {
        background: none;
        border: none;
        font-size: 1.25rem;
        color: var(--text-secondary, #64748b);
        cursor: pointer;
        padding: 0.25rem 0.5rem;
        border-radius: 8px;
        transition: background-color 0.2s;
      }
      .btn-close:hover {
        background-color: var(--surface-hover, #f1f5f9);
      }
      .dialog-body {
        padding: 1.5rem;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
      }
      .source-banner {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        background: #f8fafc;
        border: 1px solid #e2e8f0;
      }
      .source-icon {
        font-size: 1.25rem;
      }
      .source-info {
        display: flex;
        flex-direction: column;
      }
      .source-label {
        font-size: 0.75rem;
        color: #64748b;
        text-transform: uppercase;
      }
      .source-name {
        font-size: 0.95rem;
        color: #0f172a;
      }
      .input-group {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
      }
      .input-label {
        font-size: 0.85rem;
        font-weight: 600;
        color: #334155;
      }
      .form-textarea {
        width: 100%;
        border-radius: 10px;
        border: 1px solid #cbd5e1;
        padding: 0.75rem;
        font-size: 0.9rem;
        font-family: inherit;
        resize: vertical;
        box-sizing: border-box;
      }
      .form-textarea:focus {
        outline: none;
        border-color: #3b82f6;
        box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2);
      }
      .controls-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        flex-wrap: wrap;
      }
      .controls-label {
        font-size: 0.8rem;
        font-weight: 600;
        color: #64748b;
        margin-bottom: 0.35rem;
        display: block;
      }
      .mode-chips {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .mode-chip {
        padding: 0.45rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.8rem;
        font-weight: 500;
        border: 1px solid #cbd5e1;
        background: #ffffff;
        color: #475569;
        cursor: pointer;
        transition: all 0.2s;
      }
      .mode-chip:hover {
        background: #f1f5f9;
      }
      .mode-chip.active {
        background: #2563eb;
        color: #ffffff;
        border-color: #2563eb;
      }
      .form-select {
        padding: 0.45rem 0.85rem;
        border-radius: 8px;
        border: 1px solid #cbd5e1;
        font-size: 0.85rem;
      }
      .action-row {
        display: flex;
        justify-content: flex-end;
      }
      .btn-primary-ai {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        background: linear-gradient(135deg, #2563eb 0%, #7c3aed 100%);
        color: #ffffff;
        border: none;
        padding: 0.65rem 1.4rem;
        border-radius: 10px;
        font-weight: 600;
        font-size: 0.9rem;
        cursor: pointer;
        transition: opacity 0.2s;
      }
      .btn-primary-ai:hover:not(:disabled) {
        opacity: 0.92;
      }
      .btn-primary-ai:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .alert-error {
        background: #fef2f2;
        border: 1px solid #fecaca;
        color: #b91c1c;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        font-size: 0.85rem;
      }
      .results-container {
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
        margin-top: 0.5rem;
      }
      .result-stats-bar {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
      }
      .stat-pill {
        background: #f1f5f9;
        padding: 0.25rem 0.65rem;
        border-radius: 6px;
        font-size: 0.75rem;
        color: #475569;
      }
      .result-card {
        border-radius: 12px;
        padding: 1.25rem;
        border: 1px solid #e2e8f0;
      }
      .result-card-title {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 1rem;
        font-weight: 700;
        margin: 0 0 0.75rem 0;
        color: #1e293b;
      }
      .executive-card {
        background: #f8fafc;
        border-left: 4px solid #3b82f6;
      }
      .summary-text {
        font-size: 0.92rem;
        line-height: 1.6;
        color: #334155;
        margin: 0;
      }
      .key-points-card {
        background: #ffffff;
      }
      .key-points-list,
      .risks-list {
        margin: 0;
        padding-left: 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        font-size: 0.9rem;
        color: #334155;
      }
      .risks-card {
        background: #fffbeb;
        border-left: 4px solid #f59e0b;
        border-color: #fde68a;
      }
      .actions-card {
        background: #ffffff;
      }
      .actions-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 0.75rem;
      }
      .items-count {
        font-weight: 500;
        color: #64748b;
        font-size: 0.85rem;
      }
      .btn-toggle-all {
        background: none;
        border: none;
        color: #2563eb;
        font-size: 0.8rem;
        font-weight: 600;
        cursor: pointer;
      }
      .action-items-list {
        display: flex;
        flex-direction: column;
        gap: 0.6rem;
      }
      .action-item-row {
        display: flex;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        border: 1px solid #e2e8f0;
        background: #fafafa;
        transition: all 0.2s;
      }
      .action-item-row.selected {
        border-color: #93c5fd;
        background: #eff6ff;
      }
      .action-checkbox-wrap {
        display: flex;
        align-items: flex-start;
        padding-top: 0.25rem;
      }
      .action-content {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }
      .action-meta-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .action-index {
        font-size: 0.75rem;
        font-weight: 700;
        color: #64748b;
      }
      .action-title {
        font-size: 0.9rem;
        color: #0f172a;
      }
      .badge-dept {
        background: #e0e7ff;
        color: #3730a3;
        font-size: 0.7rem;
        padding: 0.15rem 0.5rem;
        border-radius: 9999px;
        font-weight: 600;
      }
      .badge-priority {
        font-size: 0.7rem;
        padding: 0.15rem 0.5rem;
        border-radius: 9999px;
        font-weight: 600;
      }
      .pri-critical,
      .pri-urgent {
        background: #fee2e2;
        color: #991b1b;
      }
      .pri-high {
        background: #ffedd5;
        color: #9a3412;
      }
      .pri-medium {
        background: #fef3c7;
        color: #92400e;
      }
      .pri-low {
        background: #f1f5f9;
        color: #475569;
      }
      .badge-days {
        font-size: 0.75rem;
        color: #64748b;
      }
      .action-desc {
        font-size: 0.85rem;
        color: #475569;
        margin: 0;
        line-height: 1.4;
      }
      .create-tasks-bar {
        display: flex;
        justify-content: flex-end;
        margin-top: 1rem;
      }
      .btn-create-tasks {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        background: #16a34a;
        color: #ffffff;
        border: none;
        padding: 0.6rem 1.25rem;
        border-radius: 8px;
        font-weight: 600;
        font-size: 0.85rem;
        cursor: pointer;
        transition: background 0.2s;
      }
      .btn-create-tasks:hover:not(:disabled) {
        background: #15803d;
      }
      .btn-create-tasks:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      .tasks-success-banner {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        margin-top: 1rem;
        padding: 0.75rem 1rem;
        border-radius: 10px;
        background: #f0fdf4;
        border: 1px solid #bbf7d0;
        color: #166534;
      }
      .success-icon {
        font-size: 1.25rem;
        font-weight: bold;
      }
      .success-detail {
        font-size: 0.8rem;
        margin: 0.15rem 0 0 0;
      }
      .spinner {
        display: inline-block;
        width: 14px;
        height: 14px;
        border: 2px solid rgba(255, 255, 255, 0.4);
        border-radius: 50%;
        border-top-color: #ffffff;
        animation: spin 0.8s linear infinite;
      }
      .spinner-small {
        display: inline-block;
        width: 12px;
        height: 12px;
        border: 2px solid rgba(255, 255, 255, 0.4);
        border-radius: 50%;
        border-top-color: #ffffff;
        animation: spin 0.8s linear infinite;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class DocumentSummarizerDialog {
  @Input() isOpen = false;
  @Input() attachmentId: string | null = null;
  @Input() fileName: string | null = null;
  @Input() rawText = '';

  @Output() close = new EventEmitter<void>();
  @Output() tasksCreated = new EventEmitter<CreatedSummaryTaskResult[]>();

  private readonly http = inject(HttpClient);

  selectedMode: 'COMPREHENSIVE' | 'EXECUTIVE' | 'ACTION_ITEMS' | 'RISKS_COMPLIANCE' =
    'COMPREHENSIVE';
  selectedLang: 'vi' | 'en' | 'ja' = 'vi';

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly summaryResult = signal<DocumentSummarizeResult | null>(null);

  readonly selectedActionIndexes = new Set<number>();
  readonly creatingTasks = signal(false);
  readonly createdTasksSuccess = signal(false);
  readonly createdTasksResult = signal<CreateTasksFromSummaryResponse | null>(null);

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.closeDialog();
    }
  }

  closeDialog() {
    this.close.emit();
  }

  summarize() {
    this.loading.set(true);
    this.errorMessage.set('');
    this.createdTasksSuccess.set(false);
    this.selectedActionIndexes.clear();

    const payload = {
      rawText: this.attachmentId ? null : this.rawText.trim(),
      attachmentId: this.attachmentId || null,
      mode: this.selectedMode,
      language: this.selectedLang,
    };

    this.http.post<DocumentSummarizeResult>('/api/v1/ai/documents/summarize', payload).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.summaryResult.set(res);
        // By default select all action items
        if (res.actionItems?.length) {
          for (const item of res.actionItems) {
            this.selectedActionIndexes.add(item.orderIndex);
          }
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message || 'Có lỗi xảy ra khi tóm tắt tài liệu.');
      },
    });
  }

  toggleActionSelection(index: number) {
    if (this.selectedActionIndexes.has(index)) {
      this.selectedActionIndexes.delete(index);
    } else {
      this.selectedActionIndexes.add(index);
    }
  }

  allSelected(): boolean {
    const items = this.summaryResult()?.actionItems || [];
    return items.length > 0 && this.selectedActionIndexes.size === items.length;
  }

  toggleSelectAll() {
    const items = this.summaryResult()?.actionItems || [];
    if (this.allSelected()) {
      this.selectedActionIndexes.clear();
    } else {
      for (const item of items) {
        this.selectedActionIndexes.add(item.orderIndex);
      }
    }
  }

  createTasksFromSelected() {
    const res = this.summaryResult();
    if (!res || this.selectedActionIndexes.size === 0 || this.creatingTasks()) return;

    const selectedItems = res.actionItems.filter((i) =>
      this.selectedActionIndexes.has(i.orderIndex),
    );
    this.creatingTasks.set(true);
    this.errorMessage.set('');

    const payload = {
      sourceTitle: res.sourceTitle || this.fileName || 'Tài liệu tóm tắt',
      sourceReference: this.attachmentId || 'Văn bản trực tiếp',
      items: selectedItems,
    };

    this.http
      .post<CreateTasksFromSummaryResponse>('/api/v1/ai/documents/create-tasks', payload)
      .subscribe({
        next: (creationResp) => {
          this.creatingTasks.set(false);
          this.createdTasksSuccess.set(true);
          this.createdTasksResult.set(creationResp);
          this.tasksCreated.emit(creationResp.createdTasks);
        },
        error: (err) => {
          this.creatingTasks.set(false);
          this.errorMessage.set(err?.error?.message || 'Không thể tạo nhiệm vụ từ tài liệu.');
        },
      });
  }
}
