import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { WorkflowVersion } from './workflow-version';
import { SessionService } from '../../core/auth/session';

describe('workflow version view', () => {
  let canConfigure = false;
  beforeEach(() => {
    canConfigure = false;
    const params = convertToParamMap({ id: 'workflow', v: 'version' });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: class {} }]),
        {
          provide: SessionService,
          useValue: {
            identity: () => null,
            hasPermission: (code: string) => canConfigure && code === 'workflows.configure',
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(params), snapshot: { paramMap: params } },
        },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne('/api/v1/workflow-versions/version');
  const version = {
    workflowId: 'workflow',
    name: '<script>untrusted</script>',
    businessType: 'TASK',
    versionId: 'version',
    versionNo: 1,
    status: 'DRAFT',
    publishedAt: null,
    definition: {},
    steps: [],
    transitions: [],
  };

  const flushLookups = () => {
    const http = TestBed.inject(HttpTestingController);
    http.match('/api/v1/workflows/lookup/departments').forEach((r) => r.flush([]));
    http.match('/api/v1/workflows/lookup/roles').forEach((r) => r.flush([]));
    http.match('/api/v1/workflows/lookup/services').forEach((r) => r.flush([]));
    http.match('/api/v1/workflows/workflow/versions').forEach((r) => r.flush([]));
  };

  it('renders a safe read-only draft and explains missing configuration', () => {
    const fixture = TestBed.createComponent(WorkflowVersion);
    pending().flush(version);
    flushLookups();
    fixture.detectChanges();
    expect(fixture.componentInstance.workflowNameEdit()).toBe('<script>untrusted</script>');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Bản nháp');
    fixture.componentInstance.createNextVersion();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/workflows/workflow/versions');
  });

  it('creates exactly one empty draft and leaves the displayed historical version intact', () => {
    canConfigure = true;
    const fixture = TestBed.createComponent(WorkflowVersion);
    pending().flush({ ...version, status: 'PUBLISHED' });
    flushLookups();
    fixture.componentInstance.createNextVersion();
    fixture.componentInstance.createNextVersion();
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/workflows/workflow/versions',
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.header-actions button.btn-primary') as HTMLButtonElement;
    expect(btn?.disabled).toBe(true);
    request.flush({
      workflowId: 'workflow',
      latestVersionId: 'new-version',
      latestVersionNo: 2,
      latestVersionStatus: 'DRAFT',
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.version()?.status).toBe('PUBLISHED');
    expect(fixture.nativeElement.textContent).toContain('Đã khởi tạo bản nháp');
    expect(
      fixture.nativeElement.querySelector(
        'a[href="/settings/workflows/workflow/versions/new-version"]',
      ),
    ).not.toBeNull();
    fixture.componentInstance.createNextVersion();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/workflows/workflow/versions');
  });

  it.each([403, 404, 409, 503])(
    'handles creation failure %i without hiding history or exposing server details',
    (status) => {
      canConfigure = true;
      const fixture = TestBed.createComponent(WorkflowVersion);
      pending().flush(version);
      flushLookups();
      fixture.componentInstance.createNextVersion();
      TestBed.inject(HttpTestingController)
        .expectOne('/api/v1/workflows/workflow/versions')
        .flush({ message: 'private-provider-detail' }, { status, statusText: 'Failure' });
      fixture.detectChanges();
      expect(fixture.componentInstance.creating()).toBe(false);
      expect(fixture.componentInstance.created()).toBeNull();
      expect(fixture.componentInstance.version()?.versionId).toBe('version');
      expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).not.toContain(
        'private-provider-detail',
      );
      if (status === 503)
        expect(fixture.nativeElement.textContent).toContain('Không thể tạo phiên bản mới');
    },
  );

  it('does not render a version belonging to a different workflow route', () => {
    const fixture = TestBed.createComponent(WorkflowVersion);
    pending().flush({ ...version, workflowId: 'other' });
    flushLookups();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Không tìm thấy phiên bản quy trình này');
    expect(fixture.nativeElement.textContent).not.toContain('untrusted');
  });

  it('clears previously loaded configuration on denial and does not expose provider errors', () => {
    const fixture = TestBed.createComponent(WorkflowVersion);
    pending().flush(version);
    flushLookups();
    fixture.componentInstance.load();
    pending().flush({ message: 'private-details' }, { status: 403, statusText: 'Forbidden' });
    flushLookups();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bạn không có quyền truy cập');
    expect(fixture.nativeElement.textContent).not.toContain('untrusted');
    expect(fixture.nativeElement.textContent).not.toContain('private-details');
  });
});
