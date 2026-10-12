import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-settings',
  imports: [RouterLink],
  template: `
    <header class="public-header">
      <a class="wordmark" routerLink="/workspace">
        <span class="brand-mark" aria-hidden="true">B</span> BizFlow
      </a>
      <span class="tenant-badge">{{ session.identity()?.tenantName ?? 'Workspace settings' }}</span>
    </header>

    <main id="main-content" class="page-content" tabindex="-1">
      <nav aria-label="Breadcrumb" class="settings-breadcrumb">
        <a routerLink="/workspace">Back to workspace</a>
      </nav>

      <div class="settings-header">
        <div>
          <span class="eyebrow">Enterprise Administration</span>
          <h1>Company Setup & Configuration</h1>
          <p class="subtitle">
            Configure tenant parameters, organizational structure, internal services, workflow state machines, and SLA compliance policies.
          </p>
        </div>
      </div>

      <!-- Quick Setup Progress Tracker -->
      <section class="setup-progress-banner">
        <div class="progress-info">
          <h3>Tenant Onboarding & Operational Readiness</h3>
          <p>4 of 5 core business modules configured for active tenancy.</p>
        </div>
        <div class="progress-bar-wrap">
          <div class="progress-fill" style="width: 80%;"></div>
        </div>
      </section>

      <!-- Settings Hub Grid -->
      <div class="settings-grid">
        <!-- Module 1: Internal Services -->
        <article class="setting-card">
          <div class="card-icon" style="background: #eaf4ff; color: #145da0;">⚙</div>
          <h2>Internal services</h2>
          <p class="card-desc">
            Define internal service categories, requester fields, SLAs, and assignment routing.
          </p>
          <div class="card-links">
            <p>
              <a routerLink="/settings/services" class="action-link">Service catalog</a> — browse services and categories.
            </p>
          </div>
        </article>

        <!-- Module 2: Organization -->
        <article class="setting-card">
          <div class="card-icon" style="background: #ecfdf5; color: #047857;">👥</div>
          <h2>Organization</h2>
          <p class="card-desc">
            Manage company departments, user directories, and role-based access control.
          </p>
          <div class="card-links">
            <p>
              <a routerLink="/settings/organization/departments" class="action-link">Department directory</a> — browse existing departments.
            </p>
            @if (session.hasPermission('users.read')) {
              <p>
                <a routerLink="/settings/organization/users" class="action-link">People directory</a> — find colleagues and departments.
              </p>
            }
            @if (session.hasPermission('roles.configure')) {
              <p>
                <a routerLink="/settings/organization/roles" class="action-link">Roles and permissions</a> — configure custom role access.
              </p>
            }
          </div>
        </article>

        <!-- Module 3: Workflow Configuration -->
        @if (session.hasPermission('workflows.read')) {
          <article class="setting-card">
            <div class="card-icon" style="background: #e0f2fe; color: #0369a1;">🔀</div>
            <h2>Workflow configuration</h2>
            <p class="card-desc">
              Design multi-step task and request workflows with version immutability.
            </p>
            <div class="card-links">
              <p>
                <a routerLink="/settings/workflows" class="action-link">Workflows</a> — browse versions and start workflow drafts.
              </p>
            </div>
          </article>
        }

        <!-- Module 4: SLA Configuration -->
        @if (session.hasPermission('sla.read')) {
          <article class="setting-card">
            <div class="card-icon" style="background: #fef3c7; color: #92400e;">⏱</div>
            <h2>SLA configuration</h2>
            <p class="card-desc">
              Configure business calendars, target response thresholds, and tiered escalation matrices.
            </p>
            <div class="card-links">
              <p>
                <a routerLink="/settings/sla-profiles" class="action-link">SLA profiles</a> — browse and create named draft profiles.
              </p>
            </div>
          </article>
        }
      </div>
    </main>
  `,
  styles: [`
    .settings-breadcrumb {
      margin-bottom: 0.75rem;
    }
    .settings-header {
      margin-bottom: 1.5rem;
    }
    .subtitle {
      color: #63758b;
      margin: 0.35rem 0 0;
      font-size: 0.95rem;
      max-width: 48rem;
    }
    .setup-progress-banner {
      background: linear-gradient(135deg, #071b33 0%, #0b2a4a 55%, #145da0 100%);
      color: #ffffff;
      border-radius: 12px;
      padding: 1.25rem 1.5rem;
      margin-bottom: 1.75rem;
      box-shadow: 0 4px 16px rgba(7, 27, 51, 0.1);
      h3 { margin: 0 0 0.25rem; font-size: 1.05rem; color: #ffffff; }
      p { margin: 0; font-size: 0.8125rem; color: #8ec5ff; }
    }
    .progress-bar-wrap {
      height: 6px;
      background: rgba(255, 255, 255, 0.2);
      border-radius: 9999px;
      margin-top: 0.75rem;
      overflow: hidden;
    }
    .progress-fill {
      height: 100%;
      background: #2563eb;
      border-radius: 9999px;
      box-shadow: 0 0 10px #8ec5ff;
    }
    .settings-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 1.25rem;
    }
    .setting-card {
      background: #ffffff;
      border: 1px solid #dfe8f2;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 2px 8px rgba(7, 27, 51, 0.04);
      transition: all 200ms cubic-bezier(0.16, 1, 0.3, 1);
      &:hover {
        transform: translateY(-2px);
        box-shadow: 0 10px 28px -4px rgba(37, 99, 235, 0.12);
        border-color: #8ec5ff;
      }
      h2 {
        font-size: 1.2rem;
        margin: 0.75rem 0 0.35rem;
        color: #071b33;
      }
    }
    .card-icon {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 10px;
      display: grid;
      place-items: center;
      font-size: 1.25rem;
    }
    .card-desc {
      color: #63758b;
      font-size: 0.875rem;
      line-height: 1.5;
      margin: 0 0 1rem;
    }
    .card-links {
      border-top: 1px solid #f1f5f9;
      padding-top: 0.75rem;
      p {
        margin: 0.5rem 0;
        font-size: 0.875rem;
        color: #475569;
      }
    }
    .action-link {
      font-weight: 600;
      color: #2563eb;
      &:hover { text-decoration: underline; }
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
export class Settings {
  readonly session = inject(SessionService);
}
