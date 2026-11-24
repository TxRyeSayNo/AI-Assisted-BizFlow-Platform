import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';
import { TaskAssignmentEditor } from './task-assignment';
import { TaskAcceptanceEditor } from './task-acceptance';
import { TaskExecutionAction } from './task-execution';
import { TaskSubmissionEditor } from './task-submission';
import { TaskConfirmationEditor } from './task-confirmation';
import { WorkItemComments } from '../collaboration/work-item-comments';
import { WorkItemAttachments } from '../collaboration/work-item-attachments';
import { WorkItemWatch } from '../collaboration/work-item-watch';
import { TaskReminders } from './task-reminders';
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
export interface WorkPackageContribution {
  taskId: string;
  title: string;
  description?: string | null;
  status: string;
  priority: string;
  deadline: string | null;
  assignedUserId: string | null;
  assignedUserName: string | null;
  assignedEmployeeCode?: string | null;
  assignedDepartmentId?: string | null;
  assignedDepartmentName: string | null;
  individualProgressPercent: number;
  weightPercent: number;
  contributedPercent: number;
  isCompleted: boolean;
  isOverdue: boolean;
  latestReportPercent?: number | null;
  latestReportContent?: string | null;
  latestReportAt?: string | null;
  latestResultRevisionNo?: number | null;
  latestResultContent?: string | null;
  latestResultAt?: string | null;
  updatedAt?: string | null;
  taskKind?: string | null;
  childContributions?: WorkPackageContribution[] | null;
}
export interface TaskProgressOverview {
  overallCompletionPercent: number;
  participatingEmployeesCount: number;
  totalWorkPackagesCount: number;
  completedWorkPackagesCount: number;
  inProgressWorkPackagesCount: number;
  unfinishedWorkPackagesCount: number;
  overdueWorkPackagesCount: number;
  pendingReviewWorkPackagesCount: number;
  responsibleDepartmentName?: string | null;
  accountableManagerName?: string | null;
  contributions: WorkPackageContribution[];
  taskKind?: string | null;
  participatingDepartmentsCount?: number;
  accountableManagersCount?: number;
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
    progressPercent?: number;
    accountableManagerName?: string | null;
    taskKind?: string | null;
    weight?: number | null;
  };
  parentTaskId?: string | null;
  parentTaskTitle?: string | null;
  subtasks?: Array<any>;
  progressOverview?: TaskProgressOverview | null;
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
    FormsModule,
    MatButtonModule,
    RouterLink,
    WorkItemComments,
    WorkItemAttachments,
    WorkItemWatch,
    TaskReminders,
    TranslatePipe,
  ],
  templateUrl: './task-detail.html',
  styleUrls: ['../organization/departments.scss', './task-editor.scss'],
})
export class TaskDetail {
  readonly session = inject(SessionService);
  private readonly i18n = inject(I18nService);
  private readonly toast = inject(ToastService);
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

  // Employee & Manager progress reporting state (Requirement 5.G & Project Actions)
  readonly showProgressForm = signal(false);
  readonly reportingProgress = signal(false);
  readonly progressTargetTaskId = signal<string | null>(null);
  readonly progressTargetTaskTitle = signal<string | null>(null);
  progressPercentInput = 50;
  progressContentInput = '';

  readonly sections = [
    {
      key: 'checklist',
      nameKey: 'taskDetail.checklistSection',
      fallback: 'Checklist',
      emptyKey: 'taskDetail.noChecklist',
      emptyFallback: 'Chưa có mục kiểm tra nào trong trang này.',
    },
    {
      key: 'reports',
      nameKey: 'taskDetail.progressSection',
      fallback: 'Progress reports',
      emptyKey: 'taskDetail.noReports',
      emptyFallback: 'Chưa có báo cáo tiến độ nào trong trang này.',
    },
    {
      key: 'results',
      nameKey: 'taskDetail.resultsSection',
      fallback: 'Results',
      emptyKey: 'taskDetail.noResults',
      emptyFallback: 'Chưa có kết quả nộp nào trong trang này.',
    },
  ] as const;

  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed(this.destroy))
      .subscribe((params) => {
        this.id = params.get('id') ?? '';
        this.pages = { checklist: 1, reports: 1, results: 1 };
        this.showProgressForm.set(false);
        this.load();
      });
  }

  isCompanyAdmin(): boolean {
    return this.session.hasPermission('roles.configure');
  }

  isManager(): boolean {
    return !this.isCompanyAdmin() && this.session.hasPermission('tasks.assign');
  }

  isEmployeeOnly(): boolean {
    return !this.isCompanyAdmin() && !this.session.hasPermission('tasks.assign');
  }

  canCreateSubtask(): boolean {
    const d = this.data();
    if (!d?.task) return false;
    const isManagerOrAdmin =
      this.session.hasPermission('tasks.assign') && this.session.hasPermission('tasks.create');
    return isManagerOrAdmin && d.task.status !== 'CANCELLED' && d.task.status !== 'COMPLETED';
  }

  canReportProgress(): boolean {
    if (this.isCompanyAdmin()) return false;
    const task = this.data()?.task;
    const currentUserId = this.session.identity()?.userId;
    if (!task || !this.session.hasPermission('tasks.execute')) return false;
    const isSelfAssigned = !task.assignedUserId || task.assignedUserId === currentUserId;
    return ['ACCEPTED', 'IN_PROGRESS', 'OVERDUE'].includes(task.status) && isSelfAssigned;
  }

  openProgressModalFor(taskId: string, title: string, currentPercent?: number): void {
    this.progressTargetTaskId.set(taskId);
    this.progressTargetTaskTitle.set(title);
    this.progressPercentInput = typeof currentPercent === 'number' ? Math.min(99, currentPercent + 15) : 50;
    this.progressContentInput = '';
    this.showProgressForm.set(true);
  }

  toggleProgressForm(): void {
    const next = !this.showProgressForm();
    this.showProgressForm.set(next);
    if (next) {
      this.progressTargetTaskId.set(null);
      this.progressTargetTaskTitle.set(null);
      const latest = this.data()?.reports?.items?.[0]?.percent;
      this.progressPercentInput = typeof latest === 'number' ? Math.min(99, latest + 15) : 50;
      this.progressContentInput = '';
    }
  }

  submitProgressReport(): void {
    const targetTaskId = this.progressTargetTaskId() || this.data()?.task?.taskId;
    if (!targetTaskId || this.reportingProgress()) return;
    const pct = Math.round(Number(this.progressPercentInput));
    if (!Number.isFinite(pct) || pct < 0 || pct > 99) {
      this.toast.error('Tỷ lệ tiến độ định kỳ phải từ 0% đến 99%. Để hoàn tất 100%, vui lòng nhấn Nộp kết quả.');
      return;
    }
    const content = this.progressContentInput.trim();
    if (!content) {
      this.toast.error('Vui lòng nhập nội dung báo cáo tiến độ thực hiện để gửi cho cấp trên.');
      return;
    }
    this.reportingProgress.set(true);
    this.http
      .post(`/api/v1/tasks/${encodeURIComponent(targetTaskId)}/progress`, {
        percent: pct,
        content,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: () => {
          this.reportingProgress.set(false);
          this.showProgressForm.set(false);
          this.progressTargetTaskId.set(null);
          this.progressTargetTaskTitle.set(null);
          this.progressContentInput = '';
          this.toast.success(`Đã gửi báo cáo tiến độ (${pct}%) thành công.`);
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.reportingProgress.set(false);
          this.toast.error(err?.error?.message ?? 'Không thể gửi báo cáo tiến độ. Vui lòng thử lại.');
        },
      });
  }

  completedSubtasksCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.completedWorkPackagesCount;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return subs.filter((s: any) => s.status === 'COMPLETED' || s.status === 'CONFIRMED').length;
  }

  totalWorkPackagesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.totalWorkPackagesCount;
    return this.data()?.subtasks?.length ?? 0;
  }

  inProgressWorkPackagesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.inProgressWorkPackagesCount;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return subs.filter((s: any) =>
      ['ACCEPTED', 'IN_PROGRESS', 'SUBMITTED', 'OVERDUE'].includes(s.status),
    ).length;
  }

  unfinishedWorkPackagesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.unfinishedWorkPackagesCount;
    return Math.max(0, this.totalWorkPackagesCount() - this.completedSubtasksCount());
  }

  overdueWorkPackagesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.overdueWorkPackagesCount;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return subs.filter((s: any) => s.status === 'OVERDUE').length;
  }

  pendingReviewWorkPackagesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.pendingReviewWorkPackagesCount;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    return subs.filter((s: any) => s.status === 'SUBMITTED').length;
  }

  participatingEmployeesCount(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.participatingEmployeesCount;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) return 0;
    const ids = new Set(subs.map((s: any) => s.assignedUserId).filter(Boolean));
    return ids.size || subs.length;
  }

  subtasksProgressPercent(): number {
    const ov = this.data()?.progressOverview;
    if (ov && ov.totalWorkPackagesCount > 0) return ov.overallCompletionPercent;
    const subs = this.data()?.subtasks;
    if (!subs || !subs.length) {
      const task = this.data()?.task;
      if (!task) return 0;
      if (task.status === 'COMPLETED' || task.status === 'CONFIRMED') return 100;
      const latestReport = this.data()?.reports?.items?.[0]?.percent;
      if (typeof latestReport === 'number') return latestReport;
      if (task.status === 'SUBMITTED') return 85;
      if (task.status === 'IN_PROGRESS' || task.status === 'OVERDUE') return 50;
      if (task.status === 'ACCEPTED') return 15;
      return 0;
    }
    return Math.round((this.completedSubtasksCount() / subs.length) * 100);
  }

  contributionsList(): WorkPackageContribution[] {
    const ov = this.data()?.progressOverview;
    if (ov?.contributions && ov.contributions.length > 0) {
      return ov.contributions;
    }
    const subs = this.data()?.subtasks ?? [];
    if (!subs.length) return [];
    const weight = Math.round((100 / subs.length) * 10) / 10;
    return subs.map((s: any) => {
      const isDone = s.status === 'COMPLETED' || s.status === 'CONFIRMED';
      const indiv = isDone
        ? 100
        : typeof s.progressPercent === 'number' && s.progressPercent > 0
          ? s.progressPercent
          : s.status === 'SUBMITTED'
            ? 85
            : s.status === 'IN_PROGRESS' || s.status === 'OVERDUE'
              ? 50
              : s.status === 'ACCEPTED'
                ? 15
                : 0;
      return {
        taskId: s.taskId,
        title: s.title,
        description: s.description ?? null,
        status: s.status,
        priority: s.priority,
        deadline: s.deadline,
        assignedUserId: s.assignedUserId,
        assignedUserName: s.assignedUserName,
        assignedEmployeeCode: s.assignedEmployeeCode ?? null,
        assignedDepartmentId: s.assignedDepartmentId,
        assignedDepartmentName: s.assignedDepartmentName,
        individualProgressPercent: indiv,
        weightPercent: weight,
        contributedPercent: isDone ? weight : 0,
        isCompleted: isDone,
        isOverdue: s.status === 'OVERDUE',
      };
    });
  }

  confirmSubtask(subtaskId: string, subtaskTitle: string) {
    if (this.assigning || !this.session.hasPermission('tasks.confirm')) return;
    this.assigning = true;
    this.dialog
      .open(TaskConfirmationEditor, {
        data: { taskId: subtaskId, title: subtaskTitle },
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
        this.load();
      });
  }

  isOverdue(deadline?: string | null, status?: string): boolean {
    if (!deadline) return false;
    if (status === 'COMPLETED' || status === 'CANCELLED' || status === 'CONFIRMED') return false;
    return new Date(deadline).getTime() < Date.now();
  }

  currentEffectiveProgressPercent(): number {
    const d = this.data();
    if (!d?.task) return 0;
    if (d.task.status === 'COMPLETED' || d.task.status === 'CONFIRMED') return 100;
    const subProg = this.subtasksProgressPercent();
    if (subProg > 0) return subProg;
    if (typeof d.task.progressPercent === 'number' && d.task.progressPercent > 0) {
      return d.task.progressPercent;
    }
    const ovProg = d.progressOverview?.overallCompletionPercent;
    if (typeof ovProg === 'number' && ovProg > 0) return ovProg;
    const repProg = d.reports?.items?.[0]?.percent;
    if (typeof repProg === 'number') return repProg;
    return 0;
  }

  formatPriorityLabel(priority?: string): string {
    switch (priority) {
      case 'URGENT':
        return 'Khẩn cấp';
      case 'HIGH':
        return 'Cao';
      case 'MEDIUM':
        return 'Trung bình';
      case 'LOW':
        return 'Thấp';
      default:
        return priority || 'Bình thường';
    }
  }

  formatStatusLabel(status?: string): string {
    switch (status) {
      case 'DRAFT':
        return 'Bản nháp';
      case 'ASSIGNED':
        return 'Đã phân công';
      case 'ACCEPTED':
        return 'Đã tiếp nhận';
      case 'IN_PROGRESS':
        return 'Đang thực hiện';
      case 'SUBMITTED':
        return 'Chờ nghiệm thu';
      case 'COMPLETED':
        return 'Đã hoàn thành';
      case 'REJECTED':
        return 'Từ chối / Cần sửa';
      case 'CANCELLED':
        return 'Đã hủy';
      case 'OVERDUE':
        return 'Quá hạn';
      default:
        return status || 'Đang xử lý';
    }
  }

  page(section: keyof typeof this.pages, value: number) {
    this.pages[section] = value;
    this.load();
  }
  participatingPersonnelList(): Array<{
    id?: string;
    name: string;
    department?: string;
    employeeCode?: string;
    isPrimary?: boolean;
  }> {
    const d = this.data();
    if (!d) return [];
    const map = new Map<
      string,
      { id?: string; name: string; department?: string; employeeCode?: string; isPrimary?: boolean }
    >();

    if (d.task?.assignedUserName) {
      map.set(d.task.assignedUserName, {
        id: d.task.assignedUserId ?? undefined,
        name: d.task.assignedUserName,
        department: d.task.assignedDepartmentName ?? undefined,
      });
    }

    const subs = this.contributionsList();
    for (const s of subs) {
      if (s.assignedUserName && !map.has(s.assignedUserName)) {
        map.set(s.assignedUserName, {
          id: s.assignedUserId ?? undefined,
          name: s.assignedUserName,
          department: s.assignedDepartmentName ?? undefined,
          employeeCode: s.assignedEmployeeCode ?? undefined,
          isPrimary: s.taskKind === 'PRIMARY_SUBTASK' || s.taskKind === 'WORK_PACKAGE',
        });
      }
      if (s.childContributions) {
        for (const c of s.childContributions) {
          if (c.assignedUserName && !map.has(c.assignedUserName)) {
            map.set(c.assignedUserName, {
              id: c.assignedUserId ?? undefined,
              name: c.assignedUserName,
              department: c.assignedDepartmentName ?? undefined,
              employeeCode: c.assignedEmployeeCode ?? undefined,
            });
          }
        }
      }
    }

    if (d.subtasks) {
      for (const st of d.subtasks) {
        if (st.assignedUserName && !map.has(st.assignedUserName)) {
          map.set(st.assignedUserName, {
            id: st.assignedUserId ?? undefined,
            name: st.assignedUserName,
            department: st.assignedDepartmentName ?? undefined,
          });
        }
      }
    }

    return Array.from(map.values());
  }

  assignedPersonnelSummary(): string {
    const list = this.participatingPersonnelList();
    if (list.length === 0) return 'Chờ phân công cụ thể';
    if (list.length === 1) {
      const p = list[0];
      return p.name + (p.department ? ` (${p.department})` : '');
    }
    if (list.length <= 3) {
      return list.map((p) => p.name).join(', ');
    }
    return `${list.slice(0, 3).map((p) => p.name).join(', ')} (+${list.length - 3} nhân sự khác)`;
  }

  myWorkPackage(): WorkPackageContribution | undefined {
    if (this.isCompanyAdmin()) return undefined;
    const currentUserId = this.session.identity()?.userId;
    if (!currentUserId) return undefined;
    const d = this.data();
    if (!d?.task) return undefined;
    // If we are directly viewing our own task, no need for the banner
    if (d.task.assignedUserId === currentUserId) return undefined;

    const contributions = this.contributionsList();
    const pkg = contributions.find((c) => c.assignedUserId === currentUserId);
    if (pkg) return pkg;

    for (const c of contributions) {
      if (c.childContributions) {
        const child = c.childContributions.find((ch) => ch.assignedUserId === currentUserId);
        if (child) return child;
      }
    }
    return undefined;
  }

  acceptTaskById(taskId: string, title: string) {
    if (this.assigning || !this.session.hasPermission('tasks.accept')) return;
    this.assigning = true;
    this.dialog
      .open(TaskAcceptanceEditor, {
        data: { taskId, title },
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
        this.load();
      });
  }

  startTaskById(taskId: string, title: string, isOverdue: boolean = false) {
    if (this.assigning || !this.session.hasPermission('tasks.execute')) return;
    this.assigning = true;
    this.dialog
      .open(TaskExecutionAction, {
        data: { taskId, title, resume: isOverdue },
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
        this.load();
      });
  }

  submitTaskById(taskId: string, title: string) {
    if (this.assigning || !this.session.hasPermission('tasks.submit')) return;
    this.assigning = true;
    this.dialog
      .open(TaskSubmissionEditor, {
        data: { taskId, title },
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
        this.load();
      });
  }

  canAccept() {
    if (this.isCompanyAdmin()) return false;
    const task = this.data()?.task;
    return (
      task?.status === 'ASSIGNED' &&
      this.session.hasPermission('tasks.accept') &&
      (!task.assignedUserId || task.assignedUserId === this.session.identity()?.userId)
    );
  }
  canStart() {
    if (this.isCompanyAdmin()) return false;
    const task = this.data()?.task;
    return (
      !!task &&
      ['ACCEPTED', 'OVERDUE'].includes(task.status) &&
      this.session.hasPermission('tasks.execute') &&
      (!task.assignedUserId || task.assignedUserId === this.session.identity()?.userId)
    );
  }
  start() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canStart()) return;
    this.startTaskById(task.taskId, task.title, task.status === 'OVERDUE');
  }
  canSubmit() {
    if (this.isCompanyAdmin()) return false;
    const task = this.data()?.task;
    return (
      !!task &&
      task.status === 'IN_PROGRESS' &&
      this.session.hasPermission('tasks.submit') &&
      (!task.assignedUserId || task.assignedUserId === this.session.identity()?.userId)
    );
  }
  submit() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canSubmit()) return;
    this.submitTaskById(task.taskId, task.title);
  }
  canConfirm() {
    const task = this.data()?.task;
    return !!task && task.status === 'SUBMITTED' && this.session.hasPermission('tasks.confirm');
  }
  confirm() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canConfirm()) return;
    this.confirmSubtask(task.taskId, task.title);
  }
  accept() {
    const task = this.data()?.task;
    if (!task || this.assigning || !this.canAccept()) return;
    this.acceptTaskById(task.taskId, task.title);
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
          alert(
            failure?.error?.message ??
              this.i18n.t('taskDetail.errArchive', 'Failed to archive task.'),
          );
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
              ? this.i18n.t(
                  'taskDetail.errUnavailable',
                  'This task is unavailable or outside your access scope.',
                )
              : this.i18n.t(
                  'taskDetail.errLoad',
                  'Task details could not be loaded. Please try again.',
                ),
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
