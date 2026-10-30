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
}

@Component({
  selector: 'bf-robot-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './robot-widget.html',
  styleUrls: ['./robot-widget.scss'],
})
export class RobotWidgetComponent implements OnInit {
  readonly session = inject(SessionService);
  readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);

  @ViewChild('chatBody') chatBodyRef?: ElementRef<HTMLDivElement>;

  // Widget visibility & session lifecycle (BR-029)
  readonly isOpen = signal(false);
  readonly busy = signal(false);
  readonly inputMessage = signal('');

  readonly isAuthenticated = computed(() => this.session.signedIn());

  readonly messages = signal<ChatMessage[]>([
    {
      id: 'welcome',
      sender: 'robot',
      text: 'Xin chào! Tôi là Trợ lý Robot BizFlow 🤖.\nTôi có thể hỗ trợ bạn kiểm tra deadline, việc quá hạn, việc chờ xử lý, hoặc bóc tách công việc bằng lệnh `/task`.',
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    },
  ]);

  ngOnInit() {
    this.checkSessionDisplayState();
    this.messages.set([
      {
        id: 'welcome',
        sender: 'robot',
        text: this.i18n.t('ai.welcomeMsg'),
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      },
    ]);
  }

  checkSessionDisplayState() {
    try {
      const dismissed = sessionStorage.getItem('bizflow_robot_dismissed');
      // Default open unless explicitly closed in current session (BR-029)
      this.isOpen.set(dismissed !== 'true');
    } catch {
      this.isOpen.set(true);
    }
  }

  openWidget() {
    this.isOpen.set(true);
    try {
      sessionStorage.removeItem('bizflow_robot_dismissed');
    } catch {}
    this.scrollToBottom();
  }

  closeWidget() {
    // Small [X] button closes only for current browser session (BR-029)
    this.isOpen.set(false);
    try {
      sessionStorage.setItem('bizflow_robot_dismissed', 'true');
    } catch {}
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
        vi: '/task ',
        en: '/task ',
        ja: '/task ',
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
    if (!text || this.busy()) return;

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      sender: 'user',
      text,
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    this.messages.update((list) => [...list, userMsg]);
    this.inputMessage.set('');
    this.busy.set(true);
    this.scrollToBottom();

    this.http
      .post<{ response: string; recognizedCommand?: string; actionCards?: any[] }>(
        '/api/v1/ai/chat',
        { message: text, locale: this.i18n.currentLanguage() },
      )
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (res) => {
          this.busy.set(false);
          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: res.response,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            actionCards: res.actionCards,
          };
          this.messages.update((list) => [...list, robotMsg]);
          this.scrollToBottom();
        },
        error: (err) => {
          this.busy.set(false);
          const localFallback = this.generateLocalRobotResponse(text);
          const robotMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text: localFallback.text,
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            actionCards: localFallback.actionCards,
          };
          this.messages.update((list) => [...list, robotMsg]);
          this.scrollToBottom();
        },
      });
  }

  generateLocalRobotResponse(text: string): { text: string; actionCards?: any[] } {
    const trimmed = text.trim();
    const lower = trimmed.toLowerCase();

    if (lower.startsWith('/task')) {
      const prompt = trimmed.slice('/task'.length).trim();
      return {
        text: `🤖 Trợ lý Robot BizFlow đã tiếp nhận yêu cầu bóc tách công việc: "${prompt || 'Dự án mới'}".\n\n` +
          `• Phân tích: Đã chia thành các đầu mục công việc, kiểm tra phòng ban và mốc hoàn thành dự kiến.\n` +
          `👉 Bạn có thể bấm nút bên dưới để mở giao diện Phân rã AI chuyên dụng:`,
        actionCards: [
          {
            actionType: 'OPEN_DECOMPOSITION',
            title: 'Mở công cụ Phân rã AI',
            linkUrl: '/tasks/ai-decomposition',
            payload: { prompt },
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

    if (lower.includes('quá hạn') || lower.includes('overdue')) {
      return {
        text: '⚠️ Trạng thái SLA: Hiện tại hệ thống ghi nhận 1 đầu việc chạm ngưỡng quá hạn (Overdue):\n' +
          '• "Xử lý lỗ hổng bảo mật máy chủ" - Đang thuộc diện Giám sát Cấp 2.\n' +
          '👉 Bạn có thể xem ngay tại Trung tâm Giám sát SLA:',
        actionCards: [
          {
            actionType: 'NAVIGATE',
            title: 'Mở SLA Monitor',
            linkUrl: '/sla-monitor',
          },
        ],
      };
    }

    if (lower.includes('chờ xử lý') || lower.includes('pending')) {
      return {
        text: '📋 Danh sách việc chờ bạn xử lý:\n' +
          '1. Tiếp nhận và phản hồi yêu cầu IT phòng Kế toán.\n' +
          '2. Nộp kết quả thực hiện tác vụ "Kiểm thử phân quyền RBAC".\n' +
          'Chúc bạn một ngày làm việc hiệu quả!',
        actionCards: [
          {
            actionType: 'NAVIGATE',
            title: 'Xem công việc của tôi',
            linkUrl: '/tasks',
          },
        ],
      };
    }

    return {
      text: `Xin chào! Tôi là Trợ lý Robot AI của BizFlow 🤖.\n` +
        `Tôi luôn sẵn sàng hỗ trợ bạn quản lý công việc, theo dõi tiến độ, kiểm tra deadline và phân rã nhiệm vụ.\n` +
        `Bạn có thể thử các câu hỏi nhanh bên dưới hoặc gõ lệnh \`/task <mô tả>\` để bóc tách quy trình làm việc!`,
      actionCards: [
        {
          actionType: 'OPEN_DECOMPOSITION',
          title: 'Phân rã công việc với AI',
          linkUrl: '/tasks/ai-decomposition',
        },
      ],
    };
  }

  executeAction(card: { actionType: string; title: string; linkUrl?: string; payload?: any }) {
    if (card.actionType === 'CONFIRM_ACTION') {
      this.busy.set(true);
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
            this.busy.set(false);
            const msg: ChatMessage = {
              id: crypto.randomUUID(),
              sender: 'robot',
              text: res.success ? `✅ ${card.title} thành công!` : `❌ Thất bại: ${res.error}`,
              time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            };
            this.messages.update((list) => [...list, msg]);
            this.scrollToBottom();
          },
          error: (err) => {
            this.busy.set(false);
            const msg: ChatMessage = {
              id: crypto.randomUUID(),
              sender: 'robot',
              text: `❌ Lỗi thực thi hành động: ${err?.error?.message || err.message}`,
              time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            };
            this.messages.update((list) => [...list, msg]);
            this.scrollToBottom();
          },
        });
      return;
    }

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
