import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Feedback } from './feedback';

@Component({
  selector: 'bf-access-denied',
  imports: [Feedback, RouterLink],
  template: `<main id="main-content" class="page-content" tabindex="-1">
    <bf-feedback
      state="denied"
      title="Access unavailable"
      message="You do not have access to this page. Contact your company administrator if you need help."
    />
    <a routerLink="/login">Back to sign in</a>
  </main>`,
})
export class AccessDenied {}
