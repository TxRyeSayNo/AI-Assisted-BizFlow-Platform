import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface EditableProposal {
  selected: boolean;
  title: string;
  description: string;
  type: 'Assignment' | 'SupportRequest';
  departmentId: string | null;
  departmentName: string | null;
  priority: 'LOW' | 'MEDIUM' | 'HIGH';
  checklistItems: string[];
  newChecklistInput?: string;
  reasoning?: string;
}

@Component({
  selector: 'bf-task-decomposition-preview',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './task-decomposition-preview.html',
  styleUrls: [
    '../organization/departments.scss',
    './task-editor.scss',
    './task-decomposition-preview.scss',
  ],
})
export class TaskDecompositionPreview implements OnInit {
  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);

  readonly prompt = signal('');
  readonly previewBusy = signal(false);
  readonly confirmBusy = signal(false);
  readonly error = signal('');
  readonly successMessage = signal('');
  readonly proposals = signal<EditableProposal[]>([]);
  readonly departments = signal<Array<{ id: string; name: string }>>([]);

  ngOnInit() {
    this.loadDepartments();
  }

  loadDepartments() {
    this.http
      .get<any>('/api/v1/departments')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          const list = Array.isArray(res) ? res : res?.items || [];
          this.departments.set(list.map((d: any) => ({ id: d.id, name: d.name })));
        },
        error: () => {},
      });
  }

  setSamplePrompt(text: string) {
    this.prompt.set(text);
  }

  previewDecomposition() {
    const text = this.prompt().trim();
    if (!text || this.previewBusy()) return;

    this.previewBusy.set(true);
    this.error.set('');
    this.successMessage.set('');

    this.http
      .post<{ originalPrompt: string; proposals: any[] }>(
        '/api/v1/tasks/ai-decomposition/preview',
        {
          prompt: text,
        },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.previewBusy.set(false);
          const mapped: EditableProposal[] = (result.proposals || []).map((p) => ({
            selected: true,
            title: p.title || '',
            description: p.description || '',
            type: p.type === 'SupportRequest' ? 'SupportRequest' : 'Assignment',
            departmentId: p.departmentId || null,
            departmentName: p.departmentName || null,
            priority: p.priority === 'HIGH' || p.priority === 'LOW' ? p.priority : 'MEDIUM',
            checklistItems: Array.isArray(p.checklistItems) ? [...p.checklistItems] : [],
            newChecklistInput: '',
            reasoning: p.reasoning || '',
          }));
          this.proposals.set(mapped);
        },
        error: (err) => {
          this.previewBusy.set(false);
          this.error.set(err?.error?.message || 'Không thể bóc tách công việc. Vui lòng thử lại.');
        },
      });
  }

  toggleSelectAll(checked: boolean) {
    this.proposals.update((items) => items.map((it) => ({ ...it, selected: checked })));
  }

  get selectedCount(): number {
    return this.proposals().filter((p) => p.selected).length;
  }

  removeItem(index: number) {
    this.proposals.update((items) => items.filter((_, idx) => idx !== index));
  }

  addChecklistItem(item: EditableProposal) {
    const text = item.newChecklistInput?.trim();
    if (!text) return;
    item.checklistItems.push(text);
    item.newChecklistInput = '';
  }

  removeChecklistItem(item: EditableProposal, checkIndex: number) {
    item.checklistItems.splice(checkIndex, 1);
  }

  addNewProposal() {
    this.proposals.update((items) => [
      ...items,
      {
        selected: true,
        title: 'Công việc mới bổ sung',
        description: '',
        type: 'Assignment',
        departmentId: null,
        departmentName: null,
        priority: 'MEDIUM',
        checklistItems: [],
        newChecklistInput: '',
      },
    ]);
  }

  confirmDecomposition() {
    const selected = this.proposals().filter((p) => p.selected);
    if (selected.length === 0 || this.confirmBusy()) return;

    for (const item of selected) {
      if (!item.title.trim()) {
        this.error.set('Tất cả các đầu việc được chọn đều phải có tiêu đề.');
        return;
      }
    }

    this.confirmBusy.set(true);
    this.error.set('');

    const payload = {
      items: selected.map((it) => ({
        title: it.title.trim(),
        description: it.description?.trim() || null,
        type: it.type,
        departmentId: it.departmentId || null,
        priority: it.priority,
        checklistItems: it.checklistItems.length > 0 ? it.checklistItems : null,
      })),
    };

    this.http
      .post<{ totalCreated: number; items: any[] }>(
        '/api/v1/tasks/ai-decomposition/confirm',
        payload,
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (resp) => {
          this.confirmBusy.set(false);
          this.successMessage.set(
            `Đã tạo thành công ${resp.totalCreated} công việc / yêu cầu vào hệ thống!`,
          );
          this.proposals.set([]);
          setTimeout(() => {
            this.router.navigate(['/tasks']);
          }, 1500);
        },
        error: (err) => {
          this.confirmBusy.set(false);
          this.error.set(
            err?.error?.message ||
              'Xác nhận tạo việc thất bại. Vui lòng kiểm tra quyền hạn của bạn.',
          );
        },
      });
  }
}
