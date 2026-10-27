import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';

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
  imports: [CommonModule, FormsModule],
  templateUrl: './robot-widget.html',
  styleUrls: ['./robot-widget.scss'],
})
export class RobotWidgetComponent implements OnInit {
  readonly session = inject(SessionService);
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
        { message: text },
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
          const errorMsg: ChatMessage = {
            id: crypto.randomUUID(),
            sender: 'robot',
            text:
              err?.error?.message ||
              'Xin lỗi, không thể kết nối tới dịch vụ AI lúc này. Vui lòng thử lại.',
            time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          };
          this.messages.update((list) => [...list, errorMsg]);
          this.scrollToBottom();
        },
      });
  }

  executeAction(card: { actionType: string; title: string; linkUrl?: string; payload?: any }) {
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
