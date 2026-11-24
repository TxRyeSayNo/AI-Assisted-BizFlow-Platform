import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BrowserPushSettings } from './browser-push-settings';

describe('browser push settings', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    vi.stubGlobal('isSecureContext', true);
    vi.stubGlobal('PushManager', class {});
    vi.stubGlobal('Notification', { requestPermission: vi.fn().mockResolvedValue('granted') });
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });
  function setup(pushManager: object) {
    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: vi.fn().mockResolvedValue({ active: {}, pushManager }),
        getRegistration: vi.fn().mockResolvedValue({ pushManager }),
      },
    });
    return TestBed.createComponent(BrowserPushSettings).componentInstance;
  }
  it('requests permission only on user action and registers the current browser keys', async () => {
    const subscription = {
      endpoint: 'https://fcm.googleapis.com/one',
      options: {},
      toJSON: () => ({ keys: { p256dh: 'public', auth: 'secret' } }),
    };
    const subscribe = vi.fn().mockResolvedValue(subscription);
    const component = setup({ getSubscription: vi.fn().mockResolvedValue(null), subscribe });
    expect(Notification.requestPermission).not.toHaveBeenCalled();
    const pending = component.enable();
    await Promise.resolve();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/notifications/reminders/vapid-public-key')
      .flush({ publicKey: 'BAEC' });
    await vi.waitFor(() => expect(subscribe).toHaveBeenCalledOnce());
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/notifications/push/subscribe',
    );
    expect(request.request.body).toEqual({
      endpoint: subscription.endpoint,
      p256dh: 'public',
      auth: 'secret',
    });
    request.flush({});
    await pending;
    expect(component.message()).toContain('Đã bật');
  });
  it('explains missing server configuration without subscribing', async () => {
    const subscribe = vi.fn();
    const component = setup({ subscribe });
    const pending = component.enable();
    await Promise.resolve();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/notifications/reminders/vapid-public-key')
      .flush({ publicKey: '' });
    await pending;
    expect(subscribe).not.toHaveBeenCalled();
    expect(component.message()).toContain('chưa cấu hình');
  });
  it('turns off the browser subscription even when the server is unavailable', async () => {
    const unsubscribe = vi.fn().mockResolvedValue(true);
    const component = setup({
      getSubscription: vi
        .fn()
        .mockResolvedValue({ endpoint: 'https://fcm.googleapis.com/one', unsubscribe }),
    });
    const pending = component.disable();
    await vi.waitFor(() => expect(unsubscribe).toHaveBeenCalledOnce());
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/notifications/push/subscribe',
    );
    expect(request.request.method).toBe('DELETE');
    request.flush({}, { status: 503, statusText: 'Unavailable' });
    await pending;
    expect(component.busy()).toBe(false);
  });
});
