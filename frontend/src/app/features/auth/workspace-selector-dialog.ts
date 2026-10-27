import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { WorkspaceCandidate } from '../../core/auth/session';

export interface WorkspaceSelectorDialogData {
  workspaces: WorkspaceCandidate[];
  ticket: string;
}

@Component({
  selector: 'bf-workspace-selector-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <div class="workspace-selector-container">
      <h2 mat-dialog-title class="dialog-title">
        <span class="icon-badge">🏢</span> Chọn Không Gian Làm Việc
      </h2>
      <mat-dialog-content class="dialog-content">
        <p class="subtitle">
          Địa chỉ email của bạn thuộc về nhiều tổ chức. Vui lòng chọn không gian làm việc muốn truy cập:
        </p>

        <div class="workspace-list" role="radiogroup" aria-label="Danh sách không gian làm việc">
          @for (ws of data.workspaces; track ws.tenantKey) {
            <button
              type="button"
              class="workspace-item"
              [class.active]="selectedKey() === ws.tenantKey"
              (click)="selectedKey.set(ws.tenantKey)"
              role="radio"
              [attr.aria-checked]="selectedKey() === ws.tenantKey"
            >
              <div class="ws-info">
                <span class="ws-name">{{ ws.tenantName }}</span>
                <span class="ws-key">{{ ws.tenantKey }}</span>
              </div>
              <div class="radio-indicator">
                @if (selectedKey() === ws.tenantKey) {
                  <span class="dot"></span>
                }
              </div>
            </button>
          }
        </div>
      </mat-dialog-content>

      <mat-dialog-actions align="end" class="dialog-actions">
        <button mat-button (click)="dialogRef.close(null)">Hủy</button>
        <button
          mat-flat-button
          color="primary"
          [disabled]="!selectedKey()"
          (click)="confirmSelection()"
        >
          Vào Không Gian Làm Việc
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .workspace-selector-container {
      padding: 0.5rem 0.25rem;
      min-width: 380px;
      max-width: 500px;
    }
    .dialog-title {
      font-size: 1.25rem;
      font-weight: 700;
      color: #0f172a;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .icon-badge {
      font-size: 1.4rem;
    }
    .subtitle {
      font-size: 0.875rem;
      color: #475569;
      margin-bottom: 1rem;
      line-height: 1.4;
    }
    .workspace-list {
      display: flex;
      flex-direction: column;
      gap: 0.625rem;
      margin-bottom: 1rem;
      max-height: 320px;
      overflow-y: auto;
    }
    .workspace-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.875rem 1rem;
      border-radius: 8px;
      border: 1.5px solid #e2e8f0;
      background: #f8fafc;
      cursor: pointer;
      text-align: left;
      transition: all 0.15s ease-in-out;
      width: 100%;
    }
    .workspace-item:hover {
      background: #f1f5f9;
      border-color: #cbd5e1;
    }
    .workspace-item.active {
      border-color: #2563eb;
      background: #eff6ff;
    }
    .ws-info {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .ws-name {
      font-size: 0.95rem;
      font-weight: 600;
      color: #1e293b;
    }
    .workspace-item.active .ws-name {
      color: #1d4ed8;
    }
    .ws-key {
      font-size: 0.775rem;
      color: #64748b;
      font-family: monospace;
    }
    .radio-indicator {
      width: 20px;
      height: 20px;
      border-radius: 50%;
      border: 2px solid #cbd5e1;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .workspace-item.active .radio-indicator {
      border-color: #2563eb;
    }
    .radio-indicator .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: #2563eb;
    }
    .dialog-actions {
      padding-top: 0.5rem;
      border-top: 1px solid #f1f5f9;
    }
  `],
})
export class WorkspaceSelectorDialog {
  readonly dialogRef = inject(MatDialogRef<WorkspaceSelectorDialog, string | null>);
  readonly data: WorkspaceSelectorDialogData = inject(MAT_DIALOG_DATA);
  readonly selectedKey = signal<string>(this.data.workspaces[0]?.tenantKey ?? '');

  confirmSelection(): void {
    const key = this.selectedKey();
    if (key) {
      this.dialogRef.close(key);
    }
  }
}
