import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'bf-browser-push-settings',
  template: `
    <section aria-label="Thông báo trình duyệt">
      <h2>Thông báo trình duyệt</h2>
      <p>Nhận nhắc hạn trên thiết bị này. Nội dung công việc chỉ hiển thị sau khi mở BizFlow.</p>
      @if (supported) {
        <button type="button" [disabled]="busy()" (click)="enable()">Bật hoặc kết nối lại</button>
        <button type="button" [disabled]="busy()" (click)="disable()">Tắt trên thiết bị này</button>
      } @else {
        <p>
          Trình duyệt này chưa hỗ trợ thông báo đẩy. Hãy dùng HTTPS và trình duyệt hỗ trợ Web Push.
        </p>
      }
      <p role="status">{{ message() }}</p>
    </section>
  `,
})
export class BrowserPushSettings {
  private readonly http = inject(HttpClient);
  readonly supported =
    typeof navigator !== 'undefined' &&
    'serviceWorker' in navigator &&
    typeof PushManager !== 'undefined' &&
    typeof Notification !== 'undefined' &&
    globalThis.isSecureContext;
  readonly busy = signal(false);
  readonly message = signal('');

  async enable() {
    if (!this.supported || this.busy()) return;
    this.busy.set(true);
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        this.message.set(
          'Chưa được cấp quyền thông báo. Bạn có thể thay đổi trong cài đặt của trình duyệt.',
        );
        return;
      }
      const key = await firstValueFrom(
        this.http.get<{ publicKey: string }>('/api/v1/notifications/reminders/vapid-public-key'),
      );
      if (!key.publicKey) {
        this.message.set('Quản trị viên chưa cấu hình thông báo trình duyệt.');
        return;
      }
      const registration = await navigator.serviceWorker.register('/bizflow-push-worker.js', {
        scope: '/bizflow-push/',
      });
      if (!registration.active) {
        const worker = registration.installing ?? registration.waiting;
        if (!worker) throw new Error('Missing service worker');
        await new Promise<void>((resolve, reject) => {
          const check = () => {
            if (worker.state === 'activated') {
              worker.removeEventListener('statechange', check);
              resolve();
            } else if (worker.state === 'redundant') {
              worker.removeEventListener('statechange', check);
              reject(new Error('Activation failed'));
            }
          };
          worker.addEventListener('statechange', check);
          check();
        });
      }
      const encoded = key.publicKey.replace(/-/g, '+').replace(/_/g, '/');
      const applicationServerKey = Uint8Array.from(
        atob(encoded.padEnd(Math.ceil(encoded.length / 4) * 4, '=')),
        (c) => c.charCodeAt(0),
      );
      let subscription = await registration.pushManager.getSubscription();
      const currentKey = subscription?.options.applicationServerKey;
      if (
        subscription &&
        (!currentKey || !this.sameKey(new Uint8Array(currentKey), applicationServerKey))
      ) {
        await subscription.unsubscribe();
        subscription = null;
      }
      subscription ??= await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey,
      });
      const data = subscription.toJSON();
      await firstValueFrom(
        this.http.post('/api/v1/notifications/push/subscribe', {
          endpoint: subscription.endpoint,
          p256dh: data.keys?.['p256dh'],
          auth: data.keys?.['auth'],
        }),
      );
      this.message.set('Đã bật thông báo trên thiết bị này cho tài khoản hiện tại.');
    } catch {
      this.message.set(
        'Chưa bật được thông báo. Kiểm tra kết nối, quyền trình duyệt và cấu hình của quản trị viên rồi thử lại.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  async disable() {
    if (!this.supported || this.busy()) return;
    this.busy.set(true);
    try {
      const registration = await navigator.serviceWorker.getRegistration('/bizflow-push/');
      const subscription = await registration?.pushManager.getSubscription();
      if (subscription) {
        // Disable on the device even if the server is temporarily unreachable.
        await subscription.unsubscribe();
        await firstValueFrom(
          this.http.request('DELETE', '/api/v1/notifications/push/subscribe', {
            body: { endpoint: subscription.endpoint },
          }),
        );
      }
      this.message.set('Đã tắt thông báo trên thiết bị này.');
    } catch {
      this.message.set(
        'Không xác nhận được việc tắt. Kiểm tra kết nối hoặc chặn quyền thông báo trong cài đặt trình duyệt.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  private sameKey(left: Uint8Array, right: Uint8Array) {
    return left.length === right.length && left.every((value, i) => value === right[i]);
  }
}
