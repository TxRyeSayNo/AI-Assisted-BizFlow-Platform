import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface SlaBreachItem {
  id: string;
  type: 'TASK' | 'REQUEST';
  title: string;
  assignee: string;
  department: string | null;
  deadline: string | null;
  deadlineLabel: string;
  slaTargetLabel: string;
  state: 'OVERDUE' | 'WARNING' | 'HEALTHY';
  hoursRemaining: number;
  remainingLabel: string;
  escalationLevel: string;
}

interface SlaReportApiResponse {
  totalTracked: number;
  compliantCount: number;
  breachedCount: number;
  complianceRatePercent: number;
}

interface TaskSummaryApiItem {
  taskId: string;
  title: string;
  status: string;
  priority: string;
  deadline: string | null;
  assignedUserName?: string | null;
  assignedDepartmentName?: string | null;
}

const PRIORITY_LABELS: Record<string, string> = {
  LOW: 'Ưu tiên thấp',
  MEDIUM: 'Ưu tiên trung bình',
  HIGH: 'Ưu tiên cao',
  CRITICAL: 'Ưu tiên khẩn cấp',
};

const WARNING_WINDOW_HOURS = 24;

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
      <nav aria-label="Breadcrumb" class="sla-breadcrumb">
        <a routerLink="/workspace">{{ 'common.workspace' | translate: 'Không gian làm việc' }}</a>
        <span aria-hidden="true"> / </span>
        <span>{{ 'slaMonitor.breadcrumb' | translate: 'Giám sát SLA' }}</span>
      </nav>

      <div class="sla-header">
        <div class="sla-title-group">
          <h1>{{ 'slaMonitor.title' | translate: 'Trung tâm giám sát SLA & leo thang' }}</h1>
          <p class="subtitle">
            {{
              'slaMonitor.subtitle'
                | translate
                  : 'Theo dõi liên tục thời hạn bàn giao, các công việc có nguy cơ vi phạm và ngưỡng leo thang đa cấp độ.'
            }}
          </p>
        </div>
        <div class="header-actions">
          <button
            type="button"
            class="btn-secondary"
            (click)="loadMonitorData()"
            [disabled]="loading()"
          >
            {{
              loading()
                ? ('slaMonitor.refreshing' | translate: 'Đang cập nhật...')
                : ('slaMonitor.refreshTimers' | translate: 'Làm mới dữ liệu')
            }}
          </button>
          @if (session.hasPermission('sla.read')) {
            <a class="btn-primary-link" routerLink="/settings/sla-profiles">
              {{ 'slaMonitor.manageProfiles' | translate: 'Quản lý hồ sơ SLA →' }}
            </a>
          }
        </div>
      </div>

      @if (errorMessage()) {
        <div class="sla-alert" role="alert">
          {{ errorMessage() }}
        </div>
      }

      <!-- Lưới thẻ thống kê SLA thực tế -->
      <section class="sla-kpi-grid" aria-label="Tổng quan chỉ số SLA">
        <article class="kpi-card highlight-azure">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.onTimeCompliance' | translate: 'Tỷ lệ đúng hạn' }}</span>
            <span class="live-beacon" aria-hidden="true"></span>
          </div>
          <div class="kpi-body">
            <div class="kpi-value text-azure" [class.kpi-value-muted]="complianceRate() === null">
              @if (loading() && complianceRate() === null && !reportUnavailable()) {
                Đang tải...
              } @else if (complianceRate() !== null) {
                {{ complianceRate() }}
              } @else {
                Chưa có dữ liệu
              }
            </div>
            <p class="kpi-meta">{{ complianceMeta() }}</p>
          </div>
        </article>

        <article class="kpi-card highlight-danger">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.overdueItems' | translate: 'Mục quá hạn' }}</span>
            <span class="pulse-beacon" aria-hidden="true"></span>
          </div>
          <div class="kpi-body">
            <div class="kpi-value text-danger">
              {{ overdueCount() !== null ? overdueCount() : '—' }}
            </div>
            <p class="kpi-meta">
              @if (overdueCount() === null) {
                {{ 'slaMonitor.dataUnavailable' | translate: 'Không thể tải dữ liệu nhiệm vụ' }}
              } @else if (overdueCount() === 0) {
                {{ 'slaMonitor.noOverdueHint' | translate: 'Không có nhiệm vụ nào bị quá hạn' }}
              } @else {
                {{ 'slaMonitor.immediateIntervention' | translate: 'Cần can thiệp xử lý ngay' }}
              }
            </p>
          </div>
        </article>

        <article class="kpi-card highlight-warning">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.atRisk' | translate: 'Nguy cơ trễ (< 24 giờ)' }}</span>
            <span class="kpi-dot dot-warning" aria-hidden="true"></span>
          </div>
          <div class="kpi-body">
            <div class="kpi-value text-warning">
              {{ warningCount() !== null ? warningCount() : '—' }}
            </div>
            <p class="kpi-meta">
              {{
                'slaMonitor.approachingThreshold'
                  | translate: 'Sắp chạm ngưỡng thời hạn trong 24 giờ tới'
              }}
            </p>
          </div>
        </article>

        <article class="kpi-card highlight-neutral">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.activeCalendar' | translate: 'Lịch làm việc hiện tại' }}</span>
            <span class="kpi-dot dot-neutral" aria-hidden="true"></span>
          </div>
          <div class="kpi-body">
            <div class="kpi-value text-navy kpi-value-text">
              {{ 'slaMonitor.defaultCalendar' | translate: 'Giờ hành chính 8x5' }}
            </div>
            <p class="kpi-meta">
              {{ 'slaMonitor.calendarHours' | translate: 'Thứ 2 – Thứ 6 · 08:00 – 17:00 (UTC+7)' }}
            </p>
          </div>
        </article>
      </section>

      <!-- Danh sách hạng mục cần theo dõi -->
      <section class="directory-panel">
        <div class="panel-header-row">
          <h2>{{ 'slaMonitor.monitoredItems' | translate: 'Danh sách cần chú ý & xử lý' }}</h2>
          <span class="badge-count">
            {{ monitoredItems().length }} {{ 'slaMonitor.activeWatch' | translate: 'hạng mục đang theo dõi' }}
          </span>
        </div>

        <div class="table-container table-scroll">
          <table class="sla-table" aria-label="Danh sách công việc theo dõi SLA">
            <thead>
              <tr>
                <th scope="col">{{ 'slaMonitor.colSlaState' | translate: 'Trạng thái' }}</th>
                <th scope="col">{{ 'slaMonitor.colWorkItem' | translate: 'Tiêu đề công việc' }}</th>
                <th scope="col">{{ 'slaMonitor.colType' | translate: 'Loại hình' }}</th>
                <th scope="col">{{ 'slaMonitor.colOwnerDept' | translate: 'Người xử lý' }}</th>
                <th scope="col">{{ 'slaMonitor.colSlaProfile' | translate: 'Mục tiêu SLA' }}</th>
                <th scope="col">{{ 'slaMonitor.colTimeRemaining' | translate: 'Thời gian còn lại' }}</th>
                <th scope="col">{{ 'slaMonitor.colEscalation' | translate: 'Cấp độ leo thang' }}</th>
                <th scope="col">{{ 'slaMonitor.colAction' | translate: 'Thao tác' }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading() && monitoredItems().length === 0) {
                <tr>
                  <td colspan="8" class="sla-empty-cell">
                    {{ 'slaMonitor.loadingItems' | translate: 'Đang tải danh sách công việc theo dõi SLA...' }}
                  </td>
                </tr>
              } @else if (monitoredItems().length === 0) {
                <tr>
                  <td colspan="8" class="sla-empty-cell">
                    {{
                      'slaMonitor.noBreaches'
                        | translate: 'Hiện không có công việc nào vi phạm hoặc tiệm cận ngưỡng cảnh báo SLA.'
                    }}
                  </td>
                </tr>
              } @else {
                @for (item of monitoredItems(); track item.id) {
                  <tr [class.row-overdue]="item.state === 'OVERDUE'">
                    <td>
                      <span class="status-chip" [attr.data-status]="item.state">
                        {{
                          item.state === 'OVERDUE'
                            ? 'Quá hạn'
                            : item.state === 'WARNING'
                              ? 'Nguy cơ trễ'
                              : 'Đúng tiến độ'
                        }}
                      </span>
                    </td>
                    <td class="col-title">
                      <strong class="item-title">{{ item.title }}</strong>
                      @if (item.deadlineLabel) {
                        <div class="item-submeta">Hạn bàn giao: {{ item.deadlineLabel }}</div>
                      }
                    </td>
                    <td>
                      <span class="type-badge">{{ item.type === 'TASK' ? 'Nhiệm vụ' : 'Yêu cầu' }}</span>
                    </td>
                    <td>
                      <div class="owner-name">{{ item.assignee }}</div>
                      @if (item.department) {
                        <div class="item-submeta">{{ item.department }}</div>
                      }
                    </td>
                    <td>{{ item.slaTargetLabel }}</td>
                    <td>
                      <span [class]="item.state === 'OVERDUE' ? 'time-overdue' : 'time-warning'">
                        {{ item.remainingLabel }}
                      </span>
                    </td>
                    <td class="col-escalation">
                      <span class="escalation-badge" [class.escalation-warning]="item.state === 'WARNING'">
                        {{ item.escalationLevel }}
                      </span>
                    </td>
                    <td class="col-action">
                      @if (item.type === 'TASK') {
                        <a [routerLink]="['/tasks', item.id]" class="view-link">
                          <span>{{ 'slaMonitor.openTask' | translate: 'Xem nhiệm vụ' }}</span>
                          <span class="arrow-icon" aria-hidden="true">→</span>
                        </a>
                      } @else {
                        <a [routerLink]="['/requests', item.id]" class="view-link">
                          <span>{{ 'slaMonitor.openRequest' | translate: 'Xem yêu cầu' }}</span>
                          <span class="arrow-icon" aria-hidden="true">→</span>
                        </a>
                      }
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Ma trận quy tắc leo thang -->
      <section class="directory-panel policy-box">
        <h3>{{ 'slaMonitor.matrixTitle' | translate: 'Ma trận leo thang phân cấp' }}</h3>
        <p class="policy-desc">
          {{
            'slaMonitor.policyDesc'
              | translate
                : 'Khi một hạng mục công việc chạm tới các ngưỡng thời hạn, hệ thống tự động gửi cảnh báo và áp dụng quy tắc leo thang theo cấp độ:'
          }}
        </p>
        <div class="matrix-grid">
          <div class="matrix-card">
            <span class="tier-tag">{{ 'slaMonitor.tier1Tag' | translate: 'Cấp 1 · Sắp chạm hạn' }}</span>
            <h4>{{ 'slaMonitor.tier1Title' | translate: 'Cảnh báo trực tiếp' }}</h4>
            <p>
              {{
                'slaMonitor.tier1Desc'
                  | translate: 'Gửi thông báo tức thì tới nhân viên phụ trách và quản lý trực tiếp.'
              }}
            </p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-2">{{ 'slaMonitor.tier2Tag' | translate: 'Cấp 2 · Quá hạn 100%' }}</span>
            <h4>{{ 'slaMonitor.tier2Title' | translate: 'Leo thang cấp quản lý' }}</h4>
            <p>
              {{
                'slaMonitor.tier2Desc'
                  | translate: 'Công việc được đánh dấu quá hạn để trưởng bộ phận theo dõi và điều phối lại.'
              }}
            </p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-3">{{ 'slaMonitor.tier3Tag' | translate: 'Cấp 3 · Quá hạn > 24 giờ' }}</span>
            <h4>{{ 'slaMonitor.tier3Title' | translate: 'Cảnh báo ban lãnh đạo' }}</h4>
            <p>
              {{
                'slaMonitor.tier3Desc'
                  | translate: 'Hiển thị ưu tiên trên báo cáo điều hành để xử lý các điểm nghẽn kéo dài.'
              }}
            </p>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: [`
    :host {
      display: block;
      min-width: 0;
    }
    .sla-breadcrumb {
      margin-bottom: 0.5rem;
      font-size: 0.875rem;
      color: #63758b;
    }
    .sla-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .sla-title-group {
      min-width: 0;
      flex: 1 1 320px;
    }
    .subtitle {
      color: #63758b;
      margin: 0.35rem 0 0;
      font-size: 0.95rem;
      line-height: 1.5;
    }
    .header-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
    }
    .btn-secondary,
    .btn-primary-link {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 40px;
      padding: 0.55rem 1rem;
      border-radius: 10px;
      font-weight: 600;
      font-size: 0.875rem;
      text-decoration: none;
      cursor: pointer;
      white-space: nowrap;
      transition: background-color 0.15s ease, border-color 0.15s ease;
    }
    .btn-secondary {
      background: #ffffff;
      border: 1px solid #d5e1ef;
      color: #0f3d75;
    }
    .btn-secondary:hover:not(:disabled) {
      background: #f8fafd;
      border-color: #8ec5ff;
    }
    .btn-secondary:disabled {
      opacity: 0.65;
      cursor: wait;
    }
    .btn-primary-link {
      background: #eaf4ff;
      border: 1px solid #bfdbfe;
      color: #0b4f9c;
    }
    .btn-primary-link:hover {
      background: #dbeafe;
      border-color: #93c5fd;
    }
    .sla-alert {
      margin-bottom: 1.25rem;
      padding: 0.85rem 1rem;
      border-radius: 10px;
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
      font-size: 0.875rem;
    }
    .sla-kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 1rem;
      margin-bottom: 1.5rem;
      width: 100%;
    }
    @media (max-width: 1120px) {
      .sla-kpi-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
    @media (max-width: 600px) {
      .sla-kpi-grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .kpi-card {
      min-width: 0;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 0.75rem;
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 14px;
      padding: 1.25rem;
      box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
      position: relative;
      overflow: hidden;
    }
    .kpi-card::before {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      height: 3px;
    }
    .kpi-card.highlight-azure::before { background: #2563eb; }
    .kpi-card.highlight-danger::before { background: #dc2626; }
    .kpi-card.highlight-warning::before { background: #d97706; }
    .kpi-card.highlight-neutral::before { background: #0b2a4a; }
    .kpi-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
    }
    .kpi-label {
      font-size: 0.8125rem;
      font-weight: 700;
      color: #4f637a;
      line-height: 1.3;
      overflow-wrap: anywhere;
    }
    .kpi-body {
      min-width: 0;
    }
    .kpi-value {
      font-size: clamp(1.45rem, 2.2vw, 2.05rem);
      font-weight: 800;
      line-height: 1.15;
      margin-bottom: 0.35rem;
      overflow-wrap: anywhere;
    }
    .kpi-value-text {
      font-size: clamp(1.25rem, 1.8vw, 1.55rem);
    }
    .kpi-value-muted {
      font-size: 1.25rem;
      color: #63758b;
      font-weight: 700;
    }
    .kpi-meta {
      font-size: 0.8125rem;
      color: #63758b;
      margin: 0;
      line-height: 1.4;
      overflow-wrap: anywhere;
    }
    .kpi-dot,
    .live-beacon,
    .pulse-beacon {
      width: 10px;
      height: 10px;
      border-radius: 9999px;
      flex: 0 0 auto;
    }
    .live-beacon {
      background: #10b981;
      box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.16);
    }
    .pulse-beacon {
      background: #dc2626;
      box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.14);
    }
    .dot-warning {
      background: #d97706;
      box-shadow: 0 0 0 3px rgba(217, 119, 6, 0.16);
    }
    .dot-neutral {
      background: #0b2a4a;
      box-shadow: 0 0 0 3px rgba(11, 42, 74, 0.12);
    }
    .text-azure { color: #1d4ed8; }
    .text-danger { color: #dc2626; }
    .text-warning { color: #b45309; }
    .text-navy { color: #0b2a4a; }
    .panel-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-bottom: 1.15rem;
    }
    .panel-header-row h2 {
      margin: 0;
      font-size: 1.15rem;
    }
    .badge-count {
      background: #eaf4ff;
      color: #0b4f9c;
      padding: 0.3rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .table-container {
      width: 100%;
      overflow-x: auto;
      border-radius: 10px;
      border: 1px solid #e5edf5;
    }
    .sla-table {
      width: 100%;
      min-width: 900px;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .sla-table th,
    .sla-table td {
      padding: 0.85rem 1rem;
      text-align: left;
      vertical-align: middle;
    }
    .sla-table th {
      font-size: 0.78rem;
      font-weight: 700;
      color: #4f637a;
      background: #f8fafd;
      border-bottom: 1px solid #dfe8f2;
      white-space: nowrap;
    }
    .sla-table tbody tr {
      border-bottom: 1px solid #f1f5f9;
      transition: background-color 0.15s ease;
    }
    .sla-table tbody tr:hover {
      background: #f8fafd;
    }
    .sla-table tbody tr.row-overdue {
      background: rgba(254, 242, 242, 0.35);
    }
    .sla-empty-cell {
      text-align: center;
      padding: 2.25rem 1rem;
      color: #63758b;
    }
    .col-title {
      min-width: 220px;
    }
    .item-title {
      color: #071b33;
      line-height: 1.4;
      display: block;
    }
    .item-submeta {
      margin-top: 0.2rem;
      font-size: 0.78rem;
      color: #63758b;
    }
    .owner-name {
      font-weight: 600;
      color: #1e293b;
    }
    .col-escalation {
      white-space: nowrap;
      min-width: 195px;
    }
    .col-action {
      white-space: nowrap;
      min-width: 130px;
    }
    .time-overdue {
      color: #dc2626;
      font-weight: 700;
      white-space: nowrap;
    }
    .time-warning {
      color: #b45309;
      font-weight: 600;
      white-space: nowrap;
    }
    .type-badge {
      display: inline-block;
      white-space: nowrap;
      background: #f1f5f9;
      color: #334155;
      padding: 0.2rem 0.55rem;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .escalation-badge {
      display: inline-block;
      white-space: nowrap;
      background: #fef2f2;
      color: #991b1b;
      padding: 0.28rem 0.7rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
      border: 1px solid #fecaca;
      line-height: 1.3;
    }
    .escalation-badge.escalation-warning {
      background: #fffbeb;
      color: #92400e;
      border-color: #fde68a;
    }
    .view-link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      white-space: nowrap;
      font-size: 0.84rem;
      color: #1d4ed8;
      font-weight: 600;
      text-decoration: none;
    }
    .view-link:hover {
      color: #1e40af;
      text-decoration: underline;
    }
    .view-link .arrow-icon {
      display: inline-block;
      transition: transform 0.15s ease;
    }
    .view-link:hover .arrow-icon {
      transform: translateX(3px);
    }
    .policy-box {
      margin-top: 1.5rem;
    }
    .policy-box h3 {
      margin: 0 0 0.45rem;
      font-size: 1.125rem;
      color: #071b33;
    }
    .policy-desc {
      color: #63758b;
      font-size: 0.875rem;
      margin: 0 0 1rem;
      line-height: 1.5;
    }
    .matrix-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 1rem;
    }
    @media (max-width: 900px) {
      .matrix-grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .matrix-card {
      background: #f8fafd;
      border: 1px solid #dfe8f2;
      border-radius: 10px;
      padding: 1rem 1.1rem;
      min-width: 0;
    }
    .matrix-card h4 {
      margin: 0.55rem 0 0.3rem;
      font-size: 0.95rem;
      color: #0b2a4a;
    }
    .matrix-card p {
      margin: 0;
      font-size: 0.84rem;
      color: #526478;
      line-height: 1.5;
    }
    .tier-tag {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      padding: 0.2rem 0.55rem;
      border-radius: 6px;
      background: #fef3c7;
      color: #92400e;
    }
    .tier-tag.tier-2 {
      background: #fee2e2;
      color: #991b1b;
    }
    .tier-tag.tier-3 {
      background: #ede9fe;
      color: #5b21b6;
    }
    .tenant-badge {
      background: #eaf4ff;
      color: #145da0;
      border: 1px solid #bfdbfe;
      padding: 0.2rem 0.65rem;
      border-radius: 9999px;
      font-size: 0.8125rem;
      font-weight: 600;
      white-space: nowrap;
    }
  `],
})
export class SlaMonitor implements OnInit {
  private readonly http = inject(HttpClient);
  readonly session = inject(SessionService);

  readonly loading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly reportUnavailable = signal<boolean>(false);
  readonly monitoredItems = signal<SlaBreachItem[]>([]);
  readonly overdueCount = signal<number | null>(null);
  readonly warningCount = signal<number | null>(null);
  readonly complianceRate = signal<string | null>(null);
  readonly complianceMeta = signal<string>('Đang tổng hợp dữ liệu SLA 30 ngày gần nhất...');

  ngOnInit(): void {
    this.loadMonitorData();
  }

  loadMonitorData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.reportUnavailable.set(false);

    let slaFailed = false;
    let tasksFailed = false;

    forkJoin({
      slaReport: this.http.get<SlaReportApiResponse>('/api/v1/reports/sla').pipe(
        catchError(() => {
          slaFailed = true;
          return of(null);
        }),
      ),
      tasksPage: this.http
        .get<{ items: TaskSummaryApiItem[] }>('/api/v1/tasks?page=1&pageSize=100')
        .pipe(
          catchError(() => {
            tasksFailed = true;
            return of(null);
          }),
        ),
    }).subscribe(({ slaReport, tasksPage }) => {
      this.loading.set(false);

      if (slaReport && typeof slaReport.totalTracked === 'number') {
        if (slaReport.totalTracked > 0 && typeof slaReport.complianceRatePercent === 'number') {
          this.complianceRate.set(`${slaReport.complianceRatePercent.toFixed(1)}%`);
          this.complianceMeta.set(
            `Đạt chuẩn ${slaReport.compliantCount}/${slaReport.totalTracked} yêu cầu · 30 ngày gần nhất`,
          );
        } else {
          this.complianceRate.set(null);
          this.complianceMeta.set('Chưa có hồ sơ SLA phát sinh trong 30 ngày gần nhất');
        }
      } else {
        this.reportUnavailable.set(true);
        this.complianceRate.set(null);
        this.complianceMeta.set(
          slaFailed
            ? 'Không có quyền xem thống kê hoặc máy chủ báo cáo chưa sẵn sàng'
            : 'Chưa có dữ liệu thống kê SLA',
        );
      }

      if (tasksPage && Array.isArray(tasksPage.items)) {
        const nowMs = Date.now();
        const items: SlaBreachItem[] = [];

        for (const task of tasksPage.items) {
          if (task.status === 'COMPLETED' || task.status === 'CANCELLED' || task.status === 'DRAFT') {
            continue;
          }

          const deadlineMs = task.deadline ? Date.parse(task.deadline) : Number.NaN;
          const hasValidDeadline = !Number.isNaN(deadlineMs);
          const hoursRemaining = hasValidDeadline ? (deadlineMs - nowMs) / 3_600_000 : Number.POSITIVE_INFINITY;
          const isOverdue = task.status === 'OVERDUE' || (hasValidDeadline && hoursRemaining < 0);
          const isWarning = !isOverdue && hasValidDeadline && hoursRemaining <= WARNING_WINDOW_HOURS;

          if (!isOverdue && !isWarning) {
            continue;
          }

          const priorityText = PRIORITY_LABELS[task.priority] ?? 'Ưu tiên trung bình';
          const deadlineLabel = hasValidDeadline
            ? new Intl.DateTimeFormat('vi-VN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              }).format(deadlineMs)
            : '';

          let escalationLevel: string;
          if (isOverdue) {
            escalationLevel =
              hoursRemaining <= -24
                ? 'Cấp 3 · Cảnh báo ban lãnh đạo'
                : 'Cấp 2 · Leo thang cấp quản lý';
          } else {
            escalationLevel = 'Cấp 1 · Sắp chạm ngưỡng hạn';
          }

          items.push({
            id: task.taskId,
            type: 'TASK',
            title: task.title,
            assignee: task.assignedUserName || 'Chưa phân công',
            department: task.assignedDepartmentName || null,
            deadline: task.deadline,
            deadlineLabel,
            slaTargetLabel: priorityText,
            state: isOverdue ? 'OVERDUE' : 'WARNING',
            hoursRemaining,
            remainingLabel: this.formatRemainingDuration(hoursRemaining, isOverdue),
            escalationLevel,
          });
        }

        items.sort((a, b) => a.hoursRemaining - b.hoursRemaining);
        this.monitoredItems.set(items);
        this.overdueCount.set(items.filter((i) => i.state === 'OVERDUE').length);
        this.warningCount.set(items.filter((i) => i.state === 'WARNING').length);
      } else {
        this.monitoredItems.set([]);
        this.overdueCount.set(null);
        this.warningCount.set(null);
        if (tasksFailed) {
          this.errorMessage.set('Không thể tải danh sách công việc theo dõi SLA từ máy chủ.');
        }
      }
    });
  }

  private formatRemainingDuration(hoursRemaining: number, isOverdue: boolean): string {
    if (!Number.isFinite(hoursRemaining)) {
      return isOverdue ? 'Đã quá hạn' : 'Chưa thiết lập hạn';
    }

    const totalMinutes = Math.max(1, Math.round(Math.abs(hoursRemaining) * 60));
    const days = Math.floor(totalMinutes / (60 * 24));
    const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
    const minutes = totalMinutes % 60;

    let spanText: string;
    if (days > 0) {
      spanText = hours > 0 ? `${days} ngày ${hours} giờ` : `${days} ngày`;
    } else if (hours > 0) {
      spanText = `${hours} giờ`;
    } else {
      spanText = `${minutes} phút`;
    }

    return hoursRemaining < 0 || isOverdue ? `Quá hạn ${spanText}` : `Còn ${spanText}`;
  }
}
