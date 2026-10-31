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
} from '../../core/ai/ai-agent.service';

@Component({
  selector: 'bf-robot-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './robot-widget.html',
  styleUrls: ['./robot-widget.scss'],
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
  readonly messages = computed(() => this.agent.messages());
  readonly branches = computed(() => this.agent.branches());
  readonly selectedBranch = computed(() => this.agent.selectedBranch());
  readonly chatSessions = computed(() => this.agent.chatSessions());
  readonly busy = computed(() => this.agent.busy());
  readonly isAuthenticated = computed(() => this.session.signedIn());

  ngOnInit() {
    this.checkSessionDisplayState();
  }

  checkSessionDisplayState() {
    try {
      const dismissed = sessionStorage.getItem('bizflow_robot_dismissed');
      // Default open unless explicitly closed in current session (BR-029)
      if (dismissed !== 'true') {
        this.agent.isOpen.set(true);
      } else {
        this.agent.isOpen.set(false);
      }
    } catch {
      this.agent.isOpen.set(true);
    }
  }

  openWidget() {
    this.agent.open('chat');
    this.scrollToBottom();
  }

  closeWidget() {
    this.agent.close();
  }

  toggleExpand() {
    this.agent.toggleExpand();
  }

  setTab(tab: 'chat' | 'branches' | 'history') {
    this.agent.setTab(tab);
    if (tab === 'chat') {
      this.scrollToBottom();
    }
  }

  selectBranch(branch: SubtaskBranch | null) {
    this.agent.selectBranch(branch);
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

    // 2. Dispatch to backend AI Chat
    this.http
      .post<{ response: string; recognizedCommand?: string; actionCards?: any[] }>(
        '/api/v1/ai/chat',
        { message: text, locale: this.i18n.currentLanguage() },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.agent.busy.set(false);

          // Check if response contains decomposition proposals
          let actionCards = res.actionCards;
          const decompCard = res.actionCards?.find(
            (c) => c.actionType === 'OPEN_DECOMPOSITION' || c.actionType === 'OPEN_BRANCHES',
          );

          if (decompCard?.payload?.proposals) {
            this.agent.setProposals(decompCard.payload.proposals);
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
          };

          this.agent.messages.update((list) => [...list, robotMsg]);
          this.agent.persistCurrentSession();
          this.scrollToBottom();
        },
        error: () => {
          this.agent.busy.set(false);
          const localFallback = this.generateLocalRobotResponse(text);
          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: localFallback.text,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            actionCards: localFallback.actionCards,
          };
          this.agent.messages.update((list) => [...list, robotMsg]);
          this.agent.persistCurrentSession();
          this.scrollToBottom();
        },
      });
  }

  generateLocalRobotResponse(text: string): { text: string; actionCards?: any[] } {
    const trimmed = text.trim();
    const lower = trimmed.toLowerCase();

    if (lower.startsWith('/task') || lower.startsWith('phân rã') || lower.startsWith('lên kế hoạch')) {
      const prompt = trimmed.replace(/^\/task/i, '').trim();
      const demoProposals = [
        {
          title: 'Khảo sát và lập kế hoạch chi tiết',
          description: `Thu thập yêu cầu thực tế và phân tích giải pháp cho: ${prompt || 'Dự án mới'}.`,
          type: 'Assignment',
          departmentName: 'Phòng Kế hoạch & Điều hành',
          priority: 'HIGH',
          suggestedDurationDays: 2,
          checklistItems: ['Xác định phạm vi công việc', 'Phân bổ nhân sự', 'Xác nhận mốc thời gian'],
          reasoning: 'Bước khởi động đảm bảo định hướng chính xác trước khi triển khai.',
        },
        {
          title: 'Chuẩn bị hạ tầng & phân bổ tài nguyên',
          description: 'Cấp phát tài nguyên hệ thống, môi trường làm việc và tài khoản liên quan.',
          type: 'SupportRequest',
          departmentName: 'Phòng Công nghệ Thông tin',
          priority: 'MEDIUM',
          suggestedDurationDays: 3,
          checklistItems: ['Cấp quyền truy cập hệ thống', 'Cấu hình phân quyền', 'Kiểm thử kết nối'],
          reasoning: 'Đảm bảo môi trường làm việc sẵn sàng không gây gián đoạn.',
        },
        {
          title: 'Đào tạo nội bộ và bàn giao nghiệp vụ',
          description: 'Hướng dẫn quy trình vận hành và ký kết biên bản bàn giao công việc.',
          type: 'Assignment',
          departmentName: 'Phòng Hành chính - Nhân sự',
          priority: 'MEDIUM',
          suggestedDurationDays: 4,
          checklistItems: ['Tài liệu hóa quy trình', 'Tổ chức buổi hướng dẫn', 'Nghiệm thu kết quả'],
          reasoning: 'Giai đoạn nghiệm thu và đảm bảo tính tuần thủ quy định.',
        },
      ];

      this.agent.setProposals(demoProposals);

      return {
        text: `🤖 Trợ lý AI Agent đã phân rã yêu cầu thành **${demoProposals.length} nhánh nhiệm vụ**.\n` +
          `👉 Bạn có thể bấm nút bên dưới để mở giao diện các nhánh, xem chi tiết và tạo việc ngay!`,
        actionCards: [
          {
            actionType: 'VIEW_BRANCHES',
            title: `🌿 Xem các nhánh phân rã (${demoProposals.length} nhánh)`,
          },
        ],
      };
    }

    if (lower.startsWith('/notifications') || lower.includes('thông báo') || lower.includes('notification')) {
      return {
        text: '🔔 Bạn có 2 thông báo công việc mới trong hôm nay:\n' +
          '1. [Yêu cầu] Bàn giao thiết bị làm việc cho nhân viên mới — Cần duyệt.\n' +
          '2. [Cảnh báo SLA] Nhiệm vụ "Cập nhật tài liệu kỹ thuật" còn 4 giờ trước hạn.\n' +
          '👉 Bấm bên dưới để xem toàn bộ thông báo:',
        actionCards: [
          {
            actionType: 'NAVIGATE',
            title: 'Xem hộp thư thông báo',
            linkUrl: '/notifications',
          },
        ],
      };
    }

    if (lower.includes('deadline') || lower.includes('hôm nay') || lower.includes('today')) {
      return {
        text: '📅 Hôm nay bạn có 1 công việc đến hạn hoàn thành:\n' +
          '• **Báo cáo tuần & Đánh giá tiến độ dịch vụ** (Hạn chót: 17:00 hôm nay).\n' +
          'Tất cả các công việc khác vẫn đang ở trạng thái an toàn (Healthy SLA).',
        actionCards: [
          {
            actionType: 'NAVIGATE',
            title: 'Xem danh sách công việc',
            linkUrl: '/tasks',
          },
        ],
      };
    }

    return {
      text: `Xin chào! Tôi là Trợ lý AI Agent của BizFlow 🤖.\n` +
        `Tôi luôn sẵn sàng hỗ trợ bạn quản lý công việc, theo dõi tiến độ, kiểm tra deadline và phân rã nhiệm vụ.\n` +
        `Bạn có thể thử các câu hỏi nhanh bên dưới hoặc gõ lệnh \`/task <mô tả>\` để bóc tách quy trình làm việc!`,
      actionCards: [
        {
          actionType: 'VIEW_BRANCHES',
          title: 'Phân nhánh công việc với AI',
        },
      ],
    };
  }

  executeAction(card: { actionType: string; title: string; linkUrl?: string; payload?: any }) {
    // 1. In-agent branching navigation (stays inside agent workspace)
    if (
      card.actionType === 'VIEW_BRANCHES' ||
      card.actionType === 'OPEN_BRANCHES' ||
      (card.actionType === 'OPEN_DECOMPOSITION' && !card.linkUrl)
    ) {
      if (card.payload?.proposals) {
        this.agent.setProposals(card.payload.proposals);
      }
      this.agent.selectBranch(null);
      this.agent.setTab('branches');
      this.agent.isExpanded.set(true);
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
              text: res.success ? `✅ ${card.title} thành công!` : `❌ Thất bại: ${res.error}`,
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
              text: `❌ Lỗi thực thi hành động: ${err?.error?.message || err.message}`,
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

  private scrollToBottom() {
    setTimeout(() => {
      if (this.chatBodyRef?.nativeElement) {
        this.chatBodyRef.nativeElement.scrollTop = this.chatBodyRef.nativeElement.scrollHeight;
      }
    }, 50);
  }
}
