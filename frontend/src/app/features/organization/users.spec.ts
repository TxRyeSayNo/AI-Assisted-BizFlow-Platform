import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Users } from './users';

describe('tenant people directory', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/users');
  const empty = { items: [], total: 0, page: 1, pageSize: 25 };

  it('loads without a tenant override and renders the empty state and disabled paging', () => {
    const fixture = TestBed.createComponent(Users);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading people');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No people match');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    expect(buttons.find((b) => b.textContent?.includes('Next'))?.disabled).toBe(true);
    expect(buttons.find((b) => b.textContent?.includes('Previous'))?.disabled).toBe(true);
  });

  it('escapes names, renders nullable department and clears all stale rows on denial', () => {
    const fixture = TestBed.createComponent(Users);
    pending().flush({
      ...empty,
      total: 1,
      items: [
        {
          userId: 'one',
          employeeCode: 'EMP',
          fullName: '<img src=x onerror=alert(1)>',
          email: 'employee@example.test',
          departmentId: null,
          departmentName: null,
          status: 'LOCKED',
        },
      ],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('No department');
    expect(fixture.nativeElement.textContent).toContain('Locked');
    fixture.componentInstance.load(1);
    pending().flush({ message: 'internal-secret' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('internal-secret');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
  });

  it('retries safely, retains applied filters across paging and cancels outdated requests', () => {
    const fixture = TestBed.createComponent(Users);
    pending().flush({ message: 'private-details' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('People could not be loaded');
    expect(fixture.nativeElement.textContent).not.toContain('private-details');
    const component = fixture.componentInstance;
    component.load(2);
    const old = pending();
    component.filters.setValue({ search: ' Employee ', status: 'INACTIVE' });
    component.apply();
    expect(old.cancelled).toBe(true);
    const filtered = pending();
    expect(filtered.request.params.get('page')).toBe('1');
    expect(filtered.request.params.get('search')).toBe('Employee');
    expect(filtered.request.params.get('status')).toBe('INACTIVE');
    filtered.flush({ ...empty, total: 30 });
    component.filters.setValue({ search: 'Unapplied', status: 'LOCKED' });
    component.load(2);
    const next = pending();
    expect(next.request.params.get('search')).toBe('Employee');
    next.flush(empty);
  });

  it('cancels pending retrieval when destroyed', () => {
    const fixture = TestBed.createComponent(Users);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
