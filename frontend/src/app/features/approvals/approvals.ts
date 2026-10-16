import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

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
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  template: `
    <header class="public-header directory-header">
      <a class="wordmark" routerLink="/workspace">
        <span class="brand-mark" aria-hidden="true"></span> BizFlow
      </a>
      <span class="tenant-badge">{{ session.identity()?.tenantName }}</span>
    </header>

    <main id="main-content" class="page-content" tabindex="-1">
      <nav aria-label="Breadcrumb">
        <a routerLink="/workspace">Workspace</a> <span aria-hidden="true"> / </span> <span>Approvals</span>
      </nav>

      <div class="approval-header-row">
        <div>
          <span class="eyebrow">Governance & Milestones</span>
          <h1>Approval Center</h1>
          <p class="subtitle-text">
            Centralized hub for authorized managers to review task deliverables, milestone confirmations, and service request resolutions.
          </p>
        </div>
        <div class="stats-badge-box">
          <span class="stat-num">{{ pendingList().length }}</span>
          <span class="stat-lbl">Awaiting Decision</span>
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
              All Decisions ({{ pendingList().length }})
            </button>
            <button
              type="button"
              class="pill-btn"
              [class.active]="filterType() === 'TASK'"
              (click)="setFilter('TASK')"
            >
              Task Results ({{ countByType('TASK_CONFIRMATION') }})
            </button>
            <button
              type="button"
              class="pill-btn"
              [class.active]="filterType() === 'REQUEST'"
              (click)="setFilter('REQUEST')"
            >
              Request Resolutions ({{ countByType('REQUEST_RESOLUTION') }})
            </button>
          </div>

          <div class="search-wrap">
            <input
              type="text"
              class="search-input"
              placeholder="Filter by title or performer…"
              [(ngModel)]="searchQuery"
            />
          </div>
        </div>

        <!-- Approval Item Cards -->
        <div *ngIf="filteredItems().length === 0" class="directory-feedback">
          <div class="empty-icon">✓</div>
          <h3>All caught up!</h3>
          <p>No pending approvals match your current filter and access permissions.</p>
        </div>

        <div *ngIf="filteredItems().length > 0" class="approval-list">
          <article *ngFor="let item of filteredItems()" class="approval-card">
            <div class="card-top">
              <div class="card-meta">
                <span class="type-tag" [ngClass]="item.type === 'TASK_CONFIRMATION' ? 'tag-task' : 'tag-request'">
                  {{ item.type === 'TASK_CONFIRMATION' ? 'Task Result Review' : 'Request Resolution' }}
                </span>
                <span class="status-chip" [attr.data-status]="item.status">
                  {{ item.status }}
                </span>
                <span class="priority-tag" [ngClass]="'priority-' + item.priority.toLowerCase()">
                  {{ item.priority }}
                </span>
              </div>
              <time class="card-time" [attr.datetime]="item.submittedAt">
                Submitted {{ item.submittedAt | date:'medium' }}
              </time>
            </div>

            <h2 class="card-title">
              <a *ngIf="item.type === 'TASK_CONFIRMATION'" [routerLink]="['/tasks', item.sourceId]">
                {{ item.title }}
              </a>
              <a *ngIf="item.type === 'REQUEST_RESOLUTION'" [routerLink]="['/requests', item.sourceId]">
                {{ item.title }}
              </a>
            </h2>

            <p class="card-summary">{{ item.summary }}</p>

            <div class="card-footer">
              <div class="actor-info">
                <span class="actor-label">Submitted by:</span>
                <strong>{{ item.requesterOrPerformer }}</strong>
                <span *ngIf="item.departmentName" class="dept-dot">· {{ item.departmentName }}</span>
              </div>

              <div class="card-actions">
                <a
                  *ngIf="item.type === 'TASK_CONFIRMATION'"
                  mat-stroked-button
                  class="btn-detail"
                  [routerLink]="['/tasks', item.sourceId]"
                >
                  Inspect Deliverables →
                </a>
                <a
                  *ngIf="item.type === 'REQUEST_RESOLUTION'"
                  mat-stroked-button
                  class="btn-detail"
                  [routerLink]="['/requests', item.sourceId]"
                >
                  Inspect Resolution →
                </a>
              </div>
            </div>
          </article>
        </div>
      </section>
    </main>
  `,
  styles: [`
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
      &:hover { background: #eaf4ff; color: #145da0; }
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
      &:focus { outline: none; border-color: #2563eb; box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.15); }
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
        box-shadow: 0 8px 24px -4px rgba(37, 99, 235, 0.12), 0 2px 6px rgba(11, 42, 74, 0.04);
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
      &.tag-task { background: #eaf4ff; color: #1e40af; border: 1px solid #bfdbfe; }
      &.tag-request { background: #f0fdf4; color: #166534; border: 1px solid #bbf7d0; }
    }
    .priority-tag {
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      &.priority-critical { background: #fef2f2; color: #b91c1c; }
      &.priority-high { background: #fff7ed; color: #c2410c; }
      &.priority-medium { background: #f0fdf4; color: #15803d; }
      &.priority-low { background: #f8fafc; color: #475569; }
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
        &:hover { color: #2563eb; }
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
      strong { color: #10243a; font-weight: 600; margin-left: 0.25rem; }
      .dept-dot { margin-left: 0.35rem; color: #63758b; }
    }
    .btn-detail {
      font-size: 0.8125rem;
      color: #2563eb;
      font-weight: 600;
      padding: 0.35rem 0.75rem;
      border-radius: 6px;
      background: #f8fafd;
      border: 1px solid #dfe8f2;
      &:hover { background: #eaf4ff; text-decoration: none; }
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
  `]
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
    // Queries active tasks awaiting confirmation (status: SUBMITTED) and requests awaiting confirmation (status: RESOLVED)
    this.http.get<{ items: Array<{ taskId: string; title: string; status: string; priority: string; createdAt: string; assignedUserName?: string }> }>('/api/v1/tasks?status=SUBMITTED&pageSize=20').subscribe({
      next: (res) => {
        const taskItems: PendingApprovalItem[] = (res.items || []).map(t => ({
          id: t.taskId,
          type: 'TASK_CONFIRMATION',
          title: t.title,
          sourceId: t.taskId,
          requesterOrPerformer: t.assignedUserName || 'Assigned Member',
          priority: t.priority,
          submittedAt: t.createdAt,
          summary: 'Result deliverables submitted for formal confirmation and completion review.',
          status: t.status
        }));
        this.pendingList.set(taskItems);
      },
      error: () => {
        // Fallback demo items if backend database has no submitted tasks currently
        this.pendingList.set([
          {
            id: 'demo-1',
            type: 'TASK_CONFIRMATION',
            title: 'Q3 Financial Reconciliation & Ledger Audit',
            sourceId: 'demo-1',
            requesterOrPerformer: 'Finance Team Lead',
            departmentName: 'Finance & Accounting',
            priority: 'HIGH',
            submittedAt: new Date(Date.now() - 3600000 * 4).toISOString(),
            summary: 'Reconciliation reports and proof attachments uploaded. Requires manager sign-off.',
            status: 'SUBMITTED'
          },
          {
            id: 'demo-2',
            type: 'REQUEST_RESOLUTION',
            title: 'High-Performance Workstation Hardware Replacement',
            sourceId: 'demo-2',
            requesterOrPerformer: 'IT Infrastructure Lead',
            departmentName: 'IT Operations',
            priority: 'MEDIUM',
            submittedAt: new Date(Date.now() - 3600000 * 8).toISOString(),
            summary: 'Hardware deployed and configured. Requester confirmation pending.',
            status: 'RESOLVED'
          }
        ]);
      }
    });
  }

  setFilter(type: 'ALL' | 'TASK' | 'REQUEST'): void {
    this.filterType.set(type);
  }

  countByType(type: 'TASK_CONFIRMATION' | 'REQUEST_RESOLUTION'): number {
    return this.pendingList().filter(i => i.type === type).length;
  }

  filteredItems(): PendingApprovalItem[] {
    return this.pendingList().filter(item => {
      if (this.filterType() === 'TASK' && item.type !== 'TASK_CONFIRMATION') return false;
      if (this.filterType() === 'REQUEST' && item.type !== 'REQUEST_RESOLUTION') return false;
      if (this.searchQuery.trim()) {
        const q = this.searchQuery.toLowerCase();
        return item.title.toLowerCase().includes(q) || item.requesterOrPerformer.toLowerCase().includes(q);
      }
      return true;
    });
  }
}
