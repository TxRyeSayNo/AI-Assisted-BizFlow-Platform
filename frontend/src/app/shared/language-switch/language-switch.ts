import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nService } from '../../core/i18n/i18n.service';
import { LanguageCode } from '../../core/i18n/i18n.types';

@Component({
  selector: 'bf-language-switch',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="lang-switch-container" role="group" aria-label="Language selector">
      @for (lang of i18n.supportedLanguages; track lang.code) {
        <button
          type="button"
          class="lang-pill"
          [class.active]="i18n.currentLanguage() === lang.code"
          [attr.aria-pressed]="i18n.currentLanguage() === lang.code"
          [title]="lang.nativeName"
          (click)="selectLanguage(lang.code)"
        >
          <span class="lang-flag" aria-hidden="true">{{ lang.flag }}</span>
          <span class="lang-code">{{ lang.code.toUpperCase() }}</span>
        </button>
      }
    </div>
  `,
  styleUrls: ['./language-switch.scss'],
})
export class LanguageSwitchComponent {
  readonly i18n = inject(I18nService);

  selectLanguage(code: LanguageCode): void {
    this.i18n.setLanguage(code);
  }
}
