import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { lastValueFrom } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DocumentSummarizerDialog } from '../../shared/ai/document-summarizer-dialog';
import { ToastService } from '../../core/toast/toast.service';

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
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatProgressBarModule,
    TranslatePipe,
    DocumentSummarizerDialog,
  ],
  templateUrl: './work-item-attachments.html',
  styleUrls: ['./work-item-attachments.scss'],
})
export class WorkItemAttachments implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly attachments = signal<AttachmentItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly uploading = signal(false);
  readonly uploadMessage = signal('');

  readonly summarizerOpen = signal(false);
  readonly activeAttachmentForSummary = signal<AttachmentItem | null>(null);

  // 500 MB hard limit
  readonly MAX_FILE_SIZE = 524288000;

  readonly ALLOWED_EXTENSIONS = [
    'pdf',
    'doc',
    'docx',
    'xls',
    'xlsx',
    'ppt',
    'pptx',
    'txt',
    'csv',
    'png',
    'jpg',
    'jpeg',
    'gif',
    'webp',
    'zip',
  ];

  openAiSummarizer(att: AttachmentItem) {
    this.activeAttachmentForSummary.set(att);
    this.summarizerOpen.set(true);
  }

  closeAiSummarizer() {
    this.summarizerOpen.set(false);
    this.activeAttachmentForSummary.set(null);
  }

  onTasksCreatedFromSummary(_tasks: any[]) {
    // Optionally trigger refresh if we are on a task/request view
  }

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
          this.error.set(
            err?.error?.message ??
              this.i18n.t('attachments.errLoad', 'Failed to load attachments.'),
          );
          this.loading.set(false);
        },
      });
  }

  async onFileSelected(_event: Event, inputElement: HTMLInputElement) {
    const files = inputElement.files;
    if (!files || files.length === 0) return;

    const file = files[0];

    // Client-side validation: file extension check
    const ext = file.name.split('.').pop()?.toLowerCase() || '';
    if (!this.ALLOWED_EXTENSIONS.includes(ext)) {
      const msg = `Định dạng tệp ".${ext || 'không xác định'}" không được hỗ trợ. Vui lòng chọn tệp tài liệu (PDF, Word, Excel, PowerPoint), văn bản (TXT, CSV), hình ảnh (PNG, JPG, WEBP) hoặc tệp nén (ZIP).`;
      this.error.set(msg);
      this.toast.error(msg, 'Định dạng tệp không hợp lệ');
      inputElement.value = '';
      return;
    }

    if (file.size > this.MAX_FILE_SIZE) {
      const msg = 'Dung lượng tệp vượt quá giới hạn tối đa 500 MB.';
      this.error.set(msg);
      this.toast.error(msg, 'Tệp quá lớn');
      inputElement.value = '';
      return;
    }

    if (file.size === 0) {
      const msg = 'Tệp đính kèm không được để rỗng (0 bytes).';
      this.error.set(msg);
      this.toast.error(msg, 'Tệp không hợp lệ');
      inputElement.value = '';
      return;
    }

    this.uploading.set(true);
    this.uploadMessage.set('Đang chuẩn bị phiên tải lên…');
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
        }),
      );

      if (!sessionResult) {
        throw new Error(
          this.i18n.t('attachments.errCreateSession', 'Không thể khởi tạo phiên tải lên.'),
        );
      }

      // Step 2: Upload binary
      this.uploadMessage.set('Đang tải tệp lên…');
      const fileBytes = await file.arrayBuffer();
      await lastValueFrom(
        this.http.put(sessionResult.uploadUrl, fileBytes, {
          headers: { 'Content-Type': file.type || 'application/octet-stream' },
        }),
      );

      // Step 3: Compute SHA-256 hash
      this.uploadMessage.set('Đang kiểm tra toàn vẹn tệp…');
      const hashHex = await this.computeSha256(fileBytes);

      // Step 4: Finalize
      this.uploadMessage.set('Đang hoàn tất đính kèm…');
      const finalized = await lastValueFrom(
        this.http.post<AttachmentItem>('/api/v1/attachments/finalize', {
          attachmentId: sessionResult.attachmentId,
          clientHash: hashHex,
        }),
      );

      if (finalized) {
        this.attachments.update((prev) => [...prev, finalized]);
        this.toast.success(`Đã tải lên thành công tệp "${file.name}".`, 'Tải lên hoàn tất');
      }
      inputElement.value = '';
    } catch (err: any) {
      inputElement.value = '';
      const friendlyMessage = this.getFriendlyErrorMessage(err);
      this.error.set(friendlyMessage);
      this.toast.error(friendlyMessage, 'Lỗi tải tệp lên');
    } finally {
      this.uploading.set(false);
      this.uploadMessage.set('');
    }
  }

  deleteAttachment(attachmentId: string) {
    if (!confirm('Bạn có chắc chắn muốn xóa tệp đính kèm này không?')) return;

    this.error.set('');
    this.http.delete(`/api/v1/attachments/${attachmentId}`).subscribe({
      next: () => {
        this.attachments.update((prev) => prev.filter((a) => a.attachmentId !== attachmentId));
        this.toast.success('Đã xóa tệp đính kèm thành công.', 'Đã xóa');
      },
      error: (err) => {
        this.error.set(
          err?.error?.message ??
            this.i18n.t('attachments.errDelete', 'Failed to delete attachment.'),
        );
      },
    });
  }

  readonly downloading = signal<string | null>(null);

  downloadUrl(attachmentId: string): string {
    return `/api/v1/attachments/${attachmentId}/download`;
  }

  // The download endpoint requires the bearer token, so a raw anchor navigation returns 401.
  // Fetch the bytes through HttpClient (the interceptor adds the token), then save or view them.
  downloadAttachment(att: AttachmentItem, event?: Event, save = true): void {
    event?.preventDefault();
    event?.stopPropagation();
    if (this.downloading()) return;
    const viewer = save ? null : window.open('', '_blank');
    this.downloading.set(att.attachmentId);
    this.error.set('');
    this.http
      .get(this.downloadUrl(att.attachmentId), { responseType: 'blob' })
      .subscribe({
        next: (blob) => {
          this.downloading.set(null);
          const fileBlob =
            blob.type && blob.type !== 'application/octet-stream'
              ? blob
              : new Blob([blob], { type: att.contentType || 'application/octet-stream' });
          const url = window.URL.createObjectURL(fileBlob);
          if (save) {
            const anchor = document.createElement('a');
            anchor.href = url;
            anchor.download = att.fileName || 'download';
            anchor.style.display = 'none';
            document.body.appendChild(anchor);
            anchor.click();
            document.body.removeChild(anchor);
          } else if (viewer && !viewer.closed) {
            viewer.location.href = url;
          } else {
            window.open(url, '_blank');
          }
          setTimeout(() => window.URL.revokeObjectURL(url), 100);
        },
        error: (err) => {
          this.downloading.set(null);
          if (viewer && !viewer.closed) viewer.close();
          const msg =
            err?.status === 404
              ? 'Tệp đính kèm không tồn tại trên hệ thống hoặc đã bị xóa.'
              : err?.status === 403
                ? 'Bạn không có quyền tải xuống tệp đính kèm này.'
                : 'Không thể tải tệp đính kèm xuống. Vui lòng thử lại sau.';
          this.error.set(msg);
          this.toast.error(msg, 'Lỗi tải xuống');
        },
      });
  }

  private getFriendlyErrorMessage(err: any): string {
    if (!err) return 'Tải tệp đính kèm thất bại. Vui lòng thử lại.';

    const status = err?.status;
    const errCode = err?.error?.code;
    const rawMsg = String(err?.error?.message || err?.message || '');

    if (
      errCode === 'ATTACHMENT.TYPE_NOT_ALLOWED' ||
      rawMsg.includes('not permitted by tenant policy')
    ) {
      return 'Định dạng tệp này không được phép tải lên hệ thống. Vui lòng chọn tệp tài liệu, bảng tính, hình ảnh hoặc tệp nén hợp lệ.';
    }

    if (
      errCode === 'ATTACHMENT.SIZE_EXCEEDED' ||
      rawMsg.includes('exceeds maximum allowed size')
    ) {
      return 'Dung lượng tệp vượt quá giới hạn tối đa 500 MB cho phép của hệ thống.';
    }

    if (errCode === 'ATTACHMENT.INVALID_FILE_NAME') {
      return 'Tên tệp không hợp lệ. Vui lòng kiểm tra lại tên tệp và thử lại.';
    }

    if (
      status === 403 ||
      rawMsg.includes('403') ||
      rawMsg.includes('Forbidden') ||
      rawMsg.includes('SignatureDoesNotMatch')
    ) {
      return 'Máy chủ lưu trữ từ chối kết nối (403 Forbidden). Vui lòng thử lại sau hoặc liên hệ quản trị viên.';
    }

    if (status === 0 || rawMsg.includes('Http failure response')) {
      return 'Không thể kết nối đến máy chủ lưu trữ tệp. Vui lòng kiểm tra lại kết nối mạng.';
    }

    if (err?.error?.message && typeof err.error.message === 'string') {
      return err.error.message;
    }

    return 'Tải tệp đính kèm lên thất bại. Vui lòng thử lại.';
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
      if (
        typeof crypto !== 'undefined' &&
        crypto.subtle &&
        typeof crypto.subtle.digest === 'function'
      ) {
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
