import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SessionService } from './session';

describe('realtime token refresh', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('shares one rotation with HTTP refresh and never stores credentials outside session memory', async () => {
    const session = TestBed.inject(SessionService);
    const http = TestBed.inject(HttpTestingController);
    const initial = {
      accessToken: 'old',
      refreshToken: 'once',
      expiresAt: '2020-01-01T00:00:00Z',
      session: {
        userId: 'user',
        tenantId: 'tenant',
        fullName: 'User',
        tenantName: 'Tenant',
        permissions: [],
      },
    };
    session.login({ identifier: 'user', password: 'test' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(initial);
    const realtime = session.realtimeAccessToken();
    const ordinary = firstValueFrom(session.refresh());
    const request = http.expectOne('/api/v1/auth/refresh');
    expect(request.request.body).toEqual({ refreshToken: 'once' });
    request.flush({
      ...initial,
      accessToken: 'fresh',
      refreshToken: 'next',
      expiresAt: '2099-01-01T00:00:00Z',
    });
    expect(await realtime).toBe('fresh');
    expect((await ordinary).accessToken).toBe('fresh');
    expect(await session.realtimeAccessToken()).toBe('fresh');
    http.expectNone('/api/v1/auth/refresh');
    session.clear();
    await expect(session.realtimeAccessToken()).rejects.toThrow('No active session');
  });
});
