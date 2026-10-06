import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { Workflows } from './workflows';

describe('workflow library', () => {
  let configure = true;
  beforeEach(() => {
    configure = true;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantName: 'Test workspace' }),
            hasPermission: (code: string) => code === 'workflows.read' || configure,
          },
        },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/workflows');
  const empty = { items: [], page: 1, pageSize: 25, total: 0 };
  const row = {
    workflowId: 'one',
    name: 'Access requests',
    businessType: 'REQUEST',
    status: 'DRAFT',
    latestVersionId: 'version-one',
    latestVersionNo: 1,
    latestVersionStatus: 'DRAFT',
  };

  it('renders empty state without invented configuration authority for readers', () => {
    configure = false;
    const fixture = TestBed.createComponent(Workflows);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading workflows');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No workflows match');
    expect(fixture.nativeElement.textContent).not.toContain('Create workflow draft');
    fixture.componentInstance.createForm.setValue({ name: 'Unauthorized', businessType: 'TASK' });
    fixture.componentInstance.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
  });

  it('creates only metadata, prevents double submission and preserves the server-created version link', () => {
    const fixture = TestBed.createComponent(Workflows);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.createForm.setValue({ name: ' Access requests ', businessType: 'REQUEST' });
    component.create();
    component.create();
    const create = pending();
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Access requests', businessType: 'REQUEST' });
    create.flush(row);
    const reload = pending();
    expect(reload.request.params.get('search')).toBe('Access requests');
    reload.flush({ ...empty, total: 1, items: [row] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Draft created with an empty first version',
    );
    expect(
      fixture.nativeElement.querySelector('a[href="/settings/workflows/one/versions/version-one"]'),
    ).not.toBeNull();
    expect(component.createForm.getRawValue().name).toBe('');
  });

  it('escapes names and clears rows and creation controls when permission is revoked', () => {
    const fixture = TestBed.createComponent(Workflows);
    pending().flush({
      ...empty,
      total: 1,
      items: [{ ...row, name: '<img src=x onerror=alert(1)>' }],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>');
    fixture.componentInstance.createForm.setValue({ name: 'Denied', businessType: 'TASK' });
    fixture.componentInstance.create();
    pending().flush({ message: 'private-details' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('private-details');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Create workflow draft');
  });

  it('cancels outdated requests and retains applied filters during paging', () => {
    const fixture = TestBed.createComponent(Workflows);
    const old = pending();
    const component = fixture.componentInstance;
    component.filters.setValue({ search: ' Work ', businessType: 'TASK', status: 'DRAFT' });
    component.apply();
    expect(old.cancelled).toBe(true);
    const request = pending();
    expect(request.request.params.get('search')).toBe('Work');
    expect(request.request.params.get('status')).toBe('DRAFT');
    request.flush(empty);
    component.filters.patchValue({ search: 'Unapplied' });
    component.load(2);
    const second = pending();
    expect(second.request.params.get('search')).toBe('Work');
    fixture.destroy();
    expect(second.cancelled).toBe(true);
  });

  it('retains input after an uncertain outcome and requires retrieval before another create', () => {
    const fixture = TestBed.createComponent(Workflows);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.createForm.setValue({ name: 'Network draft', businessType: 'TASK' });
    component.create();
    pending().flush({ message: 'database-secret' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(component.createForm.getRawValue().name).toBe('Network draft');
    expect(fixture.nativeElement.textContent).toContain('Creation could not be confirmed');
    expect(fixture.nativeElement.textContent).not.toContain('database-secret');
    component.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
    component.load(1);
    pending().flush(empty);
    expect(component.uncertain()).toBe(false);
  });
});
