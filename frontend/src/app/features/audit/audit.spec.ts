import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { Audit } from './audit';
import { AuditRow } from './audit-model';

describe('scoped audit viewer', () => {
  let closed = 0;
  let opened: unknown;
  beforeEach(() => {
    closed = 0;
    opened = undefined;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { plane: 'tenant' } } } },
      ],
    }).overrideComponent(Audit, {
      add: {
        providers: [
          {
            provide: MatDialog,
            useValue: {
              open: (_component: unknown, options: unknown) => {
                opened = options;
                return { close: () => closed++ };
              },
            },
          },
        ],
      },
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/audit-logs');
  const empty = { items: [], page: 1, pageSize: 25, total: 0 };
  const row: AuditRow = {
    auditLogId: 'event',
    actorType: 'USER',
    actorId: 'actor',
    action: '<script>untrusted</script>',
    objectType: 'Role',
    objectId: 'role',
    before: null,
    after: { name: '<b>unsafe</b>' },
    metadata: null,
    createdAt: '2026-10-01T09:00:00Z',
  };

  it('loads without a caller-selected tenant and renders the empty state', () => {
    const fixture = TestBed.createComponent(Audit);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading audit events');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No audit events match');
  });

  it('escapes event fields and closes details when reloading revoked data', () => {
    const fixture = TestBed.createComponent(Audit);
    pending().flush({ ...empty, total: 1, items: [row] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<script>untrusted</script>');
    fixture.componentInstance.open(row);
    expect(opened).toMatchObject({
      data: { event: row, scopeLabel: 'Workspace event' },
      restoreFocus: true,
      ariaLabelledBy: 'audit-detail-title',
    });
    fixture.componentInstance.load(1);
    expect(closed).toBe(1);
    pending().flush({ message: 'internal-secret' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have permission');
    expect(fixture.nativeElement.textContent).not.toContain('untrusted');
    expect(fixture.nativeElement.textContent).not.toContain('internal-secret');
  });

  it('uses inclusive UTC day selection with an exclusive next-day API boundary', () => {
    const fixture = TestBed.createComponent(Audit);
    const old = pending();
    fixture.componentInstance.filters.patchValue({
      action: ' ROLE.CREATED ',
      fromDate: '2026-09-01',
      throughDate: '2026-09-01',
    });
    fixture.componentInstance.apply();
    expect(old.cancelled).toBe(true);
    const request = pending();
    expect(request.request.params.get('action')).toBe('ROLE.CREATED');
    expect(request.request.params.get('from')).toBe('2026-09-01T00:00:00.000Z');
    expect(request.request.params.get('until')).toBe('2026-09-02T00:00:00.000Z');
    request.flush(empty);
    fixture.componentInstance.filters.patchValue({ action: 'Unapplied' });
    fixture.componentInstance.load(2);
    const next = pending();
    expect(next.request.params.get('action')).toBe('ROLE.CREATED');
    fixture.destroy();
    expect(next.cancelled).toBe(true);
  });

  it('rejects malformed UUIDs and reversed dates locally without sending partial filters', () => {
    const fixture = TestBed.createComponent(Audit);
    pending().flush(empty);
    fixture.componentInstance.filters.patchValue({ actorId: 'invalid' });
    fixture.componentInstance.apply();
    expect(fixture.componentInstance.validation()).toContain('UUID');
    fixture.componentInstance.filters.patchValue({
      actorId: '',
      fromDate: '2026-09-03',
      throughDate: '2026-09-01',
    });
    fixture.componentInstance.apply();
    expect(fixture.componentInstance.validation()).toContain('UTC dates');
    TestBed.inject(HttpTestingController).expectNone((r) => r.url === '/api/v1/audit-logs');
  });
});
