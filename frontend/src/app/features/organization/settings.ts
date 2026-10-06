import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';

@Component({
  selector: 'bf-settings',
  imports: [RouterLink],
  template: ` <header class="public-header">
      <a class="wordmark" routerLink="/workspace">BizFlow</a><span>Workspace settings</span>
    </header>
    <main id="main-content" class="page-content" tabindex="-1">
      <a routerLink="/workspace">Back to workspace</a>
      <h1>Settings</h1>
      <h2>Internal services</h2>
      <p>
        <a routerLink="/settings/services">Service catalog</a> — browse services and categories.
      </p>
      <h2>Organization</h2>
      @if (session.hasPermission('users.read')) {
        <p>
          <a routerLink="/settings/organization/users">People directory</a> — find colleagues and
          departments.
        </p>
      }
      @if (session.hasPermission('roles.configure')) {
        <p>
          <a routerLink="/settings/organization/roles">Roles and permissions</a> — configure custom
          role access.
        </p>
      }
      <p>
        <a routerLink="/settings/organization/departments">Department directory</a> — browse
        existing departments.
      </p>
      @if (session.hasPermission('workflows.read')) {
        <h2>Workflow configuration</h2>
        <p>
          <a routerLink="/settings/workflows">Workflows</a> — browse versions and start workflow
          drafts.
        </p>
      }
      @if (session.hasPermission('sla.read')) {
        <h2>SLA configuration</h2>
        <p>
          <a routerLink="/settings/sla-profiles">SLA profiles</a> — browse and create named draft
          profiles.
        </p>
      }
    </main>`,
})
export class Settings {
  readonly session = inject(SessionService);
}
