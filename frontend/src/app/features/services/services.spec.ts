import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { ServiceRow, Services } from './services';

describe('service catalog', () => {
  let create = true;
  let update = false;
  beforeEach(() => {
    create = true;
    update = false;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: {
            identity: () => ({ tenantName: 'Workspace' }),
            hasPermission: (code: string) =>
              (create && code === 'service.create') || (update && code === 'service.update'),
          },
        },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/api/v1/services');
  const empty = { items: [], page: 1, pageSize: 25, total: 0 };
  const row: ServiceRow = {
    serviceId: 'service',
    code: 'IT',
    name: '<script>Help</script>',
    description: '<img src=x onerror=alert(1)>',
    status: 'ACTIVE',
    categories: [
      { serviceCategoryId: 'category', code: 'DESKTOP', name: 'Devices', status: 'ACTIVE' },
    ],
    activeWorkflowVersionId: null,
    activeSlaVersionId: null,
    eTag: '"1"',
  };

  it('allows read-only browsing without granting creation and renders text safely', () => {
    create = false;
    const fixture = TestBed.createComponent(Services);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading services');
    pending().flush({ ...empty, items: [row], total: 1 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('<script>Help</script>');
    expect(fixture.nativeElement.querySelector('script,img')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('DESKTOP');
    expect(fixture.nativeElement.textContent).not.toContain('Create service');
    fixture.componentInstance.form.patchValue({ code: 'IT', name: 'Help' });
    fixture.componentInstance.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
  });
  it('posts only metadata and categories, prevents double submission and reloads the persisted service', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.form.patchValue({ code: ' IT ', name: ' Help ', description: ' Text ' });
    component.addCategory();
    component.categories.at(0).setValue({ code: ' DESKTOP ', name: ' Devices ', active: false });
    component.create();
    component.create();
    const request = pending();
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      code: 'IT',
      name: 'Help',
      description: 'Text',
      active: true,
      categories: [{ code: 'DESKTOP', name: 'Devices', active: false }],
    });
    component.addCategory();
    component.removeCategory(0);
    expect(component.categories.length).toBe(1);
    request.flush(row);
    expect(component.categories.length).toBe(0);
    const refresh = pending();
    expect(refresh.request.params.get('search')).toBe('IT');
    refresh.flush({ ...empty, items: [row], total: 1 });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Service created: IT');
  });
  it('rejects duplicate trimmed category codes before posting and permits removing a category', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.form.patchValue({ code: 'IT', name: 'Help' });
    component.addCategory();
    component.addCategory();
    component.categories.at(0).setValue({ code: ' A ', name: 'First', active: true });
    component.categories.at(1).setValue({ code: 'A', name: 'Second', active: true });
    component.create();
    expect(component.mutationError()).toContain('unique');
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
    component.removeCategory(1);
    expect(component.categories.length).toBe(1);
  });
  it.each([409, 422])(
    'retains user input on known rejection %i without automatic retry',
    (status) => {
      const fixture = TestBed.createComponent(Services);
      pending().flush(empty);
      const component = fixture.componentInstance;
      component.form.patchValue({ code: 'IT', name: 'Help' });
      component.create();
      pending().flush({ detail: 'private-details' }, { status, statusText: 'Rejected' });
      fixture.detectChanges();
      expect(component.form.getRawValue().code).toBe('IT');
      expect(component.uncertain()).toBe(false);
      expect(component.saving()).toBe(false);
      expect(fixture.nativeElement.textContent).not.toContain('private-details');
      expect(component.mutationError()).toContain(status === 409 ? 'already exists' : 'Check');
    },
  );
  it('requires a successful catalog read before another attempt after an uncertain save', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush(empty);
    const component = fixture.componentInstance;
    component.form.patchValue({ code: 'IT', name: 'Help' });
    component.create();
    pending().flush({}, { status: 503, statusText: 'Unknown' });
    component.create();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
    expect(component.uncertain()).toBe(true);
    component.load(1);
    pending().flush({}, { status: 503, statusText: 'Unknown' });
    expect(component.uncertain()).toBe(true);
    component.load(1);
    pending().flush(empty);
    expect(component.uncertain()).toBe(false);
    expect(component.form.getRawValue().name).toBe('Help');
  });
  it('clears rows after permission denial and cancels stale filtered requests', () => {
    const fixture = TestBed.createComponent(Services);
    const old = pending();
    fixture.componentInstance.filters.patchValue({ search: ' IT ', status: 'ACTIVE' });
    fixture.componentInstance.apply();
    expect(old.cancelled).toBe(true);
    const request = pending();
    expect(request.request.params.get('search')).toBe('IT');
    expect(request.request.params.get('status')).toBe('ACTIVE');
    request.flush({ ...empty, items: [row], total: 1 });
    fixture.componentInstance.load(2);
    pending().flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('no longer have access');
    expect(fixture.nativeElement.textContent).not.toContain('Devices');
  });
  const categoryRequest = () =>
    TestBed.inject(HttpTestingController).expectOne('/api/v1/services/service/categories');
  it('adds a category using the selected ETag, preserves other rows and serializes mutations', () => {
    const fixture = TestBed.createComponent(Services);
    const other = { ...row, serviceId: 'other', code: 'OTHER' };
    pending().flush({ ...empty, items: [row, other], total: 2 });
    const component = fixture.componentInstance;
    component.beginCategory(component.result()!.items[0]);
    component.existingCategoryForm.setValue({ code: ' PHONE ', name: ' Phone ', active: false });
    component.saveCategory();
    component.saveCategory();
    component.load(1);
    component.form.patchValue({ code: 'NEW', name: 'New service' });
    component.create();
    component.addCategory();
    expect(component.categories.length).toBe(0);
    const request = categoryRequest();
    expect(request.request.headers.get('If-Match')).toBe('"1"');
    expect(request.request.body).toEqual({ code: 'PHONE', name: 'Phone', active: false });
    const updated = {
      ...row,
      eTag: '"2"',
      categories: [
        ...row.categories,
        { serviceCategoryId: 'new', code: 'PHONE', name: 'Phone', status: 'INACTIVE' },
      ],
    };
    request.flush(updated);
    expect(component.result()?.items).toEqual([updated, other]);
    expect(component.selectedService()).toBeNull();
    expect(component.categoryCreated()).toContain('PHONE to IT');
    expect(component.existingCategoryForm.getRawValue().code).toBe('');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('PHONE');
  });
  it.each([409, 503])(
    'requires reload and explicit reselection after category save failure %i',
    (status) => {
      const fixture = TestBed.createComponent(Services);
      pending().flush({ ...empty, items: [row], total: 1 });
      const component = fixture.componentInstance;
      component.beginCategory(component.result()!.items[0]);
      component.existingCategoryForm.setValue({ code: 'PHONE', name: 'Phone', active: true });
      component.saveCategory();
      categoryRequest().flush(
        { code: 'SERVICE.VERSION_CONFLICT', details: { etag: ['"999"'] }, detail: 'private' },
        { status, statusText: 'Failed' },
      );
      component.saveCategory();
      expect(component.categoryNeedsReload()).toBe(true);
      expect(component.selectedService()?.eTag).toBe('"1"');
      component.load(1);
      pending().flush({}, { status: 503, statusText: 'Failed' });
      expect(component.categoryNeedsReload()).toBe(true);
      component.load(1);
      pending().flush({ ...empty, items: [{ ...row, eTag: '"2"' }], total: 1 });
      expect(component.categoryNeedsReload()).toBe(false);
      expect(component.selectedService()).toBeNull();
      component.saveCategory();
      TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
      component.beginCategory(component.result()!.items[0]);
      expect(component.existingCategoryForm.getRawValue().code).toBe('PHONE');
      component.saveCategory();
      const retry = categoryRequest();
      expect(retry.request.headers.get('If-Match')).toBe('"2"');
      retry.flush({ ...row, eTag: '"3"' });
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).not.toContain('private');
    },
  );
  it('retains category input on duplicate rejection and permits an explicit corrected save', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush({ ...empty, items: [row], total: 1 });
    const component = fixture.componentInstance;
    component.beginCategory(component.result()!.items[0]);
    component.existingCategoryForm.setValue({ code: 'DESKTOP', name: 'Devices', active: true });
    component.saveCategory();
    categoryRequest().flush(
      { code: 'SERVICE.CATEGORY_CODE_EXISTS' },
      { status: 409, statusText: 'Conflict' },
    );
    expect(component.categoryNeedsReload()).toBe(false);
    expect(component.categoryError()).toContain('already exists');
    expect(component.existingCategoryForm.getRawValue().code).toBe('DESKTOP');
    component.existingCategoryForm.patchValue({ code: 'PHONE' });
    component.saveCategory();
    const retry = categoryRequest();
    expect(retry.request.headers.get('If-Match')).toBe('"1"');
    retry.flush({ ...row, eTag: '"2"' });
  });
  it('clears the catalog on category permission denial and never posts without the grant', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush({ ...empty, items: [row], total: 1 });
    const component = fixture.componentInstance;
    component.beginCategory(component.result()!.items[0]);
    component.existingCategoryForm.setValue({ code: 'PHONE', name: 'Phone', active: true });
    create = false;
    component.saveCategory();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'POST');
    create = true;
    component.saveCategory();
    categoryRequest().flush({}, { status: 403, statusText: 'Forbidden' });
    expect(component.result()).toBeNull();
    expect(component.selectedService()).toBeNull();
    expect(component.denied()).toBe(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Devices');
  });
  const metadataRequest = () =>
    TestBed.inject(HttpTestingController).expectOne('/api/v1/services/service');
  it('allows update-only editors to send metadata and ETag without granting creation', () => {
    create = false;
    update = true;
    const fixture = TestBed.createComponent(Services);
    pending().flush({ ...empty, items: [row], total: 1 });
    const component = fixture.componentInstance;
    component.beginMetadata(component.result()!.items[0]);
    expect(component.metadataForm.getRawValue().code).toBe('IT');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit service IT');
    expect(fixture.nativeElement.textContent).not.toContain('Create service');
    component.metadataForm.setValue({
      code: ' NEW ',
      name: ' New name ',
      description: ' ',
      active: false,
    });
    component.saveMetadata();
    component.saveMetadata();
    component.load(1);
    const request = metadataRequest();
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('"1"');
    expect(request.request.body).toEqual({
      code: 'NEW',
      name: 'New name',
      description: null,
      active: false,
    });
    component.beginCategory(component.result()!.items[0]);
    expect(component.selectedService()).toBeNull();
    const updated = { ...row, code: 'NEW', name: 'New name', status: 'INACTIVE', eTag: '"2"' };
    request.flush(updated);
    const refresh = pending();
    expect(refresh.request.params.get('search')).toBe('NEW');
    refresh.flush({ ...empty, items: [updated], total: 1 });
    expect(component.metadataSaved()).toContain('Service updated: NEW');
    expect(component.editingService()).toBeNull();
    expect(component.result()!.items[0].categories).toEqual(row.categories);
  });
  it.each([409, 503])(
    'preserves metadata edits and requires reload/reselection after failure %i',
    (status) => {
      update = true;
      const fixture = TestBed.createComponent(Services);
      pending().flush({ ...empty, items: [row], total: 1 });
      const component = fixture.componentInstance;
      component.beginMetadata(component.result()!.items[0]);
      component.metadataForm.patchValue({ name: 'My change' });
      component.saveMetadata();
      metadataRequest().flush(
        { code: 'SERVICE.VERSION_CONFLICT', details: { etag: ['"999"'] } },
        { status, statusText: 'Failed' },
      );
      component.saveMetadata();
      component.create();
      expect(component.metadataNeedsReload()).toBe(true);
      expect(component.editingService()?.eTag).toBe('"1"');
      component.load(1);
      pending().flush({}, { status: 503, statusText: 'Failed' });
      expect(component.metadataNeedsReload()).toBe(true);
      component.load(1);
      pending().flush({
        ...empty,
        items: [{ ...row, name: 'Their change', eTag: '"2"' }],
        total: 1,
      });
      component.saveMetadata();
      TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'PUT');
      expect(component.editingService()).toBeNull();
      component.beginMetadata(component.result()!.items[0]);
      expect(component.metadataForm.getRawValue().name).toBe('My change');
      component.saveMetadata();
      const retry = metadataRequest();
      expect(retry.request.headers.get('If-Match')).toBe('"2"');
      retry.flush({ ...row, name: 'My change', eTag: '"3"' });
      pending().flush({ ...empty, items: [row], total: 1 });
    },
  );
  it.each([409, 422])(
    'retains metadata input after known rejection %i and does not retry automatically',
    (status) => {
      update = true;
      const fixture = TestBed.createComponent(Services);
      pending().flush({ ...empty, items: [row], total: 1 });
      const component = fixture.componentInstance;
      component.beginMetadata(component.result()!.items[0]);
      component.metadataForm.patchValue({ code: 'NEW' });
      component.saveMetadata();
      metadataRequest().flush(
        { code: 'SERVICE.DUPLICATE_CODE', detail: 'private' },
        { status, statusText: 'Rejected' },
      );
      expect(component.metadataNeedsReload()).toBe(false);
      expect(component.metadataForm.getRawValue().code).toBe('NEW');
      expect(component.metadataError()).toContain(status === 409 ? 'already exists' : 'Check');
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).not.toContain('private');
    },
  );
  it('requires update authority rather than create authority and clears rows on revoked access', () => {
    const fixture = TestBed.createComponent(Services);
    pending().flush({ ...empty, items: [row], total: 1 });
    const component = fixture.componentInstance;
    component.beginMetadata(component.result()!.items[0]);
    expect(component.editingService()).toBeNull();
    update = true;
    component.beginMetadata(component.result()!.items[0]);
    update = false;
    component.saveMetadata();
    TestBed.inject(HttpTestingController).expectNone((r) => r.method === 'PUT');
    update = true;
    component.saveMetadata();
    metadataRequest().flush({}, { status: 403, statusText: 'Forbidden' });
    expect(component.denied()).toBe(true);
    expect(component.result()).toBeNull();
    expect(component.editingService()).toBeNull();
  });
});
