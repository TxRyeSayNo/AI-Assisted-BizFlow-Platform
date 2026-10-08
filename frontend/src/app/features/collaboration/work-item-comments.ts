import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { SessionService } from '../../core/auth/session';

export interface CommentItem {
  commentId: string;
  objectType: string;
  objectId: string;
  authorId: string;
  authorName: string;
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
    CommonModule,
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './work-item-comments.html',
  styleUrls: ['./work-item-comments.scss'],
})
export class WorkItemComments implements OnInit {
  @Input({ required: true }) objectType!: 'TASK' | 'REQUEST';
  @Input({ required: true }) objectId!: string;

  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);

  readonly comments = signal<CommentItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly submitting = signal(false);

  newCommentText = '';
  editingCommentId: string | null = null;
  editingText = '';
  savingEdit = false;

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
          this.error.set(err?.error?.message ?? 'Failed to load comments.');
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
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to post comment.');
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

    this.http
      .put<CommentItem>(`/api/v1/comments/${commentId}`, { content: text })
      .subscribe({
        next: (updated) => {
          this.comments.update((prev) =>
            prev.map((c) => (c.commentId === commentId ? updated : c))
          );
          this.editingCommentId = null;
          this.editingText = '';
          this.savingEdit = false;
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to update comment.');
          this.savingEdit = false;
        },
      });
  }

  deleteComment(commentId: string) {
    if (!confirm('Are you sure you want to delete this comment?')) return;

    this.error.set('');
    this.http.delete(`/api/v1/comments/${commentId}`).subscribe({
      next: () => {
        this.comments.update((prev) => prev.filter((c) => c.commentId !== commentId));
      },
      error: (err) => {
        this.error.set(err?.error?.message ?? 'Failed to delete comment.');
      },
    });
  }
}
