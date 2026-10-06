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
  };
  description: string | null;
  creatorName: string | null;
  checklist: EvidencePage;
  reports: EvidencePage;
  results: EvidencePage;
}
@Component({
  selector: 'bf-task-detail',
  imports: [DatePipe, MatButtonModule, RouterLink],
  templateUrl: './task-detail.html',
  styleUrls: ['../organization/departments.scss', './task-editor.scss'],
})
export class TaskDetail {
  readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private assigning = false;
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private id = '';
  private pages = { checklist: 1, reports: 1, results: 1 };
  readonly data = signal<Detail | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly sections = [
    { key: 'checklist', name: 'Checklist' },
    { key: 'reports', name: 'Progress reports' },
    { key: 'results', name: 'Results' },
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
              ? 'This task is unavailable or outside your access scope.'
              : 'Task details could not be loaded. Please try again.',
          );
        },
      });
  }
}
