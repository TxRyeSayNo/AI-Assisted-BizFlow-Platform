import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'bf-settings',
  imports: [RouterLink, TranslatePipe],
  styleUrl: './settings.scss',
  template: `
    <div class="settings-page-wrapper">
      <!-- Top Application Navigation Bar -->
      <header class="public-header settings-header-nav">
        <a class="wordmark" routerLink="/workspace">
          <span class="brand-mark" aria-hidden="true"></span> BizFlow
        </a>
        <span class="tenant-badge">{{
          session.identity()?.tenantName ??
            ('settings.workspaceSettings' | translate: 'Workspace settings')
        }}</span>
      </header>

      <!-- Main Settings Content Area -->
      <main id="main-content" class="page-content settings-main-container" tabindex="-1">
        <nav aria-label="Breadcrumb" class="settings-breadcrumb">
          <a routerLink="/workspace" class="breadcrumb-back-link">
            <span class="arrow" aria-hidden="true">←</span>
            {{ 'settings.backToWorkspace' | translate: 'Back to workspace' }}
          </a>
        </nav>

        <div class="settings-header">
          <div class="header-content">
            <div class="eyebrow-badge">
              <span class="eyebrow">{{
                'settings.eyebrow' | translate: 'Enterprise Administration'
              }}</span>
            </div>
            <h1>{{ 'settings.title' | translate: 'Company Setup & Configuration' }}</h1>
            <p class="subtitle">
              {{
                'settings.subtitle'
                  | translate
                    : 'Configure tenant parameters, organizational structure, internal services, workflow state machines, and SLA compliance policies.'
              }}
            </p>
          </div>
        </div>

        <!-- Quick Setup Progress Tracker -->
        <section class="setup-progress-banner">
          <div class="progress-info">
            <h3>
              {{
                'settings.onboardingTitle' | translate: 'Tenant Onboarding & Operational Readiness'
              }}
            </h3>
            <p>
              {{
                'settings.onboardingDesc'
                  | translate: '4 of 5 core business modules configured for active tenancy.'
              }}
            </p>
          </div>
          <div class="progress-bar-container">
            <div class="progress-bar-wrap">
              <div class="progress-fill" style="width: 80%;"></div>
            </div>
            <div class="progress-percent-badge">
              {{ 'settings.readyBadge' | translate: '80% Ready' }}
            </div>
          </div>
        </section>

        <!-- Compact Settings Hub Grid -->
        <div class="settings-grid">
          <!-- Module 1: Internal Services -->
          <article class="setting-card">
            <div class="card-header-row">
              <div class="card-icon icon-services" aria-hidden="true">
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <path
                    d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"
                  />
                  <circle cx="12" cy="12" r="3" />
                </svg>
              </div>
            </div>
            <h2>{{ 'settings.servicesTitle' | translate: 'Internal services' }}</h2>
            <p class="card-desc">
              {{
                'settings.servicesDesc'
                  | translate
                    : 'Define internal service categories, requester fields, SLAs, and assignment routing.'
              }}
            </p>
            <div class="card-links">
              <div class="link-item">
                <a routerLink="/settings/services" class="action-link">
                  {{ 'settings.serviceCatalog' | translate: 'Service catalog' }}
                  <span class="link-arrow" aria-hidden="true">→</span>
                </a>
                <span class="link-desc">{{
                  'settings.serviceCatalogDesc' | translate: ' — browse services and categories.'
                }}</span>
              </div>
            </div>
          </article>

          <!-- Module 2: Organization -->
          <article class="setting-card">
            <div class="card-header-row">
              <div class="card-icon icon-organization" aria-hidden="true">
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                  <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
              </div>
            </div>
            <h2>{{ 'settings.orgTitle' | translate: 'Organization' }}</h2>
            <p class="card-desc">
              {{
                'settings.orgDesc'
                  | translate
                    : 'Manage company departments, user directories, and role-based access control.'
              }}
            </p>
            <div class="card-links">
              <div class="link-item">
                <a routerLink="/settings/organization/departments" class="action-link">
                  {{ 'settings.deptDirectory' | translate: 'Department directory' }}
                  <span class="link-arrow" aria-hidden="true">→</span>
                </a>
                <span class="link-desc">{{
                  'settings.deptDirectoryDesc' | translate: ' — browse existing departments.'
                }}</span>
              </div>
              @if (session.hasPermission('users.read')) {
                <div class="link-item">
                  <a routerLink="/settings/organization/users" class="action-link">
                    {{ 'settings.peopleDirectory' | translate: 'People directory' }}
                    <span class="link-arrow" aria-hidden="true">→</span>
                  </a>
                  <span class="link-desc">{{
                    'settings.peopleDirectoryDesc'
                      | translate: ' — find colleagues and departments.'
                  }}</span>
                </div>
              }
              @if (session.hasPermission('roles.configure')) {
                <div class="link-item">
                  <a routerLink="/settings/organization/roles" class="action-link">
                    {{ 'settings.rolesPermissions' | translate: 'Roles and permissions' }}
                    <span class="link-arrow" aria-hidden="true">→</span>
                  </a>
                  <span class="link-desc">{{
                    'settings.rolesPermissionsDesc' | translate: ' — configure custom role access.'
                  }}</span>
                </div>
              }
            </div>
          </article>

          <!-- Module 3: Workflow Configuration -->
          @if (session.hasPermission('workflows.read')) {
            <article class="setting-card">
              <div class="card-header-row">
                <div class="card-icon icon-workflow" aria-hidden="true">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  >
                    <rect x="3" y="3" width="6" height="6" rx="1.5" />
                    <rect x="15" y="15" width="6" height="6" rx="1.5" />
                    <path d="M6 9v3a3 3 0 0 0 3 3h6" />
                    <path d="M18 9v6" />
                  </svg>
                </div>
              </div>
              <h2>{{ 'settings.workflowTitle' | translate: 'Workflow configuration' }}</h2>
              <p class="card-desc">
                {{
                  'settings.workflowDesc'
                    | translate
                      : 'Design multi-step task and request workflows with version immutability.'
                }}
              </p>
              <div class="card-links">
                <div class="link-item">
                  <a routerLink="/settings/workflows" class="action-link">
                    {{ 'settings.workflows' | translate: 'Workflows' }}
                    <span class="link-arrow" aria-hidden="true">→</span>
                  </a>
                  <span class="link-desc">{{
                    'settings.workflowsDesc'
                      | translate: ' — browse versions and start workflow drafts.'
                  }}</span>
                </div>
              </div>
            </article>
          }

          <!-- Module 4: SLA Configuration -->
          @if (session.hasPermission('sla.read')) {
            <article class="setting-card">
              <div class="card-header-row">
                <div class="card-icon icon-sla" aria-hidden="true">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  >
                    <circle cx="12" cy="12" r="10" />
                    <polyline points="12 6 12 12 16 14" />
                  </svg>
                </div>
              </div>
              <h2>{{ 'settings.slaTitle' | translate: 'SLA configuration' }}</h2>
              <p class="card-desc">
                {{
                  'settings.slaDesc'
                    | translate
                      : 'Configure business calendars, target response thresholds, and tiered escalation matrices.'
                }}
              </p>
              <div class="card-links">
                <div class="link-item">
                  <a routerLink="/settings/sla-profiles" class="action-link">
                    {{ 'settings.slaProfiles' | translate: 'SLA profiles' }}
                    <span class="link-arrow" aria-hidden="true">→</span>
                  </a>
                  <span class="link-desc">{{
                    'settings.slaProfilesDesc'
                      | translate: ' — browse and create named draft profiles.'
                  }}</span>
                </div>
              </div>
            </article>
          }
        </div>
      </main>
    </div>
  `,
})
export class Settings {
  readonly session = inject(SessionService);
}
