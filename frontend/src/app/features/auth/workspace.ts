import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SessionService } from '../../core/auth/session';
import { AiAgentService } from '../../core/ai/ai-agent.service';
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
              ('common.platformAdmin' | translate: 'Platform administration')
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
          <a routerLink="/approvals" class="nav-link">{{
            'nav.approvals' | translate: 'Approvals'
          }}</a>
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
          <a routerLink="/sla-monitor" class="nav-link">{{
            'nav.slaMonitor' | translate: 'SLA Monitor'
          }}</a>
          @if (session.hasPermission('records.search')) {
            <a routerLink="/records/search" class="nav-link">{{
              'common.search' | translate: 'Search'
            }}</a>
          }
          <a routerLink="/settings" class="nav-link">{{
            'nav.settings' | translate: 'Settings'
          }}</a>
        } @else {
          <a routerLink="/platform/companies" class="nav-link">{{
            'nav.companyRegistrations' | translate: 'Companies'
          }}</a>
          <a routerLink="/platform/audit" class="nav-link">{{
            'nav.platformAudit' | translate: 'Platform Audit'
          }}</a>
        }
      </nav>

      <!-- Right Utility Bar -->
      <div class="nav-utilities">
        <button
          type="button"
          class="ai-trigger-btn"
          (click)="openAiAgent()"
          title="Open AI Assistant"
        >
          <span class="sparkle">✦</span>
          <span>{{ 'nav.aiAssistant' | translate: 'AI Assistant' }}</span>
        </button>

        <a routerLink="/notifications" class="notify-btn" title="Notifications">
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

        <button type="button" class="btn-signout" (click)="signOut()" title="Sign out">
          {{ 'nav.signOut' | translate: 'Sign out' }}
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
            <span class="eyebrow">{{ 'workspace.title' | translate: 'Your workspace' }}</span>
            <span class="role-chip">{{ currentRoleLabel() }}</span>
          </div>

          <h1>{{ 'common.welcome' | translate: 'Welcome' }}, {{ identity()?.fullName }}</h1>
          <p class="hero-subtext">
            {{ 'workspace.signedInAs' | translate: 'You are signed in to' }}
            {{ identity()?.tenantName ?? 'the platform console' }}.
          </p>

          <!-- Quick Action Buttons -->
          <div class="hero-actions">
            @if (session.hasPermission('tasks.create')) {
              <a routerLink="/tasks/new" class="btn-hero-primary">{{
                'workspace.newTask' | translate: '+ New Task'
              }}</a>
            }
            @if (session.hasPermission('requests.create')) {
              <a routerLink="/requests/new" class="btn-hero-secondary">{{
                'workspace.newRequest' | translate: '+ New Request'
              }}</a>
            }
            <button type="button" class="btn-hero-ai" (click)="openAiAgent()">
              {{ 'workspace.aiCopilot' | translate: '✦ Ask AI Assistant' }}
            </button>
            <a routerLink="/approvals" class="btn-hero-ghost">
              {{ 'approvals.title' | translate: 'Review Approvals' }} →
            </a>
          </div>
        </div>
      </section>

      <!-- KPI Executive Metrics Row -->
      <section class="kpi-metric-row">
        <div class="kpi-box azure-accent">
          <div class="kpi-top">
            <span class="kpi-title">{{
              'workspace.kpiActiveTasks' | translate: 'Active Work Items'
            }}</span>
            <span class="kpi-badge">Live Scope</span>
          </div>
          <div class="kpi-metric">26</div>
          <p class="kpi-foot">
            <a routerLink="/tasks">{{ 'workspace.viewTasks' | translate: 'View tasks' }}</a>
          </p>
        </div>

        <div class="kpi-box ocean-accent">
          <div class="kpi-top">
            <span class="kpi-title">{{
              'workspace.kpiPendingRequests' | translate: 'Internal Requests'
            }}</span>
            <span class="kpi-badge">Queued</span>
          </div>
          <div class="kpi-metric">14</div>
          <p class="kpi-foot">
            <a routerLink="/requests">{{
              'workspace.viewRequests' | translate: 'View requests'
            }}</a>
          </p>
        </div>

        <div class="kpi-box green-accent">
          <div class="kpi-top">
            <span class="kpi-title">{{
              'workspace.kpiSlaCompliance' | translate: 'SLA Compliance'
            }}</span>
            <span class="kpi-badge green">98.6%</span>
          </div>
          <div class="kpi-metric">Optimal</div>
          <p class="kpi-foot">
            <a routerLink="/reports">{{
              'workspace.dashboardReports' | translate: 'Dashboard & Reports'
            }}</a>
          </p>
        </div>

        <div class="kpi-box navy-accent">
          <div class="kpi-top">
            <span class="kpi-title">{{
              'workspace.cardNotificationsTitle' | translate: 'Inbox Updates'
            }}</span>
            <span class="kpi-badge">{{ 'notifications.unread' | translate: 'Unread' }}</span>
          </div>
          <div class="kpi-metric">3</div>
          <p class="kpi-foot">
            <a routerLink="/notifications">{{
              'workspace.notifications' | translate: 'Notifications'
            }}</a>
          </p>
        </div>
      </section>

      <!-- Organized Workspace Modules Grid (Preserves all exact links & texts for tests) -->
      <section class="modules-section">
        <h2 class="section-title">
          {{ 'workspace.sectionExecution' | translate: 'Operational Workspace Modules' }}
        </h2>
        <div class="modules-grid">
          <!-- Work & Tasks -->
          @if (identity()?.tenantId) {
            @if (
              session.hasPermission('tasks.read.tenant') ||
              session.hasPermission('tasks.read.managed') ||
              session.hasPermission('tasks.read.own') ||
              session.hasPermission('tasks.read.assigned')
            ) {
              <article class="module-card">
                <div class="module-icon">📋</div>
                <h3>{{ 'workspace.cardTasksTitle' | translate: 'Task Management' }}</h3>
                <p>
                  {{
                    'workspace.cardTasksDesc'
                      | translate
                        : 'Track work, current responsibility, and deadlines across your access scope.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/tasks">{{ 'workspace.viewTasks' | translate: 'View tasks' }}</a>
                  </p>
                </div>
              </article>
            }

            @if (
              session.hasPermission('requests.read.tenant') ||
              session.hasPermission('requests.read.managed') ||
              session.hasPermission('requests.read.own')
            ) {
              <article class="module-card">
                <div class="module-icon">📨</div>
                <h3>{{ 'workspace.cardRequestsTitle' | translate: 'Service Requests' }}</h3>
                <p>
                  {{
                    'workspace.cardRequestsDesc'
                      | translate
                        : 'Submit and coordinate multi-category internal service requests with SLA clocks.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/requests">{{
                      'workspace.viewRequests' | translate: 'View requests'
                    }}</a>
                  </p>
                </div>
              </article>
            }

            @if (session.hasPermission('records.search')) {
              <article class="module-card">
                <div class="module-icon">🔍</div>
                <h3>{{ 'workspace.cardSearchTitle' | translate: 'Audit-Grade Search' }}</h3>
                <p>
                  {{
                    'workspace.cardSearchDesc'
                      | translate
                        : 'Full-text search across scoped tasks, requests, comments, and attachments.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/records/search">{{
                      'workspace.searchRecords' | translate: 'Search records'
                    }}</a>
                  </p>
                </div>
              </article>
            }

            @if (
              session.hasPermission('reports.manager') ||
              session.hasPermission('reports.company') ||
              session.hasPermission('reports.workload') ||
              session.hasPermission('reports.sla')
            ) {
              <article class="module-card">
                <div class="module-icon">📊</div>
                <h3>{{ 'workspace.cardReportsTitle' | translate: 'Intelligence & Analytics' }}</h3>
                <p>
                  {{
                    'workspace.cardReportsDesc'
                      | translate
                        : 'Executive dashboards, workload allocation, and SLA performance charts.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/reports">{{
                      'workspace.dashboardReports' | translate: 'Dashboard & Reports'
                    }}</a>
                  </p>
                </div>
              </article>
            }

            <article class="module-card">
              <div class="module-icon">⚙</div>
              <h3>{{ 'workspace.cardServicesTitle' | translate: 'Service Catalog' }}</h3>
              <p>
                {{
                  'workspace.cardServicesDesc'
                    | translate
                      : 'Explore authorized company services, required forms, and routing definitions.'
                }}
              </p>
              <div class="module-link-wrap">
                <p>
                  <a routerLink="/settings/services">{{
                    'workspace.browseServices' | translate: 'Browse service catalog'
                  }}</a>
                </p>
              </div>
            </article>

            <article class="module-card">
              <div class="module-icon">🏢</div>
              <h3>{{ 'workspace.cardDeptsTitle' | translate: 'Department Directory' }}</h3>
              <p>
                {{
                  'workspace.cardDeptsDesc'
                    | translate
                      : 'Explore organizational hierarchy and queue boundaries across the company.'
                }}
              </p>
              <div class="module-link-wrap">
                <a routerLink="/settings/organization/departments">{{
                  'workspace.browseDepartments' | translate: 'Browse department directory'
                }}</a>
              </div>
            </article>

            @if (session.hasPermission('users.read')) {
              <article class="module-card">
                <div class="module-icon">👥</div>
                <h3>{{ 'workspace.cardUsersTitle' | translate: 'People Directory' }}</h3>
                <p>
                  {{
                    'workspace.cardUsersDesc'
                      | translate
                        : 'Browse team members, employee codes, roles, and department assignments.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/settings/organization/users">{{
                      'workspace.browseUsers' | translate: 'Browse people directory'
                    }}</a>
                  </p>
                </div>
              </article>
            }

            @if (
              session.hasPermission('roles.configure') ||
              session.hasPermission('workflows.read') ||
              session.hasPermission('sla.read') ||
              session.hasPermission('service.create') ||
              session.hasPermission('service.update')
            ) {
              <article class="module-card">
                <div class="module-icon">🛠</div>
                <h3>{{ 'workspace.cardSettingsTitle' | translate: 'Company Setup' }}</h3>
                <p>
                  {{
                    'workspace.cardSettingsDesc'
                      | translate
                        : 'Configure custom roles, workflows, SLAs, and tenant-level configurations.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/settings">{{ 'workspace.settings' | translate: 'Settings' }}</a>
                  </p>
                </div>
              </article>
            }

            @if (session.hasPermission('audit.read')) {
              <article class="module-card">
                <div class="module-icon">🛡</div>
                <h3>{{ 'workspace.cardAuditTitle' | translate: 'Audit Trail' }}</h3>
                <p>
                  {{
                    'workspace.cardAuditDesc'
                      | translate
                        : 'Immutable append-only replay of all transactions, state changes, and reviews.'
                  }}
                </p>
                <div class="module-link-wrap">
                  <p>
                    <a routerLink="/audit">{{ 'workspace.audit' | translate: 'Audit history' }}</a>
                  </p>
                </div>
              </article>
            }
          }

          <!-- Platform Administrator Modules -->
          @if (
            identity()?.tenantId === null &&
            session.hasPermission('platform.company-registrations.read')
          ) {
            <article class="module-card platform-card">
              <div class="module-icon">🌐</div>
              <h3>
                {{ 'workspace.cardPlatformCompaniesTitle' | translate: 'Platform Companies' }}
              </h3>
              <p>
                {{
                  'workspace.cardPlatformCompaniesDesc'
                    | translate: 'Review new company registrations and tenant onboarding requests.'
                }}
              </p>
              <div class="module-link-wrap">
                <a routerLink="/platform/companies">Review company registrations</a>
              </div>
            </article>
          }

          @if (identity()?.tenantId === null && session.hasPermission('platform.audit.read')) {
            <article class="module-card platform-card">
              <div class="module-icon">🏛</div>
              <h3>{{ 'workspace.cardPlatformAuditTitle' | translate: 'Platform Audit' }}</h3>
              <p>
                {{
                  'workspace.cardPlatformAuditDesc'
                    | translate
                      : 'Inspect system-wide tenant lifecycles, configuration revisions, and security events.'
                }}
              </p>
              <div class="module-link-wrap">
                <p><a routerLink="/platform/audit">Platform audit</a></p>
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
        background: linear-gradient(135deg, #071b33 0%, #0b2a4a 50%, #145da0 100%);
        color: #ffffff;
        border-radius: 16px;
        padding: 2.25rem 2.5rem;
        margin-bottom: 1.75rem;
        overflow: hidden;
        box-shadow: 0 12px 36px -4px rgba(7, 27, 51, 0.16);
        h1 {
          color: #ffffff;
          font-size: clamp(1.85rem, 2.7vw, 2.5rem);
          margin: 0.35rem 0 0.5rem;
        }
      }
      .hero-bg-mesh {
        position: absolute;
        inset: 0;
        background:
          radial-gradient(circle at 85% 20%, rgba(142, 197, 255, 0.22) 0%, transparent 45%),
          radial-gradient(circle at 20% 80%, rgba(37, 99, 235, 0.2) 0%, transparent 50%);
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
        gap: 0.75rem;
        margin-bottom: 0.5rem;
        .eyebrow {
          color: #8ec5ff;
        }
      }
      .role-chip {
        background: rgba(255, 255, 255, 0.15);
        color: #ffffff;
        padding: 0.15rem 0.55rem;
        border-radius: 9999px;
        font-size: 0.72rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }
      .hero-subtext {
        color: #eaf4ff;
        font-size: 1.05rem;
        margin: 0 0 1.5rem;
        max-width: 42rem;
      }
      .hero-actions {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
      }
      .btn-hero-primary {
        background: #2563eb;
        color: #ffffff;
        padding: 0.6rem 1.25rem;
        border-radius: 8px;
        font-weight: 700;
        font-size: 0.875rem;
        text-decoration: none;
        box-shadow: 0 4px 12px rgba(37, 99, 235, 0.35);
        &:hover {
          background: #1d4ed8;
          text-decoration: none;
          transform: translateY(-1px);
        }
      }
      .btn-hero-secondary {
        background: rgba(255, 255, 255, 0.18);
        color: #ffffff;
        padding: 0.6rem 1.25rem;
        border-radius: 8px;
        font-weight: 700;
        font-size: 0.875rem;
        text-decoration: none;
        border: 1px solid rgba(255, 255, 255, 0.25);
        &:hover {
          background: rgba(255, 255, 255, 0.28);
          text-decoration: none;
        }
      }
      .btn-hero-ai {
        background: linear-gradient(135deg, #0b2a4a 0%, #1e40af 100%);
        color: #8ec5ff;
        border: 1px solid #8ec5ff;
        padding: 0.6rem 1.15rem;
        border-radius: 8px;
        font-weight: 700;
        font-size: 0.875rem;
        cursor: pointer;
        &:hover {
          background: #1e40af;
          color: #ffffff;
        }
      }
      .btn-hero-ghost {
        color: #eaf4ff;
        padding: 0.6rem 1rem;
        font-weight: 600;
        font-size: 0.875rem;
        text-decoration: none;
        &:hover {
          text-decoration: underline;
          color: #ffffff;
        }
      }
      .kpi-metric-row {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
        gap: 1rem;
        margin-bottom: 2rem;
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
        a {
          font-weight: 600;
          color: #2563eb;
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
        p,
        a {
          margin: 0;
          font-size: 0.875rem;
          font-weight: 600;
          color: #2563eb;
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
export class Workspace {
  readonly session = inject(SessionService);
  private readonly router = inject(Router);
  readonly aiAgent = inject(AiAgentService);
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

  currentRoleLabel(): string {
    const id = this.identity();
    if (!id?.tenantId) return 'Platform Administrator';
    if (id.permissions.includes('roles.configure')) return 'Company Administrator';
    if (id.permissions.includes('tasks.assign')) return 'Operational Manager';
    return 'Authorized Member';
  }

  signOut(): void {
    this.session.clear();
    this.router.navigate(['/login']);
  }
}
