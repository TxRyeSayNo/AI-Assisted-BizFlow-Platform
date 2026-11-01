import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from './core/auth/session';
import { I18nService } from './core/i18n/i18n.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { RobotWidgetComponent } from './shared/ai/robot-widget';
import { ToastContainerComponent } from './core/toast/toast-container.component';
import { AiAgentService } from './core/ai/ai-agent.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslatePipe,
    RobotWidgetComponent,
    ToastContainerComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly session = inject(SessionService, { optional: true });
  readonly i18n = inject(I18nService);
  readonly aiAgent = inject(AiAgentService);
  private readonly router = inject(Router);

  readonly avatarFailed = signal(false);
  readonly sidebarWidth = signal<number>(this.getInitialSidebarWidth());
  private isResizing = false;

  private getInitialSidebarWidth(): number {
    if (typeof window !== 'undefined' && window.localStorage) {
      const saved = localStorage.getItem('bizflow_sidebar_width');
      if (saved) {
        const parsed = parseInt(saved, 10);
        if (!isNaN(parsed) && parsed >= 230 && parsed <= 330) {
          return parsed;
        }
      }
    }
    return 260; // Default width comfortably fits all Vietnamese navigation labels without truncation
  }

  onStartResize(event: MouseEvent): void {
    event.preventDefault();
    this.isResizing = true;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    const onMouseMove = (e: MouseEvent) => {
      if (!this.isResizing) return;
      const newWidth = Math.max(230, Math.min(320, e.clientX));
      this.sidebarWidth.set(newWidth);
    };

    const onMouseUp = () => {
      this.isResizing = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
      try {
        localStorage.setItem('bizflow_sidebar_width', this.sidebarWidth().toString());
      } catch {
        // Ignore storage errors
      }
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  }

  readonly identity = computed(() => this.session?.identity() ?? null);
  readonly signedIn = computed(() => this.session?.signedIn() ?? false);

  readonly profilePhotoUrl = computed(() => {
    const id = this.identity();
    return id?.avatarUrl || (id as any)?.picture || (id as any)?.photoUrl || null;
  });

  onAvatarError(): void {
    this.avatarFailed.set(true);
  }

  userInitials(): string {
    const name = this.identity()?.fullName || 'User';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  welcomeName(): string {
    const isEn = this.i18n.currentLanguage() === 'en';
    const raw = this.identity()?.fullName?.trim();
    if (!raw) return isEn ? 'Platform Administrator' : 'Quản trị viên Hệ thống';
    if (raw === 'Platform Administrator' || raw === 'superadmin' || raw.toLowerCase().includes('platform admin')) {
      return isEn ? 'Platform Administrator' : 'Quản trị viên Hệ thống';
    }
    if (raw === 'Company Administrator') {
      return isEn ? 'Company Administrator' : 'Quản trị viên Doanh nghiệp';
    }
    if (raw === 'Operation Manager' || raw === 'Operational Manager') {
      return isEn ? 'Operational Manager' : 'Quản lý Vận hành';
    }
    if (raw === 'Regular Employee') {
      return isEn ? 'Regular Employee' : 'Nhân viên Doanh nghiệp';
    }
    return raw;
  }

  currentRoleLabel(): string {
    const id = this.identity();
    const isEn = this.i18n.currentLanguage() === 'en';
    if (!id?.tenantId) return isEn ? 'Platform Administrator' : 'Quản trị viên Hệ thống';
    if (id.permissions?.includes('roles.configure')) return isEn ? 'Company Administrator' : 'Quản trị viên Doanh nghiệp';
    if (id.permissions?.includes('tasks.assign')) return isEn ? 'Operational Manager' : 'Quản lý Vận hành';
    return isEn ? 'Authorized Member' : 'Thành viên';
  }

  hasPermission(perm: string): boolean {
    return this.session?.hasPermission(perm) ?? false;
  }

  openAiAgent(): void {
    this.aiAgent.open('chat');
  }

  signOut(): void {
    this.session?.clear();
    this.router.navigate(['/login']);
  }
}
