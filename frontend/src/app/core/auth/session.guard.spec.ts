import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  provideRouter,
} from '@angular/router';
import { SessionService } from './session';
import { permissionGuard, tenantGuard } from './session.guard';
import { routes } from '../../app.routes';

describe('Task route permission union', () => {
  for (const permission of [
    'tasks.read.tenant',
    'tasks.read.managed',
    'tasks.read.own',
    'tasks.read.assigned',
    'users.read',
  ]) {
    it(`checks the real route for ${permission}`, () => {
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          {
            provide: SessionService,
            useValue: {
              signedIn: () => true,
              hasPermission: (code: string) => code === permission,
            },
          },
        ],
      });
      const route = routes.find((r) => r.path === 'tasks')!;
      const result = TestBed.runInInjectionContext(() =>
        permissionGuard({ data: route.data } as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      );
      expect(result).toEqual(
        permission.startsWith('tasks.read.')
          ? true
          : TestBed.inject(Router).createUrlTree(['/access-denied']),
      );
    });
  }
});

describe('tenant directory navigation guard', () => {
  for (const [label, signedIn, tenantId, destination] of [
    ['anonymous visitor', false, null, '/login'],
    ['tenantless platform account', true, null, '/access-denied'],
    ['tenant member without grants', true, 'tenant-a', null],
  ] as const) {
    it(`handles ${label}`, () => {
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          {
            provide: SessionService,
            useValue: { signedIn: () => signedIn, identity: () => ({ tenantId, permissions: [] }) },
          },
        ],
      });
      const result = TestBed.runInInjectionContext(() =>
        tenantGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      );
      expect(result).toEqual(
        destination ? TestBed.inject(Router).createUrlTree([destination]) : true,
      );
    });
  }
});

describe('people directory permission guard', () => {
  for (const [label, signedIn, allowed, destination] of [
    ['anonymous visitor', false, false, '/login'],
    ['tenant member without read grant', true, false, '/access-denied'],
    ['tenant member with read grant', true, true, null],
  ] as const) {
    it(`handles ${label}`, () => {
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          {
            provide: SessionService,
            useValue: {
              signedIn: () => signedIn,
              hasPermission: (code: string) => code === 'users.read' && allowed,
            },
          },
        ],
      });
      const result = TestBed.runInInjectionContext(() =>
        permissionGuard(
          { data: { permission: 'users.read' } } as unknown as ActivatedRouteSnapshot,
          {} as RouterStateSnapshot,
        ),
      );
      expect(result).toEqual(
        destination ? TestBed.inject(Router).createUrlTree([destination]) : true,
      );
    });
  }
});

describe('settings navigation alternatives', () => {
  for (const [label, codes, allowed] of [
    ['workflow reader', ['workflows.read'], true],
    ['SLA reader', ['sla.read'], true],
    ['service creator', ['service.create'], true],
    ['service editor', ['service.update'], true],
    ['role configurator', ['roles.configure'], true],
    ['unrelated grant', ['users.read'], false],
  ] as const) {
    it(`handles ${label} without broadening individual feature guards`, () => {
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          {
            provide: SessionService,
            useValue: {
              signedIn: () => true,
              hasPermission: (code: string) => (codes as readonly string[]).includes(code),
            },
          },
        ],
      });
      const result = TestBed.runInInjectionContext(() =>
        permissionGuard(
          {
            data: {
              anyPermissions: [
                'roles.configure',
                'workflows.read',
                'sla.read',
                'service.create',
                'service.update',
              ],
            },
          } as unknown as ActivatedRouteSnapshot,
          {} as RouterStateSnapshot,
        ),
      );
      expect(result).toEqual(
        allowed ? true : TestBed.inject(Router).createUrlTree(['/access-denied']),
      );
    });
  }
});
