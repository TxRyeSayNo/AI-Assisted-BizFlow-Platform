import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { SavedFilters, SavedFilterView } from './saved-filters';

describe('saved filters', () => {
  const url = '/api/v1/saved-filters';

  const configure = (enabled: boolean) =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FeatureFlagsService, useValue: { savedFilters: () => enabled } },
      ],
    });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const row = (over: Partial<SavedFilterView> = {}): SavedFilterView => ({
    savedFilterId: 'f-1',
    scope: 'TASK',
    name: 'Việc gấp',
    query: 'priority=HIGH',
    createdAt: '2026-11-01T00:00:00Z',
    updatedAt: null,
    ...over,
  });

  const create = (currentQuery = 'priority=HIGH') => {
    const fixture = TestBed.createComponent(SavedFilters);
    fixture.componentRef.setInput('scope', 'TASK');
    fixture.componentRef.setInput('currentQuery', currentQuery);
    fixture.detectChanges();
    return fixture;
  };

  const pending = () => TestBed.inject(HttpTestingController).expectOne((r) => r.url === url);

  it('lists the saved filters for its own scope', () => {
    configure(true);
    const fixture = create();
    const request = pending();
    expect(request.request.params.get('scope')).toBe('TASK');
    request.flush([row(), row({ savedFilterId: 'f-2', name: 'Tuần này' })]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Việc gấp');
    expect(fixture.nativeElement.textContent).toContain('Tuần này');
  });

  it('emits the stored query when a filter is chosen', () => {
    configure(true);
    const fixture = create();
    pending().flush([row({ query: 'status=ASSIGNED&priority=HIGH' })]);
    const applied: string[] = [];
    fixture.componentInstance.apply.subscribe((query) => applied.push(query));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.saved-filter-chip button').click();

    expect(applied).toEqual(['status=ASSIGNED&priority=HIGH']);
  });

  it('saves the query the page is currently showing, under a trimmed name', () => {
    configure(true);
    const fixture = create('search=b%C3%A1o&priority=HIGH');
    const http = TestBed.inject(HttpTestingController);
    pending().flush([]);

    fixture.componentInstance.name.setValue('  Của tôi  ');
    fixture.componentInstance.save();

    const posted = http.expectOne((r) => r.method === 'POST' && r.url === url);
    expect(posted.request.body).toEqual({
      scope: 'TASK',
      name: 'Của tôi',
      query: 'search=b%C3%A1o&priority=HIGH',
    });
    posted.flush(row());
    // The list is re-read so the new chip appears.
    pending().flush([row()]);
    expect(fixture.componentInstance.name.value).toBe('');
  });

  it('refuses to save an unnamed filter without asking the server', () => {
    configure(true);
    const fixture = create();
    pending().flush([]);

    fixture.componentInstance.name.setValue('   ');
    fixture.componentInstance.save();

    TestBed.inject(HttpTestingController).verify();
  });

  it('deletes a filter and drops it from the strip', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    pending().flush([row(), row({ savedFilterId: 'f-2', name: 'Tuần này' })]);

    fixture.componentInstance.remove(row());
    http.expectOne((r) => r.method === 'DELETE' && r.url === `${url}/f-1`).flush(null);

    expect(fixture.componentInstance.items().map((f) => f.savedFilterId)).toEqual(['f-2']);
  });

  it('shows a curated 4xx message but never a 5xx body', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    pending().flush([]);

    fixture.componentInstance.name.setValue('Trùng tên');
    fixture.componentInstance.save();
    http
      .expectOne((r) => r.method === 'POST')
      .flush(
        { message: 'You already have a saved filter with this name.' },
        { status: 409, statusText: 'Conflict' },
      );
    expect(TestBed.inject(ToastService).toasts()[0].message).toContain('already have');

    fixture.componentInstance.name.setValue('Khác');
    fixture.componentInstance.save();
    http
      .expectOne((r) => r.method === 'POST')
      .flush({ message: 'internal detail' }, { status: 500, statusText: 'Server Error' });
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts[toasts.length - 1].message).not.toContain('internal detail');
  });

  it('renders nothing and asks for nothing while the feature is off', () => {
    configure(false);
    const fixture = create();

    TestBed.inject(HttpTestingController).expectNone((r) => r.url === url);
    expect(fixture.nativeElement.querySelector('.saved-filters')).toBeNull();
  });

  it('stays hidden when the read is refused', () => {
    configure(true);
    const fixture = create();
    pending().flush({ message: 'private detail' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.available()).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
  });

  it('cancels an outstanding read when the view is destroyed', () => {
    configure(true);
    const fixture = create();
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
