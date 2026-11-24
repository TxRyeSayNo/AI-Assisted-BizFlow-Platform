import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationPreferences } from './notification-preferences';

describe('notification preferences', () => {
  const endpoint = '/api/v1/notifications/preferences';
  const defaults = {
    emailEnabled: true,
    deadlineEmailMode: 'AwayOnly',
    dailyDigestEnabled: false,
    digestHour: 8,
    digestDays: 1,
    digestEmail: true,
  };
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('loads on demand and saves the selected delivery policy', () => {
    const fixture = TestBed.createComponent(NotificationPreferences);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectNone(endpoint);
    fixture.componentInstance.load();
    http.expectOne(endpoint).flush({ ...defaults });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('fieldset').length).toBe(2);
    const settings = fixture.componentInstance.settings!;
    settings.deadlineEmailMode = 'Always';
    settings.dailyDigestEnabled = true;
    settings.digestDays = 7;
    fixture.componentInstance.save();
    const request = http.expectOne(endpoint);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      ...defaults,
      deadlineEmailMode: 'Always',
      dailyDigestEnabled: true,
      digestDays: 7,
    });
    request.flush(request.request.body);
    expect(fixture.componentInstance.busy()).toBe(false);
  });

  it('submits the requested dates once while pending and handles validation errors safely', () => {
    const component = TestBed.createComponent(NotificationPreferences).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    component.from = '2026-11-25';
    component.to = '2026-11-27';
    component.digest();
    component.digest();
    const request = http.expectOne('/api/v1/notifications/digests');
    expect(request.request.body).toEqual({ from: '2026-11-25', to: '2026-11-27' });
    request.flush({ created: true, result: { taskCount: 3 } });
    expect(component.message()).toContain('3');
    component.digest();
    http
      .expectOne('/api/v1/notifications/digests')
      .flush({ message: 'private server details' }, { status: 422, statusText: 'Invalid range' });
    expect(component.busy()).toBe(false);
    expect(component.message()).not.toContain('private server details');
  });
});
