import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskAssignmentEditor } from './task-assignment';
import { TaskAcceptanceEditor } from './task-acceptance';
import { TaskExecutionAction } from './task-execution';
import { TaskSubmissionEditor } from './task-submission';
import { TaskConfirmationEditor } from './task-confirmation';
import { WorkItemComments } from '../collaboration/work-item-comments';
import { WorkItemAttachments } from '../collaboration/work-item-attachments';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface Evidence {
  id: string;
  title?: string;
  isCompleted?: boolean;
  content?: string;
  percent?: number;
  revisionNo?: number;
  submittedAt?: string;
}
interface EvidencePage {
  items: Evidence[];
  page: number;
  pageSize: number;
  total: number;
}
interface Detail {
  task: {
    taskId: string;
    title: string;
    status: string;
    priority: string;
    deadline: string | null;
    createdAt: string;
    requestId: string | null;
    assignedUserName: string | null;
    assignedUserId: string | null;
    assignedDepartmentName: string | null;
    assignedDepartmentId?: string | null;
    parentTaskId?: string | null;
    subtaskCount?: number;
    completedSubtaskCount?: number;
  };
  parentTaskId?: string | null;
  parentTaskTitle?: string | null;
  subtasks?: Array<any>;
  description: string | null;
  creatorName: string | null;
  checklist: EvidencePage;
  reports: EvidencePage;
  results: EvidencePage;
}
@Component({
  selector: 'bf-task-detail',
  imports: [
    DatePipe,
    MatButtonModule,
    RouterLink,
    WorkItemComments,
    WorkItemAttachments,
    TranslatePipe,
  ],
  templateUrl: './task-detail.html',
  styleUrls: ['../organization/departments.scss', './task-editor.scss'],
})
export class TaskDetail {
  canCreateSubtask(): boolean {
    const d = this.data();
    if (!d?.task) return false;
    // Only Managers and Company Admins can decompose/create subtasks for subordinates
    const isManagerOrAdmin = this.session.hasPermission('tasks.assign') && this.session.hasPermission('tasks.create');
    return isManagerOrAdmin && d.task.status !== 'CANCELLED' && d.task.status !== 'COMPLETED';
  }

  completedSubtasksCount(): number {
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return subs.filter((s: any) => s.status === 'COMPLETED' || s.status === 'CONFIRMED').length;
  }

  subtasksProgressPercent(): number {
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return Math.round((this.completedSubtasksCount() / subs.length) * 100);
  }

  readonly session = inject(SessionService);
  private readonly i18n = inject(I18nService);
  private readonly dialog = inject(MatDialog);
  private assigning = false;
  archiving = false;
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private id = '';
  private pages = { checklist: 1, reports: 1, results: 1 };
  readonly data = signal<Detail | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly sections = [
    { key: 'checklist', nameKey: 'taskDetail.checklistSection', fallback: 'Checklist', emptyKey: 'taskDetail.noChecklist', emptyFallback: 'Chưa có mục kiểm tra nào trong trang này.' },
    { key: 'reports', nameKey: 'taskDetail.progressSection', fallback: 'Progress reports', emptyKey: 'taskDetail.noReports', emptyFallback: 'Chưa có báo cáo tiến độ nào trong trang này.' },
    { key: 'results', nameKey: 'taskDetail.resultsSection', fallback: 'Results', emptyKey: 'taskDetail.noResults', emptyFallback: 'Chưa có kết quả nộp nào trong trang này.' },
  ] as const;
  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed(this.destroy))
      .subscribe((params) => {
        this.id = params.get('id') ?? '';
        this.pages = { checklist: 1, reports: 1, results: 1 };
        this.load();
      });
  }
  page(section: keyof typeof this.pages, value: number) {
    this.pages[section] = value;
    this.load();
  }
  canAccept() {
    const task = this.data()?.task;
    return (
      task?.status === 'ASSIGNED' &&
      this.session.hasPermission('tasks.accept') &&
      (!task.assignedUserId || task.assignedUserId === this.session.identity()?.userId)
    );
  }
  canStart() {
    const task = this.data()?.task;
    return (
      !!task &&
      ['ACCEPTED', 'OVERDUE'].includes(task.status) &&
      this.session.hasPermission('tasks.execute') &&
      !!task.assignedUserId &&
      task.assignedUserId === this.session.identity()?.userId
    );
  }
  start() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canStart()) return;
    this.assigning = true;
    this.dialog
      .open(TaskExecutionAction, {
        data: { taskId: task.taskId, title: task.title, resume: task.status === 'OVERDUE' },
        width: '36rem',
        maxWidth: '100vw',
        ariaLabelledBy: 'task-execution-title',
        autoFocus: 'first-heading',
        restoreFocus: true,
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => {
        this.assigning = false;
        if (this.id === task.taskId) this.load();
      });
  }
  canSubmit() {
    const task = this.data()?.task;
    return (
      !!task &&
      task.status === 'IN_PROGRESS' &&
      this.session.hasPermission('tasks.submit') &&
      !!task.assignedUserId &&
      task.assignedUserId === this.session.identity()?.userId
    );
  }
  submit() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canSubmit()) return;
    this.assigning = true;
    this.dialog
      .open(TaskSubmissionEditor, {
        data: { taskId: task.taskId, title: task.title },
        width: '36rem',
        maxWidth: '100vw',
        ariaLabelledBy: 'task-submission-title',
        autoFocus: 'first-heading',
        restoreFocus: true,
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => {
        this.assigning = false;
        if (this.id === task.taskId) this.load();
      });
  }
  canConfirm() {
    const task = this.data()?.task;
    return !!task && task.status === 'SUBMITTED' && this.session.hasPermission('tasks.confirm');
  }
  confirm() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canConfirm()) return;
    this.assigning = true;
    this.dialog
      .open(TaskConfirmationEditor, {
        data: { taskId: task.taskId, title: task.title },
        width: '36rem',
        maxWidth: '100vw',
        ariaLabelledBy: 'task-confirmation-title',
        autoFocus: 'first-heading',
        restoreFocus: true,
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => {
        this.assigning = false;
        if (this.id === task.taskId) this.load();
      });
  }
  accept() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canAccept()) return;
    this.assigning = true;
    this.dialog
      .open(TaskAcceptanceEditor, {
        data: { taskId: task.taskId, title: task.title },
        width: '36rem',
        maxWidth: '100vw',
        ariaLabelledBy: 'task-acceptance-title',
        autoFocus: 'first-heading',
        restoreFocus: true,
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => {
        this.assigning = false;
        // Escape/backdrop closure may follow an uncertain response without a dialog result.
        if (this.id === task.taskId) this.load();
      });
  }
  assign() {
    const task = this.data()?.task;
    if (
      !task ||
      this.assigning ||
      !this.session.hasPermission('tasks.assign') ||
      !['DRAFT', 'REJECTED'].includes(task.status)
    )
      return;
    this.assigning = true;
    this.dialog
      .open(TaskAssignmentEditor, {
        data: { taskId: task.taskId, title: task.title },
        width: '44rem',
        maxWidth: '100vw',
        height: '100dvh',
        position: { right: '0', top: '0' },
        ariaLabelledBy: 'task-assignment-title',
        autoFocus: 'first-heading',
        restoreFocus: true,
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe((changed) => {
        this.assigning = false;
        if (changed && this.id === task.taskId) this.load();
      });
  }

  canArchive() {
    const task = this.data()?.task;
    return (
      !!task &&
      ['COMPLETED', 'CANCELLED'].includes(task.status) &&
      this.session.hasPermission('records.archive')
    );
  }
  archive() {
    const task = this.data()?.task;
    if (!task || this.archiving || !this.canArchive()) return;
    if (
      !confirm(
        `Bạn có chắc chắn muốn lưu trữ nhiệm vụ "${task.title}"? Sau khi lưu trữ, nhiệm vụ sẽ được chuyển vào hồ sơ lịch sử.`,
      )
    ) {
      return;
    }
    this.archiving = true;
    this.http
      .post('/api/v1/records/task/' + encodeURIComponent(task.taskId) + '/archive', null)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.archiving = false;
          this.load();
        },
        error: (failure: HttpErrorResponse) => {
          this.archiving = false;
          alert(failure?.error?.message ?? this.i18n.t('taskDetail.errArchive', 'Failed to archive task.'));
        },
      });
  }
  load() {
    this.request?.unsubscribe();
    this.data.set(null);
    this.busy.set(true);
    this.error.set('');
    const params = new HttpParams()
      .set('checklistPage', this.pages.checklist)
      .set('reportPage', this.pages.reports)
      .set('resultPage', this.pages.results);
    this.request = this.http
      .get<Detail>('/api/v1/tasks/' + encodeURIComponent(this.id), { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.data.set(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(
            failure.status === 404
              ? this.i18n.t('taskDetail.errUnavailable', 'This task is unavailable or outside your access scope.')
              : this.i18n.t('taskDetail.errLoad', 'Task details could not be loaded. Please try again.'),
          );
        },
      });
  }

  readonly aiRisk = signal<any>(null);
  readonly aiSummary = signal<any>(null);
  readonly aiBusy = signal(false);
  readonly aiError = signal('');

  evaluateRisk() {
    if (this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiError.set('');
    this.http
      .post<any>('/api/v1/ai/task-risk', { taskId: this.id })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.aiBusy.set(false);
          this.aiRisk.set(res);
        },
        error: (err: HttpErrorResponse) => {
          this.aiBusy.set(false);
          this.aiError.set(err?.error?.message ?? 'Không thể đánh giá rủi ro.');
        },

      });
  }

  summarizeProgress() {
    if (this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiError.set('');
    this.http
      .post<any>('/api/v1/ai/task-summary', { taskId: this.id })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.aiBusy.set(false);
          this.aiSummary.set(res);
        },
        error: (err: HttpErrorResponse) => {
          this.aiBusy.set(false);
          this.aiError.set(err?.error?.message ?? 'Không thể tạo tóm tắt tiến độ.');
        },

      });
  }
}
