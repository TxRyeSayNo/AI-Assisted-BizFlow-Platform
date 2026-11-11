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

  // Session history list
  readonly chatSessions = signal<AiChatSession[]>([]);

  // Department metadata for matching
  readonly departments = signal<Array<{ id: string; name: string }>>([]);

  // Toast feedback
  readonly toastMessage = signal<{ text: string; type: 'success' | 'error' | 'info' } | null>(null);

  private currentUserId: string | null = null;

  constructor() {
    this.restoreForCurrentUser();

    // Watch session identity changes (login, logout, switch account)
    try {
      effect(() => {
        const identity = typeof this.session?.identity === 'function' ? this.session.identity() : null;
        const newUserId = identity?.userId || null;
        if (newUserId !== this.currentUserId) {
          if (this.currentUserId) {
            this.persistCurrentSession();
          }
          this.currentUserId = newUserId;
          this.restoreForCurrentUser();
        }
      });
    } catch {
      // In case effect is outside reactive context in certain test fixtures
    }
  }

  private getStorageKey(baseKey: string): string {
    const identity = typeof this.session?.identity === 'function' ? this.session.identity() : null;
    const userId = identity?.userId;
    return userId ? `${baseKey}_${userId}` : baseKey;
  }

  private restoreForCurrentUser(): void {
    this.restoreHistoryFromStorage();
    this.restoreCurrentSessionFromStorage();
  }

  loadDepartments(force = false): void {
    if (!force && this.departments().length > 0) return;
    // fetch departments
    this.http.get<any>('/api/v1/departments?pageSize=100').subscribe({
      next: (res) => {
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
      error: () => { },
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
    } catch { }
  }

  close(): void {
    this.isOpen.set(false);
    try {
      sessionStorage.removeItem('bizflow_robot_open');
      sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    } catch { }
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
    if (this.branches().length === 0) {
      this.initDefaultBranches();
    }
    this.selectedBranch.set(null);
    this.isBranchModalOpen.set(true);
  }

  closeBranchModal(): void {
    this.isBranchModalOpen.set(false);
  }

  initDefaultBranches(): void {
    this.setProposals([
      {
        title: 'Khảo sát và xác minh yêu cầu kỹ thuật',
        description: 'Kiểm tra hiện trạng, xác định nguyên nhân và thiết bị cần thay thế.',
        type: 'Assignment',
        priority: 'HIGH',
        departmentName: 'Phòng Kỹ thuật & CNTT',
        checklistItems: ['Kiểm tra kết nối và phần cứng', 'Ghi nhận mã lỗi', 'Lập biên bản khảo sát'],
        reasoning: 'Bước đầu tiên bắt buộc để làm rõ phạm vi xử lý.',
      },
      {
        title: 'Cung cấp linh kiện và vật tư thay thế',
        description: 'Đề xuất tạm ứng và cấp phát linh kiện từ kho nội bộ.',
        type: 'SupportRequest',
        priority: 'MEDIUM',
        departmentName: 'Phòng Hành chính - Quản trị',
        checklistItems: ['Kiểm tra tồn kho vật tư', 'Trình phê duyệt xuất kho', 'Bàn giao vật tư cho kỹ thuật'],
        reasoning: 'Yêu cầu phối hợp liên phòng ban để chuẩn bị vật tư.',
      },
      {
        title: 'Triển khai sửa chữa và kiểm thử vận hành',
        description: 'Tiến hành khắc phục sự cố và chạy thử nghiệm chức năng.',
        type: 'Assignment',
        priority: 'MEDIUM',
        departmentName: 'Phòng Kỹ thuật & CNTT',
        checklistItems: ['Lắp đặt linh kiện mới', 'Cấu hình và in thử', 'Bàn giao người dùng nghiệm thu'],
        reasoning: 'Hoàn thành hạng mục công việc chính.',
      },
    ]);
  }

  addCustomBranch(title?: string): void {
    const list = [...this.branches()];
    const newIdx = list.length + 1;
    const newBranch: SubtaskBranch = {
      id: crypto.randomUUID(),
      branchIndex: newIdx,
      title: title || `Nhiệm vụ phân nhánh #${newIdx}`,
      description: 'Nhiệm vụ được tạo bổ sung từ không gian phân rã công việc.',
      type: 'Assignment',
      priority: 'MEDIUM',
      departmentName: 'Phòng Kỹ thuật & CNTT',
      suggestedDurationDays: 2,
      checklistItems: ['Tiếp nhận yêu cầu', 'Thực hiện xử lý', 'Nghiệm thu kết quả'],
      reasoning: 'Nhánh công việc bổ sung theo yêu cầu người dùng.',
      status: 'pending',
    };
    list.push(newBranch);
    this.branches.set(list);
    this.showToast(`Đã thêm nhánh #${newIdx}`, 'info');
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
    } catch { }
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
    } catch { }

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
        text: `${greeting}Tôi là Trợ lý AI Agent BizFlow 🤖.\nTôi có thể hỗ trợ bạn mọi tác vụ: kiểm tra deadline, phân rã kế hoạch công việc thành các nhánh nhiệm vụ, tra cứu quy định và trực tiếp tạo task vào hệ thống.`,
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
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
    } catch { }
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
    } catch { }
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

  handleInPlaceMutation(text: string): boolean {
    const list = [...this.branches()];
    if (list.length === 0) return false;

    const trimmed = text.trim();

    // 1. Delete branch: "Xóa nhánh X", "Bỏ nhánh X", "Hủy nhánh X"
    const deleteMatch = trimmed.match(/(?:xóa|bỏ|hủy)\s+(?:nhánh|task|nhiệm\s*vụ)\s*(?:số\s*)?(\d+)/i);
    if (deleteMatch) {
      const idx = parseInt(deleteMatch[1], 10) - 1;
      if (idx >= 0 && idx < list.length) {
        const removed = list.splice(idx, 1)[0];
        // Re-index remaining branches
        list.forEach((b, i) => (b.branchIndex = i + 1));
        this.branches.set(list);
        if (this.selectedBranch()?.id === removed.id) {
          this.selectedBranch.set(null);
        }
        this.addRobotFeedback(
          `🗑️ Đã xóa nhánh #${idx + 1}: **"${removed.title}"** khỏi danh sách phân rã theo yêu cầu của bạn.`,
          true,
        );
        this.persistCurrentSession();
        return true;
      }
    }

    // 2. Change department: "Đổi phòng ban nhánh X thành Y", "Chuyển nhánh X cho phòng Y"
    const deptMatch =
      trimmed.match(/(?:đổi|chuyển|sửa)\s+(?:phòng\s*ban|bộ\s*phận|phòng)\s+(?:của\s+)?(?:nhánh\s*)?(\d+)\s+(?:thành|cho|sang)\s+(.+)/i) ||
      trimmed.match(/(?:nhánh\s*)(\d+)\s+(?:đổi|chuyển)\s+(?:phòng\s*ban|phòng)\s+(?:thành|cho|sang)\s+(.+)/i);
    if (deptMatch) {
      const idx = parseInt(deptMatch[1], 10) - 1;
      const targetDeptName = deptMatch[2].trim();
      if (idx >= 0 && idx < list.length) {
        const branch = { ...list[idx] };
        branch.departmentName = targetDeptName;
        // Try match ID if available
        const matched = this.departments().find((d) =>
          d.name.toLowerCase().includes(targetDeptName.toLowerCase()),
        );
        if (matched) {
          branch.departmentId = matched.id;
          branch.departmentName = matched.name;
        }
        list[idx] = branch;
        this.branches.set(list);
        if (this.selectedBranch()?.id === branch.id) {
          this.selectedBranch.set(branch);
        }
        this.addRobotFeedback(
          `🏢 Đã chuyển phòng ban nhận của nhánh #${idx + 1} (**${branch.title}**) sang **${branch.departmentName}**!`,
          true,
        );
        this.persistCurrentSession();
        return true;
      }
    }

    // 3. Change title: "Đổi tiêu đề nhánh X thành Y", "Đổi tên nhánh X thành Y"
    const titleMatch =
      trimmed.match(/(?:đổi|sửa)\s+(?:tiêu\s*đề|tên)\s+(?:của\s+)?(?:nhánh\s*)?(\d+)\s+(?:thành|là)\s+(.+)/i) ||
      trimmed.match(/(?:nhánh\s*)(\d+)\s+(?:đổi|sửa)\s+(?:tiêu\s*đề|tên)\s+(?:thành|là)\s+(.+)/i);
    if (titleMatch) {
      const idx = parseInt(titleMatch[1], 10) - 1;
      const newTitle = titleMatch[2].trim();
      if (idx >= 0 && idx < list.length) {
        const branch = { ...list[idx] };
        branch.title = newTitle;
        list[idx] = branch;
        this.branches.set(list);
        if (this.selectedBranch()?.id === branch.id) {
          this.selectedBranch.set(branch);
        }
        this.addRobotFeedback(
          `✏️ Đã đổi tiêu đề nhánh #${idx + 1} thành: **"${newTitle}"**!`,
          true,
        );
        this.persistCurrentSession();
        return true;
      }
    }

    // 4. Change priority: "Đổi ưu tiên nhánh X thành HIGH/MEDIUM/LOW/Cao/Thấp"
    const prioMatch =
      trimmed.match(/(?:đổi|sửa)\s+(?:mức\s+)?ưu\s*tiên\s+(?:của\s+)?(?:nhánh\s*)?(\d+)\s+(?:thành|là)\s+(.+)/i) ||
      trimmed.match(/(?:nhánh\s*)(\d+)\s+(?:đổi|sửa)\s+(?:mức\s+)?ưu\s*tiên\s+(?:thành|là)\s+(.+)/i);
    if (prioMatch) {
      const idx = parseInt(prioMatch[1], 10) - 1;
      const rawPrio = prioMatch[2].trim().toLowerCase();
      let prio: 'LOW' | 'MEDIUM' | 'HIGH' = 'MEDIUM';
      if (rawPrio.includes('cao') || rawPrio.includes('high') || rawPrio.includes('gấp') || rawPrio.includes('khẩn')) {
        prio = 'HIGH';
      } else if (rawPrio.includes('thấp') || rawPrio.includes('low')) {
        prio = 'LOW';
      } else {
        prio = 'MEDIUM';
      }

      if (idx >= 0 && idx < list.length) {
        const branch = { ...list[idx] };
        branch.priority = prio;
        list[idx] = branch;
        this.branches.set(list);
        if (this.selectedBranch()?.id === branch.id) {
          this.selectedBranch.set(branch);
        }
        this.addRobotFeedback(
          `⚡ Đã cập nhật mức ưu tiên của nhánh #${idx + 1} thành: **${prio}**!`,
          true,
        );
        this.persistCurrentSession();
        return true;
      }
    }

    // 5. Add new branch: "Thêm nhánh mới: Y", "Bổ sung nhánh Y"
    const addMatch = trimmed.match(/(?:thêm|tạo\s*thêm|bổ\s*sung)\s+nhánh(?:\s+mới)?(?::|\s+là)\s+(.+)/i);
    if (addMatch) {
      const desc = addMatch[1].trim();
      const newBranch: SubtaskBranch = {
        id: crypto.randomUUID(),
        branchIndex: list.length + 1,
        title: desc.length > 60 ? desc.slice(0, 60) + '…' : desc,
        description: desc,
        type: 'Assignment',
        departmentName: this.departments()[0]?.name || 'Phòng Điều hành',
        departmentId: this.departments()[0]?.id || null,
        priority: 'MEDIUM',
        suggestedDurationDays: 3,
        checklistItems: [
          `Bước 1: Tiếp nhận và làm rõ ${desc.slice(0, 40)}`,
          `Bước 2: Triển khai thực hiện`,
          `Bước 3: Nghiệm thu và bàn giao`,
        ],
        reasoning: 'Nhánh bổ sung theo yêu cầu chỉnh sửa trực tiếp của người dùng.',
        status: 'pending',
      };
      list.push(newBranch);
      this.branches.set(list);
      this.addRobotFeedback(
        `➕ Đã thêm nhánh mới #${newBranch.branchIndex}: **"${newBranch.title}"** (Phòng ban: ${newBranch.departmentName}) vào danh sách phân rã!`,
        true,
      );
      this.persistCurrentSession();
      return true;
    }

    return false;
  }

  private addRobotFeedback(text: string, withActionCard: boolean = true): void {
    const msg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'robot',
      text,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
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
      const type = (!canCreateTasks ? 'SupportRequest' : (rawType === 'SupportRequest' ? 'SupportRequest' : 'Assignment'));
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
      const priority = (rawPriority === 'HIGH' || rawPriority === 'LOW' ? rawPriority : 'MEDIUM');
      const suggestedDurationDays = p.suggestedDurationDays || p.SuggestedDurationDays || 3;
      const rawChecklist = p.checklistItems || p.ChecklistItems;
      const checklistItems = Array.isArray(rawChecklist) && rawChecklist.length ? rawChecklist : [
        `Bước 1: Chuẩn bị nội dung và kế hoạch`,
        `Bước 2: Thực hiện công việc`,
        `Bước 3: Hoàn tất và báo cáo`,
      ];
      const reasoning = p.reasoning || p.Reasoning || 'Được bóc tách tự động bởi AI Agent dựa trên mục tiêu tổng thể.';

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
      } else if (updates.departmentName !== undefined && !updates.departmentId) {
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

  submitSingleBranch(branch: SubtaskBranch, force: boolean = false): void {
    if (branch.status === 'submitting') return;
    if (!this.session.hasPermission('tasks.create')) {
      this.showToast('Tài khoản Nhân viên không thể tạo nhiệm vụ cho cấp dưới. Vui lòng tạo Yêu cầu nếu cần đề xuất công việc.', 'error');
      return;
    }

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
        const list = [...this.branches()];
        const bIdx = list.findIndex((b) => b.id === branch.id);
        if (bIdx >= 0) {
          list[bIdx] = { ...list[bIdx], status: 'pending' };
          this.branches.set(list);
          this.selectedBranch.set(list[bIdx]);
        }
        const errMsg = err?.error?.message || err?.message || 'Không thể tạo nhiệm vụ. Vui lòng kiểm tra lại dịch vụ máy chủ.';
        this.showToast(`❌ Lỗi tạo nhiệm vụ: ${errMsg}`, 'error');
      },
    });
  }

  submitAllBranches(): void {
    if (!this.session.hasPermission('tasks.create')) {
      this.showToast('Tài khoản Nhân viên không thể tạo nhiệm vụ cho cấp dưới. Vui lòng tạo Yêu cầu nếu cần đề xuất công việc.', 'error');
      return;
    }
    let pendingBranches = this.branches().filter((b) => b.status !== 'created');
    if (pendingBranches.length === 0 && this.branches().length > 0) {
      pendingBranches = this.branches();
    }
    if (pendingBranches.length === 0) {
      this.showToast('Chưa có nhánh nhiệm vụ nào để gửi vào hệ thống.', 'info');
      return;
    }

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
        const list = this.branches().map((b) => ({
          ...b,
          status: 'created' as const,
        }));
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
        const errMsg = err?.error?.message || err?.message || 'Không thể tạo hàng loạt. Vui lòng kiểm tra lại dịch vụ máy chủ.';
        this.showToast(`❌ Lỗi tạo hàng loạt: ${errMsg}`, 'error');
      },
    });
  }

  sendFormAnswers(formId: string, answers: AgentAnswer[], summaryText: string, messageId: string): void {
    // 1. Mark questionnaire in the original robot message as answered
    this.messages.update((msgs) =>
      msgs.map((m) => (m.id === messageId ? { ...m, questionnaireAnswered: true, userAnswersSummary: summaryText } : m)),
    );

    // 2. Append user message showing the selected answers
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'user',
      text: summaryText,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };
    this.messages.update((list) => [...list, userMsg]);
    this.busy.set(true);

    // Prior conversation history for multi-turn context
    const priorHistory = this.messages()
      .slice(0, -1)
      .filter((m) => m.id !== 'welcome')
      .slice(-8)
      .map((m) => ({ sender: m.sender, text: m.text }));

    const payload = {
      message: summaryText,
      locale: this.i18n.currentLanguage(),
      history: priorHistory,
      formId,
      formAnswers: answers,
    };

    this.http
      .post<{ response: string; recognizedCommand?: string; actionCards?: any[]; questionnaire?: any; agentThought?: string }>(
        '/api/v1/ai/chat',
        payload,
      )
      .subscribe({
        next: (res) => {
          this.busy.set(false);

          let actionCards = res.actionCards;
          const decompCard = res.actionCards?.find(
            (c) => c.actionType === 'OPEN_DECOMPOSITION' || c.actionType === 'OPEN_BRANCHES' || c.actionType === 'VIEW_BRANCHES',
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
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            actionCards,
            questionnaire: res.questionnaire,
            agentThought: res.agentThought,
            isThoughtExpanded: !!res.agentThought,
          };

          this.messages.update((list) => [...list, robotMsg]);
          this.persistCurrentSession();
        },
        error: (err) => {
          this.busy.set(false);
          const errMsg = err?.error?.message || err?.message || 'Không thể gửi phản hồi. Vui lòng thử lại.';
          this.showToast(`❌ Lỗi kết nối: ${errMsg}`, 'error');
        },
      });
  }
}
