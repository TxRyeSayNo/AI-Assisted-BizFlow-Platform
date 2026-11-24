import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { ToastService } from '../../core/toast/toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

export interface AssignableManager {
  id: string;
  fullName: string;
  employeeCode: string;
  title: string;
}

export interface AssignableEmployee {
  id: string;
  fullName: string;
  employeeCode: string;
  departmentId?: string | null;
  departmentName?: string | null;
  roleTitle: string;
}

export interface AssignableDepartment {
  id: string;
  name: string;
  code: string;
  managers: AssignableManager[];
  employees: AssignableEmployee[];
}

export interface AssignableRecipientsView {
  departments: AssignableDepartment[];
  allEmployees: AssignableEmployee[];
}

export interface SelectedIndependentRecipient {
  userId: string;
  fullName: string;
  employeeCode: string;
  departmentName: string;
  isPrimary: boolean;
  responsibility: string;
  deadline: string;
  weight: number;
}

export interface SelectedProjectDepartmentPackage {
  departmentId: string;
  departmentName: string;
  departmentCode: string;
  managerId: string;
  availableManagers: AssignableManager[];
  packageName: string;
  scopeDescription: string;
  deadline: string;
  weight: number;
}

@Component({
  selector: 'bf-task-create',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterLink,
    TranslatePipe,
  ],
  templateUrl: './task-create.html',
  styleUrls: ['../organization/departments.scss', './task-editor.scss'],
})
export class TaskCreate {
  readonly Math = Math;
  readonly session = inject(SessionService);
  private readonly toast = inject(ToastService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);

  readonly busy = signal(false);
  readonly uncertain = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  readonly requestId = signal<string | null>(null);
  readonly parentTaskId = signal<string | null>(null);
  readonly parentTaskTitle = signal<string | null>(null);

  // AI Assistance
  readonly aiPrompt = signal('');
  readonly aiBusy = signal(false);
  readonly aiMessage = signal('');

  // Assignment permissions & modes (S1.1 & S2.1: Enabled for both Company Admin and Department Managers)
  readonly isCompanyAdmin = computed(() => this.session.hasPermission('roles.configure'));
  readonly canAssignDirectly = computed(
    () =>
      this.session.hasPermission('tasks.create') &&
      (this.session.hasPermission('tasks.assign') || this.session.hasPermission('roles.configure')),
  );
  readonly isManager = computed(() => !this.isCompanyAdmin() && this.canAssignDirectly());
  readonly assignmentType = signal<'INDEPENDENT' | 'PROJECT'>('INDEPENDENT');
  readonly assignableData = signal<AssignableRecipientsView | null>(null);
  readonly loadingRecipients = signal(false);

  // Independent Task & Subtask WBS State
  readonly independentDeptFilter = signal<string>('');
  readonly independentSearchQuery = signal<string>('');
  readonly selectedRecipients = signal<SelectedIndependentRecipient[]>([]);

  // Project State
  readonly selectedProjectPackages = signal<SelectedProjectDepartmentPackage[]>([]);
  readonly projectDeptToAdd = signal<string>('');

  private key = crypto.randomUUID();
  private submitted?: object;

  readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(300)]],
    description: [''],
    priority: ['MEDIUM'],
    deadline: [''],
    checklist: [''],
  });

  // Filtered employees for Independent Task & Subtask WBS picker
  readonly filteredEmployees = computed(() => {
    const data = this.assignableData();
    if (!data) return [];
    let list = data.allEmployees || [];

    const deptId = this.independentDeptFilter();
    if (deptId) {
      list = list.filter((e) => e.departmentId === deptId);
    }

    const q = this.independentSearchQuery().trim().toLowerCase();
    if (q) {
      list = list.filter(
        (e) =>
          e.fullName.toLowerCase().includes(q) ||
          e.employeeCode.toLowerCase().includes(q) ||
          (e.departmentName && e.departmentName.toLowerCase().includes(q)),
      );
    }

    return list;
  });

  // Unselected departments available for project addition
  readonly availableDepartmentsForProject = computed(() => {
    const data = this.assignableData();
    if (!data) return [];
    const chosenIds = new Set(this.selectedProjectPackages().map((p) => p.departmentId));
    return data.departments.filter((d) => !chosenIds.has(d.id));
  });

  // Total independent / subtask WBS weight (S1.4 & S2.3)
  readonly totalIndependentWeight = computed(() => {
    return (
      Math.round(
        this.selectedRecipients().reduce((acc, r) => acc + (Number(r.weight) || 0), 0) * 10,
      ) / 10
    );
  });

  // Total project weight
  readonly totalProjectWeight = computed(() => {
    return (
      Math.round(
        this.selectedProjectPackages().reduce((acc, p) => acc + (Number(p.weight) || 0), 0) * 10,
      ) / 10
    );
  });

  constructor() {
    if (!this.session.hasPermission('tasks.create')) {
      this.toast.warning(
        'Tài khoản Nhân viên không thể tạo nhiệm vụ cho cấp dưới. Vui lòng tạo Yêu cầu nếu cần đề xuất công việc.',
        'Phân quyền từ chối',
      );
      void this.router.navigateByUrl('/tasks', { replaceUrl: true });
      return;
    }

    this.route.queryParams.pipe(takeUntilDestroyed(this.destroy)).subscribe((params) => {
      if (params['requestId']) {
        this.requestId.set(params['requestId']);
      }
      if (params['parentTaskId']) {
        this.parentTaskId.set(params['parentTaskId']);
        if (params['parentTaskTitle']) {
          this.parentTaskTitle.set(params['parentTaskTitle']);
        } else {
          this.http
            .get<any>(`/api/v1/tasks/${params['parentTaskId']}`)
            .pipe(takeUntilDestroyed(this.destroy))
            .subscribe({
              next: (t) => {
                this.parentTaskTitle.set(t.task?.title || t.title || 'Nhiệm vụ cha');
              },
            });
        }
      }
      if (params['type'] === 'PROJECT') {
        this.assignmentType.set('PROJECT');
      } else if (params['type'] === 'INDEPENDENT') {
        this.assignmentType.set('INDEPENDENT');
      }
      if (params['aiTitle'] || params['title']) {
        this.form.patchValue({
          title: params['aiTitle'] || params['title'] || '',
          description: params['aiDescription'] || params['description'] || '',
          priority: params['aiPriority'] || params['priority'] || 'MEDIUM',
          checklist: params['aiChecklist'] || params['checklist'] || '',
        });
      }
    });

    const hasActiveIdentity =
      typeof (this.session as any).identity === 'function' && !!(this.session as any).identity();
    if (this.canAssignDirectly() && hasActiveIdentity) {
      this.loadAssignableRecipients();
    }
  }

  loadAssignableRecipients(): void {
    this.loadingRecipients.set(true);
    this.http
      .get<AssignableRecipientsView>('/api/v1/tasks/assignable-recipients')
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (data) => {
          this.loadingRecipients.set(false);
          if (data) {
            data.allEmployees = (data.allEmployees || []).map((e: any) => ({
              ...e,
              id: e.id || e.userId,
            }));
            data.departments = (data.departments || []).map((d: any) => ({
              ...d,
              id: d.id || d.departmentId,
              managers: (d.managers || []).map((m: any) => ({ ...m, id: m.id || m.userId })),
              employees: (d.employees || []).map((e: any) => ({ ...e, id: e.id || e.userId })),
            }));
          }
          this.assignableData.set(data);

          // If Project mode and no packages yet, automatically prepare the first active departments
          if (this.assignmentType() === 'PROJECT' && this.selectedProjectPackages().length === 0) {
            this.initDefaultProjectPackages(data.departments);
          }
        },
        error: () => {
          this.loadingRecipients.set(false);
          this.toast.error('Không thể tải danh sách phòng ban và nhân sự để phân công.');
        },
      });
  }

  setAssignmentType(type: 'INDEPENDENT' | 'PROJECT'): void {
    this.assignmentType.set(type);
    this.error.set('');
    if (type === 'PROJECT' && this.selectedProjectPackages().length === 0) {
      const depts = this.assignableData()?.departments;
      if (depts?.length) {
        this.initDefaultProjectPackages(depts);
      }
    }
  }

  private initDefaultProjectPackages(departments: AssignableDepartment[]): void {
    if (!departments || departments.length === 0) return;
    // Pre-select up to 2 departments to guide administrator
    const initialDepts = departments.slice(0, Math.min(2, departments.length));
    const currentUserId =
      typeof (this.session as any).identity === 'function'
        ? (this.session as any).identity()?.userId || ''
        : '';
    const packages: SelectedProjectDepartmentPackage[] = initialDepts.map((d) => {
      const defaultMgr = d.managers[0]?.id || currentUserId;
      return {
        departmentId: d.id,
        departmentName: d.name,
        departmentCode: d.code,
        managerId: defaultMgr,
        availableManagers: d.managers,
        packageName: `[Gói việc] ${this.form.controls.title.value.trim() || 'Dự án'} — ${d.name}`,
        scopeDescription: '',
        deadline: this.form.controls.deadline.value || '',
        weight: 50.0,
      };
    });
    this.selectedProjectPackages.set(packages);
    this.distributeWeightsEqually();
  }

  // --- Independent Task Multi-Employee Picker Actions ---
  isEmployeeSelected(userId?: string): boolean {
    if (!userId) return false;
    return this.selectedRecipients().some((r) => r.userId === userId);
  }

  toggleEmployee(emp: AssignableEmployee): void {
    const empId = emp.id || (emp as any).userId;
    if (!empId) return;
    const current = this.selectedRecipients();
    const existingIndex = current.findIndex((r) => r.userId === empId);

    if (existingIndex >= 0) {
      // Remove
      const wasPrimary = current[existingIndex].isPrimary;
      const updated = current.filter((r) => r.userId !== empId);
      if (wasPrimary && updated.length > 0) {
        updated[0].isPrimary = true;
      }
      this.selectedRecipients.set([...updated]);
      this.distributeIndependentWeightsEqually();
    } else {
      // Add
      const isFirst = current.length === 0;
      const newRecipient: SelectedIndependentRecipient = {
        userId: empId,
        fullName: emp.fullName,
        employeeCode: emp.employeeCode,
        departmentName: emp.departmentName || 'Chưa xếp phòng ban',
        isPrimary: isFirst,
        responsibility: '',
        deadline: this.form.controls.deadline.value || '',
        weight: 100,
      };
      this.selectedRecipients.set([...current, newRecipient]);
      this.distributeIndependentWeightsEqually();
    }
  }

  setPrimaryRecipient(userId: string): void {
    const updated = this.selectedRecipients().map((r) => ({
      ...r,
      isPrimary: r.userId === userId,
    }));
    this.selectedRecipients.set(updated);
  }

  removeRecipient(userId: string): void {
    const current = this.selectedRecipients();
    const existingIndex = current.findIndex((r) => r.userId === userId);
    if (existingIndex < 0) return;
    const wasPrimary = current[existingIndex].isPrimary;
    const updated = current.filter((r) => r.userId !== userId);
    if (wasPrimary && updated.length > 0) {
      updated[0].isPrimary = true;
    }
    this.selectedRecipients.set(updated);
    this.distributeIndependentWeightsEqually();
  }

  distributeIndependentWeightsEqually(): void {
    const list = this.selectedRecipients();
    if (list.length === 0) return;
    const equalWeight = Math.round((100 / list.length) * 10) / 10;
    const updated = list.map((r, idx) => ({
      ...r,
      weight:
        idx === list.length - 1
          ? Math.round((100 - equalWeight * (list.length - 1)) * 10) / 10
          : equalWeight,
    }));
    this.selectedRecipients.set(updated);
  }

  updateRecipientResponsibility(userId: string, responsibility: string): void {
    const updated = this.selectedRecipients().map((r) =>
      r.userId === userId ? { ...r, responsibility } : r,
    );
    this.selectedRecipients.set(updated);
  }

  updateRecipientDeadline(userId: string, deadline: string): void {
    const updated = this.selectedRecipients().map((r) =>
      r.userId === userId ? { ...r, deadline } : r,
    );
    this.selectedRecipients.set(updated);
  }

  updateRecipientWeight(userId: string, rawWeight: number): void {
    const weight = Number(rawWeight) || 0;
    const updated = this.selectedRecipients().map((r) =>
      r.userId === userId ? { ...r, weight } : r,
    );
    this.selectedRecipients.set(updated);
  }

  // --- Project Multi-Department Packages Actions ---
  addDepartmentPackage(deptId: string): void {
    if (!deptId) return;
    const dept = this.assignableData()?.departments.find((d) => d.id === deptId);
    if (!dept) return;

    if (this.selectedProjectPackages().some((p) => p.departmentId === deptId)) {
      this.toast.warning('Phòng ban này đã được thêm vào dự án.');
      return;
    }

    const currentUserId =
      typeof (this.session as any).identity === 'function'
        ? (this.session as any).identity()?.userId || ''
        : '';
    const defaultMgr = dept.managers[0]?.id || currentUserId;
    const newPkg: SelectedProjectDepartmentPackage = {
      departmentId: dept.id,
      departmentName: dept.name,
      departmentCode: dept.code,
      managerId: defaultMgr,
      availableManagers: dept.managers,
      packageName: `[Gói việc] ${this.form.controls.title.value.trim() || 'Dự án'} — ${dept.name}`,
      scopeDescription: '',
      deadline: this.form.controls.deadline.value || '',
      weight: 0,
    };

    this.selectedProjectPackages.set([...this.selectedProjectPackages(), newPkg]);
    this.distributeWeightsEqually();
    this.projectDeptToAdd.set('');
  }

  removeDepartmentPackage(deptId: string): void {
    const updated = this.selectedProjectPackages().filter((p) => p.departmentId !== deptId);
    this.selectedProjectPackages.set(updated);
    this.distributeWeightsEqually();
  }

  distributeWeightsEqually(): void {
    const pkgs = this.selectedProjectPackages();
    if (pkgs.length === 0) return;
    const equalWeight = Math.round((100 / pkgs.length) * 10) / 10;
    const updated = pkgs.map((p, idx) => ({
      ...p,
      // Ensure the sum adds up to exactly 100
      weight:
        idx === pkgs.length - 1
          ? Math.round((100 - equalWeight * (pkgs.length - 1)) * 10) / 10
          : equalWeight,
    }));
    this.selectedProjectPackages.set(updated);
  }

  updatePackageManager(deptId: string, managerId: string): void {
    const updated = this.selectedProjectPackages().map((p) =>
      p.departmentId === deptId ? { ...p, managerId } : p,
    );
    this.selectedProjectPackages.set(updated);
  }

  updatePackageName(deptId: string, packageName: string): void {
    const updated = this.selectedProjectPackages().map((p) =>
      p.departmentId === deptId ? { ...p, packageName } : p,
    );
    this.selectedProjectPackages.set(updated);
  }

  updatePackageScope(deptId: string, scopeDescription: string): void {
    const updated = this.selectedProjectPackages().map((p) =>
      p.departmentId === deptId ? { ...p, scopeDescription } : p,
    );
    this.selectedProjectPackages.set(updated);
  }

  updatePackageDeadline(deptId: string, deadline: string): void {
    const updated = this.selectedProjectPackages().map((p) =>
      p.departmentId === deptId ? { ...p, deadline } : p,
    );
    this.selectedProjectPackages.set(updated);
  }

  updatePackageWeight(deptId: string, rawWeight: number): void {
    const weight = Number(rawWeight) || 0;
    const updated = this.selectedProjectPackages().map((p) =>
      p.departmentId === deptId ? { ...p, weight } : p,
    );
    this.selectedProjectPackages.set(updated);
  }

  // --- AI Assistance ---
  assistWithAi() {
    const prompt = this.aiPrompt().trim();
    if (!prompt || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.aiMessage.set('');
    this.http
      .post<any>('/api/v1/ai/task-assistance', { prompt })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (draft) => {
          this.aiBusy.set(false);
          if (draft) {
            this.form.patchValue({
              title: draft.title || '',
              description: draft.description || '',
              priority: draft.priority || 'MEDIUM',
              checklist: (draft.checklistItems || []).join('\n'),
            });
            this.aiMessage.set(
              `AI đã tạo bản thảo (Độ tin cậy: ${(draft.confidence * 100).toFixed(0)}%): ${draft.reasoning}`,
            );
          }
        },
        error: () => {
          this.aiBusy.set(false);
          this.aiMessage.set('Không thể kết nối trợ lý AI. Vui lòng thử lại.');
        },
      });
  }

  breakdownWithAi() {
    const title = this.form.controls.title.value.trim();
    if (!title || this.aiBusy()) return;
    this.aiBusy.set(true);
    this.http
      .post<any>('/api/v1/ai/task-breakdown', {
        title,
        description: this.form.controls.description.value,
        targetSubtaskCount: 4,
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.aiBusy.set(false);
          if (res?.subtasks?.length) {
            const checklistText = res.subtasks.map((s: any) => s.title).join('\n');
            this.form.patchValue({ checklist: checklistText });
            this.aiMessage.set(
              `AI đã gợi ý phân rã thành ${res.subtasks.length} hạng mục checklist.`,
            );
          }
        },
        error: () => {
          this.aiBusy.set(false);
          this.aiMessage.set('Không thể phân rã công việc qua AI.');
        },
      });
  }

  // --- Submission Handler ---
  submit() {
    if (this.done() || this.busy() || !this.session.hasPermission('tasks.create')) return;
    this.error.set('');

    if (this.uncertain() && this.submitted) {
      this.sendStandardTaskRequest();
      return;
    }

    const value = this.form.getRawValue();
    const title = value.title.trim();
    if (!title) {
      this.error.set('Vui lòng nhập tiêu đề công việc / dự án.');
      return;
    }

    const rawDeadline = value.deadline.trim();
    let deadline: Date | null = null;
    if (rawDeadline) {
      deadline = new Date(rawDeadline);
      if (Number.isNaN(deadline.getTime())) {
        this.error.set('Hạn chót không hợp lệ. Vui lòng nhập định dạng ngày giờ chuẩn.');
        return;
      }
      if (deadline.getTime() <= Date.now()) {
        this.error.set('Hạn chót phải ở trong tương lai.');
        return;
      }
    }

    const checklist = value.checklist
      .split('\n')
      .map((v: string) => v.trim())
      .filter(Boolean);

    // Project flow (when PROJECT mode is selected at root level)
    if (this.canAssignDirectly() && !this.parentTaskId() && this.assignmentType() === 'PROJECT') {
      this.submitProject(title, value.description, value.priority, deadline, checklist);
      return;
    }

    // Independent Task or Subtask WBS Direct Assignment flow (when recipients are selected)
    if (this.canAssignDirectly() && this.selectedRecipients().length > 0) {
      this.submitIndependentTask(title, value.description, value.priority, deadline, checklist);
      return;
    }

    // Top-level Independent Task when recipient directory is loaded requires at least 1 recipient
    if (
      this.canAssignDirectly() &&
      !this.parentTaskId() &&
      this.assignmentType() === 'INDEPENDENT' &&
      this.assignableData() !== null
    ) {
      this.error.set('Vui lòng chọn ít nhất một nhân sự để phân công nhiệm vụ độc lập.');
      return;
    }

    // Default / Draft Task / Unassigned Subtask creation flow
    this.submitStandardTask(value, deadline, checklist);
  }

  private submitIndependentTask(
    title: string,
    description: string,
    priority: string,
    deadline: Date | null,
    checklist: string[],
  ): void {
    const recipients = this.selectedRecipients();
    if (recipients.length === 0) {
      this.error.set('Vui lòng chọn ít nhất một nhân sự để phân công nhiệm vụ.');
      return;
    }

    this.busy.set(true);
    const payload = {
      title,
      description: description.trim() || null,
      priority,
      deadline: deadline?.toISOString() ?? null,
      checklist,
      ...(this.parentTaskId() ? { parentTaskId: this.parentTaskId() } : {}),
      ...(this.requestId() ? { requestId: this.requestId() } : {}),
      recipients: recipients.map((r) => ({
        userId: r.userId,
        responsibility: r.responsibility.trim() || null,
        deadline: r.deadline ? new Date(r.deadline).toISOString() : deadline?.toISOString() ?? null,
        isPrimary: r.isPrimary,
        weight: Number(r.weight) || 0,
      })),
    };

    this.http
      .post<{ taskId: string; message: string }>('/api/v1/tasks/assign-independent', payload, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.done.set(true);
          this.toast.success(
            result.message || 'Đã phân công nhiệm vụ thành công cho các nhân sự!',
          );
          void this.router.navigate(['/tasks', result.taskId]);
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(
            err.error?.message ||
              'Lỗi khi phân công nhiệm vụ. Vui lòng kiểm tra lại thông tin.',
          );
        },
      });
  }

  private submitProject(
    title: string,
    description: string,
    priority: string,
    deadline: Date | null,
    checklist: string[],
  ): void {
    const packages = this.selectedProjectPackages();
    if (packages.length === 0) {
      this.error.set('Vui lòng chọn ít nhất một phòng ban để khởi tạo dự án.');
      return;
    }

    // Validate every department has a designated manager
    for (const pkg of packages) {
      if (!pkg.managerId) {
        this.error.set(
          `Vui lòng chọn Trưởng phòng phụ trách cho phòng ban "${pkg.departmentName}".`,
        );
        return;
      }
    }

    this.busy.set(true);
    const payload = {
      title,
      description: description.trim() || null,
      priority,
      deadline: deadline?.toISOString() ?? null,
      checklist,
      departmentPackages: packages.map((pkg) => ({
        departmentId: pkg.departmentId,
        managerId: pkg.managerId,
        packageName: pkg.packageName.trim() || `[Gói việc] ${title} — ${pkg.departmentName}`,
        scopeDescription: pkg.scopeDescription.trim() || null,
        deadline: pkg.deadline
          ? new Date(pkg.deadline).toISOString()
          : deadline?.toISOString() ?? null,
        weight: Number(pkg.weight) || 0,
      })),
    };

    this.http
      .post<{ taskId: string; message: string }>('/api/v1/tasks/assign-project', payload, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.done.set(true);
          this.toast.success(
            result.message || 'Đã khởi tạo và phân công dự án liên phòng ban thành công!',
          );
          void this.router.navigate(['/tasks', result.taskId]);
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(
            err.error?.message ||
            'Lỗi khi khởi tạo dự án liên phòng ban. Vui lòng kiểm tra lại thông tin.',
          );
        },
      });
  }

  private submitStandardTask(value: any, deadline: Date | null, checklist: string[]): void {
    this.submitted = {
      title: value.title.trim(),
      description: value.description.trim() ? value.description.trim() : null,
      priority: value.priority,
      deadline: deadline?.toISOString() ?? null,
      checklist,
      ...(this.requestId() ? { requestId: this.requestId() } : {}),
      ...(this.parentTaskId() ? { parentTaskId: this.parentTaskId() } : {}),
    };

    this.sendStandardTaskRequest();
  }

  private sendStandardTaskRequest(): void {
    this.busy.set(true);
    this.http
      .post<{ taskId: string }>('/api/v1/tasks', this.submitted, {
        headers: { 'Idempotency-Key': this.key },
      })
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.uncertain.set(false);
          this.done.set(true);
          this.toast.success('Đã khởi tạo nhiệm vụ thành công!');
          void this.router.navigate(['/tasks', result.taskId]);
        },
        error: (err: HttpErrorResponse) => {
          this.busy.set(false);
          if (err.status === 0 || err.status >= 500) {
            this.uncertain.set(true);
            this.form.disable();
            this.error.set(
              'Kết quả chưa xác định do gián đoạn kết nối. Nhấn gửi lại để thử lại an toàn với cùng mã định danh.',
            );
            return;
          }
          this.error.set(err.error?.message || 'Không thể tạo nhiệm vụ. Vui lòng kiểm tra lại.');
        },
      });
  }
}
