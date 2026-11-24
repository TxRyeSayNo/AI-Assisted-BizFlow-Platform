import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NotificationCountService } from '../../core/notifications/notification-count.service';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SessionService } from '../../core/auth/session';
import { AiAgentService } from '../../core/ai/ai-agent.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface DashboardTaskItem {
  taskId: string;
  title: string;
  status: string;
  priority: string;
  deadline?: string | null;
  assignedDepartmentName?: string | null;
  assignedUserName?: string | null;
  accountableManagerName?: string | null;
  progressPercent: number;
  subtaskCount: number;
  completedSubtaskCount: number;
  inProgressSubtaskCount: number;
  overdueSubtaskCount: number;
  participantCount: number;
}

interface DashboardRequestItem {
  requestId: string;
  title: string;
  status: string;
  priority: string;
  serviceName?: string | null;
  requesterName?: string | null;
  requesterDepartmentName?: string | null;
  currentRoutingDepartmentName?: string | null;
  currentRoutingUserName?: string | null;
  escalationState?: string | null;
  escalatedByName?: string | null;
  createdAt: string;
}

@Component({
  selector: 'bf-workspace',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslatePipe],
  template: `
    <!-- Top Frosted Ocean Header -->
    <header class="public-header workspace-nav">
      <div class="nav-brand-section">
        <a class="wordmark" routerLink="/workspace">
          <span class="brand-mark" aria-hidden="true"></span> BizFlow
        </a>
        <span class="tenant-badge">
          {{
            identity()?.tenantName ??
              ('common.platformAdmin' | translate: 'Quản trị Nền tảng Hệ thống')
          }}
        </span>
      </div>

      <!-- Quick Action Navigation -->
      <nav class="nav-links" aria-label="Main application">
        @if (identity()?.tenantId) {
          @if (
            session.hasPermission('tasks.read.tenant') ||
            session.hasPermission('tasks.read.managed') ||
            session.hasPermission('tasks.read.own') ||
            session.hasPermission('tasks.read.assigned')
          ) {
            <a routerLink="/tasks" class="nav-link">{{ 'nav.tasks' | translate: 'Tasks' }}</a>
          }
          @if (
            session.hasPermission('requests.read.tenant') ||
            session.hasPermission('requests.read.managed') ||
            session.hasPermission('requests.read.own')
          ) {
            <a routerLink="/requests" class="nav-link">{{
              'nav.requests' | translate: 'Requests'
            }}</a>
          }
          @if (
            session.hasPermission('tasks.confirm') ||
            session.hasPermission('tasks.read.managed') ||
            session.hasPermission('roles.configure')
          ) {
            <a routerLink="/approvals" class="nav-link">{{
              'nav.approvals' | translate: 'Approvals'
            }}</a>
          }
          @if (
            session.hasPermission('tasks.read.tenant') ||
            session.hasPermission('tasks.read.managed') ||
            session.hasPermission('tasks.read.own') ||
            session.hasPermission('tasks.read.assigned')
          ) {
            <a routerLink="/dashboard/projects" class="nav-link">{{
              'projectDashboard.breadcrumb' | translate: 'Dashboard dự án'
            }}</a>
          }
          @if (
            session.hasPermission('reports.manager') ||
            session.hasPermission('reports.company') ||
            session.hasPermission('reports.workload') ||
            session.hasPermission('reports.sla')
          ) {
            <a routerLink="/reports" class="nav-link">{{
              'reports.breadcrumb' | translate: 'Reports'
            }}</a>
          }
          @if (
            session.hasPermission('sla.read') ||
            session.hasPermission('reports.sla') ||
            session.hasPermission('tasks.read.managed') ||
            session.hasPermission('roles.configure')
          ) {
            <a routerLink="/sla-monitor" class="nav-link">{{
              'nav.slaMonitor' | translate: 'SLA Monitor'
            }}</a>
          }
          @if (session.hasPermission('records.search')) {
            <a routerLink="/records/search" class="nav-link">{{
              'common.search' | translate: 'Search'
            }}</a>
          }
          @if (
            session.hasPermission('roles.configure') ||
            session.hasPermission('users.read') ||
            session.hasPermission('departments.read') ||
            session.hasPermission('workflows.configure') ||
            session.hasPermission('sla.configure') ||
            session.hasPermission('service.create')
          ) {
            <a routerLink="/settings" class="nav-link">{{
              'nav.settings' | translate: 'Settings'
            }}</a>
          }
        } @else {
          <a routerLink="/platform/companies" class="nav-link">{{
            'nav.companyRegistrations' | translate: 'Doanh nghiệp'
          }}</a>
          <a routerLink="/platform/audit" class="nav-link">{{
            'nav.platformAudit' | translate: 'Kiểm toán Nền tảng'
          }}</a>
        }
      </nav>

      <!-- Right Utility Bar -->
      <div class="nav-utilities">
        <a routerLink="/notifications" class="notify-btn" title="Thông báo">
          🔔
          <span class="unread-dot"></span>
        </a>

        <div class="user-chip">
          @if (profilePhotoUrl() && !avatarFailed()) {
            <img
              [src]="profilePhotoUrl()"
              [alt]="identity()?.fullName || 'User avatar'"
              class="user-avatar-img"
              (error)="onAvatarError()"
            />
          } @else {
            <span class="user-avatar">{{ userInitials() }}</span>
          }
          <span class="user-name">{{ identity()?.fullName }}</span>
        </div>

        <button type="button" class="btn-signout" (click)="signOut()" title="Đăng xuất">
          {{ 'nav.signOut' | translate: 'Đăng xuất' }}
        </button>
      </div>
    </header>

    <!-- Main Workspace Dashboard Canvas -->
    <main id="main-content" class="page-content workspace-main" tabindex="-1">
      <!-- Hero Command Banner -->
      <section class="hero-command-card">
        <div class="hero-bg-mesh" aria-hidden="true"></div>
        <div class="hero-content">
          <div class="hero-badges">
            <span class="role-chip">{{ currentRoleLabel() }}</span>
          </div>

          <h1>{{ 'common.welcome' | translate: 'Xin chào' }} {{ welcomeName() }}</h1>
        </div>
      </section>

      <!-- KPI Executive Metrics Row (Positioned directly under welcome banner) -->
      @if (kpiData(); as kpi) {
        <section class="kpi-metric-row">
          <div class="kpi-box azure-accent">
            <div class="kpi-top">
              <span class="kpi-title">{{ kpi.box1.title }}</span>
              <span class="kpi-badge">{{ kpi.box1.badge }}</span>
            </div>
            <div class="kpi-metric">{{ kpi.box1.metric }}</div>
            <p class="kpi-foot">
              <a [routerLink]="kpi.box1.link">{{ kpi.box1.label }}</a>
            </p>
          </div>

          <div class="kpi-box ocean-accent">
            <div class="kpi-top">
              <span class="kpi-title">{{ kpi.box2.title }}</span>
              <span class="kpi-badge">{{ kpi.box2.badge }}</span>
            </div>
            <div class="kpi-metric">{{ kpi.box2.metric }}</div>
            <p class="kpi-foot">
              <a [routerLink]="kpi.box2.link">{{ kpi.box2.label }}</a>
            </p>
          </div>

          <div class="kpi-box green-accent">
            <div class="kpi-top">
              <span class="kpi-title">{{ kpi.box3.title }}</span>
              <span class="kpi-badge green">{{ kpi.box3.badge }}</span>
            </div>
            <div class="kpi-metric">{{ kpi.box3.metric }}</div>
            <p class="kpi-foot">
              <a [routerLink]="kpi.box3.link">{{ kpi.box3.label }}</a>
            </p>
          </div>

          <div class="kpi-box navy-accent">
            <div class="kpi-top">
              <span class="kpi-title">{{ kpi.box4.title }}</span>
              <span class="kpi-badge">{{ kpi.box4.badge }}</span>
            </div>
            <div class="kpi-metric">{{ kpi.box4.metric }}</div>
            <p class="kpi-foot">
              <a [routerLink]="kpi.box4.link">{{ kpi.box4.label }}</a>
            </p>
          </div>
        </section>
      }

      <!-- ROLE-SPECIFIC OPERATIONAL DASHBOARD: Nhiệm vụ (trên) & Yêu cầu (dưới) - Horizontal cards -->
      @if (identity()?.tenantId) {
        <section class="role-operational-dashboard" [attr.data-role]="userRole()">
          <!-- COMPANY ADMINISTRATOR DASHBOARD -->
          @if (userRole() === 'COMPANY_ADMIN') {
            <div class="op-stacked-layout">
              <!-- Section 1 (TOP) — Main assignments / tasks in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">🎯</span>
                    <div>
                      <h3>Nhiệm vụ trọng tâm toàn công ty (Đã giao việc)</h3>
                      <p class="op-subtitle">Các nhiệm vụ đang thực hiện, được phân công theo dõi hoặc cận hạn hoàn thành</p>
                    </div>
                  </div>
                  <a routerLink="/tasks" class="op-header-link">Xem tất cả nhiệm vụ →</a>
                </div>

                <div class="op-card-body">
                  @if (adminUrgentTasks().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Không có nhiệm vụ nào đang được giao thực hiện.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (task of adminUrgentTasks(); track task.taskId) {
                        <div class="op-task-card">
                          <div class="op-card-top-row">
                            <span class="priority-chip" [attr.data-priority]="task.priority">
                              {{ formatPriorityLabel(task.priority) }}
                            </span>
                            <span class="status-badge" [attr.data-status]="task.status">
                              {{ formatStatusLabel(task.status) }}
                            </span>
                            @if (isOverdue(task.deadline, task.status)) {
                              <span class="badge-overdue">⚠️ Quá hạn</span>
                            }
                          </div>
                          <a [routerLink]="['/tasks', task.taskId]" class="op-card-title">
                            {{ task.title }}
                          </a>
                          <div class="op-card-meta">
                            <span>🏢 {{ task.assignedDepartmentName || 'Toàn công ty' }}</span>
                            <span>👤 Quản lý: <strong>{{ task.accountableManagerName || 'Chưa chỉ định' }}</strong></span>
                            @if (task.assignedUserName) {
                              <span>👷 Thực hiện: <strong>{{ task.assignedUserName }}</strong></span>
                            }
                            @if (task.deadline) {
                              <span [class.text-danger]="isOverdue(task.deadline, task.status)">
                                📅 Hạn: {{ task.deadline | date: 'dd/MM/yyyy' }}
                              </span>
                            }
                          </div>
                          <div class="op-card-footer">
                            <div class="op-progress-bar-wrap">
                              <div class="op-progress-bar">
                                <div class="op-progress-fill" [style.width.%]="task.progressPercent"></div>
                              </div>
                              <span class="op-progress-val">{{ task.progressPercent }}%</span>
                            </div>
                            <span class="op-progress-sub">Tiến độ</span>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>

              <!-- Section 2 (BOTTOM) — Requests in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">📨</span>
                    <div>
                      <h3>Yêu cầu cần xử lý & Theo dõi</h3>
                      <p class="op-subtitle">Theo dõi tình trạng xem xét của Quản lý và các yêu cầu đã leo thang</p>
                    </div>
                  </div>
                  <a routerLink="/requests" class="op-header-link">Xem tất cả yêu cầu →</a>
                </div>

                <div class="op-card-body">
                  @if (adminSubordinateRequests().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Hiện không có yêu cầu nào đang chờ xử lý.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (req of adminSubordinateRequests(); track req.requestId) {
                        <div class="op-request-card" [class.is-escalated]="req.escalationState === 'ESCALATED'">
                          <div class="op-card-top-row">
                            <span class="status-badge" [attr.data-status]="req.status">
                              {{ formatStatusLabel(req.status) }}
                            </span>
                            @if (req.escalationState === 'ESCALATED') {
                              <span class="badge-escalated">🔥 Đã leo thang</span>
                            }
                          </div>
                          <a [routerLink]="['/requests', req.requestId]" class="op-card-title">
                            {{ req.title }}
                          </a>
                          <div class="op-card-meta">
                            <span>👤 Người gửi: {{ req.requesterName || 'Người dùng' }}</span>
                            @if (req.requesterDepartmentName) {
                              <span>🏢 {{ req.requesterDepartmentName }}</span>
                            }
                            <span>🕒 {{ req.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                          </div>
                          <div class="op-card-footer">
                            <span class="op-manager-status-chip" [ngClass]="getAdminRequestReviewStage(req).class">
                              {{ getAdminRequestReviewStage(req).label }}
                            </span>
                            <a [routerLink]="['/requests', req.requestId]" class="op-view-link">
                              Chi tiết →
                            </a>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- MANAGER DASHBOARD -->
          @if (userRole() === 'MANAGER') {
            <div class="op-stacked-layout">
              <!-- Section 1 (TOP) — Department tasks and assignments in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">📊</span>
                    <div>
                      <h3>Nhiệm vụ & Tiến độ phòng ban (Đã giao việc)</h3>
                      <p class="op-subtitle">Tổng quan hoàn thành và các nhiệm vụ đang thực hiện của phòng ban</p>
                    </div>
                  </div>
                  <a routerLink="/tasks" class="op-header-link">Quản lý nhiệm vụ →</a>
                </div>

                <div class="op-card-body">
                  <!-- Department aggregated overview stats -->
                  <div class="op-dept-stats-row">
                    <div class="op-stat-pill">
                      <span class="op-stat-num">{{ managerDeptStats().avgProgress }}%</span>
                      <span class="op-stat-lbl">Hoàn thành phòng ban</span>
                    </div>
                    <div class="op-stat-pill">
                      <span class="op-stat-num">{{ managerDeptStats().assignedEmployeesCount }}</span>
                      <span class="op-stat-lbl">Nhân sự tham gia</span>
                    </div>
                    <div class="op-stat-pill">
                      <span class="op-stat-num">{{ managerDeptStats().completedChildTasks }}</span>
                      <span class="op-stat-lbl">Việc con đã xong</span>
                    </div>
                    <div class="op-stat-pill">
                      <span class="op-stat-num text-azure">{{ managerDeptStats().inProgressCount }}</span>
                      <span class="op-stat-lbl">Đang tiến hành</span>
                    </div>
                    <div class="op-stat-pill">
                      <span class="op-stat-num" [class.text-danger]="managerDeptStats().overdueCount > 0">
                        {{ managerDeptStats().overdueCount }}
                      </span>
                      <span class="op-stat-lbl">Việc quá hạn</span>
                    </div>
                  </div>

                  @if (managerUrgentTasks().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Không có nhiệm vụ phòng ban nào đang thực hiện.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (task of managerUrgentTasks(); track task.taskId) {
                        <div class="op-task-card">
                          <div class="op-card-top-row">
                            <span class="priority-chip" [attr.data-priority]="task.priority">
                              {{ formatPriorityLabel(task.priority) }}
                            </span>
                            <span class="status-badge" [attr.data-status]="task.status">
                              {{ formatStatusLabel(task.status) }}
                            </span>
                            @if (isOverdue(task.deadline, task.status)) {
                              <span class="badge-overdue">⚠️ Quá hạn</span>
                            }
                          </div>
                          <a [routerLink]="['/tasks', task.taskId]" class="op-card-title">
                            {{ task.title }}
                          </a>
                          <div class="op-card-meta">
                            <span>👤 Phụ trách: <strong>{{ task.assignedUserName || 'Chưa giao cụ thể' }}</strong></span>
                            @if (task.subtaskCount > 0) {
                              <span>📋 Việc con: {{ task.completedSubtaskCount }}/{{ task.subtaskCount }} hoàn tất</span>
                            }
                            @if (task.deadline) {
                              <span [class.text-danger]="isOverdue(task.deadline, task.status)">
                                📅 Hạn: {{ task.deadline | date: 'dd/MM/yyyy' }}
                              </span>
                            }
                          </div>
                          <div class="op-card-footer">
                            <div class="op-progress-bar-wrap">
                              <div class="op-progress-bar">
                                <div class="op-progress-fill" [style.width.%]="task.progressPercent"></div>
                              </div>
                              <span class="op-progress-val">{{ task.progressPercent }}%</span>
                            </div>
                            <span class="op-progress-sub">Tiến độ</span>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>

              <!-- Section 2 (BOTTOM) — Employee Requests in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">📨</span>
                    <div>
                      <h3>Yêu cầu từ nhân viên phòng ban</h3>
                      <p class="op-subtitle">Theo dõi tình trạng tiếp nhận, xem xét bước 1 và leo thang</p>
                    </div>
                  </div>
                  <a routerLink="/requests" class="op-header-link">Xem danh sách yêu cầu →</a>
                </div>

                <div class="op-card-body">
                  @if (managerDeptRequests().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Hiện không có yêu cầu nào từ nhân viên trong phòng ban.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (req of managerDeptRequests(); track req.requestId) {
                        <div class="op-request-card" [class.is-escalated]="req.escalationState === 'ESCALATED'">
                          <div class="op-card-top-row">
                            <span class="status-badge" [attr.data-status]="req.status">
                              {{ formatStatusLabel(req.status) }}
                            </span>
                            @if (req.escalationState === 'ESCALATED') {
                              <span class="badge-escalated">🔥 Đã leo thang</span>
                            }
                          </div>
                          <a [routerLink]="['/requests', req.requestId]" class="op-card-title">
                            {{ req.title }}
                          </a>
                          <div class="op-card-meta">
                            <span>👤 Nhân viên gửi: {{ req.requesterName || 'Nhân viên' }}</span>
                            <span>🕒 {{ req.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                          </div>
                          <div class="op-card-footer">
                            <span class="op-manager-status-chip" [ngClass]="getManagerRequestReviewStage(req).class">
                              {{ getManagerRequestReviewStage(req).label }}
                            </span>
                            <a [routerLink]="['/requests', req.requestId]" class="op-review-btn">
                              Xử lý yêu cầu →
                            </a>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>
            </div>
          }

          <!-- EMPLOYEE DASHBOARD -->
          @if (userRole() === 'EMPLOYEE') {
            <div class="op-stacked-layout">
              <!-- Section 1 (TOP) — My assigned tasks in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">📝</span>
                    <div>
                      <h3>Nhiệm vụ được giao & Cần xử lý</h3>
                      <p class="op-subtitle">Danh sách nhiệm vụ đang thực hiện được giao cho bạn kèm hạn và báo cáo kết quả</p>
                    </div>
                  </div>
                  <a routerLink="/tasks" class="op-header-link">Xem tất cả việc của tôi →</a>
                </div>

                <div class="op-card-body">
                  @if (employeeUrgentTasks().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Bạn không có nhiệm vụ nào đang cần xử lý.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (task of employeeUrgentTasks(); track task.taskId) {
                        <div class="op-task-card">
                          <div class="op-card-top-row">
                            <span class="priority-chip" [attr.data-priority]="task.priority">
                              {{ formatPriorityLabel(task.priority) }}
                            </span>
                            <span class="status-badge" [attr.data-status]="task.status">
                              {{ formatStatusLabel(task.status) }}
                            </span>
                            @if (isOverdue(task.deadline, task.status)) {
                              <span class="badge-overdue">⚠️ Quá hạn</span>
                            }
                          </div>
                          <a [routerLink]="['/tasks', task.taskId]" class="op-card-title">
                            {{ task.title }}
                          </a>
                          <div class="op-card-meta">
                            @if (task.deadline) {
                              <span [class.text-danger]="isOverdue(task.deadline, task.status)">
                                📅 Hạn chót: {{ task.deadline | date: 'dd/MM/yyyy' }}
                              </span>
                            }
                            <span class="op-report-status-badge" [attr.data-type]="getEmployeeTaskPendingReportStatus(task.status).badgeClass">
                              {{ getEmployeeTaskPendingReportStatus(task.status).label }}
                            </span>
                          </div>
                          <div class="op-card-footer">
                            <div class="op-progress-bar-wrap">
                              <div class="op-progress-bar">
                                <div class="op-progress-fill" [style.width.%]="task.progressPercent"></div>
                              </div>
                              <span class="op-progress-val">{{ task.progressPercent }}%</span>
                            </div>
                            <span class="op-progress-sub">Tiến độ cá nhân</span>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>

              <!-- Section 2 (BOTTOM) — My Requests in horizontal cards -->
              <div class="op-card">
                <div class="op-card-header">
                  <div class="op-header-title">
                    <span class="op-icon">✉️</span>
                    <div>
                      <h3>Yêu cầu đã gửi của tôi</h3>
                      <p class="op-subtitle">Theo dõi trạng thái xử lý, phòng ban tiếp nhận và kết quả các yêu cầu</p>
                    </div>
                  </div>
                  <a routerLink="/requests" class="op-header-link">Xem tất cả yêu cầu của tôi →</a>
                </div>

                <div class="op-card-body">
                  @if (employeeRequests().length === 0) {
                    <div class="op-empty">
                      <span class="op-empty-icon">✓</span>
                      <p>Bạn chưa gửi yêu cầu dịch vụ nào gần đây.</p>
                    </div>
                  } @else {
                    <div class="op-horizontal-cards">
                      @for (req of employeeRequests(); track req.requestId) {
                        <div class="op-request-card">
                          <div class="op-card-top-row">
                            <span class="status-badge" [attr.data-status]="req.status">
                              {{ formatStatusLabel(req.status) }}
                            </span>
                          </div>
                          <a [routerLink]="['/requests', req.requestId]" class="op-card-title">
                            {{ req.title }}
                          </a>
                          <div class="op-card-meta">
                            <span>🕒 Đã gửi: {{ req.createdAt | date: 'dd/MM/yyyy HH:mm' }}</span>
                            @if (req.currentRoutingDepartmentName) {
                              <span>🏢 Nơi xử lý: {{ req.currentRoutingDepartmentName }}</span>
                            }
                          </div>
                          <div class="op-card-footer">
                            <span class="op-employee-stage-chip" [ngClass]="getEmployeeRequestStage(req).class">
                              {{ getEmployeeRequestStage(req).label }}
                            </span>
                            <a [routerLink]="['/requests', req.requestId]" class="op-view-link">
                              Chi tiết →
                            </a>
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        </section>
      }
    </main>

    <!-- Shared AI Assistant Drawer -->
  `,
  styles: [
    `
      .workspace-nav {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1.5rem;
        padding: 0.75rem 2rem;
        flex-wrap: wrap;
      }
      .nav-brand-section {
        display: flex;
        align-items: center;
        gap: 0.85rem;
      }
      .tenant-badge {
        background: #eaf4ff;
        color: #145da0;
        border: 1px solid #bfdbfe;
        padding: 0.2rem 0.65rem;
        border-radius: 9999px;
        font-size: 0.78rem;
        font-weight: 600;
        max-width: 14rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .nav-links {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        flex-wrap: wrap;
      }
      .nav-link {
        color: #475569;
        font-weight: 600;
        font-size: 0.875rem;
        padding: 0.35rem 0.65rem;
        border-radius: 6px;
        text-decoration: none;
        transition: all 160ms ease;
        &:hover {
          background: #f1f5f9;
          color: #2563eb;
          text-decoration: none;
        }
      }
      .nav-utilities {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }
      .ai-trigger-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        background: linear-gradient(135deg, #145da0 0%, #2563eb 100%);
        color: #ffffff;
        border: none;
        padding: 0.35rem 0.75rem;
        border-radius: 8px;
        font-weight: 700;
        font-size: 0.8125rem;
        cursor: pointer;
        box-shadow: 0 2px 8px rgba(37, 99, 235, 0.25);
        animation: ai-pulse-glow 4s infinite;
        &:hover {
          transform: translateY(-1px);
          box-shadow: 0 4px 14px rgba(37, 99, 235, 0.4);
        }
      }
      .sparkle {
        color: #8ec5ff;
        font-size: 1rem;
      }
      .notify-btn {
        position: relative;
        font-size: 1.15rem;
        padding: 0.35rem 0.5rem;
        border-radius: 6px;
        color: #475569;
        text-decoration: none;
        &:hover {
          background: #f1f5f9;
          text-decoration: none;
        }
      }
      .unread-dot {
        position: absolute;
        top: 4px;
        right: 4px;
        width: 7px;
        height: 7px;
        background: #2563eb;
        border-radius: 50%;
      }
      .user-chip {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: #f8fafd;
        padding: 0.2rem 0.5rem;
        border-radius: 9999px;
        border: 1px solid #dfe8f2;
      }
      .user-avatar {
        width: 1.65rem;
        height: 1.65rem;
        background: #0b2a4a;
        color: #ffffff;
        border-radius: 50%;
        display: grid;
        place-items: center;
        font-size: 0.75rem;
        font-weight: 700;
      }
      .user-name {
        font-size: 0.8125rem;
        font-weight: 600;
        color: #10243a;
        max-width: 9rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .btn-signout {
        background: none;
        border: 1px solid #dfe8f2;
        color: #63758b;
        font-size: 0.78rem;
        font-weight: 600;
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        &:hover {
          background: #fee2e2;
          color: #b91c1c;
          border-color: #fca5a5;
        }
      }
      .hero-command-card {
        position: relative;
        background:
          radial-gradient(circle at 85% 15%, rgba(56, 189, 248, 0.16) 0%, transparent 45%),
          radial-gradient(circle at 12% 85%, rgba(99, 102, 241, 0.14) 0%, transparent 48%),
          radial-gradient(ellipse at 50% 50%, rgba(14, 165, 233, 0.08) 0%, transparent 60%),
          linear-gradient(135deg, #090e1a 0%, #0d1527 30%, #111e38 65%, #0a1122 100%);
        color: #ffffff;
        border-radius: 18px;
        padding: 2.35rem 2.65rem;
        margin-bottom: 1.85rem;
        overflow: hidden;
        border: 1px solid rgba(255, 255, 255, 0.1);
        box-shadow:
          0 20px 45px -15px rgba(2, 6, 23, 0.45),
          0 0 0 1px rgba(255, 255, 255, 0.05),
          inset 0 1px 0 rgba(255, 255, 255, 0.14);
        h1 {
          color: #ffffff;
          font-size: clamp(2.05rem, 3.2vw, 2.9rem);
          font-weight: 800;
          letter-spacing: -0.03em;
          line-height: 1.3;
          margin: 0 0 1.75rem 0;
          text-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
        }
      }
      .hero-bg-mesh {
        position: absolute;
        inset: 0;
        background:
          radial-gradient(circle at 85% 18%, rgba(56, 189, 248, 0.22) 0%, transparent 45%),
          radial-gradient(circle at 15% 82%, rgba(99, 102, 241, 0.18) 0%, transparent 50%),
          radial-gradient(circle at 50% 50%, rgba(37, 99, 235, 0.12) 0%, transparent 60%);
        pointer-events: none;
        animation: ocean-pulse-glow 8s infinite alternate;
      }
      .hero-content {
        position: relative;
        z-index: 1;
      }
      .hero-badges {
        display: flex;
        align-items: center;
        gap: 0.85rem;
        margin-bottom: 1.25rem;
      }
      .role-chip {
        background: rgba(255, 255, 255, 0.08);
        color: #93c5fd;
        padding: 0.3rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.75rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        border: 1px solid rgba(255, 255, 255, 0.18);
        backdrop-filter: blur(12px);
        -webkit-backdrop-filter: blur(12px);
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
      }
      .role-operational-dashboard {
        margin-top: 1.25rem;
        margin-bottom: 2rem;
      }
      .op-stacked-layout {
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        width: 100%;
      }
      .op-horizontal-cards {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
        gap: 1.15rem;
        width: 100%;
      }
      .op-task-card,
      .op-request-card {
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        padding: 1.15rem;
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        border-radius: 12px;
        transition: all 180ms ease;
        min-height: 155px;

        &:hover {
          background: #ffffff;
          border-color: #93c5fd;
          transform: translateY(-2px);
          box-shadow: 0 6px 18px rgba(37, 99, 235, 0.08);
        }

        &.is-escalated {
          border-left: 3.5px solid #ef4444;
          background: #fffafa;
        }
      }
      .op-card-top-row {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.45rem;
        margin-bottom: 0.65rem;
      }
      .op-card-title {
        font-size: 0.95rem;
        font-weight: 700;
        color: #0f172a;
        line-height: 1.4;
        margin-bottom: 0.5rem;
        text-decoration: none;
        display: -webkit-box;
        -webkit-line-clamp: 2;
        -webkit-box-orient: vertical;
        overflow: hidden;

        &:hover {
          color: #2563eb;
          text-decoration: underline;
        }
      }
      .op-card-meta {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        font-size: 0.785rem;
        color: #64748b;
        margin-bottom: 0.85rem;
      }
      .op-card-footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
        padding-top: 0.65rem;
        border-top: 1px solid #edf2f7;
        margin-top: auto;
      }
      .op-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 1.5rem;
        @media (max-width: 1024px) {
          grid-template-columns: 1fr;
        }
      }
      .op-card {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        border-radius: 12px;
        box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
        display: flex;
        flex-direction: column;
        overflow: hidden;
        transition: box-shadow 180ms ease, border-color 180ms ease;
        &:hover {
          box-shadow: 0 6px 18px rgba(37, 99, 235, 0.08);
          border-color: #bfdbfe;
        }
      }
      .op-card-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        padding: 1.15rem 1.4rem;
        border-bottom: 1px solid #f1f5f9;
        background: #fafcff;
        gap: 1rem;
      }
      .op-header-title {
        display: flex;
        align-items: flex-start;
        gap: 0.75rem;
        h3 {
          font-size: 1.05rem;
          font-weight: 700;
          color: #071b33;
          margin: 0;
          line-height: 1.35;
        }
      }
      .op-icon {
        font-size: 1.35rem;
        line-height: 1;
        flex-shrink: 0;
      }
      .op-subtitle {
        font-size: 0.785rem;
        color: #64748b;
        margin: 0.2rem 0 0;
        line-height: 1.4;
      }
      .op-header-link {
        font-size: 0.8125rem;
        font-weight: 600;
        color: #2563eb;
        text-decoration: none;
        white-space: nowrap;
        align-self: center;
        &:hover {
          text-decoration: underline;
        }
      }
      .op-card-body {
        padding: 1.25rem 1.4rem;
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 0.85rem;
      }
      .op-dept-stats-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(95px, 1fr));
        gap: 0.65rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid #f1f5f9;
        margin-bottom: 0.25rem;
      }
      .op-stat-pill {
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        border-radius: 8px;
        padding: 0.5rem 0.6rem;
        text-align: center;
      }
      .op-stat-num {
        display: block;
        font-size: 1.15rem;
        font-weight: 800;
        color: #0f172a;
        &.text-azure {
          color: #2563eb;
        }
        &.text-danger {
          color: #dc2626;
        }
      }
      .op-stat-lbl {
        display: block;
        font-size: 0.7rem;
        color: #64748b;
        font-weight: 600;
        margin-top: 0.2rem;
      }
      .op-task-list,
      .op-request-list {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      .op-task-item,
      .op-request-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 1rem;
        padding: 0.85rem 1rem;
        background: #ffffff;
        border: 1px solid #e2e8f0;
        border-radius: 10px;
        transition: all 160ms cubic-bezier(0.16, 1, 0.3, 1);
        &:hover {
          border-color: #93c5fd;
          background: #f8fbff;
          box-shadow: 0 4px 12px rgba(37, 99, 235, 0.06);
        }
        &.is-escalated {
          border-left: 3.5px solid #ef4444;
          background: #fffafa;
        }
      }
      .op-task-main,
      .op-req-left {
        flex: 1;
        min-width: 0;
      }
      .op-task-badges,
      .op-req-badges {
        display: flex;
        align-items: center;
        gap: 0.45rem;
        margin-bottom: 0.35rem;
        flex-wrap: wrap;
      }
      .op-task-title,
      .op-req-title {
        display: block;
        font-size: 0.925rem;
        font-weight: 700;
        color: #0f172a;
        text-decoration: none;
        margin-bottom: 0.35rem;
        line-height: 1.4;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        &:hover {
          color: #2563eb;
        }
      }
      .op-task-meta,
      .op-req-meta {
        display: flex;
        align-items: center;
        gap: 0.85rem;
        font-size: 0.785rem;
        color: #64748b;
        flex-wrap: wrap;
      }
      .op-task-progress-col {
        min-width: 135px;
        text-align: right;
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 0.25rem;
      }
      .op-progress-bar-wrap {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        width: 100%;
      }
      .op-progress-bar {
        flex: 1;
        height: 7px;
        background: #e2e8f0;
        border-radius: 9999px;
        overflow: hidden;
      }
      .op-progress-fill {
        height: 100%;
        background: linear-gradient(90deg, #2563eb, #38bdf8);
        border-radius: 9999px;
        transition: width 300ms ease;
      }
      .op-progress-val {
        font-size: 0.8125rem;
        font-weight: 700;
        color: #0f172a;
        min-width: 34px;
      }
      .op-progress-sub {
        font-size: 0.6875rem;
        color: #94a3b8;
      }
      .op-req-status-col {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 0.35rem;
        min-width: 140px;
      }
      .priority-chip {
        display: inline-flex;
        align-items: center;
        padding: 0.15rem 0.5rem;
        border-radius: 9999px;
        font-size: 0.7rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.04em;
        white-space: nowrap;
        &[data-priority='URGENT'] {
          background: #fef2f2;
          color: #b91c1c;
          border: 1px solid #fecaca;
        }
        &[data-priority='HIGH'] {
          background: #fff7ed;
          color: #c2410c;
          border: 1px solid #fed7aa;
        }
        &[data-priority='NORMAL'] {
          background: #eff6ff;
          color: #1d4ed8;
          border: 1px solid #bfdbfe;
        }
        &[data-priority='LOW'] {
          background: #f8fafc;
          color: #64748b;
          border: 1px solid #e2e8f0;
        }
      }
      .badge-overdue {
        background: #fef2f2;
        color: #dc2626;
        border: 1px solid #fca5a5;
        font-size: 0.7rem;
        font-weight: 700;
        padding: 0.15rem 0.45rem;
        border-radius: 9999px;
      }
      .badge-escalated {
        background: #fef2f2;
        color: #b91c1c;
        border: 1px solid #f87171;
        font-size: 0.7rem;
        font-weight: 800;
        padding: 0.15rem 0.45rem;
        border-radius: 9999px;
      }
      .op-manager-status-chip,
      .op-employee-stage-chip {
        font-size: 0.75rem;
        font-weight: 600;
        padding: 0.2rem 0.55rem;
        border-radius: 6px;
        white-space: nowrap;
        &.stage-pending {
          background: #fffbeb;
          color: #b45309;
          border: 1px solid #fde68a;
        }
        &.stage-reviewed {
          background: #eff6ff;
          color: #1d4ed8;
          border: 1px solid #bfdbfe;
        }
        &.stage-completed {
          background: #ecfdf5;
          color: #047857;
          border: 1px solid #a7f3d0;
        }
        &.stage-info {
          background: #fff7ed;
          color: #c2410c;
          border: 1px solid #fed7aa;
        }
        &.stage-escalated {
          background: #fef2f2;
          color: #dc2626;
          border: 1px solid #fca5a5;
          font-weight: 700;
        }
        &.stage-rejected {
          background: #f8fafc;
          color: #64748b;
          border: 1px solid #e2e8f0;
        }
      }
      .op-action-required-chip {
        font-size: 0.6875rem;
        font-weight: 700;
        color: #dc2626;
        background: #fff1f2;
        border: 1px solid #ffe4e6;
        padding: 0.15rem 0.45rem;
        border-radius: 4px;
      }
      .op-review-btn {
        font-size: 0.75rem;
        font-weight: 700;
        color: #ffffff !important;
        background: #2563eb;
        border-radius: 6px;
        padding: 0.3rem 0.65rem;
        text-decoration: none !important;
        transition: background 150ms ease;
        &:hover {
          background: #1d4ed8;
        }
      }
      .op-view-link {
        font-size: 0.785rem;
        font-weight: 600;
        color: #2563eb;
        text-decoration: none;
        &:hover {
          text-decoration: underline;
        }
      }
      .op-report-status-badge {
        font-size: 0.72rem;
        font-weight: 600;
        padding: 0.15rem 0.45rem;
        border-radius: 4px;
        &[data-type='amber'] {
          background: #fffbeb;
          color: #b45309;
          border: 1px solid #fde68a;
        }
        &[data-type='azure'] {
          background: #eff6ff;
          color: #1d4ed8;
          border: 1px solid #bfdbfe;
        }
        &[data-type='green'] {
          background: #ecfdf5;
          color: #047857;
          border: 1px solid #a7f3d0;
        }
      }
      .op-empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 2rem 1rem;
        color: #94a3b8;
        text-align: center;
        gap: 0.5rem;
      }
      .op-empty-icon {
        font-size: 1.5rem;
        color: #10b981;
      }
      .text-danger {
        color: #dc2626 !important;
        font-weight: 600;
      }
      .kpi-metric-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
        gap: 1rem;
        margin-bottom: 2rem;
      }
      .kpi-sublabel {
        font-size: 0.8125rem;
        font-weight: 500;
        color: #64748b;
        margin-top: 0.25rem;
        margin-bottom: 0.6rem;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .unread-badge {
        position: absolute;
        top: 4px;
        right: 4px;
        min-width: 18px;
        height: 18px;
        padding: 0 4px;
        background: #ef4444;
        color: #ffffff;
        border-radius: 9px;
        font-size: 0.6875rem;
        font-weight: 700;
        line-height: 18px;
        text-align: center;
        border: 2px solid #ffffff;
        box-shadow: 0 2px 4px rgba(239, 68, 68, 0.4);
        pointer-events: none;
        box-sizing: border-box;
      }
      .kpi-box {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        border-radius: 12px;
        padding: 1.25rem;
        box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
        transition: all 180ms ease;
        position: relative;
        overflow: hidden;
        &:hover {
          transform: translateY(-2px);
          box-shadow: 0 8px 20px rgba(37, 99, 235, 0.1);
          border-color: #8ec5ff;
        }
        &::before {
          content: '';
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          height: 3px;
        }
        &.azure-accent::before {
          background: #2563eb;
        }
        &.ocean-accent::before {
          background: #145da0;
        }
        &.green-accent::before {
          background: #059669;
        }
        &.navy-accent::before {
          background: #0b2a4a;
        }
      }
      .kpi-top {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.5rem;
      }
      .kpi-title {
        font-size: 0.78rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: #63758b;
      }
      .kpi-badge {
        background: #eaf4ff;
        color: #145da0;
        padding: 0.15rem 0.45rem;
        border-radius: 9999px;
        font-size: 0.72rem;
        font-weight: 700;
        &.green {
          background: #ecfdf5;
          color: #047857;
        }
      }
      .kpi-metric {
        font-size: 2rem;
        font-weight: 800;
        color: #071b33;
        margin-bottom: 0.5rem;
      }
      .kpi-foot {
        margin: 0;
        font-size: 0.8125rem;
        text-decoration: none !important;
        a {
          font-weight: 600;
          color: #2563eb;
          text-decoration: none;
          &:hover {
            text-decoration: underline;
          }
        }
      }
      .modules-section {
        margin-top: 1.5rem;
      }
      .section-title {
        font-size: 1.25rem;
        margin-bottom: 1.25rem;
        color: #071b33;
      }
      .modules-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        gap: 1.25rem;
      }
      .module-card {
        background: #ffffff;
        border: 1px solid #dfe8f2;
        border-radius: 12px;
        padding: 1.35rem;
        box-shadow: 0 2px 6px rgba(7, 27, 51, 0.03);
        transition: all 200ms cubic-bezier(0.16, 1, 0.3, 1);
        display: flex;
        flex-direction: column;
        &:hover {
          transform: translateY(-2px);
          box-shadow: 0 10px 24px -4px rgba(37, 99, 235, 0.12);
          border-color: #8ec5ff;
        }
        h3 {
          margin: 0.65rem 0 0.35rem;
          font-size: 1.1rem;
          color: #071b33;
        }
        p {
          color: #63758b;
          font-size: 0.84rem;
          line-height: 1.5;
          margin: 0 0 1rem;
          flex: 1;
        }
      }
      .module-icon {
        width: 2.25rem;
        height: 2.25rem;
        border-radius: 8px;
        background: #eaf4ff;
        display: grid;
        place-items: center;
        font-size: 1.15rem;
      }
      .module-link-wrap {
        border-top: 1px solid #f1f5f9;
        padding-top: 0.75rem;
        p {
          margin: 0;
          text-decoration: none !important;
        }
        a {
          margin: 0;
          font-size: 0.875rem;
          font-weight: 600;
          color: #2563eb;
          text-decoration: none;
          &:hover {
            text-decoration: underline;
          }
        }
      }
      .platform-card {
        border-color: #cbd5e1;
        background: #fcfdfe;
      }
    `,
  ],
})
export class Workspace implements OnInit {
  readonly session = inject(SessionService);
  readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  readonly aiAgent = inject(AiAgentService);
  private readonly http = inject(HttpClient);
  readonly notifCount = inject(NotificationCountService);

  readonly tasksActiveCount = signal<number>(0);
  readonly tasksTotalCount = signal<number>(0);
  readonly requestsPendingCount = signal<number>(0);
  readonly requestsTotalCount = signal<number>(0);
  readonly auditCount = signal<number>(0);
  readonly operationalTasks = signal<DashboardTaskItem[]>([]);
  readonly operationalRequests = signal<DashboardRequestItem[]>([]);
  readonly isOperationalLoading = signal<boolean>(false);
  readonly unreadNotificationsCount = computed(() => this.notifCount.unreadCount());
  readonly totalNotificationsCount = computed(() => this.notifCount.totalCount());
  readonly slaRate = signal<string>('100%');
  readonly slaStatus = signal<string>('Tối ưu');
  readonly identity = this.session.identity;

  openAiAgent(): void {
    this.aiAgent.open('chat');
  }

  readonly isAiOpen = signal(false);
  readonly avatarFailed = signal(false);

  readonly profilePhotoUrl = computed(() => {
    const id = this.identity();
    return id?.avatarUrl || id?.picture || id?.photoUrl || null;
  });

  onAvatarError(): void {
    this.avatarFailed.set(true);
  }

  userInitials(): string {
    const name = this.identity()?.fullName || 'User';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  ngOnInit(): void {
    this.loadLiveMetrics();
  }

  loadLiveMetrics(): void {
    this.notifCount.refresh();

    if (!this.identity()?.tenantId) {
      this.http.get<any>('/api/v1/platform/companies?pageSize=1&status=PENDING').subscribe({
        next: (res) => {
          if (typeof res?.total === 'number') this.tasksActiveCount.set(res.total);
        },
        error: () => {},
      });
      this.http.get<any>('/api/v1/audit?pageSize=1').subscribe({
        next: (res) => {
          if (typeof res?.total === 'number') this.auditCount.set(res.total);
        },
        error: () => {},
      });
      return;
    }

    // Fetch live SLA reports for company/manager
    this.http.get<any>('/api/v1/reports/sla').subscribe({
      next: (res) => {
        if (typeof res?.complianceRatePercent === 'number') {
          this.slaRate.set(`${res.complianceRatePercent.toFixed(1)}%`);
          this.slaStatus.set(
            res.complianceRatePercent >= 95
              ? 'Tối ưu'
              : res.complianceRatePercent >= 80
                ? 'Khá'
                : 'Cần chú ý',
          );
        }
      },
      error: () => {},
    });

    if (
      this.session.hasPermission('reports.manager') ||
      this.session.hasPermission('roles.configure') ||
      this.session.hasPermission('tasks.read.tenant')
    ) {
      this.http.get<any>('/api/v1/dashboard/manager').subscribe({
        next: (res) => {
          if (res?.taskMetrics) {
            this.tasksActiveCount.set(res.taskMetrics.activeTasks ?? 0);
            this.tasksTotalCount.set(res.taskMetrics.totalTasks ?? 0);
          }
          if (res?.requestMetrics) {
            this.requestsPendingCount.set(res.requestMetrics.pendingRequests ?? 0);
            this.requestsTotalCount.set(res.requestMetrics.totalRequests ?? 0);
          }
        },
        error: () => {},
      });
    } else {
      this.http.get<any>('/api/v1/tasks?pageSize=1').subscribe({
        next: (res) => {
          if (typeof res?.total === 'number') {
            this.tasksTotalCount.set(res.total);
            this.tasksActiveCount.set(res.total);
          }
        },
        error: () => {},
      });
      this.http.get<any>('/api/v1/requests?pageSize=1').subscribe({
        next: (res) => {
          if (typeof res?.total === 'number') {
            this.requestsTotalCount.set(res.total);
            this.requestsPendingCount.set(res.total);
          }
        },
        error: () => {},
      });
    }

    if (this.identity()?.tenantId) {
      this.isOperationalLoading.set(true);
      this.http.get<{ items: DashboardTaskItem[]; total: number }>('/api/v1/tasks?pageSize=50').subscribe({
        next: (res) => {
          this.operationalTasks.set(res?.items ?? []);
        },
        error: () => {},
      });

      this.http.get<{ items: DashboardRequestItem[]; total: number }>('/api/v1/requests?pageSize=50').subscribe({
        next: (res) => {
          this.operationalRequests.set(res?.items ?? []);
          this.isOperationalLoading.set(false);
        },
        error: () => {
          this.isOperationalLoading.set(false);
        },
      });
    }
  }

  welcomeName(): string {
    const raw = this.identity()?.fullName?.trim();
    if (!raw) return 'Quản trị viên Hệ thống';
    if (
      raw === 'Platform Administrator' ||
      raw === 'superadmin' ||
      raw.toLowerCase().includes('platform admin')
    ) {
      return 'Quản trị viên Hệ thống';
    }
    if (raw === 'Company Administrator') {
      return 'Quản trị viên Doanh nghiệp';
    }
    if (raw === 'Operation Manager' || raw === 'Operational Manager') {
      return 'Quản lý Vận hành';
    }
    if (raw === 'Regular Employee') {
      return 'Nhân viên Doanh nghiệp';
    }
    return raw;
  }

  readonly userRole = computed(() => {
    const id = this.identity();
    if (!id?.tenantId) return 'SUPERADMIN';
    if (id.permissions.includes('roles.configure')) return 'COMPANY_ADMIN';
    if (id.permissions.includes('tasks.assign')) return 'MANAGER';
    return 'EMPLOYEE';
  });

  readonly kpiData = computed(() => {
    switch (this.userRole()) {
      case 'SUPERADMIN':
        return {
          box1: {
            title: 'Hồ sơ đăng ký',
            badge: 'Chờ duyệt',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} doanh nghiệp chờ duyệt`,
            link: '/platform/companies',
            label: 'Duyệt đăng ký doanh nghiệp',
          },
          box2: {
            title: 'Kiểm toán hệ thống',
            badge: 'Nhật ký bảo mật',
            metric: String(this.auditCount()),
            sublabel: `${this.auditCount()} bản ghi kiểm toán`,
            link: '/platform/audit',
            label: 'Xem nhật ký kiểm toán',
          },
          box3: {
            title: 'Trạng thái nền tảng',
            badge: '100% hoạt động',
            metric: 'Ổn định',
            sublabel: 'Tất cả dịch vụ trực tuyến',
            link: '/platform/companies',
            label: 'Giám sát vận hành',
          },
          box4: {
            title: 'Thông báo hệ thống',
            badge: 'Chưa đọc',
            metric: String(this.unreadNotificationsCount()),
            sublabel: `${this.unreadNotificationsCount()} thông báo mới`,
            link: '/notifications',
            label: 'Xem thông báo',
          },
        };
      case 'COMPANY_ADMIN':
        return {
          box1: {
            title: 'Nhiệm vụ toàn công ty',
            badge: 'Đang làm',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} đang làm · ${this.tasksTotalCount()} tổng số`,
            link: '/tasks',
            label: 'Xem danh sách công việc',
          },
          box2: {
            title: 'Yêu cầu toàn công ty',
            badge: 'Chưa xong',
            metric: String(this.requestsPendingCount()),
            sublabel: `${this.requestsPendingCount()} chưa xong · ${this.requestsTotalCount()} tổng số`,
            link: '/requests',
            label: 'Xem danh sách yêu cầu',
          },
          box3: {
            title: 'Tuân thủ SLA doanh nghiệp',
            badge: this.slaRate(),
            metric: this.slaStatus(),
            sublabel: 'Cam kết chất lượng vận hành',
            link: '/reports',
            label: 'Báo cáo & thống kê',
          },
          box4: {
            title: 'Hộp thư cập nhật',
            badge: 'Chưa đọc',
            metric: String(this.unreadNotificationsCount()),
            sublabel: `${this.unreadNotificationsCount()} chưa đọc · ${this.totalNotificationsCount()} tổng số`,
            link: '/notifications',
            label: 'Xem thông báo',
          },
        };
      case 'MANAGER':
        return {
          box1: {
            title: 'Nhiệm vụ phòng ban',
            badge: 'Phụ trách',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} đang làm · ${this.tasksTotalCount()} tổng số`,
            link: '/tasks',
            label: 'Xem danh sách công việc',
          },
          box2: {
            title: 'Yêu cầu phòng ban',
            badge: 'Cần duyệt',
            metric: String(this.requestsPendingCount()),
            sublabel: `${this.requestsPendingCount()} chưa xong · ${this.requestsTotalCount()} tổng số`,
            link: '/requests',
            label: 'Xem danh sách yêu cầu',
          },
          box3: {
            title: 'Tuân thủ SLA phòng ban',
            badge: this.slaRate(),
            metric: this.slaStatus(),
            sublabel: 'Tiến độ xử lý phòng ban',
            link: '/sla-monitor',
            label: 'Giám sát SLA',
          },
          box4: {
            title: 'Hộp thư cập nhật',
            badge: 'Chưa đọc',
            metric: String(this.unreadNotificationsCount()),
            sublabel: `${this.unreadNotificationsCount()} chưa đọc · ${this.totalNotificationsCount()} tổng số`,
            link: '/notifications',
            label: 'Xem thông báo',
          },
        };
      default: {
        // EMPLOYEE
        const total = this.tasksTotalCount();
        const active = this.tasksActiveCount();
        const empRate = total > 0 ? `${Math.round(((total - active) / total) * 100)}%` : '100%';
        return {
          box1: {
            title: 'Nhiệm vụ của tôi',
            badge: 'Được giao',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} đang làm · ${this.tasksTotalCount()} tổng số`,
            link: '/tasks',
            label: 'Xem nhiệm vụ cá nhân',
          },
          box2: {
            title: 'Yêu cầu của tôi',
            badge: 'Đã gửi',
            metric: String(this.requestsPendingCount()),
            sublabel: `${this.requestsPendingCount()} chưa xong · ${this.requestsTotalCount()} tổng số`,
            link: '/requests',
            label: 'Xem yêu cầu của tôi',
          },
          box3: {
            title: 'Tiến độ hoàn thành',
            badge: empRate,
            metric: 'Đạt chuẩn',
            sublabel: 'Chỉ số năng suất cá nhân',
            link: '/tasks',
            label: 'Chi tiết tiến độ',
          },
          box4: {
            title: 'Hộp thư cập nhật',
            badge: 'Chưa đọc',
            metric: String(this.unreadNotificationsCount()),
            sublabel: `${this.unreadNotificationsCount()} chưa đọc · ${this.totalNotificationsCount()} tổng số`,
            link: '/notifications',
            label: 'Xem thông báo',
          },
        };
      }
    }
  });

  isOverdue(deadline?: string | null, status?: string): boolean {
    if (!deadline) return false;
    if (status === 'COMPLETED' || status === 'CANCELLED' || status === 'RESOLVED') return false;
    return new Date(deadline).getTime() < Date.now();
  }

  getPriorityWeight(priority?: string): number {
    switch ((priority || '').toUpperCase()) {
      case 'URGENT': return 4;
      case 'HIGH': return 3;
      case 'NORMAL': return 2;
      case 'LOW': return 1;
      default: return 0;
    }
  }

  readonly adminUrgentTasks = computed(() => {
    const tasks = this.operationalTasks().filter(t => t.status !== 'DRAFT' && t.status !== 'COMPLETED' && t.status !== 'CANCELLED');
    return [...tasks].sort((a, b) => {
      const aOverdue = this.isOverdue(a.deadline, a.status);
      const bOverdue = this.isOverdue(b.deadline, b.status);
      if (aOverdue && !bOverdue) return -1;
      if (!aOverdue && bOverdue) return 1;
      const pDiff = this.getPriorityWeight(b.priority) - this.getPriorityWeight(a.priority);
      if (pDiff !== 0) return pDiff;
      const aDate = a.deadline ? new Date(a.deadline).getTime() : Infinity;
      const bDate = b.deadline ? new Date(b.deadline).getTime() : Infinity;
      return aDate - bDate;
    }).slice(0, 8);
  });

  readonly adminSubordinateRequests = computed(() => {
    const reqs = this.operationalRequests().filter(r => r.status !== 'DRAFT' && r.status !== 'CANCELLED');
    return [...reqs].sort((a, b) => {
      if (a.escalationState === 'ESCALATED' && b.escalationState !== 'ESCALATED') return -1;
      if (a.escalationState !== 'ESCALATED' && b.escalationState === 'ESCALATED') return 1;
      if (a.status === 'SUBMITTED' && b.status !== 'SUBMITTED') return -1;
      if (a.status !== 'SUBMITTED' && b.status === 'SUBMITTED') return 1;
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    }).slice(0, 8);
  });

  readonly managerUrgentTasks = computed(() => {
    const tasks = this.operationalTasks().filter(t => t.status !== 'DRAFT' && t.status !== 'COMPLETED' && t.status !== 'CANCELLED');
    return [...tasks].sort((a, b) => {
      const aOverdue = this.isOverdue(a.deadline, a.status);
      const bOverdue = this.isOverdue(b.deadline, b.status);
      if (aOverdue && !bOverdue) return -1;
      if (!aOverdue && bOverdue) return 1;
      const pDiff = this.getPriorityWeight(b.priority) - this.getPriorityWeight(a.priority);
      if (pDiff !== 0) return pDiff;
      const aDate = a.deadline ? new Date(a.deadline).getTime() : Infinity;
      const bDate = b.deadline ? new Date(b.deadline).getTime() : Infinity;
      return aDate - bDate;
    }).slice(0, 8);
  });

  readonly managerDeptStats = computed(() => {
    const tasks = this.operationalTasks().filter(t => t.status !== 'DRAFT');
    const overdue = tasks.filter(t => this.isOverdue(t.deadline, t.status)).length;
    const inProgress = tasks.filter(t => t.status === 'IN_PROGRESS' || t.status === 'ASSIGNED').length;
    const avgProgress = tasks.length > 0
      ? Math.round(tasks.reduce((sum, t) => sum + (t.progressPercent || 0), 0) / tasks.length)
      : 100;
    const completedChildTasks = tasks.reduce((sum, t) => sum + (t.completedSubtaskCount || 0), 0);
    const assignedUsers = new Set(tasks.map(t => t.assignedUserName).filter(Boolean));

    return {
      avgProgress,
      assignedEmployeesCount: assignedUsers.size,
      completedChildTasks,
      inProgressCount: inProgress,
      overdueCount: overdue,
    };
  });

  readonly managerDeptRequests = computed(() => {
    const reqs = this.operationalRequests().filter(r => r.status !== 'DRAFT' && r.status !== 'CANCELLED');
    return [...reqs].sort((a, b) => {
      if (a.escalationState === 'ESCALATED' && b.escalationState !== 'ESCALATED') return -1;
      if (a.escalationState !== 'ESCALATED' && b.escalationState === 'ESCALATED') return 1;
      if (a.status === 'SUBMITTED' && b.status !== 'SUBMITTED') return -1;
      if (a.status !== 'SUBMITTED' && b.status === 'SUBMITTED') return 1;
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    }).slice(0, 8);
  });

  readonly employeeUrgentTasks = computed(() => {
    const tasks = this.operationalTasks().filter(t => t.status !== 'DRAFT' && t.status !== 'COMPLETED' && t.status !== 'CANCELLED');
    return [...tasks].sort((a, b) => {
      const aOverdue = this.isOverdue(a.deadline, a.status);
      const bOverdue = this.isOverdue(b.deadline, b.status);
      if (aOverdue && !bOverdue) return -1;
      if (!aOverdue && bOverdue) return 1;
      const pDiff = this.getPriorityWeight(b.priority) - this.getPriorityWeight(a.priority);
      if (pDiff !== 0) return pDiff;
      const aDate = a.deadline ? new Date(a.deadline).getTime() : Infinity;
      const bDate = b.deadline ? new Date(b.deadline).getTime() : Infinity;
      return aDate - bDate;
    }).slice(0, 8);
  });

  readonly employeeRequests = computed(() => {
    const reqs = this.operationalRequests().filter(r => r.status !== 'DRAFT' && r.status !== 'CANCELLED');
    return [...reqs].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, 8);
  });

  getAdminRequestReviewStage(req: DashboardRequestItem): { label: string; class: string } {
    if (req.escalationState === 'ESCALATED') {
      return { label: 'Đã leo thang lên Giám đốc', class: 'stage-escalated' };
    }
    switch (req.status) {
      case 'SUBMITTED':
        return { label: 'Chờ Quản lý tiếp nhận & xem xét', class: 'stage-pending' };
      case 'ROUTED':
      case 'IN_REVIEW':
        return { label: 'Quản lý đã tiếp nhận xử lý', class: 'stage-reviewed' };
      case 'ACCEPTED':
        return { label: 'Quản lý đã phê duyệt tiếp nhận', class: 'stage-reviewed' };
      case 'RESOLVED':
      case 'COMPLETED':
        return { label: 'Đã nghiệm thu / Hoàn tất', class: 'stage-completed' };
      case 'REJECTED':
        return { label: 'Đã từ chối', class: 'stage-rejected' };
      default:
        return { label: 'Đang xử lý', class: 'stage-reviewed' };
    }
  }

  getManagerRequestReviewStage(req: DashboardRequestItem): { label: string; class: string } {
    if (req.escalationState === 'ESCALATED') {
      return { label: 'Đã leo thang Ban Giám đốc', class: 'stage-escalated' };
    }
    switch (req.status) {
      case 'SUBMITTED':
        return { label: 'Chờ xem xét bước 1', class: 'stage-pending' };
      case 'ROUTED':
      case 'IN_REVIEW':
        return { label: 'Đã tiếp nhận vào luồng', class: 'stage-reviewed' };
      case 'INFO_REQUESTED':
        return { label: 'Chờ bổ sung thông tin', class: 'stage-info' };
      case 'RESOLVED':
      case 'COMPLETED':
        return { label: 'Đã xử lý hoàn thành', class: 'stage-completed' };
      case 'REJECTED':
        return { label: 'Đã từ chối', class: 'stage-rejected' };
      default:
        return { label: 'Đang theo dõi', class: 'stage-reviewed' };
    }
  }

  getEmployeeRequestStage(req: DashboardRequestItem): { label: string; class: string } {
    switch (req.status) {
      case 'SUBMITTED':
        return { label: 'Chờ Quản lý xem xét', class: 'stage-pending' };
      case 'INFO_REQUESTED':
        return { label: 'Cần bổ sung thông tin', class: 'stage-info' };
      case 'ROUTED':
      case 'IN_REVIEW':
        return { label: 'Đã tiếp nhận xử lý', class: 'stage-reviewed' };
      case 'RESOLVED':
      case 'COMPLETED':
        return { label: 'Đã xử lý xong', class: 'stage-completed' };
      case 'REJECTED':
        return { label: 'Đã từ chối', class: 'stage-rejected' };
      default:
        return { label: 'Đang xử lý', class: 'stage-reviewed' };
    }
  }

  getEmployeeTaskPendingReportStatus(status: string): { label: string; badgeClass: string } {
    switch (status) {
      case 'ASSIGNED':
        return { label: 'Chưa tiếp nhận bắt đầu', badgeClass: 'amber' };
      case 'IN_PROGRESS':
        return { label: 'Đang làm · Chưa nộp báo cáo kết quả', badgeClass: 'azure' };
      case 'SUBMITTED':
      case 'UNDER_REVIEW':
        return { label: 'Đã nộp kết quả · Chờ nghiệm thu', badgeClass: 'green' };
      case 'COMPLETED':
        return { label: 'Đã nghiệm thu hoàn thành', badgeClass: 'green' };
      default:
        return { label: 'Đang thực hiện', badgeClass: 'azure' };
    }
  }

  formatStatusLabel(status: string): string {
    switch (status) {
      case 'ASSIGNED': return 'Đã giao việc';
      case 'ACCEPTED': return 'Đã tiếp nhận';
      case 'IN_PROGRESS': return 'Đang thực hiện';
      case 'SUBMITTED': return 'Chờ duyệt / Xem xét';
      case 'UNDER_REVIEW': return 'Đang nghiệm thu';
      case 'COMPLETED': return 'Hoàn thành';
      case 'RESOLVED': return 'Đã giải quyết';
      case 'CANCELLED': return 'Đã hủy';
      case 'REJECTED': return 'Từ chối';
      case 'ROUTED': return 'Đã điều phối';
      case 'INFO_REQUESTED': return 'Cần thông tin';
      default: return status;
    }
  }

  formatPriorityLabel(priority: string): string {
    switch ((priority || '').toUpperCase()) {
      case 'URGENT': return 'Khẩn cấp';
      case 'HIGH': return 'Cao';
      case 'NORMAL': return 'Bình thường';
      case 'LOW': return 'Thấp';
      default: return priority;
    }
  }

  currentRoleLabel(): string {
    const id = this.identity();
    if (!id?.tenantId) return 'Quản trị viên nền tảng';
    if (id.permissions.includes('roles.configure')) return 'Quản trị viên doanh nghiệp';
    if (id.permissions.includes('tasks.assign')) return 'Quản lý vận hành';
    return 'Thành viên';
  }

  signOut(): void {
    this.session.clear();
    this.router.navigate(['/login']);
  }
}
