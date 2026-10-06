import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Roles, RoleRow } from './roles';

describe('workspace role configuration', () => {
  const custom: RoleRow = {
    roleId: 'custom',
    name: 'Support',
    isSystem: false,
    status: 'ACTIVE',
    permissionIds: [],
    eTag: '"1"',
  };
  const catalog = [
    {
      permissionId: 'grant',
      code: 'roles.configure',
      module: 'roles',
      action: 'configure',
      scope: 'TENANT',
    },
  ];
  const page = { items: [custom], page: 1, pageSize: 25, total: 1, availablePermissions: catalog };
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const http = () => TestBed.inject(HttpTestingController);
  const list = () =>
    http().expectOne((request) => request.url === '/api/v1/roles' && request.method === 'GET');

  it('shows loading, empty, failure and permission-denied states without provider text', () => {
    const fixture = TestBed.createComponent(Roles);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading roles and permissions');
    list().flush({ ...page, items: [], total: 0 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No roles match');
    fixture.componentInstance.load(1);
    list().flush({ message: 'private' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Roles could not be loaded');
    fixture.componentInstance.load(1);
    list().flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have permission');
    expect(fixture.nativeElement.textContent).not.toContain('private');
    expect(fixture.nativeElement.querySelector('#create-role-title')).toBeNull();
  });

  it('creates only a name, then refreshes the newly created role search', () => {
    const component = TestBed.createComponent(Roles).componentInstance;
    list().flush(page);
    component.createForm.setValue({ name: ' Helpdesk ' });
    component.create();
    const create = http().expectOne(
      (request) => request.method === 'POST' && request.url === '/api/v1/roles',
    );
    expect(create.request.body).toEqual({ name: 'Helpdesk' });
    create.flush({ ...custom, name: 'Helpdesk' });
    const reload = list();
    expect(reload.request.params.get('search')).toBe('Helpdesk');
    reload.flush(page);
    expect(component.notice()).toContain('with no permissions');
  });

  it('sends the loaded ETag and prevents overwriting a concurrent change', () => {
    const component = TestBed.createComponent(Roles).componentInstance;
    list().flush(page);
    component.select(custom);
    component.toggle('grant', true);
    component.save();
    const save = http().expectOne('/api/v1/roles/custom/permissions');
    expect(save.request.headers.get('If-Match')).toBe('"1"');
    expect(save.request.body).toEqual({ permissionIds: ['grant'] });
    save.flush(
      { code: 'ROLE.VERSION_CONFLICT', message: 'private' },
      { status: 409, statusText: 'Conflict' },
    );
    expect(component.conflict()).toBe(true);
    expect(component.mutationError()).toContain('Reload');
    expect(component.mutationError()).not.toContain('private');
    component.save();
    http().expectNone('/api/v1/roles/custom/permissions');
  });

  it('does not edit protected system roles and clears stale configuration after a denied save', () => {
    const component = TestBed.createComponent(Roles).componentInstance;
    list().flush(page);
    component.select({ ...custom, isSystem: true });
    expect(component.selected()).toBeNull();
    component.select(custom);
    component.save();
    http()
      .expectOne('/api/v1/roles/custom/permissions')
      .flush({}, { status: 403, statusText: 'Forbidden' });
    expect(component.result()).toBeNull();
    expect(component.selected()).toBeNull();
    expect(component.denied()).toBe(true);
  });

  it('adopts the returned version after a successful save and can remove permissions', () => {
    const component = TestBed.createComponent(Roles).componentInstance;
    list().flush(page);
    component.select(custom);
    component.toggle('grant', true);
    component.save();
    http()
      .expectOne('/api/v1/roles/custom/permissions')
      .flush({ ...custom, permissionIds: ['grant'], eTag: '"2"' });
    expect(component.selected()?.eTag).toBe('"2"');
    component.toggle('grant', false);
    component.save();
    const removal = http().expectOne('/api/v1/roles/custom/permissions');
    expect(removal.request.headers.get('If-Match')).toBe('"2"');
    expect(removal.request.body).toEqual({ permissionIds: [] });
    removal.flush({ ...custom, eTag: '"3"' });
    expect(component.notice()).toContain('Permissions saved');
  });
});
