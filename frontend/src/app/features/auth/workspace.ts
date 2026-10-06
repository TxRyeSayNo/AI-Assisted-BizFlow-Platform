import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-workspace',
  imports: [RouterLink],
  template: `
    <header class="public-header">
      <span class="wordmark">BizFlow</span
      ><span>{{ identity()?.tenantName ?? 'Platform administration' }}</span>
    </header>
    <main id="main-content" class="page-content" tabindex="-1">
      <span class="eyebrow">Your workspace</span>
      <h1>Welcome, {{ identity()?.fullName }}</h1>
      <p>You are signed in to {{ identity()?.tenantName ?? 'the platform console' }}.</p>
      @if (identity()?.tenantId) {
        @if (
          session.hasPermission('tasks.read.tenant') ||
          session.hasPermission('tasks.read.managed') ||
          session.hasPermission('tasks.read.own') ||
          session.hasPermission('tasks.read.assigned')
        ) {
          <p><a routerLink="/tasks">View tasks</a></p>
        }
        <p><a routerLink="/notifications">Notifications</a></p>
        <p><a routerLink="/settings/services">Browse service catalog</a></p>
        <a routerLink="/settings/organization/departments">Browse department directory</a>
        @if (session.hasPermission('users.read')) {
          <p><a routerLink="/settings/organization/users">Browse people directory</a></p>
        }
        @if (
          session.hasPermission('roles.configure') ||
          session.hasPermission('workflows.read') ||
          session.hasPermission('sla.read') ||
          session.hasPermission('service.create') ||
          session.hasPermission('service.update')
        ) {
          <p><a routerLink="/settings">Settings</a></p>
        }
        @if (session.hasPermission('audit.read')) {
          <p><a routerLink="/audit">Audit history</a></p>
        }
      }
      @if (
        identity()?.tenantId === null &&
        session.hasPermission('platform.company-registrations.read')
      ) {
        <a routerLink="/platform/companies">Review company registrations</a>
      }
      @if (identity()?.tenantId === null && session.hasPermission('platform.audit.read')) {
        <p><a routerLink="/platform/audit">Platform audit</a></p>
      }
    </main>
  `,
})
export class Workspace {
  readonly session = inject(SessionService);
  readonly identity = this.session.identity;
}
