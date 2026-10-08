import { Routes } from '@angular/router';
import { routes } from './app.routes';
import { permissionGuard, tenantGuard } from './core/auth/session.guard';

describe('Application route catalog', () => {
  it('preserves exactly one tenant-authorized SLA history route', async () => {
    const history = routes.filter((route) => route.path === 'settings/sla-profiles/:id/versions');
    expect(history).toHaveLength(1);
    expect(history[0].canActivate).toEqual([tenantGuard, permissionGuard]);
    expect(history[0].data).toEqual({ permission: 'sla.read' });
    expect(history[0].loadComponent).toBeDefined();
  });
  it('has no duplicate sibling paths that silently shadow a feature or its guards', () => {
    const check = (siblings: Routes) => {
      const paths = siblings.filter((route) => route.path !== undefined).map((route) => route.path);
      expect(new Set(paths).size).toBe(paths.length);
      for (const route of siblings) if (route.children) check(route.children);
    };
    check(routes);
  });
  it('registers request routes with appropriate tenant and permission guards', () => {
    const reqList = routes.find((r) => r.path === 'requests');
    expect(reqList).toBeDefined();
    expect(reqList?.canActivate).toEqual([tenantGuard, permissionGuard]);
    expect(reqList?.data?.['anyPermissions']).toContain('requests.read.own');

    const reqCreate = routes.find((r) => r.path === 'requests/new');
    expect(reqCreate).toBeDefined();
    expect(reqCreate?.canActivate).toEqual([tenantGuard, permissionGuard]);
    expect(reqCreate?.data?.['permission']).toBe('requests.create');

    const reqDetail = routes.find((r) => r.path === 'requests/:id');
    expect(reqDetail).toBeDefined();
    expect(reqDetail?.canActivate).toEqual([tenantGuard, permissionGuard]);
    expect(reqDetail?.data?.['anyPermissions']).toContain('requests.read.tenant');
  });
  it('registers reports route with tenant and reporting permission guards', () => {
    const reportRoute = routes.find((r) => r.path === 'reports');
    expect(reportRoute).toBeDefined();
    expect(reportRoute?.canActivate).toEqual([tenantGuard, permissionGuard]);
    expect(reportRoute?.data?.['anyPermissions']).toContain('reports.manager');
    expect(reportRoute?.data?.['anyPermissions']).toContain('reports.company');
    expect(reportRoute?.data?.['anyPermissions']).toContain('reports.workload');
    expect(reportRoute?.data?.['anyPermissions']).toContain('reports.sla');
  });
});
