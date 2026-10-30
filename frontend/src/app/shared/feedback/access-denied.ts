import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Feedback } from './feedback';

@Component({
  selector: 'bf-access-denied',
  standalone: true,
  imports: [Feedback, RouterLink, TranslatePipe],
  template: `<main id="main-content" class="page-content" tabindex="-1" style="max-width: 600px; margin: 60px auto; text-align: center; padding: 24px;">
    <bf-feedback
      state="denied"
      [title]="'accessDenied.title' | translate: 'Access unavailable'"
      [message]="
        'accessDenied.message'
          | translate
            : 'You do not have access to this page. Contact your company administrator if you need help.'
      "
    />
    <div style="margin-top: 24px;">
      @if (session.signedIn()) {
        <a routerLink="/workspace" style="display: inline-block; padding: 10px 24px; background: #2563eb; color: #fff; border-radius: 8px; text-decoration: none; font-weight: 500; box-shadow: 0 2px 4px rgba(37,99,235,0.2);">
          {{ 'accessDenied.backHome' | translate: 'Quay lại Không gian làm việc' }}
        </a>
      } @else {
        <a routerLink="/login" style="display: inline-block; padding: 10px 24px; background: #2563eb; color: #fff; border-radius: 8px; text-decoration: none; font-weight: 500; box-shadow: 0 2px 4px rgba(37,99,235,0.2);">
          {{ 'accessDenied.back' | translate: 'Back to sign in' }}
        </a>
      }
    </div>
  </main>`,
})
export class AccessDenied {
  readonly session = inject(SessionService);
}
