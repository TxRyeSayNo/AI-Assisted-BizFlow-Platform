import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { Feedback } from './feedback';

@Component({
  selector: 'bf-access-denied',
  imports: [Feedback, RouterLink, TranslatePipe],
  template: `<main id="main-content" class="page-content" tabindex="-1">
    <bf-feedback
      state="denied"
      [title]="'accessDenied.title' | translate: 'Access unavailable'"
      [message]="
        'accessDenied.message'
          | translate
            : 'You do not have access to this page. Contact your company administrator if you need help.'
      "
    />
    <a routerLink="/login">{{ 'accessDenied.back' | translate: 'Back to sign in' }}</a>
  </main>`,
})
export class AccessDenied {}
