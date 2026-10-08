import { Routes } from '@angular/router';

import { permissionGuard, sessionGuard, tenantGuard } from './core/auth/session.guard';

export const routes: Routes = [
  {
    path: 'requests/new',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'requests.create' },
    loadComponent: () => import('./features/requests/request-create').then((m) => m.RequestCreate),
  },
  {
    path: 'requests/:id',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: ['requests.read.tenant', 'requests.read.managed', 'requests.read.own'],
    },
    loadComponent: () => import('./features/requests/request-detail').then((m) => m.RequestDetail),
  },
  {
    path: 'requests',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: ['requests.read.tenant', 'requests.read.managed', 'requests.read.own'],
    },
    loadComponent: () => import('./features/requests/requests').then((m) => m.Requests),
  },
  {
    path: 'tasks/new',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'tasks.create' },
    loadComponent: () => import('./features/tasks/task-create').then((m) => m.TaskCreate),
  },
  {
    path: 'tasks/:id',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: [
        'tasks.read.tenant',
        'tasks.read.managed',
        'tasks.read.own',
        'tasks.read.assigned',
      ],
    },
    loadComponent: () => import('./features/tasks/task-detail').then((m) => m.TaskDetail),
  },
  {
    path: 'tasks',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: [
        'tasks.read.tenant',
        'tasks.read.managed',
        'tasks.read.own',
        'tasks.read.assigned',
      ],
    },
    loadComponent: () => import('./features/tasks/tasks').then((m) => m.Tasks),
  },
  {
    path: 'records/search',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'records.search' },
    loadComponent: () =>
      import('./features/collaboration/record-search').then((m) => m.RecordSearch),
  },
  {
    path: 'reports',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: ['reports.manager', 'reports.company', 'reports.workload', 'reports.sla'],
    },
    loadComponent: () => import('./features/reports/reports').then((m) => m.Reports),
  },
  {
    path: 'dashboard',
    redirectTo: 'reports',
    pathMatch: 'full',
  },
  {
    path: 'settings/services',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/services/services').then((m) => m.Services),
  },
  {
    path: 'settings/sla-profiles/:id/versions',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'sla.read' },
    loadComponent: () =>
      import('./features/sla/sla-version-history').then((m) => m.SlaVersionHistory),
  },
  {
    path: 'settings/sla-profiles',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'sla.read' },
    loadComponent: () => import('./features/sla/sla-profiles').then((m) => m.SlaProfiles),
  },
  {
    path: 'notifications',
    canActivate: [tenantGuard],
    loadComponent: () =>
      import('./features/notifications/notifications').then((m) => m.Notifications),
  },
  {
    path: 'audit',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'audit.read', plane: 'tenant' },
    loadComponent: () => import('./features/audit/audit').then((m) => m.Audit),
  },
  {
    path: 'platform/audit',
    canActivate: [permissionGuard],
    data: { permission: 'platform.audit.read', plane: 'platform' },
    loadComponent: () => import('./features/audit/audit').then((m) => m.Audit),
  },
  {
    path: 'settings/workflows',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'workflows.read' },
    loadComponent: () => import('./features/workflows/workflows').then((m) => m.Workflows),
  },
  {
    path: 'settings/workflows/:id/versions/:v',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'workflows.read' },
    loadComponent: () =>
      import('./features/workflows/workflow-version').then((m) => m.WorkflowVersion),
  },
  {
    path: 'settings/organization/users',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'users.read' },
    loadComponent: () => import('./features/organization/users').then((m) => m.Users),
  },
  {
    path: 'settings',
    canActivate: [tenantGuard, permissionGuard],
    data: {
      anyPermissions: [
        'roles.configure',
        'workflows.read',
        'sla.read',
        'service.create',
        'service.update',
      ],
    },
    loadComponent: () => import('./features/organization/settings').then((m) => m.Settings),
  },
  {
    path: 'settings/organization/roles',
    canActivate: [tenantGuard, permissionGuard],
    data: { permission: 'roles.configure' },
    loadComponent: () => import('./features/organization/roles').then((m) => m.Roles),
  },
  {
    path: 'settings/organization/departments',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/organization/departments').then((m) => m.Departments),
  },
  {
    path: 'platform/companies',
    canActivate: [permissionGuard],
    data: { permission: 'platform.company-registrations.read' },
    loadComponent: () =>
      import('./features/platform/company-registrations').then((m) => m.CompanyRegistrations),
  },
  { path: 'login', loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  {
    path: 'forgot-password',
    loadComponent: () => import('./features/auth/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: 'workspace',
    canActivate: [sessionGuard],
    loadComponent: () => import('./features/auth/workspace').then((m) => m.Workspace),
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./shared/feedback/access-denied').then((m) => m.AccessDenied),
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'access-denied' },
];
