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
});
