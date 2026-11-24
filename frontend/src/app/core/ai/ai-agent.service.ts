import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal, effect } from '@angular/core';
import { SessionService } from '../auth/session';
import { I18nService } from '../i18n/i18n.service';

export interface AgentQuestionOption {
  id: string;
  label: string;
  isDefault?: boolean;
}

export interface AgentQuestion {
  id: string;
  title: string;
  type: 'single_choice' | 'multiple_choice' | 'text';
  options?: AgentQuestionOption[];
  required?: boolean;
  placeholder?: string;
  helpText?: string;
  allowCustomInput?: boolean;
}

export interface AgentClarificationForm {
  formId: string;
  topic: string;
  contextExplanation: string;
  riskAnalysis?: string;
  questions: AgentQuestion[];
  submitButtonText?: string;
  skipButtonText?: string;
}

export interface AgentAnswer {
  questionId: string;
  selectedOptionIds?: string[];
  customText?: string;
}

export interface ChatMessage {
  id: string;
  sender: 'user' | 'robot';
  text: string;
  time: string;
  actionCards?: Array<{
    actionType: string;
    title: string;
    linkUrl?: string;
    payload?: any;
  }>;
  questionnaire?: AgentClarificationForm;
  questionnaireAnswered?: boolean;
  userAnswersSummary?: string;
  agentThought?: string;
  isThoughtExpanded?: boolean;
}

export interface SubtaskBranch {
  id: string;
  branchIndex: number;
  title: string;
  description: string;
  type: 'Assignment' | 'SupportRequest';
  departmentId?: string | null;
  departmentName: string;
  priority: 'LOW' | 'MEDIUM' | 'HIGH';
  suggestedDurationDays: number;
  checklistItems: string[];
  reasoning?: string;
  status: 'pending' | 'submitting' | 'created';
}

export interface AiChatSession {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
  messages: ChatMessage[];
  branches?: SubtaskBranch[];
}

@Injectable({
  providedIn: 'root',
})
export class AiAgentService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly i18n = inject(I18nService);

  private readonly CURRENT_STORAGE_KEY = 'bizflow_ai_current_session';
  private readonly HISTORY_STORAGE_KEY = 'bizflow_ai_chat_history';

  // Workspace display state
  readonly isOpen = signal(false);
  readonly isExpanded = signal(false);
  readonly activeTab = signal<'chat' | 'branches' | 'history'>('chat');

  // Dedicated Branching Workspace modal state (rendered in center screen next to chat)
  readonly isBranchModalOpen = signal(false);

  // Branching proposals state
  readonly branches = signal<SubtaskBranch[]>([]);
  readonly selectedBranch = signal<SubtaskBranch | null>(null);

  // Chat conversation state
  readonly currentSessionId = signal<string>(crypto.randomUUID());
  readonly messages = signal<ChatMessage[]>([]);
  readonly busy = signal<boolean>(false);
  readonly branchError = signal<string | null>(null);

  // Session history list
  readonly chatSessions = signal<AiChatSession[]>([]);

  // Department metadata for matching
  readonly departments = signal<Array<{ id: string; name: string }>>([]);

  // Toast feedback
  readonly toastMessage = signal<{ text: string; type: 'success' | 'error' | 'info' } | null>(null);

  private currentUserScope = this.identityScope();

  constructor() {
    this.restoreForCurrentUser();

    // Watch session identity changes (login, logout, switch account)
    try {
      effect(() => {
        const newScope = this.identityScope();
        if (newScope !== this.currentUserScope) {
          this.persistCurrentSession();
          this.currentUserScope = newScope;
          this.restoreForCurrentUser();
        }
      });
    } catch {
      // In case effect is outside reactive context in certain test fixtures
    }
  }

  private identityScope(): string {
    const identity = typeof this.session?.identity === 'function' ? this.session.identity() : null;
    return identity?.userId ? `${identity.tenantId ?? 'platform'}_${identity.userId}` : 'guest';
  }

  private getStorageKey(baseKey: string): string {
    return `${baseKey}_${this.currentUserScope}`;
  }

  private restoreForCurrentUser(): void {
    this.branchError.set(null);
    this.currentSessionId.set(crypto.randomUUID());
    this.branches.set([]);
    this.selectedBranch.set(null);
    this.departments.set([]);
    this.busy.set(false);
    this.restoreHistoryFromStorage();
    this.restoreCurrentSessionFromStorage();
  }

  loadDepartments(force = false): void {
    if (!force && this.departments().length > 0) return;
    const scope = this.currentUserScope;
    // fetch departments
    this.http.get<any>('/api/v1/departments?pageSize=100').subscribe({
      next: (res) => {
        if (scope !== this.currentUserScope) return;
        const list = Array.isArray(res) ? res : res?.items || [];
        this.departments.set(list.map((d: any) => ({ id: d.departmentId || d.id, name: d.name })));

        // Re-match existing branches if departmentId was missing
        const currentBranches = this.branches();
        if (currentBranches.length > 0 && list.length > 0) {
          const updated = currentBranches.map((b) => {
            if (!b.departmentId && b.departmentName) {
              const match = this.departments().find(
                (d) =>
                  d.name.toLowerCase().includes(b.departmentName.toLowerCase()) ||
                  b.departmentName.toLowerCase().includes(d.name.toLowerCase()),
              );
              if (match) {
                return { ...b, departmentId: match.id, departmentName: match.name };
              }
            }
            return b;
          });
          this.branches.set(updated);
        }
      },
      error: () => {},
    });
  }

  open(tab: 'chat' | 'branches' | 'history' = 'chat', expand?: boolean): void {
    if (tab === 'branches') {
      this.loadDepartments();
      this.openBranchModal();
    }
    this.isOpen.set(true);
    this.activeTab.set(tab);
    if (expand !== undefined) {
      this.isExpanded.set(expand);
    }
    try {
      sessionStorage.setItem('bizflow_robot_open', 'true');
      sessionStorage.removeItem('bizflow_robot_dismissed');
    } catch {}
  }

  close(): void {
    this.isOpen.set(false);
    try {
      sessionStorage.removeItem('bizflow_robot_open');
      sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    } catch {}
  }

  toggleExpand(): void {
    this.isExpanded.update((v) => !v);
  }

  setTab(tab: 'chat' | 'branches' | 'history'): void {
    if (tab === 'branches') {
      this.openBranchModal();
    }
    this.activeTab.set(tab);
  }

  openBranchModal(): void {
    this.branchError.set(null);
    this.selectedBranch.set(null);
    this.isBranchModalOpen.set(true);
  }

  closeBranchModal(): void {
    this.isBranchModalOpen.set(false);
  }

  addCustomBranch(title?: string): void {
    const list = [...this.branches()];
    const newIdx = list.length + 1;
    const newBranch: SubtaskBranch = {
      id: crypto.randomUUID(),
      branchIndex: newIdx,
      title: title || '',
      description: '',
      type: 'Assignment',
      priority: 'MEDIUM',
      departmentId: null,
      departmentName: '',
      suggestedDurationDays: 2,
      checklistItems: [],
      status: 'pending',
    };
    list.push(newBranch);
    this.branches.set(list);
    this.branchError.set(null);
    this.selectedBranch.set(newBranch);
    this.isBranchModalOpen.set(true);
    this.persistCurrentSession();
  }

  selectBranch(branch: SubtaskBranch | null): void {
    this.selectedBranch.set(branch);
  }

  showToast(text: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.toastMessage.set({ text, type });
    setTimeout(() => {
      if (this.toastMessage()?.text === text) {
        this.toastMessage.set(null);
      }
    }, 4000);
  }

  // --- Session Storage & Management ---

  private restoreHistoryFromStorage(): void {
    try {
      const key = this.getStorageKey(this.HISTORY_STORAGE_KEY);
      const raw = localStorage.getItem(key);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
          this.chatSessions.set(parsed);
          return;
        }
      }
      this.chatSessions.set([]);
    } catch {
      this.chatSessions.set([]);
    }
  }

  private persistHistoryToStorage(): void {
    try {
      const key = this.getStorageKey(this.HISTORY_STORAGE_KEY);
      localStorage.setItem(key, JSON.stringify(this.chatSessions()));
    } catch {}
  }

  private restoreCurrentSessionFromStorage(): void {
    try {
      const key = this.getStorageKey(this.CURRENT_STORAGE_KEY);
      const raw = localStorage.getItem(key);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed?.messages?.length) {
          this.currentSessionId.set(parsed.id || crypto.randomUUID());
          this.messages.set(parsed.messages);
          if (Array.isArray(parsed.branches)) {
            this.branches.set(parsed.branches);
          }
          return;
        }
      }
    } catch {}

    // Initial default welcome message
    this.initWelcomeMessage();
  }

  private initWelcomeMessage(): void {
    const identity = typeof this.session?.identity === 'function' ? this.session.identity() : null;
    const userName = identity?.fullName;
    const greeting = userName ? `Xin chào ${userName}! ` : 'Xin chào! ';
    this.messages.set([
      {
        id: 'welcome',
        sender: 'robot',
        text: `${greeting}Tôi là AI Agent cá nhân của bạn. Hãy chia sẻ mục tiêu, trở ngại hoặc cách làm việc bạn muốn tôi ghi nhớ. Tôi có thể tra cứu công việc theo quyền của bạn, đề xuất kế hoạch và chuẩn bị hành động để bạn xác nhận.`,
        time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false }),
      },
    ]);
  }

  persistCurrentSession(): void {
    try {
      const msgs = this.messages();
      if (!msgs.length) return;
      const firstUserMsg = msgs.find((m) => m.sender === 'user');
      const title = firstUserMsg ? firstUserMsg.text.slice(0, 50) : 'Cuộc trò chuyện mới';

      const sessionData: AiChatSession = {
        id: this.currentSessionId(),
        title,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        messageCount: msgs.length,
        messages: msgs,
        branches: this.branches(),
      };

      const key = this.getStorageKey(this.CURRENT_STORAGE_KEY);
      localStorage.setItem(key, JSON.stringify(sessionData));

      // Also update or add in history list if user interacted
      if (firstUserMsg) {
        const sessions = [...this.chatSessions()];
        const idx = sessions.findIndex((s) => s.id === sessionData.id);
        if (idx >= 0) {
          sessions[idx] = sessionData;
        } else {
          sessions.unshift(sessionData);
        }
        this.chatSessions.set(sessions);
        this.persistHistoryToStorage();
      }
    } catch {}
  }

  startNewChat(saveCurrent: boolean = true): void {
    if (saveCurrent) {
      this.persistCurrentSession();
    }
    this.currentSessionId.set(crypto.randomUUID());
    this.branches.set([]);
    this.selectedBranch.set(null);
    this.initWelcomeMessage();
    this.activeTab.set('chat');
    try {
      const key = this.getStorageKey(this.CURRENT_STORAGE_KEY);
      localStorage.removeItem(key);
    } catch {}
    this.showToast('Đã bắt đầu cuộc trò chuyện mới', 'info');
  }

  loadSession(sessionId: string): void {
    const found = this.chatSessions().find((s) => s.id === sessionId);
    if (!found) return;

    this.persistCurrentSession();
    this.currentSessionId.set(found.id);
    this.messages.set(found.messages);
    this.branches.set(found.branches || []);
    this.selectedBranch.set(null);
    this.activeTab.set('chat');
    this.persistCurrentSession();
    this.showToast(`Đã mở lại cuộc trò chuyện: "${found.title}"`, 'info');
  }

  deleteSession(sessionId: string): void {
    const filtered = this.chatSessions().filter((s) => s.id !== sessionId);
    this.chatSessions.set(filtered);
    this.persistHistoryToStorage();
    if (this.currentSessionId() === sessionId) {
      this.startNewChat(false);
    }
    this.showToast('Đã xóa đoạn trò chuyện khỏi lịch sử', 'info');
  }

  clearAllHistory(): void {
    this.chatSessions.set([]);
    this.persistHistoryToStorage();
    this.startNewChat(false);
    this.showToast('Đã xóa toàn bộ lịch sử trò chuyện', 'info');
  }

  // --- In-Place Natural Language Modification of Current Branching View ---

  private addRobotFeedback(text: string, withActionCard: boolean = true): void {
    const msg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'robot',
      text,
      time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false }),
      actionCards: withActionCard
        ? [
            {
              actionType: 'VIEW_BRANCHES',
              title: `🌿 Xem các nhánh phân rã (${this.branches().length} nhánh)`,
            },
          ]
        : undefined,
    };
    this.messages.update((msgs) => [...msgs, msg]);
  }

  // --- Decompose and Load Proposals into Branches ---

  setProposals(proposals: any[]): void {
    if (!Array.isArray(proposals) || proposals.length === 0) return;
    const canCreateTasks = this.session.hasPermission('tasks.create');

    const mapped: SubtaskBranch[] = proposals.map((p, idx) => {
      const title = p.title || p.Title || `Nhiệm vụ #${idx + 1}`;
      const description = p.description || p.Description || title;
      const rawType = p.type || p.Type;
      const type = !canCreateTasks
        ? 'SupportRequest'
        : rawType === 'SupportRequest'
          ? 'SupportRequest'
          : 'Assignment';
      const deptName = p.departmentName || p.DepartmentName || 'Phòng ban chuyên trách';
      let deptId = p.departmentId || p.DepartmentId || null;
      if (!deptId && deptName) {
        const match = this.departments().find(
          (d) =>
            d.name.toLowerCase().includes(deptName.toLowerCase()) ||
            deptName.toLowerCase().includes(d.name.toLowerCase()),
        );
        if (match) deptId = match.id;
      }
      const rawPriority = p.priority || p.Priority;
      const priority = rawPriority === 'HIGH' || rawPriority === 'LOW' ? rawPriority : 'MEDIUM';
      const suggestedDurationDays = p.suggestedDurationDays || p.SuggestedDurationDays || 3;
      const rawChecklist = p.checklistItems || p.ChecklistItems;
      const checklistItems =
        Array.isArray(rawChecklist) && rawChecklist.length
          ? rawChecklist
          : [
              `Bước 1: Chuẩn bị nội dung và kế hoạch`,
              `Bước 2: Thực hiện công việc`,
              `Bước 3: Hoàn tất và báo cáo`,
            ];
      const reasoning =
        p.reasoning ||
        p.Reasoning ||
        'Được bóc tách tự động bởi AI Agent dựa trên mục tiêu tổng thể.';

      return {
        id: crypto.randomUUID(),
        branchIndex: idx + 1,
        title,
        description,
        type,
        departmentId: deptId,
        departmentName: deptName,
        priority,
        suggestedDurationDays,
        checklistItems,
        reasoning,
        status: 'pending',
      };
    });

    this.branches.set(mapped);
    this.selectedBranch.set(null);
    this.persistCurrentSession();
  }

  updateBranch(branchId: string, updates: Partial<SubtaskBranch>): void {
    const list = [...this.branches()];
    const idx = list.findIndex((b) => b.id === branchId);
    if (idx >= 0) {
      const updated: SubtaskBranch = { ...list[idx], ...updates };
      if (updates.departmentId !== undefined) {
        const found = this.departments().find((d) => d.id === updates.departmentId);
        if (found) {
          updated.departmentName = found.name;
        }
      } else if (updates.departmentName?.trim() && !updates.departmentId) {
        const match = this.departments().find(
          (d) =>
            d.name.toLowerCase().includes(updates.departmentName!.toLowerCase()) ||
            updates.departmentName!.toLowerCase().includes(d.name.toLowerCase()),
        );
        if (match) {
          updated.departmentId = match.id;
        }
      }
      list[idx] = updated;
      this.branches.set(list);
      if (this.selectedBranch()?.id === branchId) {
        this.selectedBranch.set(updated);
      }
      this.persistCurrentSession();
    }
  }

  deleteBranch(branchIndex: number): void {
    const list = [...this.branches()];
    if (branchIndex >= 0 && branchIndex < list.length) {
      const removed = list.splice(branchIndex, 1)[0];
      list.forEach((b, i) => (b.branchIndex = i + 1));
      this.branches.set(list);
      if (this.selectedBranch()?.id === removed.id) {
        this.selectedBranch.set(null);
      }
      this.persistCurrentSession();
      this.showToast(`Đã xóa nhánh #${branchIndex + 1}: "${removed.title}"`, 'info');
    }
  }

  // --- Submitting Single Branch or All Branches ---

  branchConfigurationError(branch: SubtaskBranch): string | null {
    if (!branch.title.trim()) return 'Nhập tiêu đề nhiệm vụ trước khi tạo.';
    if (!branch.departmentId && !branch.departmentName.trim())
      return 'Chọn phòng ban nhận việc trước khi tạo.';
    return null;
  }

  private reviewBranchConfiguration(branch: SubtaskBranch): boolean {
    const error = this.branchConfigurationError(branch);
    if (!error) return true;
    this.selectedBranch.set(branch);
    this.branchError.set(error);
    return false;
  }

  submitSingleBranch(branch: SubtaskBranch, force: boolean = false): void {
    const current = this.branches().find((item) => item.id === branch.id);
    if (
      this.busy() ||
      current?.status === 'submitting' ||
      (current?.status === 'created' && !force)
    )
      return;
    if (!this.session.hasPermission('tasks.create')) {
      this.showToast(
        'Tài khoản Nhân viên không thể tạo nhiệm vụ cho cấp dưới. Vui lòng tạo Yêu cầu nếu cần đề xuất công việc.',
        'error',
      );
      return;
    }

    branch = current ?? branch;
    if (!this.reviewBranchConfiguration(branch)) return;
    this.branchError.set(null);
    this.busy.set(true);
    // Optimistic UI status
    const currentList = this.branches();
    const idx = currentList.findIndex((b) => b.id === branch.id);
    if (idx >= 0) {
      const updated = [...currentList];
      updated[idx] = { ...updated[idx], status: 'submitting' };
      this.branches.set(updated);
      this.selectedBranch.set(updated[idx]);
    }

    const payload = {
      items: [
        {
          title: branch.title.trim(),
          description: branch.description?.trim() || branch.title.trim(),
          type: branch.type,
          departmentId: branch.departmentId || null,
          departmentName: branch.departmentName || null,
          suggestedDurationDays: branch.suggestedDurationDays || null,
          priority: branch.priority,
          checklistItems: branch.checklistItems?.length ? branch.checklistItems : null,
        },
      ],
    };

    this.http.post<any>('/api/v1/tasks/ai-decomposition/confirm', payload).subscribe({
      next: () => {
        this.busy.set(false);
        const list = [...this.branches()];
        const bIdx = list.findIndex((b) => b.id === branch.id);
        if (bIdx >= 0) {
          list[bIdx] = { ...list[bIdx], status: 'created' };
          this.branches.set(list);
          this.selectedBranch.set(list[bIdx]);
        }
        this.showToast(`✅ Đã tạo nhiệm vụ "${branch.title}" thành công!`, 'success');
        this.addRobotFeedback(
          `✅ Đã tạo thành công nhiệm vụ nhánh: **"${branch.title}"** [${branch.departmentName}] vào hệ thống!`,
          false,
        );
        this.persistCurrentSession();
      },
      error: (err) => {
        this.busy.set(false);
        const list = [...this.branches()];
        const bIdx = list.findIndex((b) => b.id === branch.id);
        if (bIdx >= 0) {
          list[bIdx] = { ...list[bIdx], status: 'pending' };
          this.branches.set(list);
          this.selectedBranch.set(list[bIdx]);
        }
        const errMsg = this.branchCreationError(err);
        this.branchError.set(errMsg);
        this.showToast(`❌ Lỗi tạo nhiệm vụ: ${errMsg}`, 'error');
      },
    });
  }

  submitAllBranches(): void {
    if (this.busy()) return;
    if (!this.session.hasPermission('tasks.create')) {
      this.showToast(
        'Tài khoản Nhân viên không thể tạo nhiệm vụ cho cấp dưới. Vui lòng tạo Yêu cầu nếu cần đề xuất công việc.',
        'error',
      );
      return;
    }
    const pendingBranches = this.branches().filter((b) => b.status === 'pending');
    if (pendingBranches.length === 0) {
      this.showToast('Chưa có nhánh nhiệm vụ nào để gửi vào hệ thống.', 'info');
      return;
    }

    for (const branch of pendingBranches) {
      if (!this.reviewBranchConfiguration(branch)) return;
    }
    this.branchError.set(null);
    this.busy.set(true);

    const payload = {
      items: pendingBranches.map((b) => ({
        title: b.title.trim(),
        description: b.description?.trim() || b.title.trim(),
        type: b.type,
        departmentId: b.departmentId || null,
        departmentName: b.departmentName || null,
        suggestedDurationDays: b.suggestedDurationDays || null,
        priority: b.priority,
        checklistItems: b.checklistItems?.length ? b.checklistItems : null,
      })),
    };

    this.http.post<any>('/api/v1/tasks/ai-decomposition/confirm', payload).subscribe({
      next: (resp) => {
        this.busy.set(false);
        const count = resp?.totalCreated ?? pendingBranches.length;
        const submittedIds = new Set(pendingBranches.map((b) => b.id));
        const list = this.branches().map((b) =>
          submittedIds.has(b.id)
            ? {
                ...b,
                status: 'created' as const,
              }
            : b,
        );
        this.branches.set(list);
        if (this.selectedBranch()) {
          const updatedSelected = list.find((b) => b.id === this.selectedBranch()?.id);
          this.selectedBranch.set(updatedSelected || null);
        }
        this.showToast(`🚀 Đã tạo thành công toàn bộ ${count} nhiệm vụ vào hệ thống!`, 'success');
        this.addRobotFeedback(
          `🚀 Đã tạo hàng loạt thành công **${count} nhiệm vụ** phân nhánh vào hệ thống! Bạn có thể kiểm tra danh sách nhiệm vụ trên hệ thống.`,
          false,
        );
        this.persistCurrentSession();
      },
      error: (err) => {
        this.busy.set(false);
        const errMsg = this.branchCreationError(err);
        this.branchError.set(errMsg);
        this.showToast(`❌ Lỗi tạo hàng loạt: ${errMsg}`, 'error');
      },
    });
  }

  private branchCreationError(error: any): string {
    if (error?.status === 0)
      return 'Không kết nối được máy chủ. Các đề xuất vẫn được giữ lại để bạn thử lại.';
    if (error?.status >= 500)
      return 'Máy chủ gặp lỗi khi lưu nhiệm vụ. Các đề xuất chưa được đánh dấu là đã tạo; vui lòng thử lại.';
    return (
      error?.error?.message ||
      'Không thể tạo nhiệm vụ. Vui lòng kiểm tra nội dung, phòng ban và quyền truy cập.'
    );
  }

  sendFormAnswers(
    formId: string,
    answers: AgentAnswer[],
    summaryText: string,
    messageId: string,
  ): void {
    // 1. Mark questionnaire in the original robot message as answered
    this.messages.update((msgs) =>
      msgs.map((m) =>
        m.id === messageId
          ? { ...m, questionnaireAnswered: true, userAnswersSummary: summaryText }
          : m,
      ),
    );

    // 2. Append user message showing the selected answers
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'user',
      text: summaryText,
      time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false }),
    };
    this.messages.update((list) => [...list, userMsg]);
    this.busy.set(true);

    // Prior conversation history for multi-turn context
    const priorHistory = this.messages()
      .slice(0, -1)
      .filter((m) => m.id !== 'welcome')
      .slice(-20)
      .map((m) => ({ sender: m.sender, text: m.text }));

    const payload = {
      message: summaryText,
      locale: this.i18n.currentLanguage(),
      history: priorHistory,
      formId,
      formAnswers: answers,
      conversationId: this.currentSessionId(),
    };

    this.http
      .post<{
        response: string;
        recognizedCommand?: string;
        actionCards?: any[];
        questionnaire?: any;
        agentThought?: string;
      }>('/api/v1/ai/chat', payload)
      .subscribe({
        next: (res) => {
          if (this.currentSessionId() !== payload.conversationId) return;
          this.busy.set(false);

          let actionCards = res.actionCards;
          const decompCard = res.actionCards?.find(
            (c) =>
              c.actionType === 'OPEN_DECOMPOSITION' ||
              c.actionType === 'OPEN_BRANCHES' ||
              c.actionType === 'VIEW_BRANCHES',
          );

          if (decompCard?.payload?.proposals) {
            this.setProposals(decompCard.payload.proposals);
            actionCards = [
              {
                actionType: 'VIEW_BRANCHES',
                title: `🌿 Xem các nhánh phân rã (${decompCard.payload.proposals.length} nhánh)`,
              },
            ];
          }

          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: res.response,
            time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false }),
            actionCards,
            questionnaire: res.questionnaire,
            agentThought: res.agentThought,
            isThoughtExpanded: !!res.agentThought,
          };

          this.messages.update((list) => [...list, robotMsg]);
          this.persistCurrentSession();
        },
        error: (err) => {
          if (this.currentSessionId() !== payload.conversationId) return;
          this.busy.set(false);
          const errMsg =
            err?.error?.message || err?.message || 'Không thể gửi phản hồi. Vui lòng thử lại.';
          this.showToast(`❌ Lỗi kết nối: ${errMsg}`, 'error');
        },
      });
  }
}
