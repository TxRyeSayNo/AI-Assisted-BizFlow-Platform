import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DatePipe, NgClass } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { requestPriorities, requestStates } from './requests';
import { WorkItemComments } from '../collaboration/work-item-comments';
import { WorkItemAttachments } from '../collaboration/work-item-attachments';

export interface RequestResolutionItemView {
  resolutionId: string;
  revisionNo: number;
  content: string;
  resolvedAt: string;
  resolverUserId: string;
  resolverName: string;
}

export interface RequestConfirmationItemView {
  confirmationId: string;
  milestoneType: string;
  decision: string;
  note: string | null;
  decidedAt: string;
  actorUserId: string;
  actorName: string;
}

export interface RequestLinkedTaskItemView {
  taskId: string;
  title: string;
  status: string;
  priority: string;
  deadline: string | null;
  createdAt: string;
  assignedUserId: string | null;
  assignedUserName: string | null;
  assignedDepartmentId: string | null;
  assignedDepartmentName: string | null;
}

export interface RequestDetailView {
  requestId: string;
  title: string;
  description: string | null;
  status: string;
  priority: string;
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  requesterId: string;
  requesterName: string;
  requesterEmail?: string;
  parentRequestId: string | null;
  revisedFromRequestId: string | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
  currentRoutingDepartmentId?: string | null;
  currentRoutingDepartmentName?: string | null;
  currentRoutingUserId?: string | null;
  currentRoutingUserName?: string | null;
  routedAt?: string | null;
  receivedAt?: string | null;
  rejectionReason?: string | null;
  resolutions?: RequestResolutionItemView[];
  confirmations?: RequestConfirmationItemView[];
  tasks?: RequestLinkedTaskItemView[];
}

export interface DepartmentOption {
  departmentId: string;
  name: string;
  code: string;
}

export interface UserOption {
  userId: string;
  displayName: string;
  employeeCode: string;
  departmentId: string | null;
}

@Component({
  selector: 'bf-request-detail',
  imports: [
    RouterLink,
    DatePipe,
    NgClass,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    WorkItemComments,
    WorkItemAttachments,
  ],
  templateUrl: './request-detail.html',
  styleUrls: ['../organization/departments.scss', './requests.scss', './request-detail.scss'],
})
export class RequestDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);

  readonly session = inject(SessionService);
  readonly states = requestStates;
  readonly priorities = requestPriorities;

  readonly busy = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);
  readonly data = signal<RequestDetailView | null>(null);

  // Active action modal / panel
  readonly activeModal = signal<
    'route' | 'receive' | 'reject' | 'cancel' | 'resolve' | 'confirm' | 'revise' | 'create-task' | 'ai-split' | null
  >(null);
  readonly actionInProgress = signal(false);

  // AI helper signals
  readonly aiMultiIntent = signal<any>(null);
  readonly aiRouting = signal<any>(null);
  readonly aiBusy = signal(false);
  readonly aiMessage = signal('');

  // Route form
  readonly routeDepartmentId = signal('');
  readonly routeUserId = signal('');
  readonly routeReason = signal('');
  readonly routeSource = signal<'MANUAL' | 'AI' | 'RULE'>('MANUAL');

  // Receive form
  readonly receiveNote = signal('');

  // Reject form
  readonly rejectReason = signal('');

  // Cancel form
  readonly cancelReason = signal('');

  // Resolve form
  readonly resolveContent = signal('');

  // Confirm form
  readonly confirmDecision = signal<'CONFIRMED' | 'REWORK'>('CONFIRMED');
  readonly confirmNote = signal('');

  // Revise form
  readonly reviseTitle = signal('');
  readonly reviseDescription = signal('');
  readonly revisePriority = signal('MEDIUM');
  readonly reviseSubmitImmediately = signal(false);

  // Create task form
  readonly taskTitle = signal('');
  readonly taskDescription = signal('');
  readonly taskPriority = signal('MEDIUM');
  readonly taskDeadline = signal('');
  readonly taskChecklist = signal<string[]>([]);
  readonly newChecklistItem = signal('');

  // Lookup options
  readonly departments = signal<DepartmentOption[]>([]);
  readonly users = signal<UserOption[]>([]);

  private requestId = '';

  ngOnInit() {
    this.route.params.pipe(takeUntilDestroyed(this.destroy)).subscribe((params) => {
      this.requestId = params['id'];
      if (this.requestId) {
        this.load();
        this.loadLookups();
      }
    });
  }

  load() {
    this.busy.set(true);
    this.error.set(null);

    this.http
      .get<RequestDetailView>(`/api/v1/requests/${this.requestId}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (detail) => {
          this.busy.set(false);
          this.data.set(detail);
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          if (err.status === 404) {
            this.error.set('Request not found or you do not have permission to view it.');
          } else if (err.status === 403 || err.status === 401) {
            this.error.set('You are not authorized to view this request.');
          } else {
            this.error.set('Failed to load request details. Please try again.');
          }
        },
      });
  }

  loadLookups() {
    this.http
      .get<{ items: DepartmentOption[] }>('/api/v1/departments?pageSize=100')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => this.departments.set(res.items ?? []),
        error: () => {},
      });

    this.http
      .get<{ items: UserOption[] }>('/api/v1/users?pageSize=100')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => this.users.set(res.items ?? []),
        error: () => {},
      });
  }

  openModal(
    modal: 'route' | 'receive' | 'reject' | 'cancel' | 'resolve' | 'confirm' | 'revise' | 'create-task',
  ) {
    this.activeModal.set(modal);
    this.actionError.set(null);
    this.actionSuccess.set(null);
    if (modal === 'resolve') {
      this.resolveContent.set('');
    } else if (modal === 'confirm') {
      this.confirmDecision.set('CONFIRMED');
      this.confirmNote.set('');
    } else if (modal === 'revise') {
      const d = this.data();
      this.reviseTitle.set(d ? `Revised: ${d.title}` : '');
      this.reviseDescription.set(d?.description ?? '');
      this.revisePriority.set(d?.priority ?? 'MEDIUM');
      this.reviseSubmitImmediately.set(false);
    } else if (modal === 'create-task') {
      const d = this.data();
      this.taskTitle.set(d ? `Task: ${d.title}` : '');
      this.taskDescription.set(d?.description ?? '');
      this.taskPriority.set(d?.priority ?? 'MEDIUM');
      this.taskDeadline.set('');
      this.taskChecklist.set([]);
      this.newChecklistItem.set('');
    }
  }

  closeModal() {
    this.activeModal.set(null);
    this.actionError.set(null);
  }

  analyzeMultiIntent() {
    const d = this.data();
    if (!d || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiMessage.set('');
    this.http.post<any>('/api/v1/ai/request-multi-intent', {
      requestId: d.requestId,
      title: d.title,
      description: d.description || ''
    }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (res) => {
        this.aiBusy.set(false);
        this.aiMultiIntent.set(res);
        if (res?.isMultiIntent) {
          this.activeModal.set('ai-split');
        } else {
          this.aiMessage.set('AI xác nhận yêu cầu có phạm vi đơn lẻ, không cần phân tách.');
        }
      },
      error: () => {
        this.aiBusy.set(false);
        this.aiMessage.set('Phân tích đa mục tiêu thất bại.');
      }
    });
  }

  recommendRouting() {
    const d = this.data();
    if (!d || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiMessage.set('');
    this.http.post<any>('/api/v1/ai/request-routing', {
      requestId: d.requestId,
      title: d.title,
      description: d.description || '',
      serviceId: d.serviceId
    }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: (res) => {
        this.aiBusy.set(false);
        this.aiRouting.set(res);
        if (res?.recommendedDepartmentId) {
          this.routeDepartmentId.set(res.recommendedDepartmentId);
          this.routeReason.set(res.rationale || 'Điều phối theo gợi ý AI');
          this.routeSource.set('AI');
          this.openModal('route');
        }
      },
      error: () => {
        this.aiBusy.set(false);
        this.aiMessage.set('Gợi ý điều phối thất bại.');
      }
    });
  }

  confirmSplit() {
    const d = this.data();
    const multi = this.aiMultiIntent();
    if (!d || !multi?.intents?.length || this.actionInProgress()) return;

    const splits = multi.intents.map((item: any) => ({
      title: item.title,
      description: item.description,
      serviceId: item.suggestedServiceId || d.serviceId,
      categoryId: item.suggestedCategoryId || d.categoryId
    }));

    this.actionInProgress.set(true);
    this.actionError.set(null);
    this.http.post<any>(`/api/v1/requests/${d.requestId}/split`, { splits })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.actionInProgress.set(false);
          this.activeModal.set(null);
          this.actionSuccess.set(`Đã phân tách thành công thành ${res.splitCount} yêu cầu con.`);
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          this.actionError.set(err?.error?.message ?? 'Phân tách yêu cầu thất bại.');
        }
      });
  }

  submit() {
    if (this.actionInProgress() || !this.canSubmit()) return;

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const submitKey = crypto.randomUUID();

    this.http
      .post<{ requestId: string; status: string }>(
        `/api/v1/requests/${this.requestId}/submit`,
        {},
        { headers: { 'Idempotency-Key': submitKey } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.actionSuccess.set('Request submitted successfully.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 409) {
            this.actionError.set('Request cannot be submitted in its current state.');
          } else if (err.status === 403) {
            this.actionError.set('You do not have permission to submit this request.');
          } else {
            this.actionError.set('Failed to submit request. Please try again.');
          }
        },
      });
  }

  executeRoute() {
    if (this.actionInProgress() || !this.canRoute()) return;

    const deptId = this.routeDepartmentId().trim() || null;
    const userId = this.routeUserId().trim() || null;
    if (!deptId && !userId) {
      this.actionError.set('Please select a target department or user.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const routeKey = crypto.randomUUID();
    const payload = {
      toDepartmentId: deptId,
      toUserId: userId,
      reason: this.routeReason().trim() || null,
      source: this.routeSource(),
    };

    this.http
      .post(`/api/v1/requests/${this.requestId}/route`, payload, {
        headers: { 'Idempotency-Key': routeKey },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Request routed successfully.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to route this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be routed in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to route request.');
          }
        },
      });
  }

  executeReceive() {
    if (this.actionInProgress() || !this.canReceive()) return;

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const receiveKey = crypto.randomUUID();
    const payload = {
      note: this.receiveNote().trim() || null,
    };

    this.http
      .post(`/api/v1/requests/${this.requestId}/receive`, payload, {
        headers: { 'Idempotency-Key': receiveKey },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Request received and marked for processing.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You are not authorized to receive this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be received in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to receive request.');
          }
        },
      });
  }

  executeStartWork() {
    if (this.actionInProgress() || !this.canStart()) return;

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const startKey = crypto.randomUUID();

    this.http
      .post(
        `/api/v1/requests/${this.requestId}/start`,
        {},
        { headers: { 'Idempotency-Key': startKey } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.actionSuccess.set('Work execution started (In Progress).');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to process this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot transition to In Progress in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to start processing request.');
          }
        },
      });
  }

  executeReject() {
    if (this.actionInProgress() || !this.canReject()) return;

    const reason = this.rejectReason().trim();
    if (!reason) {
      this.actionError.set('A plain-text rejection reason is required.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const rejectKey = crypto.randomUUID();

    this.http
      .post(
        `/api/v1/requests/${this.requestId}/reject`,
        { reason },
        { headers: { 'Idempotency-Key': rejectKey } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Request rejected.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to reject this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be rejected in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to reject request.');
          }
        },
      });
  }

  executeCancel() {
    if (this.actionInProgress() || !this.canCancel()) return;

    this.actionInProgress.set(true);
    this.actionError.set(null);

    const cancelKey = crypto.randomUUID();

    this.http
      .post(
        `/api/v1/requests/${this.requestId}/cancel`,
        {},
        { headers: { 'Idempotency-Key': cancelKey } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Request cancelled.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to cancel this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be cancelled in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to cancel request.');
          }
        },
      });
  }

  executeResolve() {
    if (this.actionInProgress() || !this.canResolve()) return;
    const content = this.resolveContent().trim();
    if (!content) {
      this.actionError.set('Resolution details are required.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    const resolveKey = crypto.randomUUID();

    this.http
      .post(`/api/v1/requests/${this.requestId}/resolve`, { content }, {
        headers: { 'Idempotency-Key': resolveKey },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Request resolved successfully.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to resolve this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be resolved in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to resolve request.');
          }
        },
      });
  }

  executeConfirm() {
    if (this.actionInProgress() || !this.canConfirm()) return;
    const decision = this.confirmDecision();
    const note = this.confirmNote().trim();
    if (decision === 'REWORK' && !note) {
      this.actionError.set('A note explaining why rework is needed is required.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    const confirmKey = crypto.randomUUID();

    this.http
      .post(`/api/v1/requests/${this.requestId}/confirm`, { decision, note: note || null }, {
        headers: { 'Idempotency-Key': confirmKey },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set(
            decision === 'CONFIRMED'
              ? 'Resolution confirmed and request closed.'
              : 'Rework requested and sent back to in-progress.',
          );
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to confirm or request rework for this request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be confirmed in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to submit confirmation.');
          }
        },
      });
  }

  executeRevise() {
    if (this.actionInProgress() || !this.canRevise()) return;
    const title = this.reviseTitle().trim();
    if (!title) {
      this.actionError.set('Title is required for the revised request.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    const reviseKey = crypto.randomUUID();

    this.http
      .post<{ requestId: string }>(
        `/api/v1/requests/${this.requestId}/revise`,
        {
          title,
          description: this.reviseDescription().trim() || null,
          priority: this.revisePriority(),
          submitImmediately: this.reviseSubmitImmediately(),
        },
        { headers: { 'Idempotency-Key': reviseKey } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Revised request created successfully.');
          this.router.navigate(['/requests', res.requestId]);
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to create a revised request.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be revised in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to create revised request.');
          }
        },
      });
  }

  addChecklistItem() {
    const item = this.newChecklistItem().trim();
    if (!item) return;
    this.taskChecklist.update((list) => [...list, item]);
    this.newChecklistItem.set('');
  }

  removeChecklistItem(index: number) {
    this.taskChecklist.update((list) => list.filter((_, i) => i !== index));
  }

  executeCreateTask() {
    if (this.actionInProgress() || !this.canCreateTask()) return;
    const title = this.taskTitle().trim();
    if (!title) {
      this.actionError.set('Title is required for the task.');
      return;
    }

    const deadlineStr = this.taskDeadline();
    let deadlineIso: string | null = null;
    if (deadlineStr) {
      const dl = new Date(deadlineStr);
      if (!Number.isFinite(dl.getTime()) || dl.getTime() <= Date.now()) {
        this.actionError.set('Choose a future deadline in your local time.');
        return;
      }
      deadlineIso = dl.toISOString();
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    const key = crypto.randomUUID();

    this.http
      .post<{ taskId: string }>(
        `/api/v1/requests/${this.requestId}/tasks`,
        {
          title,
          description: this.taskDescription().trim() || null,
          priority: this.taskPriority(),
          deadline: deadlineIso,
          checklist: this.taskChecklist().length > 0 ? this.taskChecklist() : null,
        },
        { headers: { 'Idempotency-Key': key } },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.closeModal();
          this.actionSuccess.set('Task created successfully and linked to this request.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to create tasks.');
          } else if (err.status === 409) {
            this.actionError.set('Cannot link task: request is not in an active state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to create task.');
          }
        },
      });
  }

  canCreateTask(): boolean {
    const d = this.data();
    if (!d) return false;
    if (d.status === 'CANCELLED' || d.status === 'CLOSED' || d.status === 'REJECTED') return false;
    return this.session.hasPermission('tasks.create');
  }

  canSubmit(): boolean {
    const d = this.data();
    if (!d || d.status !== 'DRAFT') return false;
    return this.session.hasPermission('requests.submit');
  }

  canRoute(): boolean {
    const d = this.data();
    if (!d || (d.status !== 'SUBMITTED' && d.status !== 'ROUTED')) return false;
    return this.session.hasPermission('requests.route');
  }

  canReceive(): boolean {
    const d = this.data();
    if (!d || d.status !== 'ROUTED') return false;
    return this.session.hasPermission('requests.receive');
  }

  canStart(): boolean {
    const d = this.data();
    if (!d || d.status !== 'RECEIVED') return false;
    return this.session.hasPermission('requests.process');
  }

  canResolve(): boolean {
    const d = this.data();
    if (!d || d.status !== 'IN_PROGRESS') return false;
    return this.session.hasPermission('requests.resolve');
  }

  canConfirm(): boolean {
    const d = this.data();
    if (!d || d.status !== 'RESOLVED') return false;
    const isRequester =
      typeof this.session.identity === 'function' &&
      this.session.identity()?.userId === d.requesterId;
    return this.session.hasPermission('requests.confirm') || isRequester;
  }

  canRevise(): boolean {
    const d = this.data();
    if (!d || d.status !== 'REJECTED') return false;
    return this.session.hasPermission('requests.create');
  }

  canReject(): boolean {
    const d = this.data();
    if (!d || (d.status !== 'SUBMITTED' && d.status !== 'ROUTED')) return false;
    return this.session.hasPermission('requests.reject');
  }

  canCancel(): boolean {
    const d = this.data();
    if (!d || (d.status !== 'DRAFT' && d.status !== 'SUBMITTED' && d.status !== 'ROUTED')) return false;
    return this.session.hasPermission('requests.cancel');
  }

  canArchive(): boolean {
    const d = this.data();
    if (!d) return false;
    if (d.status !== 'CLOSED' && d.status !== 'CANCELLED') return false;
    return this.session.hasPermission('records.archive');
  }

  executeArchive(): void {
    if (this.actionInProgress() || !this.canArchive()) return;
    const d = this.data();
    if (!confirm(`Are you sure you want to archive request "${d?.title}"? Once archived, it will be moved to historical records.`)) {
      return;
    }
    this.actionInProgress.set(true);
    this.actionError.set(null);
    this.http
      .post(`/api/v1/records/request/${this.requestId}/archive`, null)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.actionSuccess.set('Request archived successfully.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.actionInProgress.set(false);
          if (err.status === 403) {
            this.actionError.set('You do not have permission to archive records.');
          } else if (err.status === 409) {
            this.actionError.set('Request cannot be archived in its current state.');
          } else {
            this.actionError.set(err.error?.message || 'Failed to archive request.');
          }
        },
      });
  }

  statusClass(status: string): string {
    return 'status-' + status.toLowerCase().replace(/_/g, '-');
  }

  priorityClass(priority: string): string {
    return 'priority-' + priority.toLowerCase();
  }

  getStateName(status: string): string {
    return this.states[status as keyof typeof requestStates] ?? status;
  }

  getPriorityName(priority: string): string {
    return this.priorities[priority as keyof typeof requestPriorities] ?? priority;
  }
}
