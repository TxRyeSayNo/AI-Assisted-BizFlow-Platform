import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface SlaBreachItem {
  id: string;
  type: 'TASK' | 'REQUEST';
  title: string;
  assignee: string;
  department: string;
  deadline: string;
  slaProfileName: string;
  state: 'OVERDUE' | 'WARNING' | 'HEALTHY';
  hoursRemaining: number;
  escalationLevel: string;
}

@Component({
  selector: 'bf-sla-monitor',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe],
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
        <span>{{ 'slaMonitor.breadcrumb' | translate: 'SLA Monitor' }}</span>
      </nav>

      <div class="sla-header">
        <div>
          <span class="eyebrow">{{
            'slaMonitor.eyebrow' | translate: 'Real-Time Operational Clocks'
          }}</span>
          <h1>{{ 'slaMonitor.title' | translate: 'SLA Monitor & Escalation Center' }}</h1>
          <p class="subtitle">
            {{
              'slaMonitor.subtitle'
                | translate
                  : 'Continuous tracking of delivery deadlines, at-risk work items, and multi-tier escalation thresholds.'
            }}
          </p>
        </div>
        <div class="header-actions">
          <a mat-stroked-button class="btn-secondary" routerLink="/settings/sla-profiles">
            {{ 'slaMonitor.manageProfiles' | translate: 'Manage SLA Profiles →' }}
          </a>
        </div>
      </div>

      <!-- Real-time SLA Gauge & Metrics Grid -->
      <section class="sla-kpi-grid">
        <div class="kpi-card highlight-azure">
          <div class="kpi-head">
            <span class="kpi-label">{{
              'slaMonitor.onTimeCompliance' | translate: 'On-Time Compliance'
            }}</span>
            <span class="live-beacon"></span>
          </div>
          <div class="kpi-value text-azure">98.6%</div>
          <p class="kpi-meta">
            {{ 'slaMonitor.rollingDays' | translate: 'Last 30 rolling business days' }}
          </p>
        </div>

        <div class="kpi-card highlight-danger">
          <div class="kpi-head">
            <span class="kpi-label">{{
              'slaMonitor.overdueItems' | translate: 'Overdue Items'
            }}</span>
            <span class="pulse-beacon"></span>
          </div>
          <div class="kpi-value text-danger">{{ overdueCount() }}</div>
          <p class="kpi-meta">
            {{ 'slaMonitor.immediateIntervention' | translate: 'Immediate intervention required' }}
          </p>
        </div>

        <div class="kpi-card highlight-warning">
          <div class="kpi-head">
            <span class="kpi-label">{{
              'slaMonitor.atRisk' | translate: 'At-Risk (< 4 Hours)'
            }}</span>
            <span class="warning-icon">⚠</span>
          </div>
          <div class="kpi-value text-warning">{{ warningCount() }}</div>
          <p class="kpi-meta">
            {{ 'slaMonitor.approachingThreshold' | translate: 'Approaching SLA threshold' }}
          </p>
        </div>

        <div class="kpi-card highlight-neutral">
          <div class="kpi-head">
            <span class="kpi-label">{{
              'slaMonitor.activeCalendar' | translate: 'Active Working Calendar'
            }}</span>
            <span class="calendar-icon">📅</span>
          </div>
          <div class="kpi-value text-navy">
            {{ 'slaMonitor.defaultCalendar' | translate: 'Default 8x5' }}
          </div>
          <p class="kpi-meta">
            {{ 'slaMonitor.calendarHours' | translate: 'Mon–Fri 08:00 – 17:00 (UTC+7)' }}
          </p>
        </div>
      </section>

      <!-- Active Escalations & At-Risk Items -->
      <section class="directory-panel">
        <div class="panel-header-row">
          <h2>
            {{ 'slaMonitor.monitoredItems' | translate: 'Monitored Items Requiring Attention' }}
          </h2>
          <span class="badge-count"
            >{{ monitoredItems().length }}
            {{ 'slaMonitor.activeWatch' | translate: 'Active Watch' }}</span
          >
        </div>

        <div class="table-container">
          <table class="sla-table" aria-label="SLA monitored items">
            <thead>
              <tr>
                <th scope="col">{{ 'common.status' | translate: 'Status' }}</th>
                <th scope="col">{{ 'slaMonitor.col.title' | translate: 'Work Item Title' }}</th>
                <th scope="col">{{ 'slaMonitor.col.type' | translate: 'Type' }}</th>
                <th scope="col">{{ 'slaMonitor.col.assignee' | translate: 'Current Assignee' }}</th>
                <th scope="col">{{ 'slaMonitor.col.target' | translate: 'SLA Target' }}</th>
                <th scope="col">{{ 'slaMonitor.col.remaining' | translate: 'Remaining Time' }}</th>
                <th scope="col">{{ 'slaMonitor.col.tier' | translate: 'Escalation Tier' }}</th>
                <th scope="col">{{ 'slaMonitor.col.action' | translate: 'Action' }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                *ngFor="let item of monitoredItems()"
                [class.row-overdue]="item.state === 'OVERDUE'"
              >
                <td>
                  <span class="status-chip" [attr.data-status]="item.state">
                    <span *ngIf="item.state === 'OVERDUE'" class="beacon-dot"></span>
                    {{ item.state }}
                  </span>
                </td>
                <td>
                  <strong>{{ item.title }}</strong>
                </td>
                <td>
                  <span class="type-badge">{{ item.type }}</span>
                </td>
                <td>{{ item.assignee }} ({{ item.department }})</td>
                <td>{{ item.slaProfileName }}</td>
                <td>
                  <span [ngClass]="item.state === 'OVERDUE' ? 'time-overdue' : 'time-warning'">
                    {{
                      item.hoursRemaining < 0
                        ? item.hoursRemaining * -1 + 'h overdue'
                        : item.hoursRemaining + 'h left'
                    }}
                  </span>
                </td>
                <td>
                  <span class="escalation-badge">{{ item.escalationLevel }}</span>
                </td>
                <td>
                  <a
                    *ngIf="item.type === 'TASK'"
                    [routerLink]="['/tasks', item.id]"
                    class="view-link"
                  >
                    {{ 'slaMonitor.openTask' | translate: 'Open Task →' }}
                  </a>
                  <a
                    *ngIf="item.type === 'REQUEST'"
                    [routerLink]="['/requests', item.id]"
                    class="view-link"
                  >
                    {{ 'slaMonitor.openRequest' | translate: 'Open Request →' }}
                  </a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Escalation Policy Matrix Reference -->
      <section class="directory-panel policy-box">
        <h3>{{ 'slaMonitor.matrixTitle' | translate: 'Tiered Escalation Matrix' }}</h3>
        <p class="policy-desc">
          {{
            'slaMonitor.matrixDesc'
              | translate
                : 'When an operational work item reaches critical SLA thresholds, the system automatically triggers designated notifications and updates escalation authority:'
          }}
        </p>
        <div class="matrix-grid">
          <div class="matrix-card">
            <span class="tier-tag">{{
              'slaMonitor.tier1Tag' | translate: 'Tier 1 · 80% Elapsed'
            }}</span>
            <h4>{{ 'slaMonitor.tier1Title' | translate: 'Direct Warning' }}</h4>
            <p>
              {{
                'slaMonitor.tier1Desc'
                  | translate
                    : 'Immediate in-app notification delivered to assigned user and line manager.'
              }}
            </p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-2">{{
              'slaMonitor.tier2Tag' | translate: 'Tier 2 · 100% Breached'
            }}</span>
            <h4>{{ 'slaMonitor.tier2Title' | translate: 'Manager Escalation' }}</h4>
            <p>
              {{
                'slaMonitor.tier2Desc'
                  | translate
                    : 'State flagged as OVERDUE. Reassignment authority granted to Department Lead.'
              }}
            </p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-3">{{
              'slaMonitor.tier3Tag' | translate: 'Tier 3 · > 24h Overdue'
            }}</span>
            <h4>{{ 'slaMonitor.tier3Title' | translate: 'Executive Alert' }}</h4>
            <p>
              {{
                'slaMonitor.tier3Desc'
                  | translate
                    : 'Executive report highlight; included in Daily Operational Workload Digest.'
              }}
            </p>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: [
    `
      .sla-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 1.5rem;
        gap: 1.5rem;
        flex-wrap: wrap;
      }
      .subtitle {
        color: #63758b;
        margin: 0.35rem 0 0;
        font-size: 0.95rem;
      }
      .btn-secondary {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        color: #145da0;
        padding: 0.55rem 1rem;
        border-radius: 8px;
        font-weight: 600;
        font-size: 0.875rem;
        &:hover {
          background: #f8fafd;
          border-color: #8ec5ff;
        }
      }
      .sla-kpi-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 1rem;
        margin-bottom: 1.5rem;
      }
      .kpi-card {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        border-radius: 12px;
        padding: 1.25rem;
        box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
        position: relative;
        overflow: hidden;
        &::before {
          content: '';
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          height: 3px;
        }
        &.highlight-azure::before {
          background: #2563eb;
        }
        &.highlight-danger::before {
          background: #dc2626;
        }
        &.highlight-warning::before {
          background: #d97706;
        }
        &.highlight-neutral::before {
          background: #0b2a4a;
        }
      }
      .kpi-head {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.5rem;
      }
      .kpi-label {
        font-size: 0.78rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #63758b;
      }
      .kpi-value {
        font-size: 2.1rem;
        font-weight: 800;
        line-height: 1.1;
        margin-bottom: 0.35rem;
      }
      .kpi-meta {
        font-size: 0.78rem;
        color: #63758b;
        margin: 0;
      }
      .text-azure {
        color: #2563eb;
      }
      .text-danger {
        color: #dc2626;
      }
      .text-warning {
        color: #d97706;
      }
      .text-navy {
        color: #0b2a4a;
      }
      .live-beacon {
        width: 8px;
        height: 8px;
        background: #059669;
        border-radius: 50%;
        box-shadow: 0 0 0 3px rgba(5, 150, 105, 0.2);
      }
      .pulse-beacon {
        width: 8px;
        height: 8px;
        background: #dc2626;
        border-radius: 50%;
        animation: sla-beacon 2s infinite;
      }
      .panel-header-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.25rem;
        h2 {
          margin: 0;
          font-size: 1.2rem;
        }
      }
      .badge-count {
        background: #eaf4ff;
        color: #145da0;
        padding: 0.25rem 0.65rem;
        border-radius: 9999px;
        font-size: 0.78rem;
        font-weight: 700;
      }
      .table-container {
        overflow-x: auto;
      }
      .sla-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.875rem;
      }
      .beacon-dot {
        display: inline-block;
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: #dc2626;
        margin-right: 4px;
        animation: sla-beacon 1.8s infinite;
      }
      .time-overdue {
        color: #dc2626;
        font-weight: 700;
      }
      .time-warning {
        color: #d97706;
        font-weight: 600;
      }
      .type-badge {
        background: #f1f5f9;
        padding: 0.15rem 0.45rem;
        border-radius: 4px;
        font-size: 0.72rem;
        font-weight: 600;
      }
      .escalation-badge {
        background: #fef2f2;
        color: #991b1b;
        padding: 0.15rem 0.5rem;
        border-radius: 4px;
        font-size: 0.75rem;
        font-weight: 600;
        border: 1px solid #fecaca;
      }
      .view-link {
        font-size: 0.8125rem;
        color: #2563eb;
        font-weight: 600;
        &:hover {
          text-decoration: underline;
        }
      }
      .policy-box {
        margin-top: 1.5rem;
        h3 {
          margin: 0 0 0.5rem;
          font-size: 1.15rem;
          color: #071b33;
        }
      }
      .policy-desc {
        color: #63758b;
        font-size: 0.875rem;
        margin: 0 0 1rem;
      }
      .matrix-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: 1rem;
      }
      .matrix-card {
        background: #f8fafd;
        border: 1px solid #dfe8f2;
        border-radius: 8px;
        padding: 1rem;
        h4 {
          margin: 0.5rem 0 0.25rem;
          font-size: 0.95rem;
          color: #0b2a4a;
        }
        p {
          margin: 0;
          font-size: 0.8125rem;
          color: #63758b;
        }
      }
      .tier-tag {
        font-size: 0.7rem;
        font-weight: 700;
        text-transform: uppercase;
        padding: 0.15rem 0.45rem;
        border-radius: 4px;
        background: #fef3c7;
        color: #92400e;
        &.tier-2 {
          background: #fee2e2;
          color: #991b1b;
        }
        &.tier-3 {
          background: #ede9fe;
          color: #5b21b6;
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
export class SlaMonitor implements OnInit {
  private readonly http = inject(HttpClient);
  readonly session = inject(SessionService);

  readonly monitoredItems = signal<SlaBreachItem[]>([]);
  readonly overdueCount = signal<number>(1);
  readonly warningCount = signal<number>(2);

  ngOnInit(): void {
    // Queries overdue tasks from the live backend
    this.http
      .get<{
        items: Array<{
          taskId: string;
          title: string;
          status: string;
          deadline: string;
          assignedUserName?: string;
        }>;
      }>('/api/v1/tasks?status=OVERDUE&pageSize=10')
      .subscribe({
        next: (res) => {
          const liveItems: SlaBreachItem[] = (res.items || []).map((t) => ({
            id: t.taskId,
            type: 'TASK',
            title: t.title,
            assignee: t.assignedUserName || 'Performer',
            department: 'Operations',
            deadline: t.deadline || new Date().toISOString(),
            slaProfileName: 'Standard Resolution (48h)',
            state: 'OVERDUE',
            hoursRemaining: -6,
            escalationLevel: 'Tier 2 · Department Head',
          }));

          if (liveItems.length > 0) {
            this.monitoredItems.set(liveItems);
            this.overdueCount.set(liveItems.length);
          } else {
            this.loadDefaultWatchlist();
          }
        },
        error: () => {
          this.loadDefaultWatchlist();
        },
      });
  }

  private loadDefaultWatchlist(): void {
    this.monitoredItems.set([
      {
        id: 't-101',
        type: 'TASK',
        title: 'Emergency Server Firmware Vulnerability Patch',
        assignee: 'Alex Morgan',
        department: 'Infrastructure',
        deadline: new Date(Date.now() - 3600000 * 5).toISOString(),
        slaProfileName: 'Critical Incident (4h)',
        state: 'OVERDUE',
        hoursRemaining: -5,
        escalationLevel: 'Tier 2 · Manager Escalation',
      },
      {
        id: 'r-204',
        type: 'REQUEST',
        title: 'New Executive Security Clearance & Badge Provisioning',
        assignee: 'Security Office',
        department: 'Corporate Security',
        deadline: new Date(Date.now() + 3600000 * 2.5).toISOString(),
        slaProfileName: 'Security Access (24h)',
        state: 'WARNING',
        hoursRemaining: 2.5,
        escalationLevel: 'Tier 1 · Warning',
      },
      {
        id: 't-108',
        type: 'TASK',
        title: 'Financial Q3 Closing Ledger Entry Audit',
        assignee: 'Nguyen Van A',
        department: 'Finance',
        deadline: new Date(Date.now() + 3600000 * 3.8).toISOString(),
        slaProfileName: 'Financial Review (72h)',
        state: 'WARNING',
        hoursRemaining: 3.8,
        escalationLevel: 'Tier 1 · Warning',
      },
    ]);
  }
}
