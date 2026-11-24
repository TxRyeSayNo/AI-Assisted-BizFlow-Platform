import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Preferences {
  emailEnabled: boolean;
  deadlineEmailMode: string;
  dailyDigestEnabled: boolean;
  digestHour: number;
  digestDays: number;
  digestEmail: boolean;
}
@Component({
  selector: 'bf-notification-preferences',
  imports: [FormsModule],
  template: `
    <section aria-label="Cài đặt thông báo và tổng hợp công việc">
      <button type="button" (click)="load()" [disabled]="busy()">
        Cài đặt thông báo và tổng hợp công việc
      </button>
      @if (settings; as p) {
        <fieldset [disabled]="busy()">
          <legend>Email và nhắc hạn cá nhân</legend>
          <label
            ><input type="checkbox" [(ngModel)]="p.emailEnabled" /> Nhận email công việc và
            họp</label
          >
          <p>
            Mail kích hoạt tài khoản và reset mật khẩu vẫn được gửi. Thông báo trong ứng dụng luôn
            được giữ.
          </p>
          <label
            >Mail nhắc hạn
            <select [(ngModel)]="p.deadlineEmailMode">
              <option value="Always">Luôn gửi email</option>
              <option value="AwayOnly">Chỉ khi vắng mặt (ưu tiên push với nhiệm vụ)</option>
              <option value="Off">Không gửi email nhắc hạn</option>
            </select></label
          >
          <label
            ><input type="checkbox" [(ngModel)]="p.dailyDigestEnabled" /> Tổng hợp công việc hằng
            ngày</label
          >
          <label
            >Giờ gửi theo múi giờ doanh nghiệp
            <input type="number" min="0" max="23" [(ngModel)]="p.digestHour"
          /></label>
          <label
            >Số ngày cần tổng hợp, tính từ hôm nay
            <input type="number" min="1" max="30" [(ngModel)]="p.digestDays"
          /></label>
          <label
            ><input type="checkbox" [(ngModel)]="p.digestEmail" /> Gửi bản tổng hợp qua email (cần
            bật nhận email)</label
          >
          <button type="button" (click)="save()">Lưu tùy chọn</button>
        </fieldset>
        <fieldset [disabled]="busy()">
          <legend>Tạo bản tổng hợp theo khoảng ngày</legend>
          <p>
            Chỉ gồm công việc chưa hoàn tất được giao trực tiếp cho bạn, tối đa 31 ngày. Kết quả
            xuất hiện trong hộp thông báo; email theo tùy chọn đã lưu.
          </p>
          <label>Từ ngày <input type="date" [(ngModel)]="from" /></label>
          <label>Đến ngày <input type="date" [(ngModel)]="to" /></label>
          <button type="button" [disabled]="!from || !to" (click)="digest()">
            Tạo bản tổng hợp
          </button>
        </fieldset>
      }
      <p role="status">{{ message() }}</p>
    </section>
  `,
  styles: [
    `
      fieldset {
        display: grid;
        gap: 0.75rem;
        padding: 1rem;
        margin: 1rem 0;
      }
      label {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        align-items: center;
      }
      input,
      select,
      button {
        padding: 0.4rem;
      }
      p {
        margin: 0.25rem 0;
      }
    `,
  ],
})
export class NotificationPreferences {
  private readonly http = inject(HttpClient);
  settings: Preferences | null = null;
  from = '';
  to = '';
  readonly busy = signal(false);
  readonly message = signal('');
  load() {
    if (this.busy()) return;
    this.busy.set(true);
    this.http.get<Preferences>('/api/v1/notifications/preferences').subscribe({
      next: (data) => {
        this.settings = data;
        this.busy.set(false);
        this.message.set('');
      },
      error: () => this.failed('Không tải được tùy chọn thông báo.'),
    });
  }
  save() {
    if (!this.settings || this.busy()) return;
    this.busy.set(true);
    this.http.put<Preferences>('/api/v1/notifications/preferences', this.settings).subscribe({
      next: (data) => {
        this.settings = data;
        this.busy.set(false);
        this.message.set('Đã lưu tùy chọn thông báo.');
      },
      error: () => this.failed('Không lưu được. Kiểm tra giờ 0–23 và số ngày 1–30.'),
    });
  }
  digest() {
    if (this.busy() || !this.from || !this.to) return;
    this.busy.set(true);
    this.http
      .post<{
        created: boolean;
        result: { taskCount: number } | null;
      }>('/api/v1/notifications/digests', { from: this.from, to: this.to })
      .subscribe({
        next: (data) => {
          this.busy.set(false);
          this.message.set(
            data.created
              ? `Đã tạo tổng hợp ${data.result?.taskCount ?? 0} công việc. Bấm Làm mới hộp thông báo để xem.`
              : 'Bản tổng hợp khoảng ngày này đã có trong hộp thông báo.',
          );
        },
        error: () =>
          this.failed('Không tạo được bản tổng hợp. Chọn khoảng hợp lệ, tối đa 31 ngày.'),
      });
  }
  private failed(message: string) {
    this.busy.set(false);
    this.message.set(message);
  }
}
