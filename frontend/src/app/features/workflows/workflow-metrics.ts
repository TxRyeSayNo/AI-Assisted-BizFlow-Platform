import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
interface StepMetric {
  workflow: string;
  versionNo: number;
  step: string;
  waiting: number;
  completed: number;
  breached: number;
  averageActualMinutes: number | null;
  p90ActualMinutes: number | null;
}
@Component({
  selector: 'bf-workflow-metrics',
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: ` <main class="page-content" id="main-content">
    <a routerLink="/reports">Báo cáo</a>
    <h1>Điểm nghẽn quy trình</h1>
    <p>
      Dữ liệu theo phạm vi hồ sơ bạn được phép xem. Thời gian xử lý là phút thực tế; quá hạn được so
      với hạn bước đã lưu.
    </p>
    <label
      >Khoảng thời gian
      <select [(ngModel)]="days" (ngModelChange)="load()">
        <option [ngValue]="7">7 ngày</option>
        <option [ngValue]="30">30 ngày</option>
        <option [ngValue]="90">90 ngày</option>
      </select></label
    >
    <p>Bao gồm các bước đang chờ từ trước kỳ báo cáo.</p>
    @if (loading()) {
      <p role="status">Đang tải…</p>
    }
    @if (error()) {
      <p role="alert">{{ error() }}</p>
      <button type="button" (click)="load()">Thử lại</button>
    }
    <div style="overflow-x:auto">
      <table>
        <thead>
          <tr>
            <th>Quy trình / phiên bản</th>
            <th>Bước</th>
            <th>Đang chờ</th>
            <th>Hoàn tất</th>
            <th>Quá hạn</th>
            <th>Trung bình (phút)</th>
            <th>P90 (phút)</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track $index) {
            <tr>
              <td>{{ row.workflow }} / {{ row.versionNo }}</td>
              <td>{{ row.step }}</td>
              <td>{{ row.waiting }}</td>
              <td>{{ row.completed }}</td>
              <td>{{ row.breached }}</td>
              <td>{{ row.averageActualMinutes ?? 'Chưa có' }}</td>
              <td>{{ row.p90ActualMinutes ?? 'Chưa có' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (!loading() && !error() && rows().length === 0) {
      <p>Chưa có dữ liệu quy trình trong phạm vi này.</p>
    }
  </main>`,
  styles: [
    `
      table {
        width: 100%;
        border-collapse: collapse;
        margin-top: 1rem;
      }
      th,
      td {
        text-align: left;
        padding: 0.75rem;
        border-bottom: 1px solid #cbd5e1;
      }
    `,
  ],
})
export class WorkflowMetrics implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  readonly rows = signal<StepMetric[]>([]);
  readonly error = signal('');
  readonly loading = signal(false);
  days = 30;
  ngOnInit() {
    this.load();
  }
  load() {
    this.error.set('');
    this.loading.set(true);
    this.http
      .get<{ steps: StepMetric[] }>(`/api/v1/workflow-metrics?days=${this.days}`)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (value) => {
          this.rows.set(value.steps);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.loading.set(false);
          this.error.set('Không tải được báo cáo hoặc bạn không có quyền xem.');
        },
      });
  }
}
