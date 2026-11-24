import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';
import { TaskAcceptanceEditor } from './task-acceptance';
import { TaskAssignmentEditor } from './task-assignment';
import { TaskConfirmationEditor } from './task-confirmation';
import { TaskExecutionAction } from './task-execution';
import { TaskSubmissionEditor } from './task-submission';

const states = {
  DRAFT: 'Draft',
  ASSIGNED: 'Assigned',
  ACCEPTED: 'Accepted',
  IN_PROGRESS: 'In progress',
  SUBMITTED: 'Submitted',
  CONFIRMED: 'Confirmed',
  COMPLETED: 'Completed',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
  OVERDUE: 'Overdue',
};
const priorities = { LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', CRITICAL: 'Critical' };

type TaskStatus = keyof typeof states;

export interface BoardCard {
  taskId: string;
  title: string;
  status: TaskStatus;
  priority: keyof typeof priorities;
  deadline: string | null;
  assignedUserId: string | null;
  assignedUserName: string | null;
  assignedDepartmentId: string | null;
  assignedDepartmentName: string | null;
  requestId: string | null;
  parentTaskId?: string | null;
  subtaskCount?: number;
  completedSubtaskCount?: number;
}
export interface BoardColumn {
  status: TaskStatus;
  items: BoardCard[];
  total: number;
}
export interface BoardView {
  columns: BoardColumn[];
  columnSize: number;
}

/**
 * Which existing dialog drives a drop, keyed by `from -> to`. Every entry maps to a transition
 * TaskLifecyclePolicy already permits and to an endpoint that already exists, so the board never
 * invents a "set status" call and never duplicates lifecycle, permission, audit or SLA handling.
 *
 * Deliberately absent, because the server offers no user-driven path to them:
 *   COMPLETED and OVERDUE are reached by the system only; CANCELLED and REJECTED have no endpoint
 *   on the tasks controller; and ASSIGNED -> REJECTED would need a reason the accept command does
 *   not take. Those lanes therefore read as drop-refusals.
 */
type TransitionPlan = {
  dialog: 'assign' | 'accept' | 'start' | 'submit' | 'confirm';
  permission: string;
  resume?: boolean;
};
const transitions: Record<string, TransitionPlan> = {
  'DRAFT>ASSIGNED': { dialog: 'assign', permission: 'tasks.assign' },
  'REJECTED>ASSIGNED': { dialog: 'assign', permission: 'tasks.assign' },
  'ASSIGNED>ACCEPTED': { dialog: 'accept', permission: 'tasks.accept' },
  'ACCEPTED>IN_PROGRESS': { dialog: 'start', permission: 'tasks.execute' },
  'OVERDUE>IN_PROGRESS': { dialog: 'start', permission: 'tasks.execute', resume: true },
  'IN_PROGRESS>SUBMITTED': { dialog: 'submit', permission: 'tasks.submit' },
  'SUBMITTED>CONFIRMED': { dialog: 'confirm', permission: 'tasks.confirm' },
  // Rework: the confirmation dialog is where the mandatory reason note is captured.
  'SUBMITTED>IN_PROGRESS': { dialog: 'confirm', permission: 'tasks.confirm' },
};

@Component({
  selector: 'bf-task-board',
  imports: [
    RouterLink,
    DatePipe,
    DragDropModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
  ],
  templateUrl: './task-board.html',
  styleUrls: ['../organization/departments.scss', './task-board.scss'],
})
export class TaskBoard {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);
  private request?: Subscription;
  private applied = {
    search: '',
    priority: '',
    assignedUserId: '',
    assignedDepartmentId: '',
    deadlineFrom: '',
    deadlineTo: '',
  };
  private moving = false;

  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
  readonly filters = inject(FormBuilder).nonNullable.group(this.applied);
  readonly result = signal<BoardView | null>(null);
  readonly busy = signal(false);
  readonly denied = signal(false);
  readonly unavailable = signal(false);
  readonly error = signal(false);
  readonly states = states;
  readonly priorities = priorities;
  readonly priorityOptions = Object.entries(priorities);

  /** Lane ids stay stable so every lane can accept a card from any other lane. */
  readonly laneIds = Object.keys(states).map((status) => 'lane-' + status);

  /**
   * Filter choices are derived from the cards already on the board instead of a second lookup
   * request, which keeps the options inside the caller's own read scope by construction. Once
   * seen, a person or department stays selectable even after a filter narrows the board.
   */
  private readonly seenAssignees = signal<Map<string, string>>(new Map());
  private readonly seenDepartments = signal<Map<string, string>>(new Map());
  readonly assigneeOptions = computed(() => [...this.seenAssignees().entries()]);
  readonly departmentOptions = computed(() => [...this.seenDepartments().entries()]);

  readonly features = inject(FeatureFlagsService);

  constructor() {
    this.load();
  }

  apply() {
    this.applied = this.filters.getRawValue();
    this.load();
  }

  reset() {
    this.filters.reset();
    this.apply();
  }

  load() {
    this.request?.unsubscribe();
    this.busy.set(true);
    this.denied.set(false);
    this.unavailable.set(false);
    this.error.set(false);
    let params = new HttpParams();
    for (const [key, value] of Object.entries(this.applied)) {
      if (value.trim()) params = params.set(key, value.trim());
    }
    this.request = this.http
      .get<BoardView>('/api/v1/tasks/board', { params })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.result.set(data);
          this.remember(data);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.result.set(null);
          this.denied.set(failure.status === 403);
          // A disabled feature answers exactly like an endpoint that was never deployed.
          this.unavailable.set(failure.status === 404);
          this.error.set(![403, 404].includes(failure.status));
          this.busy.set(false);
        },
      });
  }

  lane(status: string): BoardColumn | null {
    return this.result()?.columns.find((column) => column.status === status) ?? null;
  }

  /** True when a card in `from` could legitimately be dropped into `to`. */
  canMove(from: string, to: string): boolean {
    return transitions[from + '>' + to] !== undefined;
  }

  drop(event: CdkDragDrop<BoardColumn>, target: string) {
    // The card is never moved optimistically: the board reloads from the server once a transition
    // actually succeeds, so a refused or cancelled drop leaves the lane exactly as it was.
    if (event.previousContainer === event.container || this.moving) return;
    const card = event.item.data as BoardCard;
    const plan = transitions[card.status + '>' + target];
    if (!plan) {
      this.toast.error(
        this.i18n.t(
          'taskBoard.invalidTransition',
          'Không thể chuyển nhiệm vụ sang trạng thái này. Thẻ đã được trả về vị trí cũ.',
        ),
      );
      return;
    }
    if (!this.session.hasPermission(plan.permission)) {
      this.toast.error(
        this.i18n.t(
          'taskBoard.noPermission',
          'Bạn không có quyền thực hiện chuyển trạng thái này.',
        ),
      );
      return;
    }
    this.open(plan, card);
  }

  private open(plan: TransitionPlan, card: BoardCard) {
    const common = {
      width: plan.dialog === 'assign' ? '44rem' : '36rem',
      maxWidth: '100vw',
      autoFocus: 'first-heading' as const,
      restoreFocus: true,
    };
    const reference =
      plan.dialog === 'assign'
        ? this.dialog.open(TaskAssignmentEditor, {
            ...common,
            data: { taskId: card.taskId, title: card.title },
            height: '100dvh',
            position: { right: '0', top: '0' },
            ariaLabelledBy: 'task-assignment-title',
          })
        : plan.dialog === 'accept'
          ? this.dialog.open(TaskAcceptanceEditor, {
              ...common,
              data: { taskId: card.taskId, title: card.title },
              ariaLabelledBy: 'task-acceptance-title',
            })
          : plan.dialog === 'start'
            ? this.dialog.open(TaskExecutionAction, {
                ...common,
                data: { taskId: card.taskId, title: card.title, resume: plan.resume === true },
                ariaLabelledBy: 'task-execution-title',
              })
            : plan.dialog === 'submit'
              ? this.dialog.open(TaskSubmissionEditor, {
                  ...common,
                  data: { taskId: card.taskId, title: card.title },
                  ariaLabelledBy: 'task-submission-title',
                })
              : this.dialog.open(TaskConfirmationEditor, {
                  ...common,
                  data: { taskId: card.taskId, title: card.title },
                  ariaLabelledBy: 'task-confirmation-title',
                });
    this.moving = true;
    reference
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe(() => {
        this.moving = false;
        // Escape or a backdrop click can follow an uncertain response, so always re-read.
        this.load();
      });
  }

  private remember(view: BoardView) {
    const assignees = new Map(this.seenAssignees());
    const departments = new Map(this.seenDepartments());
    for (const column of view.columns ?? []) {
      for (const card of column.items ?? []) {
        if (card.assignedUserId) {
          assignees.set(card.assignedUserId, card.assignedUserName ?? card.assignedUserId);
        }
        if (card.assignedDepartmentId) {
          departments.set(
            card.assignedDepartmentId,
            card.assignedDepartmentName ?? card.assignedDepartmentId,
          );
        }
      }
    }
    this.seenAssignees.set(assignees);
    this.seenDepartments.set(departments);
  }
}
