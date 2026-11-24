import { roleLabels } from '../../core/auth/user-display';
import { ReadableDatesPipe } from '../../shared/formatting/readable-date';
import { Component, Input, OnInit, effect, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ToastService } from '../../core/toast/toast.service';

/** A person the composer can name with "@" (F3). */
export interface MentionCandidate {
  userId: string;
  fullName: string;
  employeeCode: string;
}

export interface CommentItem {
  commentId: string;
  objectType: string;
  objectId: string;
  authorId: string;
  authorName: string;
  authorNickname?: string;
  authorRoles?: string[];
  authorEmail: string;
  content: string;
  createdAt: string;
  editedAt: string | null;
  isOwner: boolean;
  canDelete: boolean;
}

@Component({
  selector: 'bf-work-item-comments',
  standalone: true,
  imports: [
    ReadableDatesPipe,
    CommonModule,
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
  ],
  templateUrl: './work-item-comments.html',
  styleUrls: ['./work-item-comments.scss'],
})
export class WorkItemComments implements OnInit {
  readonly roleLabels = roleLabels;
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(I18nService);

  readonly comments = signal<CommentItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly submitting = signal(false);

  newCommentText = '';
  editingCommentId: string | null = null;
  editingText = '';
  savingEdit = false;

  // ---------------------------------------------------------------------------------------------
  // F3, additive: "@" mentions. Nothing below runs unless the Mentions feature is on, and the
  // comment itself is posted exactly as before — mentions are recorded afterwards, and a failure
  // to record them never affects the comment that was already created.
  // ---------------------------------------------------------------------------------------------
  readonly features = inject(FeatureFlagsService);
  readonly mentionCandidates = signal<MentionCandidate[]>([]);
  readonly mentionQuery = signal<string | null>(null);
  readonly picked = signal<MentionCandidate[]>([]);
  private candidatesLoaded = false;

  /** Suggestions for the "@" fragment currently being typed, capped for a usable list. */
  readonly mentionSuggestions = () => {
    const query = this.mentionQuery();
    if (query === null) return [];
    const needle = query.toLowerCase();
    const chosen = new Set(this.picked().map((p) => p.userId));
    return this.mentionCandidates()
      .filter((candidate) => !chosen.has(candidate.userId))
      .filter(
        (candidate) =>
          needle.length === 0 ||
          candidate.fullName.toLowerCase().includes(needle) ||
          candidate.employeeCode.toLowerCase().includes(needle),
      )
      .slice(0, 8);
  };

  /** Called as the composer text changes; opens the picker on a trailing "@fragment". */
  onCommentInput(value: string) {
    this.newCommentText = value;
    if (!this.features.mentions()) return;
    const match = /@([\p{L}\p{N}._-]*)$/u.exec(value);
    this.mentionQuery.set(match ? match[1] : null);
    if (match) this.loadCandidates();
  }

  pickMention(candidate: MentionCandidate) {
    // Replace the "@fragment" being typed with the person's name, and remember the id.
    this.newCommentText = this.newCommentText.replace(
      /@([\p{L}\p{N}._-]*)$/u,
      `@${candidate.fullName} `,
    );
    this.picked.update((prev) =>
      prev.some((p) => p.userId === candidate.userId) ? prev : [...prev, candidate],
    );
    this.mentionQuery.set(null);
  }

  removeMention(userId: string) {
    this.picked.update((prev) => prev.filter((p) => p.userId !== userId));
  }

  private loadCandidates() {
    if (this.candidatesLoaded) return;
    this.candidatesLoaded = true;
    // The directory endpoint already limits itself to what the caller may read.
    this.http
      .get<{
        items: { userId: string; fullName: string; employeeCode: string }[];
      }>('/api/v1/users', { params: { pageSize: 100 } })
      .subscribe({
        next: (page) =>
          this.mentionCandidates.set(
            (page?.items ?? [])
              .filter((item) => item.userId && item.userId !== this.session.identity()?.userId)
              .map((item) => ({
                userId: item.userId,
                fullName: item.fullName ?? item.employeeCode,
                employeeCode: item.employeeCode ?? '',
              })),
          ),
        error: () => {
          // Allow another attempt later; the composer keeps working without suggestions.
          this.candidatesLoaded = false;
          this.mentionCandidates.set([]);
        },
      });
  }

  private recordMentions(commentId: string) {
    const userIds = this.picked().map((p) => p.userId);
    this.picked.set([]);
    if (!this.features.mentions() || userIds.length === 0 || !commentId) return;
    this.http
      .post(`/api/v1/work-items/${this.objectType}/${encodeURIComponent(this.objectId)}/mentions`, {
        commentId,
        userIds,
      })
      .subscribe({
        next: () => {},
        error: () =>
          this.toast.error(
            this.i18n.t('comments.errMentions', 'Đã lưu bình luận nhưng không gửi được lời nhắc.'),
          ),
      });
  }

  constructor() {
    effect(() => {
      const err = this.error();
      if (err) {
        this.toast.error(err);
      }
    });
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
      .get<CommentItem[]>(`/api/v1/comments`, {
        params: { objectType: this.objectType, objectId: this.objectId },
      })
      .subscribe({
        next: (items) => {
          this.comments.set(items);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(
            err?.error?.message ?? this.i18n.t('comments.errLoad', 'Failed to load comments.'),
          );
          this.loading.set(false);
        },
      });
  }

  postComment() {
    const text = this.newCommentText.trim();
    if (!text || this.submitting()) return;

    this.submitting.set(true);
    this.error.set('');

    this.http
      .post<CommentItem>('/api/v1/comments', {
        objectType: this.objectType,
        objectId: this.objectId,
        content: text,
      })
      .subscribe({
        next: (created) => {
          this.comments.update((prev) => [...prev, created]);
          this.newCommentText = '';
          this.submitting.set(false);
          this.mentionQuery.set(null);
          // F3: a separate, non-blocking step once the comment itself is safely created.
          this.recordMentions(created?.commentId);
        },
        error: (err) => {
          this.error.set(
            err?.error?.message ?? this.i18n.t('comments.errPost', 'Failed to post comment.'),
          );
          this.submitting.set(false);
        },
      });
  }

  startEdit(comment: CommentItem) {
    this.editingCommentId = comment.commentId;
    this.editingText = comment.content;
  }

  cancelEdit() {
    this.editingCommentId = null;
    this.editingText = '';
  }

  saveEdit(commentId: string) {
    const text = this.editingText.trim();
    if (!text || this.savingEdit) return;

    this.savingEdit = true;
    this.error.set('');

    this.http.put<CommentItem>(`/api/v1/comments/${commentId}`, { content: text }).subscribe({
      next: (updated) => {
        this.comments.update((prev) => prev.map((c) => (c.commentId === commentId ? updated : c)));
        this.editingCommentId = null;
        this.editingText = '';
        this.savingEdit = false;
      },
      error: (err) => {
        this.error.set(
          err?.error?.message ?? this.i18n.t('comments.errUpdate', 'Failed to update comment.'),
        );
        this.savingEdit = false;
      },
    });
  }

  deleteComment(commentId: string) {
    if (!confirm('Bạn có chắc chắn muốn xóa bình luận này không?')) return;

    this.error.set('');
    this.http.delete(`/api/v1/comments/${commentId}`).subscribe({
      next: () => {
        this.comments.update((prev) => prev.filter((c) => c.commentId !== commentId));
      },
      error: (err) => {
        this.error.set(
          err?.error?.message ?? this.i18n.t('comments.errDelete', 'Failed to delete comment.'),
        );
      },
    });
  }
}
