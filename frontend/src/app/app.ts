import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from './core/auth/session';
import { AiAssistantComponent } from './shared/ai/ai-assistant';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { RobotWidgetComponent } from './shared/ai/robot-widget';
import { ToastContainerComponent } from './core/toast/toast-container.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AiAssistantComponent,
    TranslatePipe,
    RobotWidgetComponent,
    ToastContainerComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly session = inject(SessionService, { optional: true });
  private readonly router = inject(Router);

  readonly isAiOpen = signal(false);
  readonly avatarFailed = signal(false);

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

  currentRoleLabel(): string {
    const id = this.identity();
    if (!id?.tenantId) return 'Platform Administrator';
    if (id.permissions?.includes('roles.configure')) return 'Company Administrator';
    if (id.permissions?.includes('tasks.assign')) return 'Operational Manager';
    return 'Authorized Member';
  }

  hasPermission(perm: string): boolean {
    return this.session?.hasPermission(perm) ?? false;
  }

  signOut(): void {
    this.session?.clear();
    this.router.navigate(['/login']);
  }
}
