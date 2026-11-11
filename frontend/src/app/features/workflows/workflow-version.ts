import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe, CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import type { WorkflowRow } from './workflows';
import { WorkflowDesigner, LookupItem, UserLookupItem } from './workflow-designer';

export interface WorkflowVersionView {
  workflowId: string;
  name: string;
  businessType: 'TASK' | 'REQUEST';
  versionId: string;
  versionNo: number;
  status: 'DRAFT' | 'PUBLISHED' | 'RETIRED';
  publishedAt: string | null;
  definition: Record<string, unknown>;
  steps: {
    stepId: string;
    stepCode: string;
    name: string;
    type: string;
    orderNo: number;
    config: Record<string, unknown>;
  }[];
  transitions: {
    transitionId: string;
    fromState: string;
    toState: string;
    guard: Record<string, unknown> | null;
  }[];
}

export interface ServiceLookupItem {
  id: string;
  name: string;
  code: string;
  activeWorkflowVersionId: string | null;
}

export interface VersionSummaryItem {
  versionId: string;
  versionNo: number;
  status: string;
  publishedAt: string | null;
}

export interface WorkflowDraftGraphDto {
  workflowName: string;
  businessType: 'REQUEST' | 'TASK';
  summaryExplanation: string;
  nodes: {
    id: string;
    type: 'START' | 'DEPARTMENT' | 'APPROVAL' | 'DECISION' | 'CONFIRMATION' | 'END';
    name: string;
    x: number;
    y: number;
    departmentId?: string;
    departmentName?: string;
    roleId?: string;
    roleName?: string;
    slaHours?: number;
    description?: string;
    trigger?: string;
    completionState?: string;
  }[];
  edges: {
    id: string;
    source: string;
    target: string;
    label?: string;
  }[];
}

@Component({
  selector: 'bf-workflow-version',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    DatePipe,
    FormsModule,
    WorkflowDesigner,
  ],
  templateUrl: './workflow-version.html',
  styleUrls: ['../organization/departments.scss', './workflows.scss'],
})
export class WorkflowVersion {
  @ViewChild(WorkflowDesigner) designer?: WorkflowDesigner;

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);

  private request?: Subscription;
  private creation?: Subscription;

  readonly session = inject(SessionService);
  readonly version = signal<WorkflowVersionView | null>(null);
  readonly busy = signal(false);
  readonly failure = signal('');
  readonly retryable = signal(false);

  // Lookups for designer
  readonly departments = signal<LookupItem[]>([]);
  readonly roles = signal<LookupItem[]>([]);
  readonly users = signal<UserLookupItem[]>([]);
  readonly services = signal<ServiceLookupItem[]>([]);
  readonly versionHistory = signal<VersionSummaryItem[]>([]);

  // Actions state
  readonly savingDraft = signal(false);
  readonly validating = signal(false);
  readonly publishing = signal(false);
  readonly creating = signal(false);
  readonly created = signal<WorkflowRow | null>(null);
  readonly createFailure = signal('');

  // Modals & Feedback
  readonly showHistoryModal = signal(false);
  readonly showPublishModal = signal(false);
  readonly selectedAssociateServiceId = signal<string>('');
  readonly toastMessage = signal<string>('');
  readonly toastType = signal<'success' | 'error' | 'info'>('info');

  // Editable workflow name for draft
  readonly workflowNameEdit = signal<string>('');

  // AI Workflow Document Analysis
  readonly showAiModal = signal(false);
  readonly aiDocText = signal('');
  readonly aiDocFile = signal<File | null>(null);
  readonly aiAnalyzing = signal(false);
  readonly aiAnalysisResult = signal<WorkflowDraftGraphDto | null>(null);

  openAiModal() {
    this.showAiModal.set(true);
    this.aiAnalysisResult.set(null);
    this.aiDocFile.set(null);
    this.aiDocText.set('');
  }

  closeAiModal() {
    this.showAiModal.set(false);
    this.aiAnalysisResult.set(null);
  }

  onAiFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    const file = input.files[0];
    this.aiDocFile.set(file);

    if (file.name.endsWith('.docx')) {
      this.aiDocText.set(`[Tệp tin Word: ${file.name} (${(file.size / 1024).toFixed(1)} KB)]\nNhấn "Phân tích tài liệu" để hệ thống trích xuất và thiết kế quy trình.`);
    } else {
      const reader = new FileReader();
      reader.onload = (e) => {
        this.aiDocText.set((e.target?.result as string) || '');
      };
      reader.readAsText(file);
    }
  }

  runAiDocumentAnalysis() {
    const v = this.version();
    const bType = v?.businessType || 'REQUEST';
    const file = this.aiDocFile();

    this.aiAnalyzing.set(true);

    if (file && file.name.endsWith('.docx')) {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('businessType', bType);

      this.http.post<WorkflowDraftGraphDto>('/api/v1/workflows/analyze-document', formData).subscribe({
        next: (res) => {
          this.aiAnalyzing.set(false);
          this.aiAnalysisResult.set(res);
          this.showToast('Phân tích tài liệu thành công! Hãy xem trước sơ đồ trước khi nạp vào Canvas.', 'success');
        },
        error: (err) => {
          this.aiAnalyzing.set(false);
          this.showToast(err?.error?.message || 'Không thể phân tích tệp tin Word.', 'error');
        }
      });
      return;
    }

    const text = this.aiDocText().trim();
    if (!text) {
      this.aiAnalyzing.set(false);
      this.showToast('Vui lòng nhập hoặc chọn tài liệu mô tả quy trình.', 'error');
      return;
    }

    this.http.post<WorkflowDraftGraphDto>('/api/v1/workflows/analyze-text', {
      textContent: text,
      businessType: bType
    }).subscribe({
      next: (res) => {
        this.aiAnalyzing.set(false);
        this.aiAnalysisResult.set(res);
        this.showToast('Phân tích tài liệu thành công! Hãy xem trước sơ đồ trước khi nạp vào Canvas.', 'success');
      },
      error: (err) => {
        this.aiAnalyzing.set(false);
        this.showToast(err?.error?.message || 'Không thể phân tích quy trình từ văn bản.', 'error');
      }
    });
  }

  applyAiWorkflowToCanvas() {
    const res = this.aiAnalysisResult();
    if (!res || !this.designer) return;

    this.designer.nodes.set(res.nodes as any);
    this.designer.edges.set(res.edges as any);
    this.closeAiModal();
    this.showToast('Đã nạp sơ đồ quy trình AI vào Canvas! Bạn có thể chỉnh sửa inline và bấm "Lưu bản nháp".', 'success');
  }

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => {
      this.load();
      this.loadLookups();
    });
  }

  showToast(message: string, type: 'success' | 'error' | 'info' = 'info') {
    this.toastMessage.set(message);
    this.toastType.set(type);
    setTimeout(() => {
      if (this.toastMessage() === message) {
        this.toastMessage.set('');
      }
    }, 4500);
  }

  load() {
    this.request?.unsubscribe();
    this.creation?.unsubscribe();
    this.creating.set(false);
    this.created.set(null);
    this.createFailure.set('');
    this.version.set(null);
    this.busy.set(true);
    this.failure.set('');
    this.retryable.set(false);

    const workflowId = this.route.snapshot.paramMap.get('id');
    const versionId = this.route.snapshot.paramMap.get('v');
    if (!workflowId || !versionId) {
      this.busy.set(false);
      this.failure.set('Không tìm thấy phiên bản quy trình này.');
      return;
    }

    this.request = this.http
      .get<WorkflowVersionView>(`/api/v1/workflow-versions/${encodeURIComponent(versionId)}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (version) => {
          this.busy.set(false);
          if (version.workflowId !== workflowId) {
            this.failure.set('Không tìm thấy phiên bản quy trình này.');
            return;
          }
          this.version.set(version);
          this.workflowNameEdit.set(version.name);
          this.loadHistory(workflowId);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.retryable.set(failure.status !== 403 && failure.status !== 404);
          this.failure.set(
            failure.status === 403
              ? 'Bạn không có quyền truy cập hoặc cấu hình quy trình này (yêu cầu quyền Quản trị viên Công ty).'
              : failure.status === 404
                ? 'Không tìm thấy phiên bản quy trình này.'
                : 'Không thể tải phiên bản quy trình. Vui lòng thử lại sau.'
          );
        },
      });
  }

  loadLookups() {
    this.http
      .get<LookupItem[]>('/api/v1/workflows/lookup/departments')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (depts) => this.departments.set(depts),
        error: () => {},
      });

    this.http
      .get<LookupItem[]>('/api/v1/workflows/lookup/roles')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (roles) => this.roles.set(roles),
        error: () => {},
      });

    this.http
      .get<ServiceLookupItem[]>('/api/v1/workflows/lookup/services')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (svcs) => this.services.set(svcs),
        error: () => {},
      });

    this.http
      .get<UserLookupItem[]>('/api/v1/workflows/lookup/users')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (users) => this.users.set(users),
        error: () => {},
      });
  }

  loadHistory(workflowId: string) {
    this.http
      .get<VersionSummaryItem[]>(`/api/v1/workflows/${workflowId}/versions`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (history) => this.versionHistory.set(history),
        error: () => {},
      });
  }

  onSaveDraft(callback?: () => void) {
    const v = this.version();
    if (!v || v.status !== 'DRAFT' || this.savingDraft() || !this.designer) {
      callback?.();
      return;
    }

    this.savingDraft.set(true);
    const definitionJson = this.designer.getDefinitionJson();

    // Map visual nodes & edges to persistence steps & transitions
    const nodes = this.designer.nodes();
    const edges = this.designer.edges();

    const steps = nodes
      .filter((n) => n.type === 'DEPARTMENT' || n.type === 'APPROVAL' || n.type === 'CONFIRMATION')
      .map((n, idx) => {
        const stepType =
          n.type === 'DEPARTMENT'
            ? 'Action'
            : n.type === 'APPROVAL'
              ? 'Approval'
              : 'Confirmation';

        const stepCode = `STEP_${n.id.replace(/[^a-zA-Z0-9]/g, '_').toUpperCase()}`;
        return {
          stepCode,
          name: n.name,
          type: stepType,
          orderNo: idx + 1,
          configJson: JSON.stringify({
            nodeId: n.id,
            departmentId: n.departmentId,
            roleId: n.roleId,
            slaHours: n.slaHours,
            assignmentMethod: n.assignmentMethod,
            specificUserId: n.specificUserId,
            specificUserName: n.specificUserName,
            approverType: n.approverType,
            approvalCountPolicy: n.approvalCountPolicy,
            rejectionReasonRequired: n.rejectionReasonRequired,
            requireNotes: n.requireNotes,
            requireAttachment: n.requireAttachment,
            requireDeliverable: n.requireDeliverable,
            milestone: n.milestone,
            description: n.description,
          }),
        };
      });

    const transitions = edges.map((e) => ({
      fromState: e.source,
      toState: e.target,
      guardJson: JSON.stringify({
        label: e.label || '',
        outcomeCode: e.outcomeCode || 'DEFAULT',
        conditionId: e.conditionId,
        isDefault: e.isDefault,
      }),
    }));

    const newName = this.workflowNameEdit().trim() || undefined;

    this.http
      .put<WorkflowVersionView>(`/api/v1/workflow-versions/${v.versionId}`, {
        definitionJson,
        steps,
        transitions,
        name: newName,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (updated) => {
          this.savingDraft.set(false);
          this.version.set(updated);
          if (updated.name) {
            this.workflowNameEdit.set(updated.name);
          }
          this.showToast('Đã lưu bản nháp quy trình thành công!', 'success');
          callback?.();
        },
        error: (err: HttpErrorResponse) => {
          this.savingDraft.set(false);
          this.showToast(
            err.error?.message || 'Không thể lưu bản nháp quy trình. Vui lòng kiểm tra lại.',
            'error'
          );
          callback?.();
        },
      });
  }

  saveWorkflowName() {
    const v = this.version();
    const newName = this.workflowNameEdit().trim();
    if (!v || v.status !== 'DRAFT' || !newName || newName === v.name) return;

    this.http
      .patch<WorkflowRow>(`/api/v1/workflows/${v.workflowId}/name`, { name: newName })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (updated) => {
          this.version.update((curr) => (curr ? { ...curr, name: updated.name } : null));
          this.workflowNameEdit.set(updated.name);
          this.showToast('Đã đổi tên quy trình thành công!', 'success');
        },
        error: () => {
          // If PATCH name endpoint is not configured, draft save will update it
          this.onSaveDraft();
        },
      });
  }

  onValidate(callback?: () => void) {
    const v = this.version();
    if (!v || this.validating()) return;

    const executeValidation = () => {
      this.validating.set(true);
      this.http
        .post<{ isValid: boolean; errors: string[]; warnings: string[] }>(
          `/api/v1/workflow-versions/${v.versionId}/validate`,
          {}
        )
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.validating.set(false);
            const issues: { type: 'error' | 'warning'; message: string; nodeId?: string }[] = [];
            res.errors.forEach((e) => issues.push({ type: 'error', message: e }));
            res.warnings.forEach((w) => issues.push({ type: 'warning', message: w }));

            // Rich guidance will display at the bottom of the screen
            this.designer?.setValidationResult(issues);
            if (res.isValid) {
              this.showToast('Sơ đồ quy trình hoàn toàn hợp lệ!', 'success');
            }
            callback?.();
          },
          error: () => {
            this.validating.set(false);
            // In case of error, show in bottom drawer
            this.designer?.setValidationResult([
              {
                type: 'error',
                message: 'Không thể kết nối đến máy chủ để kiểm tra quy trình. Vui lòng thử lại.',
              },
            ]);
            callback?.();
          },
        });
    };

    if (v.status === 'DRAFT') {
      this.onSaveDraft(executeValidation);
    } else {
      executeValidation();
    }
  }

  openPublishModal() {
    const v = this.version();
    if (!v || v.status !== 'DRAFT') return;

    // Trigger validation first so issues appear at bottom
    this.onValidate(() => {
      this.showPublishModal.set(true);
    });
  }

  confirmPublish() {
    const v = this.version();
    if (!v || this.publishing()) return;

    this.publishing.set(true);
    const serviceId = this.selectedAssociateServiceId() || null;

    this.http
      .post<WorkflowVersionView>(`/api/v1/workflow-versions/${v.versionId}/publish`, {
        serviceId,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (published) => {
          this.publishing.set(false);
          this.showPublishModal.set(false);
          this.version.set(published);
          this.showToast(
            `Đã xuất bản thành công phiên bản v${published.versionNo}! Quy trình hiện đang áp dụng chính thức.`,
            'success'
          );
          this.loadHistory(v.workflowId);
        },
        error: (err: HttpErrorResponse) => {
          this.publishing.set(false);
          this.showPublishModal.set(false);
          // Do NOT show toast error on the right!
          // Trigger validation so bottom drawer opens with exact cause and suggestion
          this.onValidate();
        },
      });
  }

  createNextVersion() {
    const current = this.version();
    if (
      !current ||
      this.creating() ||
      this.created() ||
      !this.session.hasPermission('workflows.configure')
    )
      return;

    this.creating.set(true);
    this.createFailure.set('');
    this.creation = this.http
      .post<WorkflowRow>(`/api/v1/workflows/${current.workflowId}/versions`, {})
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (row) => {
          this.creating.set(false);
          this.created.set(row);
          this.showToast(`Đã tạo bản nháp phiên bản mới v${row.latestVersionNo}!`, 'success');
          if (row.latestVersionId) {
            this.router.navigate(['/settings/workflows', row.workflowId, 'versions', row.latestVersionId]);
          }
        },
        error: (failure: HttpErrorResponse) => {
          this.creating.set(false);
          this.createFailure.set(
            failure.status === 403
              ? 'Bạn không có quyền khởi tạo phiên bản quy trình mới.'
              : failure.status === 404
                ? 'Không tìm thấy quy trình tương ứng.'
                : 'Không thể tạo phiên bản mới. Vui lòng thử lại sau.'
          );
        },
      });
  }
}
