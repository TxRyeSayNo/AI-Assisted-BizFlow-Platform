import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface PendingApprovalItem {
  id: string;
  type: 'TASK_CONFIRMATION' | 'REQUEST_RESOLUTION';
  title: string;
  sourceId: string;
  requesterOrPerformer: string;
  departmentName?: string;
  priority: string;
  submittedAt: string;
  deadline?: string;
  summary: string;
  status: string;
}

@Component({
  selector: 'bf-approvals',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe, TranslatePipe],
  template: `
    <header class="public-header directory-header">
      <a class="wordmark" routerLink="/workspace">
        <span class="brand-mark" aria-hidden="true"></span> BizFlow
      </a>
      <span class="tenant-badge">{{ session.identity()?.tenantName }}</span>
    </header>

    <main id="main-content" class="page-content" tabindex="-1">
      <nav aria-label="Breadcrumb">
        <a routerLink="/workspace">{{ 'common.workspace' | translate: 'Workspace' }}</a>
        <span aria-hidden="true"> / </span>
        <span>{{ 'approvals.breadcrumb' | translate: 'Approvals' }}</span>
      </nav>

      <div class="approval-header-row">
        <div>
          <h1>{{ 'approvals.title' | translate: 'Trung tâm phê duyệt' }}</h1>
          <p class="subtitle-text">
            {{
              'approvals.subtitle'
                | translate
                  : 'Trung tâm tập trung dành cho cấp quản lý để đánh giá kết quả bàn giao công việc, cột mốc xác nhận và giải quyết yêu cầu dịch vụ.'
            }}
          </p>
        </div>
        <div class="stats-badge-box">
          <span class="stat-num">{{ pendingList().length }}</span>
          <span class="stat-lbl">{{
            'approvals.pendingReview' | translate: 'Awaiting Decision'
          }}</span>
        </div>
      </div>

      <!-- Filters & Type Selector -->
      <section class="directory-panel">
        <div class="approval-toolbar">
          <div class="type-pills">
            <button
              type="button"
              class="pill-btn"
              [class.active]="filterType() === 'ALL'"
              (click)="setFilter('ALL')"
            >
              {{ 'approvals.allDecisions' | translate: 'All Decisions' }} ({{
                pendingList().length
              }})
            </button>
            <button
              type="button"
              class="pill-btn"
              [class.active]="filterType() === 'TASK'"
              (click)="setFilter('TASK')"
            >
              {{ 'approvals.taskResults' | translate: 'Task Results' }} ({{
                countByType('TASK_CONFIRMATION')
              }})
            </button>
            <button
              type="button"
              class="pill-btn"
              [class.active]="filterType() === 'REQUEST'"
              (click)="setFilter('REQUEST')"
            >
              {{ 'approvals.requestResolutions' | translate: 'Request Resolutions' }} ({{
                countByType('REQUEST_RESOLUTION')
              }})
            </button>
          </div>

          <div class="search-wrap">
            <input
              type="text"
              class="search-input"
              [placeholder]="'common.search' | translate: 'Filter by title or performer…'"
              [(ngModel)]="searchQuery"
            />
          </div>
        </div>

        <!-- Approval Item Cards -->
        <div *ngIf="filteredItems().length === 0" class="directory-feedback">
          <div class="empty-icon">✓</div>
          <h3>{{ 'approvals.allCaughtUp' | translate: 'All caught up!' }}</h3>
          <p>
            {{
              'approvals.noItemsPending'
                | translate
                  : 'No pending approvals match your current filter and access permissions.'
            }}
          </p>
        </div>

        <div *ngIf="filteredItems().length > 0" class="approval-list">
          <article *ngFor="let item of filteredItems()" class="approval-card">
            <div class="card-top">
              <div class="card-meta">
                <span
                  class="type-tag"
                  [ngClass]="item.type === 'TASK_CONFIRMATION' ? 'tag-task' : 'tag-request'"
                >
                  {{
                    item.type === 'TASK_CONFIRMATION'
                      ? ('approvals.taskResults' | translate: 'Task Result Review')
                      : ('approvals.requestResolutions' | translate: 'Request Resolution')
                  }}
                </span>
                <span class="status-chip" [attr.data-status]="item.status">
                  {{ formatStatus(item.status) }}
                </span>
                <span class="priority-tag" [ngClass]="'priority-' + item.priority.toLowerCase()">
                  {{ 'priority.' + item.priority | translate: item.priority }}
                </span>
              </div>
              <time class="card-time" [attr.datetime]="item.submittedAt">
                {{ 'approvals.submittedAt' | translate: 'Đã gửi:' }} {{ item.submittedAt | date: 'dd/MM/yyyy HH:mm' }}
              </time>
            </div>

            <h2 class="card-title">
              <a *ngIf="item.type === 'TASK_CONFIRMATION'" [routerLink]="['/tasks', item.sourceId]">
                {{ item.title }}
              </a>
              <a
                *ngIf="item.type === 'REQUEST_RESOLUTION'"
                [routerLink]="['/requests', item.sourceId]"
              >
                {{ item.title }}
              </a>
            </h2>

            <p class="card-summary">{{ item.summary }}</p>

            <div class="card-footer">
              <div class="actor-info">
                <span class="actor-label">{{
                  'approvals.submittedBy' | translate: 'Submitted by:'
                }}</span>
                <strong>{{ item.requesterOrPerformer }}</strong>
                <span *ngIf="item.departmentName" class="dept-dot"
                  >· {{ item.departmentName }}</span
                >
              </div>

              <div class="card-actions">
                <a
                  *ngIf="item.type === 'TASK_CONFIRMATION'"
                  mat-stroked-button
                  class="btn-detail"
                  [routerLink]="['/tasks', item.sourceId]"
                >
                  {{ 'approvals.inspectRecord' | translate: 'Inspect Deliverables →' }}
                </a>
                <a
                  *ngIf="item.type === 'REQUEST_RESOLUTION'"
                  mat-stroked-button
                  class="btn-detail"
                  [routerLink]="['/requests', item.sourceId]"
                >
                  {{ 'approvals.inspectRecord' | translate: 'Inspect Resolution →' }}
                </a>
              </div>
            </div>
          </article>
        </div>
      </section>
    </main>
  `,
  styles: [
    `
      .approval-header-row {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 1.5rem;
        gap: 1.5rem;
      }
      .subtitle-text {
        color: #63758b;
        font-size: 0.95rem;
        max-width: 44rem;
        margin: 0.35rem 0 0;
      }
      .stats-badge-box {
        background: linear-gradient(135deg, #0b2a4a 0%, #145da0 100%);
        color: #ffffff;
        padding: 0.85rem 1.35rem;
        border-radius: 12px;
        display: flex;
        flex-direction: column;
        align-items: center;
        min-width: 8rem;
        box-shadow: 0 4px 14px rgba(11, 42, 74, 0.15);
      }
      .stat-num {
        font-size: 1.85rem;
        font-weight: 800;
        line-height: 1;
      }
      .stat-lbl {
        font-size: 0.72rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #8ec5ff;
        margin-top: 0.25rem;
      }
      .approval-toolbar {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 1rem;
        padding-bottom: 1.25rem;
        border-bottom: 1px solid #dfe8f2;
        margin-bottom: 1.25rem;
      }
      .type-pills {
        display: flex;
        gap: 0.5rem;
      }
      .pill-btn {
        background: #f8fafd;
        border: 1px solid #dfe8f2;
        color: #63758b;
        padding: 0.4rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.8125rem;
        font-weight: 600;
        cursor: pointer;
        transition: all 160ms ease;
        &:hover {
          background: #eaf4ff;
          color: #145da0;
        }
        &.active {
          background: #2563eb;
          border-color: #2563eb;
          color: #ffffff;
        }
      }
      .search-input {
        border: 1px solid #dfe8f2;
        border-radius: 8px;
        padding: 0.45rem 0.85rem;
        font-size: 0.875rem;
        min-width: 16rem;
        background: #ffffff;
        &:focus {
          outline: none;
          border-color: #2563eb;
          box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.15);
        }
      }
      .empty-icon {
        font-size: 2.5rem;
        color: #059669;
        margin-bottom: 0.5rem;
      }
      .approval-list {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }
      .approval-card {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        border-radius: 12px;
        padding: 1.25rem;
        transition: all 200ms cubic-bezier(0.16, 1, 0.3, 1);
        position: relative;
        overflow: hidden;
        box-shadow: 0 2px 6px rgba(7, 27, 51, 0.03);
        &:hover {
          transform: translateY(-2px);
          box-shadow:
            0 8px 24px -4px rgba(37, 99, 235, 0.12),
            0 2px 6px rgba(11, 42, 74, 0.04);
          border-color: #8ec5ff;
        }
      }
      .card-top {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.65rem;
        flex-wrap: wrap;
        gap: 0.5rem;
      }
      .card-meta {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }
      .type-tag {
        font-size: 0.72rem;
        font-weight: 700;
        text-transform: uppercase;
        padding: 0.2rem 0.55rem;
        border-radius: 6px;
        letter-spacing: 0.04em;
        &.tag-task {
          background: #eaf4ff;
          color: #1e40af;
          border: 1px solid #bfdbfe;
        }
        &.tag-request {
          background: #f0fdf4;
          color: #166534;
          border: 1px solid #bbf7d0;
        }
      }
      .priority-tag {
        font-size: 0.72rem;
        font-weight: 700;
        text-transform: uppercase;
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        &.priority-critical {
          background: #fef2f2;
          color: #b91c1c;
        }
        &.priority-high {
          background: #fff7ed;
          color: #c2410c;
        }
        &.priority-medium {
          background: #f0fdf4;
          color: #15803d;
        }
        &.priority-low {
          background: #f8fafc;
          color: #475569;
        }
      }
      .card-time {
        font-size: 0.75rem;
        color: #63758b;
      }
      .card-title {
        font-size: 1.15rem;
        margin: 0 0 0.5rem;
        a {
          color: #071b33;
          font-weight: 700;
          &:hover {
            color: #2563eb;
          }
        }
      }
      .card-summary {
        color: #475569;
        font-size: 0.875rem;
        line-height: 1.5;
        margin: 0 0 1rem;
      }
      .card-footer {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding-top: 0.75rem;
        border-top: 1px solid #f1f5f9;
        flex-wrap: wrap;
        gap: 0.75rem;
      }
      .actor-info {
        font-size: 0.8125rem;
        color: #63758b;
        strong {
          color: #10243a;
          font-weight: 600;
          margin-left: 0.25rem;
        }
        .dept-dot {
          margin-left: 0.35rem;
          color: #63758b;
        }
      }
      .btn-detail {
        font-size: 0.8125rem;
        color: #2563eb;
        font-weight: 600;
        padding: 0.35rem 0.75rem;
        border-radius: 6px;
        background: #f8fafd;
        border: 1px solid #dfe8f2;
        &:hover {
          background: #eaf4ff;
          text-decoration: none;
        }
      }
      .tenant-badge {
        background: #eaf4ff;
        color: #145da0;
        border: 1px solid #bfdbfe;
        padding: 0.2rem 0.65rem;
        border-radius: 9999px;
        font-size: 0.8125rem;
        font-weight: 600;
      }
    `,
  ],
})
export class Approvals implements OnInit {
  private readonly http = inject(HttpClient);
  readonly session = inject(SessionService);

  readonly pendingList = signal<PendingApprovalItem[]>([]);
  readonly filterType = signal<'ALL' | 'TASK' | 'REQUEST'>('ALL');
  searchQuery = '';

  ngOnInit(): void {
    this.loadApprovals();
  }

  loadApprovals(): void {
    const list: PendingApprovalItem[] = [];

    // 1. Fetch submitted tasks awaiting manager/admin confirmation
    this.http
      .get<{
        items: Array<{
          taskId: string;
          title: string;
          status: string;
          priority: string;
          createdAt: string;
          assignedUserName?: string;
          assignedDepartmentName?: string;
        }>;
      }>('/api/v1/tasks?status=SUBMITTED&pageSize=20')
      .subscribe({
        next: (res) => {
          const taskItems: PendingApprovalItem[] = (res.items || []).map((t) => ({
            id: t.taskId,
            type: 'TASK_CONFIRMATION',
            title: t.title,
            sourceId: t.taskId,
            requesterOrPerformer: t.assignedUserName || 'Nhân sự thực hiện',
            departmentName: t.assignedDepartmentName,
            priority: t.priority,
            submittedAt: t.createdAt,
            summary: 'Đã nộp kết quả công việc. Đang chờ cấp quản lý xem xét và nghiệm thu hoàn thành.',
            status: t.status,
          }));
          list.push(...taskItems);
          this.pendingList.set([...list]);
        },
      });

    // 2. Fetch requests awaiting first-level review or escalated approval
    this.http
      .get<{
        items: Array<{
          requestId: string;
          title: string;
          status: string;
          priority: string;
          createdAt: string;
          requesterName?: string;
          currentRoutingDepartmentName?: string;
        }>;
      }>('/api/v1/requests?status=SUBMITTED&pageSize=20')
      .subscribe({
        next: (res) => {
          const reqItems: PendingApprovalItem[] = (res.items || []).map((r) => ({
            id: r.requestId,
            type: 'REQUEST_RESOLUTION',
            title: r.title,
            sourceId: r.requestId,
            requesterOrPerformer: r.requesterName || 'Người gửi yêu cầu',
            departmentName: r.currentRoutingDepartmentName,
            priority: r.priority,
            submittedAt: r.createdAt,
            summary: 'Yêu cầu nội bộ mới được gửi. Đang chờ Trưởng phòng duyệt phân luồng hoặc leo thang cấp Công ty.',
            status: r.status,
          }));
          list.push(...reqItems);
          this.pendingList.set([...list]);
        },
        error: () => {},
      });
  }

  formatStatus(status: string): string {
    switch (status) {
      case 'SUBMITTED':
        return 'ĐÃ NỘP';
      case 'RESOLVED':
        return 'ĐÃ GIẢI QUYẾT';
      case 'PENDING':
        return 'CHỜ DUYỆT';
      case 'APPROVED':
        return 'ĐÃ DUYỆT';
      case 'REJECTED':
        return 'TỪ CHỐI';
      default:
        return status;
    }
  }

  setFilter(type: 'ALL' | 'TASK' | 'REQUEST'): void {
    this.filterType.set(type);
  }

  countByType(type: 'TASK_CONFIRMATION' | 'REQUEST_RESOLUTION'): number {
    return this.pendingList().filter((i) => i.type === type).length;
  }

  filteredItems(): PendingApprovalItem[] {
    return this.pendingList().filter((item) => {
      if (this.filterType() === 'TASK' && item.type !== 'TASK_CONFIRMATION') return false;
      if (this.filterType() === 'REQUEST' && item.type !== 'REQUEST_RESOLUTION') return false;
      if (this.searchQuery.trim()) {
        const q = this.searchQuery.toLowerCase();
        return (
          item.title.toLowerCase().includes(q) ||
          item.requesterOrPerformer.toLowerCase().includes(q)
        );
      }
      return true;
    });
  }
}
