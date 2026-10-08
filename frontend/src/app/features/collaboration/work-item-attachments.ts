import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { lastValueFrom } from 'rxjs';
import { SessionService } from '../../core/auth/session';

export interface AttachmentItem {
  attachmentId: string;
  objectType: string;
  objectId: string;
  uploadedBy: string;
  uploaderName: string;
  uploaderEmail: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  hash: string;
  createdAt: string;
  isOwner: boolean;
  canDelete: boolean;
}

export interface UploadSessionResult {
  attachmentId: string;
  objectKey: string;
  uploadUrl: string;
  expiresAt: string;
}

@Component({
  selector: 'bf-work-item-attachments',
  standalone: true,
  imports: [CommonModule, DatePipe, MatButtonModule, MatProgressBarModule],
  templateUrl: './work-item-attachments.html',
  styleUrls: ['./work-item-attachments.scss'],
})
export class WorkItemAttachments implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);

  readonly attachments = signal<AttachmentItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly uploading = signal(false);
  readonly uploadMessage = signal('');

  // 500 MB hard limit
  readonly MAX_FILE_SIZE = 524288000;

  ngOnInit() {
    if (this.objectId) {
      this.load();
    }
  }

  load() {
    this.loading.set(true);
    this.error.set('');
    this.http
      .get<AttachmentItem[]>('/api/v1/attachments', {
        params: { objectType: this.objectType, objectId: this.objectId },
      })
      .subscribe({
        next: (items) => {
          this.attachments.set(items);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load attachments.');
          this.loading.set(false);
        },
      });
  }

  async onFileSelected(event: Event, inputElement: HTMLInputElement) {
    const files = inputElement.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    if (file.size > this.MAX_FILE_SIZE) {
      this.error.set('File size exceeds maximum limit of 500 MB.');
      inputElement.value = '';
      return;
    }

    this.uploading.set(true);
    this.uploadMessage.set('Preparing upload session…');
    this.error.set('');

    try {
      // Step 1: Create upload session
      const sessionResult = await lastValueFrom(
        this.http.post<UploadSessionResult>('/api/v1/attachments/upload-session', {
          objectType: this.objectType,
          objectId: this.objectId,
          fileName: file.name,
          contentType: file.type || 'application/octet-stream',
          sizeBytes: file.size,
        })
      );

      if (!sessionResult) throw new Error('Failed to create upload session.');

      // Step 2: Upload binary
      this.uploadMessage.set('Uploading file…');
      const fileBytes = await file.arrayBuffer();
      await lastValueFrom(
        this.http.put(sessionResult.uploadUrl, fileBytes, {
          headers: { 'Content-Type': file.type || 'application/octet-stream' },
        })
      );

      // Step 3: Compute SHA-256 hash
      this.uploadMessage.set('Verifying integrity…');
      const hashHex = await this.computeSha256(fileBytes);

      // Step 4: Finalize
      this.uploadMessage.set('Finalizing attachment…');
      const finalized = await lastValueFrom(
        this.http.post<AttachmentItem>('/api/v1/attachments/finalize', {
          attachmentId: sessionResult.attachmentId,
          clientHash: hashHex,
        })
      );

      if (finalized) {
        this.attachments.update((prev) => [...prev, finalized]);
      }
      inputElement.value = '';
    } catch (err: any) {
      this.error.set(err?.error?.message ?? err?.message ?? 'Attachment upload failed.');
    } finally {
      this.uploading.set(false);
      this.uploadMessage.set('');
    }
  }

  deleteAttachment(attachmentId: string) {
    if (!confirm('Are you sure you want to delete this attachment?')) return;

    this.error.set('');
    this.http.delete(`/api/v1/attachments/${attachmentId}`).subscribe({
      next: () => {
        this.attachments.update((prev) => prev.filter((a) => a.attachmentId !== attachmentId));
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Failed to delete attachment.');
      },
    });
  }

  downloadUrl(attachmentId: string): string {
    return `/api/v1/attachments/${attachmentId}/download`;
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    const formatted = parseFloat((bytes / Math.pow(k, i)).toFixed(2));
    return `${formatted} ${sizes[i]}`;
  }

  private async computeSha256(buffer: ArrayBuffer): Promise<string> {
    try {
      if (typeof crypto !== 'undefined' && crypto.subtle && typeof crypto.subtle.digest === 'function') {
        const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
      }
    } catch {
      // Graceful fallback if subtle crypto is unavailable or fails in test environment
    }
    return '';
  }
}
