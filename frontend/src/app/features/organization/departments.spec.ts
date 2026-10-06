import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Departments } from './departments';

describe('tenant department directory', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/departments');
  const empty = { items: [], total: 0, page: 1, pageSize: 25 };

  it('loads all statuses without accepting or sending a tenant selector', () => {
    const fixture = TestBed.createComponent(Departments);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading departments');
    const request = pending();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    request.flush(empty);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No departments match');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ) as HTMLButtonElement[];
    expect(buttons.find((b) => b.textContent?.includes('Next'))?.disabled).toBe(true);
    expect(buttons.find((b) => b.textContent?.includes('Previous'))?.disabled).toBe(true);
  });

  it('renders literal names safely and clears old data on denial', () => {
    const fixture = TestBed.createComponent(Departments);
    pending().flush({
      ...empty,
      total: 1,
      items: [
        {
          departmentId: 'one',
          code: 'OPS',
          name: '<img src=x onerror=alert(1)>',
          parentDepartmentId: null,
          status: 'INACTIVE',
          createdAt: '2026-10-01T00:00:00Z',
        },
      ],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('tbody').textContent).toContain('Inactive');
    fixture.componentInstance.load(1);
    pending().flush({ message: 'internal-secret' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('internal-secret');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
  });

  it('retries failures and cancels obsolete paging when filters change', () => {
    const fixture = TestBed.createComponent(Departments);
    pending().flush({ message: 'private-details' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Departments could not be loaded');
    expect(fixture.nativeElement.textContent).not.toContain('private-details');
    const component = fixture.componentInstance;
    component.load(2);
    const old = pending();
    expect(old.request.params.get('page')).toBe('2');
    component.filters.setValue({ search: ' Operations ', status: 'ACTIVE' });
    component.apply();
    expect(old.cancelled).toBe(true);
    const next = pending();
    expect(next.request.params.get('page')).toBe('1');
    expect(next.request.params.get('search')).toBe('Operations');
    expect(next.request.params.get('status')).toBe('ACTIVE');
    next.flush(empty);
    expect(component.error()).toBe(false);
  });

  it('cancels its pending request when the directory is destroyed', () => {
    const fixture = TestBed.createComponent(Departments);
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
