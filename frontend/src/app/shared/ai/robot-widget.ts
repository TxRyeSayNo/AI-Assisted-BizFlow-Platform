import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import {
  AiAgentService,
  ChatMessage,
  SubtaskBranch,
  AgentAnswer,
} from '../../core/ai/ai-agent.service';

@Component({
  selector: 'bf-robot-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './robot-widget.html',
  styleUrls: ['./robot-widget.scss', './robot-branching.scss'],
})
export class RobotWidgetComponent implements OnInit {
  readonly agent = inject(AiAgentService);
  readonly session = inject(SessionService);
  readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);

  @ViewChild('chatBody') chatBodyRef?: ElementRef<HTMLDivElement>;

  // Local input field
  readonly inputMessage = signal('');

  // Convenience computed signals delegated to unified agent
  readonly isOpen = computed(() => this.agent.isOpen());
  readonly isExpanded = computed(() => this.agent.isExpanded());
  readonly activeTab = computed(() => this.agent.activeTab());
  readonly isBranchModalOpen = computed(() => this.agent.isBranchModalOpen());
  readonly messages = computed(() => this.agent.messages());
  readonly branches = computed(() => this.agent.branches());
  readonly selectedBranch = computed(() => this.agent.selectedBranch());
  readonly chatSessions = computed(() => this.agent.chatSessions());
  readonly busy = computed(() => this.agent.busy());
  readonly isAuthenticated = computed(() => this.session.signedIn());
  readonly canCreateTasks = computed(() => this.session.hasPermission('tasks.create'));

  ngOnInit() {
    this.checkSessionDisplayState();
  }

  checkSessionDisplayState() {
    try {
      // AI Agent Widget defaults to minimized (closed as a floating circular FAB at bottom-right)
      // so it no longer obstructs the right 1/3 of the screen across workspace screens.
      const userOpened = sessionStorage.getItem('bizflow_robot_open');
      if (userOpened === 'true') {
        this.agent.isOpen.set(true);
      } else {
        this.agent.isOpen.set(false);
      }
    } catch {
      this.agent.isOpen.set(false);
    }
  }

  openWidget() {
    this.agent.open('chat');
    try {
      sessionStorage.setItem('bizflow_robot_open', 'true');
      sessionStorage.removeItem('bizflow_robot_dismissed');
    } catch { }
    this.scrollToBottom();
  }

  closeWidget() {
    this.agent.close();
    try {
      sessionStorage.removeItem('bizflow_robot_open');
      sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    } catch { }
  }

  toggleExpand() {
    this.agent.toggleExpand();
  }

  setTab(tab: 'chat' | 'branches' | 'history') {
    if (tab === 'branches') {
      this.agent.loadDepartments();
      this.agent.openBranchModal();
    } else {
      this.agent.setTab(tab);
      if (tab === 'chat') {
        this.scrollToBottom();
      }
    }
  }

  openBranchModal() {
    this.agent.loadDepartments();
    this.agent.openBranchModal();
  }

  closeBranchModal() {
    this.agent.closeBranchModal();
  }

  selectBranch(branch: SubtaskBranch | null) {
    this.agent.selectBranch(branch);
  }

  onBranchDepartmentChange(id: string, deptId: string) {
    const dept = this.agent.departments().find((d) => d.id === deptId);
    this.agent.updateBranch(id, {
      departmentId: deptId,
      departmentName: dept ? dept.name : undefined,
    });
  }

  onBranchDurationChange(id: string, days: any) {
    const val = parseInt(days, 10);
    if (!isNaN(val) && val > 0) {
      this.agent.updateBranch(id, { suggestedDurationDays: val });
    }
  }

  onBranchPriorityChange(id: string, priority: 'LOW' | 'MEDIUM' | 'HIGH') {
    this.agent.updateBranch(id, { priority });
  }

  onBranchTypeChange(id: string, type: 'Assignment' | 'SupportRequest') {
    this.agent.updateBranch(id, { type });
  }

  cleanText(text: string | null | undefined): string {
    if (!text) return '';
    return text
      .replace(/[\u{1F300}-\u{1F9FF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}\u{FE00}-\u{FE0F}]/gu, '')
      .replace(/\*{1,3}/g, '')
      .replace(/^>\s*/gm, '')
      .replace(/[ \t]{2,}/g, ' ')
      .trim();
  }

  onBranchTitleChange(id: string, title: string) {
    this.agent.updateBranch(id, { title });
  }

  onBranchDescriptionChange(id: string, description: string) {
    this.agent.updateBranch(id, { description });
  }

  onQuickPrompt(type: 'today' | 'overdue' | 'pending' | 'task' | 'notifications') {
    const prompts: Record<string, Record<string, string>> = {
      today: {
        vi: 'Hôm nay tôi có deadline nào không?',
        en: 'Do I have any deadlines due today?',
        ja: '本日締切のタスクはありますか？',
      },
      overdue: {
        vi: 'Kiểm tra các việc bị quá hạn',
        en: 'Check my overdue tasks',
        ja: '期限超過のタスクを確認してください',
      },
      pending: {
        vi: 'Liệt kê các việc chờ xử lý của tôi',
        en: 'List my pending tasks',
        ja: '未処理のタスクを一覧表示してください',
      },
      task: {
        vi: '/task Lên kế hoạch tiếp nhận nhân viên mới và bàn giao thiết bị',
        en: '/task Onboard new employees and assign equipment',
        ja: '/task 新入社員の受け入れと備品配布',
      },
      notifications: {
        vi: '/notifications',
        en: '/notifications',
        ja: '/notifications',
      },
    };
    const lang = this.i18n.currentLanguage();
    const text = prompts[type]?.[lang] || prompts[type]?.['vi'] || '';
    if (type === 'task') {
      this.inputMessage.set(text);
    } else {
      this.sendQuickPrompt(text);
    }
  }

  sendQuickPrompt(text: string) {
    this.inputMessage.set(text);
    this.sendMessage();
  }

  sendMessage() {
    const text = this.inputMessage().trim();
    if (!text || this.agent.busy()) return;

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'user',
      text,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    // Append user message
    this.agent.messages.update((list) => [...list, userMsg]);
    this.inputMessage.set('');
    this.scrollToBottom();

    // 1. Check if user is asking to modify the current branching view in real time
    if (this.agent.handleInPlaceMutation(text)) {
      this.scrollToBottom();
      return;
    }

    this.agent.busy.set(true);

    // Prior conversation history for multi-turn context (excluding initial welcome placeholder)
    const priorHistory = this.agent
      .messages()
      .slice(0, -1)
      .filter((m) => m.id !== 'welcome')
      .slice(-8)
      .map((m) => ({ sender: m.sender, text: m.text }));

    const payload: { message: string; locale: string; history?: Array<{ sender: string; text: string }> } = {
      message: text,
      locale: this.i18n.currentLanguage(),
    };
    if (priorHistory.length > 0) {
      payload.history = priorHistory;
    }

    // 2. Dispatch to backend AI Chat
    this.http
      .post<{ response: string; recognizedCommand?: string; actionCards?: any[]; questionnaire?: any; agentThought?: string }>(
        '/api/v1/ai/chat',
        payload,
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.agent.busy.set(false);

          // Check if response contains decomposition proposals
          let actionCards = res.actionCards;
          const decompCard = res.actionCards?.find(
            (c) => c.actionType === 'OPEN_DECOMPOSITION' || c.actionType === 'OPEN_BRANCHES' || c.actionType === 'VIEW_BRANCHES',
          );

          if (decompCard?.payload?.proposals) {
            this.agent.setProposals(decompCard.payload.proposals);
            actionCards = [
              {
                actionType: 'VIEW_BRANCHES',
                title: `Xem các nhánh phân rã (${decompCard.payload.proposals.length} nhánh)`,
              },
            ];
          }

          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: this.cleanText(res.response),
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            actionCards,
            questionnaire: res.questionnaire,
            agentThought: res.agentThought ? this.cleanText(res.agentThought) : undefined,
            isThoughtExpanded: !!res.agentThought,
          };

          this.agent.messages.update((list) => [...list, robotMsg]);
          this.agent.persistCurrentSession();
          this.scrollToBottom();
        },
        error: (err) => {
          this.agent.busy.set(false);
          const errorText = err?.status === 403
            ? '🔒 Bạn không có quyền sử dụng tính năng AI Agent này.'
            : '⚠️ Không thể kết nối với máy chủ AI Agent vào lúc này. Vui lòng kiểm tra kết nối mạng hoặc thử lại sau.';
          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: errorText,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          };
          this.agent.messages.update((list) => [...list, robotMsg]);
          this.agent.persistCurrentSession();
          this.scrollToBottom();
        },
      });
  }

  executeAction(card: { actionType: string; title: string; linkUrl?: string; payload?: any }) {
    // 1. Dedicated Branching Workspace modal (center screen next to chat)
    if (
      card.actionType === 'VIEW_BRANCHES' ||
      card.actionType === 'OPEN_BRANCHES' ||
      card.actionType === 'OPEN_DECOMPOSITION'
    ) {
      const proposals = card.payload?.proposals || card.payload?.Proposals;
      if (proposals) {
        this.agent.setProposals(proposals);
      }
      this.agent.openBranchModal();
      return;
    }

    // Quick fill / Quick reply
    if (card.actionType && (card.actionType.startsWith('QUICK_FILL_') || card.actionType === 'QUICK_REPLY')) {
      const sample = card.payload?.template || card.payload?.text || card.title.replace(/^Mẫu:\s*/i, '');
      this.sendQuickPrompt(sample);
      return;
    }

    // 2. Action confirmation
    if (card.actionType === 'CONFIRM_ACTION') {
      this.agent.busy.set(true);
      this.http
        .post<{ success: boolean; resultJson?: string; error?: string }>(
          card.linkUrl || '/api/v1/ai/actions/execute',
          {
            toolName: card.payload?.toolName,
            argsJson: card.payload?.argsJson,
            confirmed: true,
            confirmationToken: card.payload?.confirmationToken,
          },
        )
        .pipe(takeUntilDestroyed(this.destroy))
        .subscribe({
          next: (res) => {
            this.agent.busy.set(false);
            const msg: ChatMessage = {
              id: crypto.randomUUID(),
              sender: 'robot',
              text: res.success ? `${card.title} thành công.` : `Thất bại: ${res.error}`,
              time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            };
            this.agent.messages.update((list) => [...list, msg]);
            this.scrollToBottom();
          },
          error: (err) => {
            this.agent.busy.set(false);
            const msg: ChatMessage = {
              id: crypto.randomUUID(),
              sender: 'robot',
              text: `Lỗi thực thi hành động: ${err?.error?.message || err.message}`,
              time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            };
            this.agent.messages.update((list) => [...list, msg]);
            this.scrollToBottom();
          },
        });
      return;
    }

    // 3. Fallback standard navigation
    if (card.linkUrl) {
      this.router.navigate([card.linkUrl], {
        queryParams: card.payload ? card.payload : undefined,
      });
    }
  }

  // Questionnaire selections state: messageId -> questionId -> { selectedOptions: string[], customText: string }
  readonly formSelections = signal<Record<string, Record<string, { selectedOptions: string[]; customText: string }>>>({});

  getSelection(msgId: string, qId: string, defaultOptIds: string[] = []): { selectedOptions: string[]; customText: string } {
    const currentMsgMap = this.formSelections()[msgId] || {};
    if (!currentMsgMap[qId]) {
      return { selectedOptions: defaultOptIds, customText: '' };
    }
    return currentMsgMap[qId];
  }

  isOptionSelected(msgId: string, qId: string, optId: string, isDefault?: boolean): boolean {
    const currentMsgMap = this.formSelections()[msgId];
    if (!currentMsgMap || !currentMsgMap[qId]) {
      return !!isDefault;
    }
    return currentMsgMap[qId].selectedOptions.includes(optId);
  }

  toggleSingleChoice(msgId: string, qId: string, optId: string) {
    this.formSelections.update((all) => {
      const msgMap = { ...(all[msgId] || {}) };
      const current = msgMap[qId] || { selectedOptions: [], customText: '' };
      msgMap[qId] = { ...current, selectedOptions: [optId] };
      return { ...all, [msgId]: msgMap };
    });
  }

  toggleMultipleChoice(msgId: string, qId: string, optId: string) {
    this.formSelections.update((all) => {
      const msgMap = { ...(all[msgId] || {}) };
      const current = msgMap[qId] || { selectedOptions: [], customText: '' };
      const exists = current.selectedOptions.includes(optId);
      const nextOpts = exists
        ? current.selectedOptions.filter((id) => id !== optId)
        : [...current.selectedOptions, optId];
      msgMap[qId] = { ...current, selectedOptions: nextOpts };
      return { ...all, [msgId]: msgMap };
    });
  }

  getCustomText(msgId: string, qId: string): string {
    return this.formSelections()[msgId]?.[qId]?.customText || '';
  }

  onCustomTextInput(msgId: string, qId: string, event: Event) {
    const input = event.target as HTMLInputElement;
    const text = input.value;
    this.formSelections.update((all) => {
      const msgMap = { ...(all[msgId] || {}) };
      const current = msgMap[qId] || { selectedOptions: [], customText: '' };
      msgMap[qId] = { ...current, customText: text };
      return { ...all, [msgId]: msgMap };
    });
  }

  toggleThought(msg: ChatMessage) {
    this.agent.messages.update((list) =>
      list.map((m) => (m.id === msg.id ? { ...m, isThoughtExpanded: !m.isThoughtExpanded } : m)),
    );
  }

  submitQuestionnaire(msg: ChatMessage) {
    if (!msg.questionnaire || this.agent.busy()) return;

    const answers: AgentAnswer[] = [];
    const lines: string[] = ['Tôi đã xác nhận các thông tin sau:'];

    for (const q of msg.questionnaire.questions) {
      const defaultOptIds = (q.options || []).filter((o) => o.isDefault).map((o) => o.id);
      const sel = this.getSelection(msg.id, q.id, defaultOptIds);

      answers.push({
        questionId: q.id,
        selectedOptionIds: sel.selectedOptions,
        customText: sel.customText ? sel.customText.trim() : undefined,
      });

      const selectedLabels = (q.options || [])
        .filter((o) => sel.selectedOptions.includes(o.id))
        .map((o) => o.label);

      let lineVal = selectedLabels.join(', ');
      if (sel.customText?.trim()) {
        lineVal = lineVal ? `${lineVal} (${sel.customText.trim()})` : sel.customText.trim();
      }

      if (lineVal) {
        lines.push(`• ${q.title}: ${lineVal}`);
      }
    }

    const summaryText = lines.join('\n');
    this.agent.sendFormAnswers(msg.questionnaire.formId, answers, summaryText, msg.id);
    this.scrollToBottom();
  }

  skipQuestionnaire(msg: ChatMessage) {
    if (!msg.questionnaire || this.agent.busy()) return;

    const defaultAnswers: AgentAnswer[] = msg.questionnaire.questions.map((q) => ({
      questionId: q.id,
      selectedOptionIds: (q.options || []).filter((o) => o.isDefault).map((o) => o.id),
      customText: undefined,
    }));

    const summary = 'Bỏ qua khảo sát, sử dụng các tham số đề xuất mặc định của hệ thống.';
    this.agent.sendFormAnswers(msg.questionnaire.formId, defaultAnswers, summary, msg.id);
    this.scrollToBottom();
  }

  private scrollToBottom() {
    setTimeout(() => {
      if (this.chatBodyRef?.nativeElement) {
        this.chatBodyRef.nativeElement.scrollTop = this.chatBodyRef.nativeElement.scrollHeight;
      }
    }, 50);
  }
}
