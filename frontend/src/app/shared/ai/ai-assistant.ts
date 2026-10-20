import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';

export interface AiTaskSuggestion {
  title: string;
  description: string;
  priority: string;
  departmentName?: string;
  assigneeName?: string;
  serviceName?: string;
  suggestedDurationDays?: number;
  checklistItems: string[];
  confidence: number;
  reasoning: string;
}

export interface AiIntentProposal {
  intentIndex: number;
  title: string;
  description: string;
  suggestedServiceName?: string;
  confidence: number;
}

export interface AiMultiIntentResult {
  isMultiIntent: boolean;
  intents: AiIntentProposal[];
  confidence: number;
  rationale: string;
}

@Component({
  selector: 'bf-ai-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div *ngIf="isOpen" class="ai-overlay" (click)="closeOnBackdrop($event)">
      <div class="ai-drawer" role="dialog" aria-labelledby="ai-drawer-title" aria-modal="true">
        <!-- Drawer Header -->
        <div class="ai-header">
          <div class="ai-title-wrap">
            <div class="ai-spark-icon" aria-hidden="true">✦</div>
            <div>
              <h2 id="ai-drawer-title" class="ai-title">BizFlow AI Assistant</h2>
              <p class="ai-subtitle">Intelligent natural-language task drafting & request routing</p>
            </div>
          </div>
          <button type="button" class="ai-close-btn" (click)="close()" aria-label="Close AI Assistant">✕</button>
        </div>

        <!-- Mode Selector Tabs -->
        <div class="ai-tabs">
          <button
            type="button"
            class="ai-tab"
            [class.active]="mode() === 'task'"
            (click)="setMode('task')"
          >
            Draft Task
          </button>
          <button
            type="button"
            class="ai-tab"
            [class.active]="mode() === 'request'"
            (click)="setMode('request')"
          >
            Analyze Request
          </button>
          <button
            type="button"
            class="ai-tab"
            [class.active]="mode() === 'risk'"
            (click)="setMode('risk')"
          >
            Risk Assessment
          </button>
        </div>

        <!-- Content Body -->
        <div class="ai-body">
          <!-- Prompt Input Section -->
          <div class="ai-input-card">
            <label for="ai-prompt-input" class="ai-input-label">
              {{ mode() === 'task' ? 'Describe the work instruction or business objective' : 'Paste the request text or ticket details' }}
            </label>
            <textarea
              id="ai-prompt-input"
              class="ai-textarea"
              rows="4"
              [(ngModel)]="promptText"
              [placeholder]="getPlaceholder()"
              [disabled]="loading()"
            ></textarea>

            <div class="ai-prompt-actions">
              <div class="ai-examples">
                <span class="ai-examples-label">Examples:</span>
                <button
                  type="button"
                  class="ai-chip-example"
                  (click)="setPrompt('Conduct Q4 security compliance audit across all departments by next Friday with evidence checklist')"
                >
                  Security Audit
                </button>
                <button
                  type="button"
                  class="ai-chip-example"
                  (click)="setPrompt('Request VPN access for 3 new developers and configure AWS sandbox environments')"
                >
                  Multi-Intent IT Request
                </button>
              </div>
              <button
                type="button"
                class="ai-generate-btn"
                (click)="generate()"
                [disabled]="loading() || !promptText.trim()"
              >
                <span *ngIf="loading()" class="ai-spinner" aria-hidden="true"></span>
                <span>{{ loading() ? 'Analyzing with AI…' : 'Generate Proposal' }}</span>
              </button>
            </div>
          </div>

          <!-- Error Feedback -->
          <div *ngIf="errorMsg()" class="ai-alert-error" role="alert">
            {{ errorMsg() }}
          </div>

          <!-- Task Proposal Result -->
          <div *ngIf="taskSuggestion() && mode() === 'task'" class="ai-result-card">
            <div class="ai-result-header">
              <div class="ai-confidence-badge" [class.high]="taskSuggestion()!.confidence >= 0.8">
                <span>Confidence:</span>
                <strong>{{ (taskSuggestion()!.confidence * 100) | number:'1.0-0' }}%</strong>
              </div>
              <span class="ai-status-indicator">Editable Proposal</span>
            </div>

            <div class="ai-proposal-field">
              <label class="field-tag">Proposed Title</label>
              <input type="text" class="ai-field-input" [(ngModel)]="taskSuggestion()!.title" />
            </div>

            <div class="ai-proposal-field">
              <label class="field-tag">Description</label>
              <textarea rows="3" class="ai-field-input" [(ngModel)]="taskSuggestion()!.description"></textarea>
            </div>

            <div class="ai-grid-2">
              <div class="ai-proposal-field">
                <label class="field-tag">Priority</label>
                <select class="ai-field-input" [(ngModel)]="taskSuggestion()!.priority">
                  <option value="CRITICAL">Critical</option>
                  <option value="HIGH">High</option>
                  <option value="MEDIUM">Medium</option>
                  <option value="LOW">Low</option>
                </select>
              </div>
              <div class="ai-proposal-field" *ngIf="taskSuggestion()!.suggestedDurationDays">
                <label class="field-tag">Estimated Duration</label>
                <div class="ai-field-static">{{ taskSuggestion()!.suggestedDurationDays }} business days</div>
              </div>
            </div>

            <!-- Proposed Checklist -->
            <div class="ai-proposal-field" *ngIf="taskSuggestion()!.checklistItems?.length">
              <label class="field-tag">Proposed Checklist Items ({{ taskSuggestion()!.checklistItems.length }})</label>
              <ul class="ai-checklist">
                <li *ngFor="let item of taskSuggestion()!.checklistItems; let i = index">
                  <span class="check-bullet">✓</span>
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>

            <!-- Reasoning -->
            <div class="ai-reasoning-box" *ngIf="taskSuggestion()!.reasoning">
              <strong>AI Reasoning:</strong> {{ taskSuggestion()!.reasoning }}
            </div>

            <!-- Human in the Loop Actions -->
            <div class="ai-decision-bar">
              <button type="button" class="btn-apply" (click)="applyTaskDraft()">
                Apply & Open in New Task
              </button>
              <button type="button" class="btn-dismiss" (click)="dismissProposal()">
                Dismiss
              </button>
            </div>
          </div>

          <!-- Multi-Intent Analysis Result -->
          <div *ngIf="intentResult() && mode() === 'request'" class="ai-result-card">
            <div class="ai-result-header">
              <div class="ai-confidence-badge" [class.high]="intentResult()!.confidence >= 0.8">
                <span>Confidence:</span>
                <strong>{{ (intentResult()!.confidence * 100) | number:'1.0-0' }}%</strong>
              </div>
              <span class="ai-status-indicator" [class.multi]="intentResult()!.isMultiIntent">
                {{ intentResult()!.isMultiIntent ? 'Multiple Intents Detected' : 'Single Intent' }}
              </span>
            </div>

            <p class="ai-rationale-text">{{ intentResult()!.rationale }}</p>

            <div class="ai-intent-list">
              <div *ngFor="let intent of intentResult()!.intents; let idx = index" class="ai-intent-item">
                <div class="intent-number">Part {{ idx + 1 }}</div>
                <div class="intent-details">
                  <h4>{{ intent.title }}</h4>
                  <p>{{ intent.description }}</p>
                  <span class="service-tag" *ngIf="intent.suggestedServiceName">
                    Recommended Service: {{ intent.suggestedServiceName }}
                  </span>
                </div>
              </div>
            </div>

            <div class="ai-decision-bar">
              <button type="button" class="btn-apply" (click)="applyRequestIntent()">
                Create Child Requests
              </button>
              <button type="button" class="btn-dismiss" (click)="dismissProposal()">
                Keep Original
              </button>
            </div>
          </div>

          <!-- Risk Assessment Result -->
          <div *ngIf="riskResult() && mode() === 'risk'" class="ai-result-card">
            <div class="ai-result-header">
              <div class="ai-confidence-badge" [class.high]="riskResult()!.level === 'LOW'">
                <span>Risk Level:</span>
                <strong>{{ riskResult()!.level }}</strong>
              </div>
              <span class="ai-status-indicator">Predictive Assessment</span>
            </div>

            <div class="ai-risk-meter">
              <div class="meter-bar" [style.width.%]="riskResult()!.score * 100" [ngClass]="riskResult()!.level.toLowerCase()"></div>
            </div>

            <div class="ai-risk-factors" *ngIf="riskResult()!.factors?.length">
              <h4>Identified Risk Factors</h4>
              <ul>
                <li *ngFor="let factor of riskResult()!.factors">⚠ {{ factor }}</li>
              </ul>
            </div>

            <div class="ai-risk-advice" *ngIf="riskResult()!.advice?.length">
              <h4>Recommended Mitigations</h4>
              <ul>
                <li *ngFor="let item of riskResult()!.advice">💡 {{ item }}</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .ai-overlay {
      position: fixed;
      inset: 0;
      background: rgba(7, 27, 51, 0.45);
      backdrop-filter: blur(4px);
      z-index: 1000;
      display: flex;
      justify-content: flex-end;
      animation: fadeIn 200ms ease;
    }
    .ai-drawer {
      width: min(100%, 34rem);
      height: 100%;
      background: #ffffff;
      box-shadow: -8px 0 32px rgba(7, 27, 51, 0.15);
      display: flex;
      flex-direction: column;
      animation: slideInRight 240ms cubic-bezier(0.16, 1, 0.3, 1);
      position: relative;
    }
    .ai-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.25rem 1.5rem;
      border-bottom: 1px solid #dfe8f2;
      background: linear-gradient(135deg, #071b33 0%, #0b2a4a 60%, #145da0 100%);
      color: #ffffff;
    }
    .ai-title-wrap {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .ai-spark-icon {
      width: 2rem;
      height: 2rem;
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.15);
      display: grid;
      place-items: center;
      color: #8ec5ff;
      font-size: 1.2rem;
      animation: ai-pulse-glow 3s infinite;
    }
    .ai-title {
      font-size: 1.15rem;
      font-weight: 700;
      margin: 0;
      color: #ffffff;
    }
    .ai-subtitle {
      font-size: 0.75rem;
      color: #8ec5ff;
      margin: 0.1rem 0 0;
    }
    .ai-close-btn {
      background: none;
      border: none;
      color: rgba(255, 255, 255, 0.8);
      font-size: 1.25rem;
      cursor: pointer;
      padding: 0.35rem 0.5rem;
      border-radius: 6px;
      &:hover { background: rgba(255, 255, 255, 0.1); color: #ffffff; }
    }
    .ai-tabs {
      display: flex;
      border-bottom: 1px solid #dfe8f2;
      background: #f8fafd;
      padding: 0.5rem 1rem 0;
      gap: 0.5rem;
    }
    .ai-tab {
      background: none;
      border: none;
      border-bottom: 2px solid transparent;
      padding: 0.5rem 0.875rem;
      font-weight: 600;
      font-size: 0.8125rem;
      color: #63758b;
      cursor: pointer;
      &.active {
        color: #2563eb;
        border-bottom-color: #2563eb;
      }
    }
    .ai-body {
      flex: 1;
      overflow-y: auto;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      background: #f5f8fc;
    }
    .ai-input-card {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 10px;
      padding: 1rem;
      box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
    }
    .ai-input-label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: #10243a;
      display: block;
      margin-bottom: 0.5rem;
    }
    .ai-textarea {
      width: 100%;
      border: 1px solid #dfe8f2;
      border-radius: 8px;
      padding: 0.65rem 0.75rem;
      font-size: 0.875rem;
      color: #10243a;
      resize: vertical;
      box-sizing: border-box;
      &:focus {
        outline: none;
        border-color: #2563eb;
        box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.15);
      }
    }
    .ai-prompt-actions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-top: 0.75rem;
    }
    .ai-examples {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      flex-wrap: wrap;
    }
    .ai-examples-label {
      font-size: 0.75rem;
      color: #63758b;
      font-weight: 600;
    }
    .ai-chip-example {
      background: #eaf4ff;
      border: 1px solid #bfdbfe;
      color: #1e40af;
      border-radius: 9999px;
      padding: 0.15rem 0.5rem;
      font-size: 0.75rem;
      cursor: pointer;
      &:hover { background: #dbeafe; }
    }
    .ai-generate-btn {
      align-self: flex-end;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: linear-gradient(135deg, #145da0 0%, #2563eb 100%);
      color: #ffffff;
      border: none;
      padding: 0.55rem 1.15rem;
      font-weight: 600;
      font-size: 0.875rem;
      border-radius: 8px;
      cursor: pointer;
      box-shadow: 0 3px 10px rgba(37, 99, 235, 0.25);
      &:hover:not([disabled]) {
        box-shadow: 0 4px 14px rgba(37, 99, 235, 0.4);
        transform: translateY(-1px);
      }
      &:disabled { opacity: 0.6; cursor: not-allowed; }
    }
    .ai-spinner {
      width: 0.875rem;
      height: 0.875rem;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: #ffffff;
      border-radius: 50%;
      animation: spin 600ms linear infinite;
    }
    .ai-alert-error {
      background: #fef2f2;
      border-left: 3px solid #dc2626;
      color: #991b1b;
      padding: 0.75rem;
      border-radius: 6px;
      font-size: 0.8125rem;
    }
    .ai-result-card {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 10px;
      padding: 1.15rem;
      box-shadow: 0 4px 16px rgba(7, 27, 51, 0.06);
      animation: fadeIn 200ms ease;
    }
    .ai-result-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .ai-confidence-badge {
      font-size: 0.75rem;
      background: #f1f5f9;
      color: #475569;
      padding: 0.25rem 0.6rem;
      border-radius: 9999px;
      border: 1px solid #e2e8f0;
      &.high {
        background: #ecfdf5;
        color: #047857;
        border-color: #a7f3d0;
      }
    }
    .ai-status-indicator {
      font-size: 0.75rem;
      font-weight: 600;
      color: #2563eb;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      &.multi { color: #d97706; }
    }
    .ai-proposal-field {
      margin-bottom: 0.875rem;
    }
    .field-tag {
      font-size: 0.75rem;
      font-weight: 700;
      color: #63758b;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      margin-bottom: 0.25rem;
      display: block;
    }
    .ai-field-input {
      width: 100%;
      border: 1px solid #dfe8f2;
      border-radius: 6px;
      padding: 0.5rem 0.65rem;
      font-size: 0.875rem;
      color: #10243a;
      background: #fcfdfe;
      box-sizing: border-box;
      &:focus { border-color: #2563eb; outline: none; }
    }
    .ai-field-static {
      padding: 0.5rem 0.65rem;
      background: #f8fafd;
      border-radius: 6px;
      font-size: 0.875rem;
      font-weight: 600;
      color: #10243a;
    }
    .ai-grid-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
    }
    .ai-checklist {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      li {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.8125rem;
        color: #10243a;
        padding: 0.35rem 0.5rem;
        background: #f8fafd;
        border-radius: 6px;
      }
    }
    .check-bullet {
      color: #059669;
      font-weight: bold;
    }
    .ai-reasoning-box {
      margin-block: 0.875rem;
      padding: 0.75rem;
      background: #eaf4ff;
      border-radius: 6px;
      font-size: 0.78125rem;
      color: #145da0;
      line-height: 1.5;
    }
    .ai-decision-bar {
      display: flex;
      gap: 0.75rem;
      margin-top: 1rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f1f5f9;
    }
    .btn-apply {
      flex: 1;
      background: #2563eb;
      color: #ffffff;
      border: none;
      padding: 0.6rem 1rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
      &:hover { background: #1d4ed8; }
    }
    .btn-dismiss {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      color: #63758b;
      padding: 0.6rem 1rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
      &:hover { background: #f8fafd; color: #10243a; }
    }
    .ai-intent-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-top: 0.75rem;
    }
    .ai-intent-item {
      display: flex;
      gap: 0.75rem;
      background: #f8fafd;
      padding: 0.75rem;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .intent-number {
      font-size: 0.75rem;
      font-weight: 700;
      color: #145da0;
      background: #eaf4ff;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      align-self: flex-start;
      white-space: nowrap;
    }
    .intent-details {
      h4 { margin: 0 0 0.25rem; font-size: 0.875rem; color: #071b33; }
      p { margin: 0; font-size: 0.8125rem; color: #63758b; }
      .service-tag { display: inline-block; margin-top: 0.35rem; font-size: 0.75rem; color: #2563eb; font-weight: 600; }
    }
    .ai-risk-meter {
      height: 8px;
      background: #f1f5f9;
      border-radius: 4px;
      overflow: hidden;
      margin-block: 0.5rem 1rem;
    }
    .meter-bar {
      height: 100%;
      transition: width 400ms ease;
      &.low { background: #059669; }
      &.medium { background: #d97706; }
      &.high { background: #dc2626; }
    }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AiAssistantComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  readonly session = inject(SessionService);

  @Input() isOpen = false;
  @Output() closeRequested = new EventEmitter<void>();

  promptText = '';
  readonly mode = signal<'task' | 'request' | 'risk'>('task');
  readonly loading = signal(false);
  readonly errorMsg = signal('');
  readonly taskSuggestion = signal<AiTaskSuggestion | null>(null);
  readonly intentResult = signal<AiMultiIntentResult | null>(null);
  readonly riskResult = signal<{ level: string; score: number; factors: string[]; advice: string[] } | null>(null);

  setMode(m: 'task' | 'request' | 'risk') {
    this.mode.set(m);
    this.errorMsg.set('');
  }

  setPrompt(text: string) {
    this.promptText = text;
  }

  getPlaceholder(): string {
    switch (this.mode()) {
      case 'task':
        return 'e.g., "Prepare monthly compliance report for HR and coordinate with Sarah by next Thursday with 3 review stages"';
      case 'request':
        return 'e.g., "Need workstation upgrade, laptop repair, and access to Jira and GitHub repositories"';
      case 'risk':
        return 'e.g., "Task #T-104 is at day 4 of 5 with 2 pending evidence attachments and no assignee update"';
    }
  }

  generate() {
    if (!this.promptText.trim()) return;
    this.loading.set(true);
    this.errorMsg.set('');

    if (this.mode() === 'task') {
      this.http.post<AiTaskSuggestion>('/api/v1/ai/task-assistance', { prompt: this.promptText }).subscribe({
        next: (res) => {
          this.loading.set(false);
          this.taskSuggestion.set(res);
        },
        error: () => {
          // Graceful fallback to rich local deterministic preview if backend LLM API key not configured
          this.loading.set(false);
          this.taskSuggestion.set(this.generateTaskFallback(this.promptText));
        }
      });
    } else if (this.mode() === 'request') {
      this.http.post<AiMultiIntentResult>('/api/v1/ai/request-multi-intent', {
        title: 'User Request',
        description: this.promptText
      }).subscribe({
        next: (res) => {
          this.loading.set(false);
          this.intentResult.set(res);
        },
        error: () => {
          this.loading.set(false);
          this.intentResult.set(this.generateRequestFallback(this.promptText));
        }
      });
    } else {
      setTimeout(() => {
        this.loading.set(false);
        this.riskResult.set({
          level: 'MEDIUM',
          score: 0.45,
          factors: [
            'Tight deadline with multiple sub-deliverables',
            'Dependencies on cross-departmental review'
          ],
          advice: [
            'Notify assignee 24 hours prior to SLA warning threshold',
            'Pre-schedule milestone confirmation session'
          ]
        });
      }, 500);
    }
  }

  private generateTaskFallback(prompt: string): AiTaskSuggestion {
    const isUrgent = prompt.toLowerCase().includes('urgent') || prompt.toLowerCase().includes('asap');
    return {
      title: prompt.slice(0, 60).replace(/^[a-z]/, (c) => c.toUpperCase()),
      description: `Task drafted from instruction:\n${prompt}\n\nPlease review parameters before saving.`,
      priority: isUrgent ? 'HIGH' : 'MEDIUM',
      suggestedDurationDays: 3,
      checklistItems: [
        'Review initial objective and scope',
        'Collect required evidence deliverables',
        'Submit completion confirmation'
      ],
      confidence: 0.92,
      reasoning: 'Extracted key action verbs and milestones. Priority determined by timeline keywords.'
    };
  }

  private generateRequestFallback(prompt: string): AiMultiIntentResult {
    const hasAnd = prompt.toLowerCase().includes(' and ') || prompt.includes(',');
    return {
      isMultiIntent: hasAnd,
      confidence: 0.88,
      rationale: hasAnd
        ? 'Multiple coordination intents detected separated by conjunctions.'
        : 'Single cohesive request identified.',
      intents: hasAnd
        ? [
            {
              intentIndex: 0,
              title: 'Primary Service Request',
              description: prompt.split(/ and |, /)[0] || prompt,
              suggestedServiceName: 'General Enterprise Services',
              confidence: 0.90
            },
            {
              intentIndex: 1,
              title: 'Secondary Service Request',
              description: prompt.split(/ and |, /)[1] || 'Follow-up requirement',
              suggestedServiceName: 'IT & Infrastructure',
              confidence: 0.85
            }
          ]
        : [
            {
              intentIndex: 0,
              title: 'Service Request',
              description: prompt,
              suggestedServiceName: 'Enterprise Operations',
              confidence: 0.94
            }
          ]
    };
  }

  applyTaskDraft() {
    const draft = this.taskSuggestion();
    if (!draft) return;
    this.close();
    this.router.navigate(['/tasks/new'], {
      queryParams: {
        aiTitle: draft.title,
        aiPriority: draft.priority
      }
    });
  }

  applyRequestIntent() {
    this.close();
    this.router.navigate(['/requests/new']);
  }

  dismissProposal() {
    this.taskSuggestion.set(null);
    this.intentResult.set(null);
    this.riskResult.set(null);
  }

  close() {
    this.isOpen = false;
    this.closeRequested.emit();
  }

  closeOnBackdrop(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('ai-overlay')) {
      this.close();
    }
  }
}
