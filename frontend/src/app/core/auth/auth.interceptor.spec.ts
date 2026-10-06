import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { LoginResponse, SessionService } from './session';

describe('authenticated HTTP requests', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let session: SessionService;
  const response = (accessToken: string, refreshToken: string): LoginResponse => ({
    accessToken,
    refreshToken,
    expiresAt: '2026-10-01T00:00:00Z',
    session: {
      userId: 'user-a',
      fullName: 'Employee',
      tenantId: 'tenant-a',
      tenantName: 'Company A',
      permissions: [],
    },
  });
  const signIn = (token = 'access-1') => {
    session.login({ identifier: 'EMP1', password: 'test-password' }).subscribe();
    controller.expectOne('/api/v1/auth/login').flush(response(token, 'refresh-1'));
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    signIn();
  });
  afterEach(() => controller.verify());

  it('attaches credentials only to relative business API URLs', () => {
    for (const url of [
      '/api/v1/tasks',
      'https://external.test/api/v1/tasks',
      '/assets/data',
      '/api/v1/auth/login',
    ]) {
      http.get(url).subscribe();
      const request = controller.expectOne(url);
      expect(request.request.headers.get('Authorization')).toBe(
        url === '/api/v1/tasks' ? 'Bearer access-1' : null,
      );
      request.flush({});
    }
  });

  it('shares one refresh across concurrent 401 responses and retries each once', () => {
    http.get('/api/v1/tasks').subscribe();
    http.get('/api/v1/requests').subscribe();
    controller.expectOne('/api/v1/tasks').flush({}, { status: 401, statusText: 'Unauthorized' });
    controller.expectOne('/api/v1/requests').flush({}, { status: 401, statusText: 'Unauthorized' });
    const refresh = controller.expectOne('/api/v1/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(refresh.request.headers.has('Authorization')).toBe(false);
    refresh.flush(response('access-2', 'refresh-2'));
    for (const url of ['/api/v1/tasks', '/api/v1/requests']) {
      const retry = controller.expectOne(url);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
      retry.flush({});
    }
    expect(session.accessToken()).toBe('access-2');
  });

  it('uses an already rotated token when an older request returns a delayed 401', () => {
    http.get('/api/v1/tasks').subscribe();
    const original = controller.expectOne('/api/v1/tasks');
    session.refresh().subscribe();
    controller.expectOne('/api/v1/auth/refresh').flush(response('access-2', 'refresh-2'));
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    const retry = controller.expectOne('/api/v1/tasks');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({});
    controller.expectNone('/api/v1/auth/refresh');
  });

  it('does not retry forbidden responses', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });
    controller.expectOne('/api/v1/tasks').flush({}, { status: 403, statusText: 'Forbidden' });
    controller.expectNone('/api/v1/auth/refresh');
    expect(session.signedIn()).toBe(true);
  });

  it('clears an invalid session after failed refresh without recursion', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });
    controller.expectOne('/api/v1/tasks').flush({}, { status: 401, statusText: 'Unauthorized' });
    controller
      .expectOne('/api/v1/auth/refresh')
      .flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(session.signedIn()).toBe(false);
    expect(TestBed.inject(Router).navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('does not replay an old request under a newly signed-in identity', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });
    const old = controller.expectOne('/api/v1/tasks');
    signIn('new-user-token');
    old.flush({}, { status: 401, statusText: 'Unauthorized' });
    controller.expectNone('/api/v1/auth/refresh');
    expect(session.accessToken()).toBe('new-user-token');
  });

  it('keeps a successfully refreshed session if the business operation is forbidden', () => {
    http.get('/api/v1/tasks').subscribe({ error: () => undefined });
    controller.expectOne('/api/v1/tasks').flush({}, { status: 401, statusText: 'Unauthorized' });
    controller.expectOne('/api/v1/auth/refresh').flush(response('access-2', 'refresh-2'));
    controller.expectOne('/api/v1/tasks').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(session.accessToken()).toBe('access-2');
    expect(TestBed.inject(Router).navigateByUrl).not.toHaveBeenCalled();
  });

  it('ignores an old refresh response after the session was cleared', () => {
    session.refresh().subscribe({ error: () => undefined });
    const old = controller.expectOne('/api/v1/auth/refresh');
    session.clear();
    old.flush(response('stale-access', 'stale-refresh'));
    expect(session.signedIn()).toBe(false);
  });
});
