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
        <a routerLink="/workspace">{{ 'common.workspace' | translate: 'Không gian làm việc' }}</a>
        <span aria-hidden="true"> / </span>
        <span>{{ 'slaMonitor.breadcrumb' | translate: 'Giám sát SLA' }}</span>
      </nav>

      <div class="sla-header">
        <div>
          <h1>{{ 'slaMonitor.title' | translate: 'Trung tâm Giám sát SLA & Cảnh báo Leo thang' }}</h1>
          <p class="subtitle">
            {{
              'slaMonitor.subtitle'
                | translate
                  : 'Theo dõi liên tục thời hạn bàn giao, các công việc có nguy cơ vi phạm và ngưỡng leo thang đa cấp độ.'
            }}
          </p>
        </div>
        <div class="header-actions">
          <a mat-stroked-button class="btn-secondary" routerLink="/settings/sla-profiles">
            {{ 'slaMonitor.manageProfiles' | translate: 'Quản lý Hồ sơ SLA →' }}
          </a>
        </div>
      </div>

      <!-- Real-time SLA Gauge & Metrics Grid -->
      <section class="sla-kpi-grid">
        <div class="kpi-card highlight-azure">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.onTimeCompliance' | translate: 'Tỷ lệ Đúng hạn' }}</span>
            <span class="live-beacon"></span>
          </div>
<<<<<<< HEAD
          <div class="kpi-value text-azure">98.6%</div>
          <p class="kpi-meta">{{ 'slaMonitor.rollingDays' | translate: '30 ngày làm việc gần nhất' }}</p>
=======
          <div class="kpi-value text-azure">{{ complianceRate() }}</div>
          <p class="kpi-meta">{{ 'slaMonitor.rollingDays' | translate: 'Last 30 rolling business days' }}</p>
>>>>>>> 167fcb1cf97dda1194ded203001ad13f8beb177d
        </div>

        <div class="kpi-card highlight-danger">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.overdueItems' | translate: 'Hạng mục Quá hạn' }}</span>
            <span class="pulse-beacon"></span>
          </div>
          <div class="kpi-value text-danger">{{ overdueCount() }}</div>
<<<<<<< HEAD
          <p class="kpi-meta">{{ 'slaMonitor.immediateIntervention' | translate: 'Cần can thiệp ngay' }}</p>
=======
          <p class="kpi-meta">{{ 'slaMonitor.immediateIntervention' | translate: 'Cần can thiệp ngay lập tức' }}</p>
>>>>>>> 167fcb1cf97dda1194ded203001ad13f8beb177d
        </div>

        <div class="kpi-card highlight-warning">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.atRisk' | translate: 'Nguy cơ trễ (< 4 Giờ)' }}</span>
            <span class="warning-icon">⚠</span>
          </div>
          <div class="kpi-value text-warning">{{ warningCount() }}</div>
          <p class="kpi-meta">{{ 'slaMonitor.approachingThreshold' | translate: 'Sắp chạm ngưỡng cảnh báo' }}</p>
        </div>

        <div class="kpi-card highlight-neutral">
          <div class="kpi-head">
            <span class="kpi-label">{{ 'slaMonitor.activeCalendar' | translate: 'Lịch Làm việc Hiện tại' }}</span>
            <span class="calendar-icon">📅</span>
          </div>
          <div class="kpi-value text-navy">{{ 'slaMonitor.defaultCalendar' | translate: 'Mặc định 8x5' }}</div>
          <p class="kpi-meta">{{ 'slaMonitor.calendarHours' | translate: 'Thứ 2 – Thứ 6 08:00 – 17:00 (UTC+7)' }}</p>
        </div>
      </section>

      <!-- Active Escalations & At-Risk Items -->
      <section class="directory-panel">
        <div class="panel-header-row">
          <h2>{{ 'slaMonitor.monitoredItems' | translate: 'Hạng mục Theo dõi Cần Chú ý' }}</h2>
          <span class="badge-count">{{ monitoredItems().length }} {{ 'slaMonitor.activeWatch' | translate: 'Đang giám sát' }}</span>
        </div>

        <div class="table-container">
          <table class="sla-table" aria-label="SLA monitored items">
            <thead>
              <tr>
                <th scope="col">TRẠNG THÁI</th>
                <th scope="col">TIÊU ĐỀ CÔNG VIỆC</th>
                <th scope="col">LOẠI HÌNH</th>
                <th scope="col">NGƯỜI XỬ LÝ</th>
                <th scope="col">MỤC TIÊU SLA</th>
                <th scope="col">THỜI GIAN CÒN LẠI</th>
                <th scope="col">CẤP ĐỘ LEO THANG</th>
                <th scope="col">THAO TÁC</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngIf="monitoredItems().length === 0">
                <td colspan="8" style="text-align: center; padding: 2.5rem; color: #64748b;">
                  {{ 'slaMonitor.noBreaches' | translate: 'Hiện không có công việc hoặc yêu cầu nào vi phạm hoặc tiệm cận ngưỡng cảnh báo SLA.' }}
                </td>
              </tr>
              <tr *ngFor="let item of monitoredItems()" [class.row-overdue]="item.state === 'OVERDUE'">
                <td>
                  <span class="status-chip" [attr.data-status]="item.state">
                    <span *ngIf="item.state === 'OVERDUE'" class="beacon-dot"></span>
                    {{ item.state === 'OVERDUE' ? 'QUÁ HẠN' : (item.state === 'WARNING' ? 'CẢNH BÁO' : 'TỐI ƯU') }}
                  </span>
                </td>
                <td>
                  <strong>{{ item.title }}</strong>
                </td>
                <td>
                  <span class="type-badge">{{ item.type === 'TASK' ? 'NHIỆM VỤ' : 'YÊU CẦU' }}</span>
                </td>
                <td>{{ item.assignee }} ({{ item.department }})</td>
                <td>{{ item.slaProfileName }}</td>
                <td>
                  <span [ngClass]="item.state === 'OVERDUE' ? 'time-overdue' : 'time-warning'">
                    {{ item.hoursRemaining < 0 ? 'Quá hạn ' + (item.hoursRemaining * -1) + ' giờ' : 'Còn ' + item.hoursRemaining + ' giờ' }}
                  </span>
                </td>
                <td class="col-escalation">
                  <span class="escalation-badge">{{ item.escalationLevel }}</span>
                </td>
                <td class="col-action">
                  <a *ngIf="item.type === 'TASK'" [routerLink]="['/tasks', item.id]" class="view-link" title="Mở chi tiết nhiệm vụ">
                    <span>{{ 'slaMonitor.openTask' | translate: 'Xem Nhiệm vụ' }}</span>
                    <span class="arrow-icon" aria-hidden="true">→</span>
                  </a>
                  <a *ngIf="item.type === 'REQUEST'" [routerLink]="['/requests', item.id]" class="view-link" title="Mở chi tiết yêu cầu">
                    <span>{{ 'slaMonitor.openRequest' | translate: 'Xem Yêu cầu' }}</span>
                    <span class="arrow-icon" aria-hidden="true">→</span>
                  </a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Escalation Policy Matrix Reference -->
      <section class="directory-panel policy-box">
        <h3>{{ 'slaMonitor.matrixTitle' | translate: 'Ma trận Leo thang Phân cấp' }}</h3>
        <p class="policy-desc">
          {{ 'slaMonitor.policyDesc' | translate: 'Khi một hạng mục công việc đạt đến các ngưỡng SLA tới hạn, hệ thống sẽ tự động kích hoạt thông báo tương ứng và cập nhật quyền hạn xử lý leo thang:' }}
        </p>
        <div class="matrix-grid">
          <div class="matrix-card">
            <span class="tier-tag">{{ 'slaMonitor.tier1Tag' | translate: 'Cấp độ 1 · Đã dùng 80% thời gian' }}</span>
            <h4>{{ 'slaMonitor.tier1Title' | translate: 'Cảnh báo trực tiếp' }}</h4>
            <p>{{ 'slaMonitor.tier1Desc' | translate: 'Gửi thông báo tức thì trong hệ thống tới nhân viên phụ trách và quản lý trực tiếp.' }}</p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-2">{{ 'slaMonitor.tier2Tag' | translate: 'Cấp độ 2 · Quá hạn 100%' }}</span>
            <h4>{{ 'slaMonitor.tier2Title' | translate: 'Leo thang cấp Quản lý' }}</h4>
            <p>{{ 'slaMonitor.tier2Desc' | translate: 'Công việc được gắn cờ QUÁ HẠN. Cấp quyền phân công lại công việc cho Trưởng bộ phận.' }}</p>
          </div>
          <div class="matrix-card">
            <span class="tier-tag tier-3">{{ 'slaMonitor.tier3Tag' | translate: 'Cấp độ 3 · Quá hạn > 24 giờ' }}</span>
            <h4>{{ 'slaMonitor.tier3Title' | translate: 'Cảnh báo khẩn cấp Ban Giám Đốc' }}</h4>
            <p>{{ 'slaMonitor.tier3Desc' | translate: 'Nổi bật trên báo cáo điều hành; đưa vào báo cáo tổng hợp tiến độ hàng ngày.' }}</p>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: [`
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
      &:hover { background: #f8fafd; border-color: #8ec5ff; }
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
        top: 0; left: 0; right: 0;
        height: 3px;
      }
      &.highlight-azure::before { background: #2563eb; }
      &.highlight-danger::before { background: #dc2626; }
      &.highlight-warning::before { background: #d97706; }
      &.highlight-neutral::before { background: #0b2a4a; }
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
    .text-azure { color: #2563eb; }
    .text-danger { color: #dc2626; }
    .text-warning { color: #d97706; }
    .text-navy { color: #0b2a4a; }
    .panel-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.25rem;
      h2 { margin: 0; font-size: 1.2rem; }
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
      border-radius: 8px;
    }
    .sla-table {
      width: 100%;
      min-width: 860px;
      border-collapse: collapse;
      font-size: 0.875rem;

      th, td {
        padding: 0.85rem 1rem;
        text-align: left;
        vertical-align: middle;
      }

      th {
        font-size: 0.75rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #63758b;
        background: #f8fafd;
        border-bottom: 1px solid #dfe8f2;
        white-space: nowrap;
      }

      tbody tr {
        border-bottom: 1px solid #f1f5f9;
        transition: background-color 0.15s ease;
        &:hover {
          background: #f8fafd;
        }
      }

      .col-escalation {
        white-space: nowrap;
        min-width: 190px;
      }

      .col-action {
        white-space: nowrap;
        min-width: 130px;
      }
    }
    .beacon-dot {
      display: none; /* Central status-chip::before handles animated dot and ring */
    }
    .time-overdue { color: #dc2626; font-weight: 700; }
    .time-warning { color: #d97706; font-weight: 600; }
    .type-badge {
      background: #f1f5f9;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .escalation-badge {
      display: inline-block;
      white-space: nowrap;
      background: #fef2f2;
      color: #991b1b;
      padding: 0.25rem 0.65rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
      border: 1px solid #fecaca;
      line-height: 1.25;
      box-shadow: 0 1px 2px rgba(153, 27, 27, 0.05);
    }
    .view-link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      white-space: nowrap;
      font-size: 0.8125rem;
      color: #2563eb;
      font-weight: 600;
      text-decoration: none;
      transition: color 0.15s ease, transform 0.15s ease;

      .arrow-icon {
        display: inline-block;
        transition: transform 0.15s ease;
      }

      &:hover {
        color: #1d4ed8;
        text-decoration: underline;

        .arrow-icon {
          transform: translateX(3px);
        }
      }

      &:focus-visible {
        outline: 2px solid #2563eb;
        outline-offset: 2px;
      }
    }
    .policy-box {
      margin-top: 1.5rem;
      h3 { margin: 0 0 0.5rem; font-size: 1.15rem; color: #071b33; }
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
      h4 { margin: 0.5rem 0 0.25rem; font-size: 0.95rem; color: #0b2a4a; }
      p { margin: 0; font-size: 0.8125rem; color: #63758b; }
    }
    .tier-tag {
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      background: #fef3c7;
      color: #92400e;
      &.tier-2 { background: #fee2e2; color: #991b1b; }
      &.tier-3 { background: #ede9fe; color: #5b21b6; }
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
export class SlaMonitor implements OnInit {
  private readonly http = inject(HttpClient);
  readonly session = inject(SessionService);

  readonly monitoredItems = signal<SlaBreachItem[]>([]);
  readonly overdueCount = signal<number>(0);
  readonly warningCount = signal<number>(0);
  readonly complianceRate = signal<string>('100%');

  ngOnInit(): void {
    // Queries real SLA performance report from the live backend
    this.http.get<any>('/api/v1/reports/sla').subscribe({
      next: (res) => {
        if (typeof res?.complianceRatePercent === 'number') {
          this.complianceRate.set(`${res.complianceRatePercent.toFixed(1)}%`);
        }
      },
      error: () => {},
    });

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
            assignee: t.assignedUserName || 'Chuyên viên xử lý',
            department: 'Phòng ban thực hiện',
            deadline: t.deadline || new Date().toISOString(),
            slaProfileName: 'Cam kết SLA Nội bộ',
            state: 'OVERDUE',
            hoursRemaining: -1,
            escalationLevel: 'Cấp 2 · Báo cáo Trưởng bộ phận',
          }));

          this.monitoredItems.set(liveItems);
          this.overdueCount.set(liveItems.length);
        },
        error: () => {
          this.monitoredItems.set([]);
          this.overdueCount.set(0);
        },
      });
  }
}
