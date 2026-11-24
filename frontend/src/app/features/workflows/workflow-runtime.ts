import {
  Component,
  DestroyRef,
  Input,
  OnChanges,
  Output,
  EventEmitter,
  inject,
  signal,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkflowDelegation } from './workflow-delegation';

interface Decision {
  actorId?: string;
  principalId?: string;
  principalName?: string;
  actorName: string;
  outcome: string;
  note: string | null;
  decidedAt: string;
}
interface Step {
  agent?: {
    id: string;
    status: string;
    attempts: number;
    errorCode: string | null;
    model: string | null;
    result: {
      summary: string;
      deliverable: string;
      needsHumanReview: boolean;
      findings: string[];
      evidence: { source: string; quote: string }[];
    } | null;
  } | null;
  id: string;
  name: string;
  nodeType: string;
  status: string;
  policy: string;
  dueAt: string | null;
  outcome: string | null;
  canAct: boolean;
  candidates: string[];
  decisions: Decision[];
  pausedAt?: string | null;
  canManageSla?: boolean;
  slaPauses?: {
    id: string;
    pausedBy: string;
    reason: string;
    pausedAt: string;
    dueAtBefore: string;
    resumedAt: string | null;
    resumedBy: string | null;
    dueAtAfter: string | null;
  }[];
  candidateDetails: { userId: string; name: string; decided: boolean }[];
  delegations: {
    id: string;
    principalName: string;
    delegateName: string;
    createdBy: string;
    reason: string;
    createdAt: string;
    expiresAt: string;
    revokedAt: string | null;
    active: boolean;
  }[];
}
interface Runtime {
  id: string;
  name: string;
  versionNo: number;
  status: string;
  revision: number;
  canDelegate: boolean;
  steps: Step[];
  formFields: { key: string; label: string }[];
  formData: Record<string, string | number>;
}
interface AgentAttempt {
  attempt: number;
  startedAt: string;
  completedAt: string;
  model: string | null;
  errorCode: string | null;
  trace: {
    round: number;
    tool?: string;
    action?: string;
    result?: { summary: string; deliverable: string };
  }[];
}

@Component({
  selector: 'bf-workflow-runtime',
  standalone: true,
  imports: [DatePipe, FormsModule, WorkflowDelegation],
  template: `
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
    @if (runtime(); as run) {
      <section aria-label="Tiến độ quy trình" class="runtime-panel">
        <h2>{{ run.name }} · Phiên bản {{ run.versionNo }}</h2>
        <p>Trạng thái: {{ label(run.status) }}</p>
        @if (run.formFields?.length) {
          <dl>
            @for (field of run.formFields; track field.key) {
              <dt>{{ field.label }}</dt>
              <dd>{{ run.formData[field.key] ?? 'Chưa nhập' }}</dd>
            }
          </dl>
        }
        <ol>
          @for (step of run.steps; track step.id) {
            <li>
              <h3>{{ step.name }} · {{ label(step.status) }}</h3>
              <p>Người phụ trách: {{ step.candidates.join(', ') }}</p>
              @if (step.agent; as agent) {
                <section aria-label="Kết quả AI Agent" class="agent-result">
                  <h4>AI Agent · {{ agentLabel(agent.status) }}</h4>
                  <p>
                    Số lần chạy: {{ agent.attempts }}
                    @if (agent.model) {
                      · Mô hình: {{ agent.model }}
                    }
                  </p>
                  @if (agent.errorCode) {
                    <p>
                      AI cần kiểm tra: {{ agent.errorCode }}. Người phụ trách có thể tiếp tục xử lý
                      bước.
                    </p>
                  }
                  @if (agent.result; as result) {
                    <p>Kết quả gần nhất:</p>
                    <p>
                      <strong>{{ result.summary }}</strong>
                    </p>
                    <p class="agent-deliverable">{{ result.deliverable }}</p>
                    @for (finding of result.findings; track $index) {
                      <p>• {{ finding }}</p>
                    }
                    <details>
                      <summary>Nguồn dữ liệu đã trích dẫn</summary>
                      @for (evidence of result.evidence; track $index) {
                          <p>{{ agentSourceLabel(evidence.source) }}: “{{ evidence.quote }}”</p>
                      }
                    </details>
                  }
                  <button type="button" [disabled]="busy()" (click)="loadAgentHistory(step)">
                    Xem lịch sử chạy AI
                  </button>
                  @for (attempt of agentHistory()[step.id] || []; track attempt.attempt) {
                    <details>
                      <summary>
                        Lần {{ attempt.attempt }} ·
                        {{ attempt.completedAt | date: 'dd/MM/yyyy HH:mm:ss' }}
                        @if (attempt.errorCode) {
                          · {{ attempt.errorCode }}
                        }
                      </summary>
                      @for (entry of attempt.trace; track entry.round) {
                        <p>{{ agentToolLabel(entry.tool || entry.action || '') }}</p>
                        @if (entry.result) {
                          <p>{{ entry.result.summary }}</p>
                          <p class="agent-deliverable">{{ entry.result.deliverable }}</p>
                        }
                      }
                    </details>
                  }
                  @if (
                    (step.canAct || run.canDelegate) &&
                    step.status === 'PENDING' &&
                    !step.pausedAt &&
                    agent.attempts < 9 &&
                    (agent.status === 'FAILED' || agent.status === 'REVIEW_REQUIRED')
                  ) {
                    <label [for]="'agent-retry-' + step.id">Lý do chạy lại AI</label>
                    <input
                      [id]="'agent-retry-' + step.id"
                      [(ngModel)]="agentReasons[step.id]"
                      maxlength="1000"
                    />
                    <button
                      type="button"
                      [disabled]="busy() || !agentReasons[step.id]?.trim()"
                      (click)="retryAgent(step)"
                    >
                      Chạy lại AI
                    </button>
                  }
                </section>
              }
              @if (step.dueAt) {
                <p>Hạn xử lý: {{ step.dueAt | date: 'dd/MM/yyyy HH:mm' }}</p>
              }
              @if (step.pausedAt) {
                <p role="status">
                  SLA đang tạm dừng từ {{ step.pausedAt | date: 'dd/MM/yyyy HH:mm' }}. Tiếp tục SLA
                  trước khi quyết định.
                </p>
              }
              @for (pause of step.slaPauses || []; track pause.id) {
                <p>
                  {{ pause.pausedBy }} tạm dừng: {{ pause.reason }} ·
                  {{ pause.pausedAt | date: 'dd/MM/yyyy HH:mm' }}
                  @if (pause.resumedAt) {
                    · {{ pause.resumedBy }} tiếp tục lúc
                    {{ pause.resumedAt | date: 'dd/MM/yyyy HH:mm' }} · hạn mới
                    {{ pause.dueAtAfter | date: 'dd/MM/yyyy HH:mm' }}
                  }
                </p>
              }
              @if (step.canManageSla) {
                @if (step.pausedAt) {
                  <button type="button" [disabled]="busy()" (click)="sla(step, true)">
                    Tiếp tục SLA
                  </button>
                } @else {
                  <label [for]="'sla-reason-' + step.id">Lý do chờ / tạm dừng SLA</label>
                  <input
                    [id]="'sla-reason-' + step.id"
                    [(ngModel)]="slaReasons[step.id]"
                    maxlength="1000"
                  />
                  <button
                    type="button"
                    [disabled]="busy() || !slaReasons[step.id]?.trim()"
                    (click)="sla(step, false)"
                  >
                    Tạm dừng SLA
                  </button>
                }
              }
              @if (step.policy === 'ALL') {
                <p>Cần tất cả người được chỉ định chấp thuận.</p>
              }
              @for (decision of step.decisions; track decision.actorName + decision.decidedAt) {
                <p>
                  {{ decision.actorName }}
                  @if (decision.principalId && decision.principalId !== decision.actorId) {
                    (thay {{ decision.principalName }})
                  }
                  : {{ label(decision.outcome) }} · {{ decision.note }}
                </p>
              }
              @for (grant of step.delegations || []; track grant.id) {
                <p>
                  {{ grant.principalName }} → {{ grant.delegateName }} ·
                  {{ grant.active ? 'Đang hiệu lực' : grant.revokedAt ? 'Đã thu hồi' : 'Hết hạn' }}
                  · đến {{ grant.expiresAt | date: 'dd/MM/yyyy HH:mm' }} · {{ grant.reason }} · bởi
                  {{ grant.createdBy }}
                </p>
                @if (
                  run.canDelegate &&
                  run.status === 'RUNNING' &&
                  step.status === 'PENDING' &&
                  !grant.revokedAt
                ) {
                  <button type="button" [disabled]="busy()" (click)="revoke(grant.id)">
                    Thu hồi ủy quyền
                  </button>
                }
              }
              @if (
                run.canDelegate &&
                run.status === 'RUNNING' &&
                step.status === 'PENDING' &&
                (step.nodeType === 'APPROVAL' || step.nodeType === 'DEPARTMENT')
              ) {
                <bf-workflow-delegation
                  [requestId]="requestId"
                  [stepId]="step.id"
                  [revision]="run.revision"
                  [candidates]="step.candidateDetails || []"
                  (updated)="load()"
                />
              }
              @if (step.canAct) {
                <label [for]="'note-' + step.id">Kết quả xử lý / lý do</label>
                <textarea
                  [id]="'note-' + step.id"
                  [(ngModel)]="notes[step.id]"
                  maxlength="4000"
                  rows="3"
                ></textarea>
                @if (step.nodeType === 'DEPARTMENT') {
                  <button type="button" [disabled]="busy()" (click)="decide(step, 'COMPLETE')">
                    Hoàn tất bước
                  </button>
                } @else {
                  <button type="button" [disabled]="busy()" (click)="decide(step, 'APPROVE')">
                    Chấp thuận
                  </button>
                  <button type="button" [disabled]="busy()" (click)="decide(step, 'REJECT')">
                    Từ chối
                  </button>
                }
              }
            </li>
          }
        </ol>
        <button type="button" [disabled]="busy()" (click)="load()">Làm mới quy trình</button>
      </section>
    }
  `,
  styles: [
    `
      .runtime-panel {
        padding: 1.25rem;
        border: 1px solid #cbd5e1;
        border-radius: 12px;
        margin: 1rem 0;
      }
      li {
        padding: 0.5rem 0;
      }
      .agent-result {
        padding: 1rem;
        margin: 0.75rem 0;
        background: #eef2ff;
        border: 1px solid #c7d2fe;
        border-radius: 0.5rem;
        overflow-wrap: anywhere;
      }
      .agent-deliverable {
        white-space: pre-wrap;
      }
      textarea {
        display: block;
        width: 100%;
        max-width: 650px;
        margin: 0.5rem 0;
      }
      button {
        margin: 0.25rem;
        padding: 0.5rem 0.75rem;
      }
    `,
  ],
})
export class WorkflowRuntime implements OnChanges {
  @Input({ required: true }) requestId = '';
  @Output() changed = new EventEmitter<void>();
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly runtime = signal<Runtime | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  notes: Record<string, string> = {};
  slaReasons: Record<string, string> = {};
  agentReasons: Record<string, string> = {};
  readonly agentHistory = signal<Record<string, AgentAttempt[]>>({});
  agentSourceLabel(source: string) {
    if (source.startsWith('fields.')) {
      const key = source.slice(7);
      return this.runtime()?.formFields.find(field => field.key === key)?.label || key;
    }
    if (source.startsWith('previous.')) return 'Tóm tắt bước AI trước đó';
    return ({title:'Tiêu đề yêu cầu',description:'Mô tả yêu cầu',step:'Bước đang xử lý'} as Record<string,string>)[source] || source;
  }
  agentToolLabel(tool: string) {
    return (
      (
        {
          read_inputs: 'Đọc dữ liệu được phép',
          validate_inputs: 'Kiểm tra đầu vào',
          read_previous_results: 'Đọc kết quả bước trước',
          prepare_deliverable: 'Lập kết quả bàn giao',
        } as Record<string, string>
      )[tool] || tool
    );
  }
  loadAgentHistory(step: Step) {
    if (this.busy()) return;
    this.busy.set(true);
    this.http
      .get<AgentAttempt[]>(
        `/api/v1/requests/${this.requestId}/workflow/steps/${step.id}/agent/history`,
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (attempts) => {
          this.agentHistory.update((history) => ({ ...history, [step.id]: attempts }));
          this.busy.set(false);
        },
        error: () => {
          this.busy.set(false);
          this.error.set('Không tải được lịch sử chạy AI.');
        },
      });
  }
  agentLabel(status: string) {
    return (
      (
        {
          QUEUED: 'Chờ chạy',
          RUNNING: 'Đang xử lý',
          RETRY: 'Chờ thử lại',
          COMPLETED: 'Đã hoàn tất',
          REVIEW_REQUIRED: 'Chờ người phụ trách kiểm tra',
          FAILED: 'Chưa xử lý được',
          HUMAN_COMPLETED: 'Người phụ trách đã xử lý',
          SUPERSEDED: 'Đã dừng vì bước thay đổi',
        } as Record<string, string>
      )[status] || status
    );
  }
  retryAgent(step: Step) {
    const run = this.runtime();
    if (!run || this.busy() || !this.agentReasons[step.id]?.trim()) return;
    this.busy.set(true);
    this.error.set('');
    this.http
      .post<Runtime>(`/api/v1/requests/${this.requestId}/workflow/steps/${step.id}/agent/retry`, {
        revision: run.revision,
        reason: this.agentReasons[step.id].trim(),
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => {
          this.runtime.set(value);
          this.busy.set(false);
          this.changed.emit();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            err.status === 409
              ? 'Quy trình đã thay đổi hoặc AI đã hết số lần chạy. Hãy tải lại.'
              : 'Không chạy lại được AI.',
          );
          if (err.status === 409) this.load();
        },
      });
  }
  ngOnChanges() {
    this.load();
  }
  load() {
    if (!this.requestId) return;
    this.http
      .get<Runtime | null>(`/api/v1/requests/${this.requestId}/workflow`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => this.runtime.set(value),
        error: (err) => {
          if (err.status !== 404) this.error.set('Không tải được tiến độ quy trình.');
        },
      });
  }
  decide(step: Step, outcome: string) {
    const run = this.runtime();
    if (!run || this.busy()) return;
    if (outcome === 'REJECT' && !this.notes[step.id]?.trim()) {
      this.error.set('Vui lòng nhập lý do từ chối.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.http
      .post<Runtime>(`/api/v1/requests/${this.requestId}/workflow/steps/${step.id}/decisions`, {
        revision: run.revision,
        outcome,
        note: this.notes[step.id] || null,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => {
          this.runtime.set(value);
          this.busy.set(false);
          this.changed.emit();
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(
            err.error?.message || 'Không xử lý được bước. Hãy tải lại trước khi thử tiếp.',
          );
          if (err.status === 409) this.load();
        },
      });
  }
  revoke(id: string) {
    const run = this.runtime();
    if (!run || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.http
      .post<Runtime>(`/api/v1/requests/${this.requestId}/workflow/delegations/${id}/revoke`, {
        revision: run.revision,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => {
          this.runtime.set(value);
          this.busy.set(false);
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(err.error?.message || 'Không thu hồi được ủy quyền.');
          if (err.status === 409) this.load();
        },
      });
  }
  sla(step: Step, resume: boolean) {
    const run = this.runtime();
    if (!run || this.busy() || (!resume && !this.slaReasons[step.id]?.trim())) return;
    this.busy.set(true);
    this.error.set('');
    this.http
      .post<Runtime>(
        `/api/v1/requests/${this.requestId}/workflow/steps/${step.id}/sla/${resume ? 'resume' : 'pause'}`,
        {
          revision: run.revision,
          reason: resume ? null : this.slaReasons[step.id].trim(),
        },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => {
          this.runtime.set(value);
          this.busy.set(false);
        },
        error: (err) => {
          this.busy.set(false);
          this.error.set(err.error?.message || 'Không thay đổi được SLA.');
          if (err.status === 409) this.load();
        },
      });
  }
  label(value: string) {
    return (
      (
        {
          RUNNING: 'Đang xử lý',
          COMPLETED: 'Hoàn tất',
          PENDING: 'Chờ xử lý',
          APPROVE: 'Chấp thuận',
          REJECT: 'Từ chối',
          REJECTED: 'Đã từ chối',
          COMPLETE: 'Hoàn tất',
          CANCELLED: 'Đã hủy',
          FAILED: 'Không thành công',
        } as Record<string, string>
      )[value] || value
    );
  }
}
