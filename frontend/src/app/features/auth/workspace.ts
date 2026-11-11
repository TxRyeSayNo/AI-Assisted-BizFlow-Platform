import { Component, computed, effect, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NotificationCountService } from '../../core/notifications/notification-count.service';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SessionService } from '../../core/auth/session';
import { AiAgentService } from '../../core/ai/ai-agent.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

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
            <img [src]="profilePhotoUrl()" [alt]="identity()?.fullName || 'User avatar'" class="user-avatar-img" (error)="onAvatarError()" />
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

          <!-- Quick Action Buttons -->
          <div class="hero-actions">
            @if (session.hasPermission('tasks.create')) {
              <a routerLink="/tasks/new" class="btn-hero-primary">{{
                'workspace.newTask' | translate: '+ Tạo nhiệm vụ mới'
              }}</a>
            }
            @if (session.hasPermission('requests.create')) {
              <a routerLink="/requests/new" class="btn-hero-secondary">{{
                'workspace.newRequest' | translate: '+ Tạo yêu cầu mới'
              }}</a>
            }
            @if (
              session.hasPermission('tasks.confirm') ||
              session.hasPermission('tasks.read.managed') ||


              session.hasPermission('roles.configure')
            ) {
              <a routerLink="/approvals" class="btn-hero-ghost">
                {{ 'approvals.title' | translate: 'Trung tâm phê duyệt' }} →
              </a>
            }
          </div>
        </div>
      </section>

      <!-- KPI Executive Metrics Row (Personalized by Role) -->
      <section class="kpi-metric-row">
        @if (kpiData(); as kpi) {
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
        }
      </section>

      <!-- Organized Workspace Modules Grid with Clear Role Separation (Requirement 1 & User Request) -->
      <section class="modules-section">
        <h2 class="section-title">
          {{ 'workspace.sectionExecution' | translate: 'Phân hệ chức năng doanh nghiệp' }}
        </h2>
        <div class="modules-grid">

          <!-- Platform Admin Modules -->
          @if (!identity()?.tenantId) {
            <article class="module-card">
              <div class="module-icon">🏢</div>
              <h3>Quản lý doanh nghiệp & Cung cấp workspace</h3>
              <p>Phê duyệt hồ sơ đăng ký doanh nghiệp, cấp phát tenant key và quản lý trạng thái tài khoản công ty.</p>
              <div class="module-link-wrap">
                <p><a routerLink="/platform/companies">Quản lý doanh nghiệp</a></p>
              </div>
            </article>

            <article class="module-card">
              <div class="module-icon">🛡️</div>
              <h3>Kiểm toán hệ thống nền tảng</h3>
              <p>Truy vết toàn diện nhật ký bảo mật, định tuyến xác thực và truy cập tài nguyên toàn hệ thống.</p>
              <div class="module-link-wrap">
                <p><a routerLink="/platform/audit">Xem nhật ký kiểm toán</a></p>
              </div>
            </article>
          }

          <!-- 1. Quản lý Nhiệm vụ -->
          @if (
            session.hasPermission('tasks.read.tenant') ||
            session.hasPermission('tasks.read.managed') ||
            session.hasPermission('tasks.read.own') ||
            session.hasPermission('tasks.read.assigned') ||
            !identity()?.tenantId
          ) {
            <article class="module-card">
              <div class="module-icon">📋</div>
              <h3>{{ 'workspace.cardTasksTitle' | translate: 'Quản lý Nhiệm vụ' }}</h3>
              <p>
                {{
                  'workspace.cardTasksDesc'
                    | translate
                      : 'Khởi tạo, phân công, theo dõi danh mục kiểm tra, nộp bằng chứng và phê duyệt kết quả công việc.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/tasks">{{ 'workspace.viewTasks' | translate: 'Xem danh sách công việc' }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 2. Yêu cầu Dịch vụ -->
          @if (
            session.hasPermission('requests.read.tenant') ||
            session.hasPermission('requests.read.managed') ||
            session.hasPermission('requests.read.own') ||
            !identity()?.tenantId
          ) {
            <article class="module-card">
              <div class="module-icon">📨</div>
              <h3>{{ 'workspace.cardRequestsTitle' | translate: 'Yêu cầu Dịch vụ' }}</h3>
              <p>
                {{
                  'workspace.cardRequestsDesc'
                    | translate
                      : 'Gửi yêu cầu dịch vụ nội bộ, điều phối về phòng ban chuyên trách và xác nhận kết quả xử lý.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/requests">{{
                    'workspace.viewRequests' | translate: 'Xem danh sách yêu cầu'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 3. Danh mục Dịch vụ -->
          @if (
            (session.hasPermission('service.create') ||
              session.hasPermission('service.update') ||
              session.hasPermission('service.manage')) &&
            identity()?.tenantId
          ) {
            <article class="module-card">
              <div class="module-icon">⚙</div>
              <h3>{{ 'workspace.cardServicesTitle' | translate: 'Danh mục dịch vụ' }}</h3>
              <p>
                {{
                  'workspace.cardServicesDesc'
                    | translate
                      : 'Quản lý các dịch vụ nội bộ, phân loại yêu cầu và quy tắc định tuyến SLA.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/settings/services">{{
                    'workspace.browseServices' | translate: 'Xem danh mục dịch vụ nội bộ'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 4. Phòng ban & Bộ phận -->
          @if (
            (session.hasPermission('roles.configure') ||
              session.hasPermission('departments.manage') ||
              session.hasPermission('departments.read')) &&
            identity()?.tenantId
          ) {
            <article class="module-card">
              <div class="module-icon">🏢</div>
              <h3>{{ 'workspace.cardDeptsTitle' | translate: 'Phòng ban & bộ phận' }}</h3>
              <p>
                {{
                  'workspace.cardDeptsDesc'
                    | translate
                      : 'Cơ cấu tổ chức, mã phòng ban, trưởng bộ phận và hàng đợi công việc.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/settings/organization/departments">{{
                    'workspace.browseDepartments' | translate: 'Xem cơ cấu tổ chức & phòng ban'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 5. Báo cáo & Phân tích thông minh -->
          @if (
            session.hasPermission('reports.manager') ||
            session.hasPermission('reports.company') ||
            session.hasPermission('reports.workload') ||
            session.hasPermission('reports.sla') ||
            !identity()?.tenantId
          ) {
            <article class="module-card">
              <div class="module-icon">📊</div>
              <h3>{{ 'workspace.cardReportsTitle' | translate: 'Báo cáo & Phân tích thông minh' }}</h3>
              <p>
                {{
                  'workspace.cardReportsDesc'
                    | translate
                      : 'Bảng điều hành quản trị, phân bổ tải công việc và biểu đồ tuân thủ SLA theo thời gian thực.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/reports">{{
                    'workspace.dashboardReports' | translate: 'Xem Báo cáo & Chỉ số'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 6. Tra cứu Hồ sơ Hợp nhất -->
          @if (session.hasPermission('records.search')) {
            <article class="module-card">
              <div class="module-icon">🔍</div>
              <h3>{{ 'workspace.cardSearchTitle' | translate: 'Tra cứu hồ sơ hợp nhất' }}</h3>
              <p>
                {{
                  'workspace.cardSearchDesc'
                    | translate
                      : 'Tìm kiếm toàn văn nâng cao trên toàn bộ nhiệm vụ, yêu cầu, thảo luận và tệp đính kèm.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/records/search">{{
                    'workspace.searchRecords' | translate: 'Tra cứu hồ sơ kiểm toán'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 7. Danh bạ Nhân sự -->
          @if (session.hasPermission('users.read')) {
            <article class="module-card">
              <div class="module-icon">👥</div>
              <h3>{{ 'workspace.cardUsersTitle' | translate: 'Danh bạ nhân sự & tài khoản' }}</h3>
              <p>
                {{
                  'workspace.cardUsersDesc'
                    | translate
                      : 'Quản lý thành viên, mã nhân viên, gán vai trò quyền hạn và phân bổ phòng ban.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/settings/organization/users">{{
                    'workspace.browseUsers' | translate: 'Quản lý danh bạ nhân sự'
                  }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 8. Thiết lập Doanh nghiệp & Quy trình -->
          @if (
            session.hasPermission('roles.configure') ||
            session.hasPermission('users.read') ||
            session.hasPermission('departments.read') ||
            session.hasPermission('workflows.configure') ||
            session.hasPermission('sla.configure') ||
            session.hasPermission('service.create')
          ) {
            <article class="module-card">
              <div class="module-icon">🛠</div>
              <h3>{{ 'workspace.cardSettingsTitle' | translate: 'Thiết lập doanh nghiệp' }}</h3>
              <p>
                {{
                  'workspace.cardSettingsDesc'
                    | translate
                      : 'Cấu hình vai trò tùy chỉnh, máy trạng thái quy trình, hồ sơ SLA và tham số doanh nghiệp.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/settings">{{ 'workspace.settings' | translate: 'Cài đặt hệ thống' }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 9. Nhật ký Kiểm toán -->
          @if (session.hasPermission('audit.read') && identity()?.tenantId) {
            <article class="module-card">
              <div class="module-icon">🛡</div>
              <h3>{{ 'workspace.cardAuditTitle' | translate: 'Nhật ký Kiểm toán' }}</h3>
              <p>
                {{
                  'workspace.cardAuditDesc'
                    | translate
                      : 'Bản ghi bất biến ghi lại toàn bộ giao dịch, thay đổi trạng thái và quyết định phê duyệt.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/audit">{{ 'workspace.audit' | translate: 'Xem nhật ký kiểm toán' }}</a>
                </p>
              </div>
            </article>
          }

          <!-- 10. Quản lý Công ty Nền tảng -->
          @if (!identity()?.tenantId && session.hasPermission('platform.company-registrations.read')) {
            <article class="module-card platform-card">
              <div class="module-icon">🌐</div>
              <h3>
                {{ 'workspace.cardPlatformCompaniesTitle' | translate: 'Công ty & Thuê bao Nền tảng' }}
              </h3>
              <p>
                {{
                  'workspace.cardPlatformCompaniesDesc'
                    | translate: 'Duyệt hồ sơ đăng ký doanh nghiệp mới và quản trị vòng đời các đơn vị khách thuê.'
                }}
              </p>
              <div class="module-link-wrap">
                <a routerLink="/platform/companies">Duyệt đăng ký doanh nghiệp</a>
              </div>
            </article>
          }

          <!-- 11. Kiểm toán Nền tảng Toàn cầu -->
          @if (!identity()?.tenantId && session.hasPermission('platform.audit.read')) {
            <article class="module-card platform-card">
              <div class="module-icon">🏛</div>
              <h3>{{ 'workspace.cardPlatformAuditTitle' | translate: 'Kiểm toán Nền tảng Toàn cầu' }}</h3>
              <p>
                {{
                  'workspace.cardPlatformAuditDesc'
                    | translate
                      : 'Giám sát cấu hình hệ thống, sự kiện an ninh và các biến động tài khoản trên toàn nền tảng.'
                }}
              </p>
              <div class="module-link-wrap">
                <p><a routerLink="/platform/audit">Kiểm toán nền tảng</a></p>
              </div>
            </article>
          }
        </div>
      </section>
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
      .hero-actions {
        display: flex;
        gap: 1.1rem;
        flex-wrap: wrap;
        align-items: center;
      }
      .btn-hero-primary {
        position: relative;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 0.75rem 1.6rem;
        border-radius: 12px;
        font-weight: 700;
        font-size: 0.9375rem;
        color: #ffffff;
        background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
        border: 1px solid rgba(255, 255, 255, 0.28);
        box-shadow: 
          0 4px 16px rgba(37, 99, 235, 0.38),
          inset 0 1px 0 rgba(255, 255, 255, 0.32);
        text-decoration: none !important;
        cursor: pointer;
        transition: transform 160ms cubic-bezier(0.16, 1, 0.3, 1), box-shadow 180ms ease, border-color 180ms ease;

        &:hover {
          transform: translateY(-2px);
          border-color: #ffffff;
          background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
          box-shadow: 0 8px 26px rgba(37, 99, 235, 0.55);
          text-decoration: none !important;
        }

        &:active {
          transform: scale(0.98);
        }
      }

      .btn-hero-secondary {
        position: relative;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 0.75rem 1.6rem;
        border-radius: 12px;
        font-weight: 700;
        font-size: 0.9375rem;
        color: #f8fafc;
        background: rgba(255, 255, 255, 0.08);
        backdrop-filter: blur(12px);
        -webkit-backdrop-filter: blur(12px);
        border: 1px solid rgba(255, 255, 255, 0.18);
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
        text-decoration: none !important;
        cursor: pointer;
        transition: transform 160ms cubic-bezier(0.16, 1, 0.3, 1), box-shadow 180ms ease, background 180ms ease, border-color 180ms ease;

        &:hover {
          background: rgba(255, 255, 255, 0.16);
          border-color: rgba(255, 255, 255, 0.38);
          color: #ffffff;
          box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
          transform: translateY(-2px);
          text-decoration: none !important;
        }

        &:active {
          transform: scale(0.98);
        }
      }
      .btn-hero-ghost {
        color: #cbd5e1;
        padding: 0.65rem 1.15rem;
        font-weight: 600;
        font-size: 0.9375rem;
        text-decoration: none;
        transition: all 160ms ease;
        &:hover {
          text-decoration: none;
          color: #ffffff;
          transform: translateX(3px);
        }
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
            title: 'Hồ sơ Đăng ký',
            badge: 'Chờ duyệt',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} doanh nghiệp chờ duyệt`,
            link: '/platform/companies',
            label: 'Duyệt đăng ký doanh nghiệp',
          },
          box2: {
            title: 'Kiểm toán Hệ thống',
            badge: 'Nhật ký bảo mật',
            metric: String(this.auditCount()),
            sublabel: `${this.auditCount()} bản ghi kiểm toán`,
            link: '/platform/audit',
            label: 'Xem nhật ký kiểm toán',
          },
          box3: {
            title: 'Trạng thái Nền tảng',
            badge: '100% Hoạt động',
            metric: 'Ổn định',
            sublabel: 'Tất cả dịch vụ trực tuyến',
            link: '/platform/companies',
            label: 'Giám sát vận hành',
          },
          box4: {
            title: 'Thông báo Hệ thống',
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
            title: 'Nhiệm vụ Toàn công ty',
            badge: 'Đang làm',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} đang làm · ${this.tasksTotalCount()} tổng số`,
            link: '/tasks',
            label: 'Xem danh sách công việc',
          },
          box2: {
            title: 'Yêu cầu Toàn công ty',
            badge: 'Chưa xong',
            metric: String(this.requestsPendingCount()),
            sublabel: `${this.requestsPendingCount()} chưa xong · ${this.requestsTotalCount()} tổng số`,
            link: '/requests',
            label: 'Xem danh sách yêu cầu',
          },
          box3: {
            title: 'Tuân thủ SLA Doanh nghiệp',
            badge: this.slaRate(),
            metric: this.slaStatus(),
            sublabel: 'Cam kết chất lượng vận hành',
            link: '/reports',
            label: 'Báo cáo & Dashboard',
          },
          box4: {
            title: 'Hộp thư Cập nhật',
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
            title: 'Nhiệm vụ Phòng ban',
            badge: 'Phụ trách',
            metric: String(this.tasksActiveCount()),
            sublabel: `${this.tasksActiveCount()} đang làm · ${this.tasksTotalCount()} tổng số`,
            link: '/tasks',
            label: 'Xem danh sách công việc',
          },
          box2: {
            title: 'Yêu cầu Phòng ban',
            badge: 'Cần duyệt',
            metric: String(this.requestsPendingCount()),
            sublabel: `${this.requestsPendingCount()} chưa xong · ${this.requestsTotalCount()} tổng số`,
            link: '/requests',
            label: 'Xem danh sách yêu cầu',
          },
          box3: {
            title: 'Tuân thủ SLA Phòng ban',
            badge: this.slaRate(),
            metric: this.slaStatus(),
            sublabel: 'Tiến độ xử lý phòng ban',
            link: '/sla-monitor',
            label: 'Giám sát SLA',
          },
          box4: {
            title: 'Hộp thư Cập nhật',
            badge: 'Chưa đọc',
            metric: String(this.unreadNotificationsCount()),
            sublabel: `${this.unreadNotificationsCount()} chưa đọc · ${this.totalNotificationsCount()} tổng số`,
            link: '/notifications',
            label: 'Xem thông báo',
          },
        };
      default: { // EMPLOYEE
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
            title: 'Tiến độ Hoàn thành',
            badge: empRate,
            metric: 'Đạt chuẩn',
            sublabel: 'Chỉ số năng suất cá nhân',
            link: '/tasks',
            label: 'Chi tiết tiến độ',
          },
          box4: {
            title: 'Hộp thư Cập nhật',
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

  currentRoleLabel(): string {
    const id = this.identity();
    if (!id?.tenantId) return 'Quản trị viên Hệ thống';
    if (id.permissions.includes('roles.configure')) return 'Quản trị viên Doanh nghiệp';
    if (id.permissions.includes('tasks.assign')) return 'Quản lý Vận hành';
    return 'Thành viên';
  }

  signOut(): void {
    this.session.clear();
    this.router.navigate(['/login']);
  }
}
