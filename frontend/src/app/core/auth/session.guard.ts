import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session';

// Navigation convenience only. Every API operation must enforce server authorization.
export const sessionGuard: CanActivateFn = () =>
  inject(SessionService).signedIn() || inject(Router).createUrlTree(['/login']);

// Tenant catalog reads require membership, not an invented role/permission grant.
export const tenantGuard: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);
  if (!session.signedIn()) return router.createUrlTree(['/login']);
  return !!session.identity()?.tenantId || router.createUrlTree(['/access-denied']);
};

export const permissionGuard: CanActivateFn = (route) => {
  const session = inject(SessionService);
  const router = inject(Router);
  if (!session.signedIn()) return router.createUrlTree(['/login']);
  const permission = route.data['permission'];
  const alternatives: unknown = route.data['anyPermissions'];
  return (
    (typeof permission === 'string' && session.hasPermission(permission)) ||
    (Array.isArray(alternatives) &&
      alternatives.some(
        (code: unknown) => typeof code === 'string' && session.hasPermission(code),
      )) ||
    router.createUrlTree(['/access-denied'])
  );
};
