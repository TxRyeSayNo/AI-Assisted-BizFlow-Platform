import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { SlaProfiles } from './sla-profiles';

describe('SLA profile catalog', () => {
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
            hasPermission: (code: string) => code === 'sla.read' || configure,
          },
        },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/sla-profiles');
  const empty = { items: [], page: 1, pageSize: 25, total: 0 };
  const row = { slaProfileId: 'one', name: 'Support response', status: 'DRAFT' };

  it('renders loading and empty states without granting configuration authority to readers', () => {
    configure = false;
    const fixture = TestBed.createComponent(SlaProfiles);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading SLA profiles');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No SLA profiles match');
    expect(fixture.nativeElement.textContent).not.toContain('Create SLA profile');
    fixture.componentInstance.createForm.setValue({ name: 'Unauthorized' });
    fixture.componentInstance.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
  });

  it('sends only trimmed metadata and blocks double submission without implying activation', () => {
    const fixture = TestBed.createComponent(SlaProfiles);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.createForm.setValue({ name: ' Support response ' });
    component.create();
    component.create();
    const create = pending();
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Support response' });
    create.flush(row);
    const reload = pending();
    expect(reload.request.params.get('search')).toBe('Support response');
    reload.flush({ ...empty, total: 1, items: [row] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Draft profile created: Support response');
    expect(fixture.nativeElement.textContent).toContain(
      'does not start a clock or activate an SLA',
    );
    expect(component.createForm.getRawValue().name).toBe('');
  });

  it('escapes names and clears sensitive rows and creation controls after permission revocation', () => {
    const fixture = TestBed.createComponent(SlaProfiles);
    pending().flush({
      ...empty,
      total: 1,
      items: [{ ...row, name: '<img src=x onerror=alert(1)>' }],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>');
    fixture.componentInstance.createForm.setValue({ name: 'Denied' });
    fixture.componentInstance.create();
    pending().flush({ message: 'private-details' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('private-details');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Create SLA profile');
  });

  it('cancels stale requests and retains applied filters while paging', () => {
    const fixture = TestBed.createComponent(SlaProfiles);
    const old = pending();
    const component = fixture.componentInstance;
    component.filters.setValue({ search: ' Support ', status: 'DRAFT' });
    component.apply();
    expect(old.cancelled).toBe(true);
    const request = pending();
    expect(request.request.params.get('search')).toBe('Support');
    expect(request.request.params.get('status')).toBe('DRAFT');
    request.flush(empty);
    component.filters.patchValue({ search: 'Unapplied' });
    component.load(2);
    const second = pending();
    expect(second.request.params.get('search')).toBe('Support');
    expect(second.request.params.get('page')).toBe('2');
    fixture.destroy();
    expect(second.cancelled).toBe(true);
  });

  it('requires a successful reload before another creation after an uncertain outcome', () => {
    const fixture = TestBed.createComponent(SlaProfiles);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.createForm.setValue({ name: 'Network draft' });
    component.create();
    pending().flush({ message: 'database-secret' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(component.createForm.getRawValue().name).toBe('Network draft');
    expect(fixture.nativeElement.textContent).toContain('Creation could not be confirmed');
    expect(fixture.nativeElement.textContent).not.toContain('database-secret');
    component.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
    component.load(1);
    pending().flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.uncertain()).toBe(true);
    component.load(1);
    pending().flush(empty);
    expect(component.uncertain()).toBe(false);
  });

  it('keeps validation failures editable and displays safe retry guidance for read failures', () => {
    const fixture = TestBed.createComponent(SlaProfiles);
    pending().flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('SLA profiles could not be loaded');
    const component = fixture.componentInstance;
    component.load(1);
    pending().flush(empty);
    component.createForm.setValue({ name: 'Valid-looking input' });
    component.create();
    pending().flush({}, { status: 422, statusText: 'Invalid' });
    fixture.detectChanges();
    expect(component.uncertain()).toBe(false);
    expect(component.createForm.getRawValue().name).toBe('Valid-looking input');
    expect(fixture.nativeElement.textContent).toContain(
      'Enter a profile name up to 200 characters',
    );
  });
});
