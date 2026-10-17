import { Injectable, signal, computed } from '@angular/core';
import { LanguageCode, LanguageOption, SUPPORTED_LANGUAGES } from './i18n.types';
import { TRANSLATIONS } from './translations';

const STORAGE_KEY = 'bizflow_language';

@Injectable({
  providedIn: 'root',
})
export class I18nService {
  readonly supportedLanguages: LanguageOption[] = SUPPORTED_LANGUAGES;

  private readonly langSignal = signal<LanguageCode>(this.getInitialLanguage());

  readonly currentLanguage = this.langSignal.asReadonly();

  readonly currentLanguageOption = computed(() => {
    const code = this.currentLanguage();
    return this.supportedLanguages.find((l) => l.code === code) ?? this.supportedLanguages[0];
  });

  constructor() {
    this.updateHtmlLang(this.langSignal());
  }

  setLanguage(lang: LanguageCode): void {
    if (lang !== 'en' && lang !== 'vi' && lang !== 'ja') {
      return;
    }
    this.langSignal.set(lang);
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        localStorage.setItem(STORAGE_KEY, lang);
      } catch {
        // Ignore quota/security errors
      }
    }
    this.updateHtmlLang(lang);
  }

  t(key: string, fallback?: string): string {
    const lang = this.currentLanguage();
    const table = TRANSLATIONS[lang];
    if (table && key in table) {
      return table[key];
    }
    // Fallback to English if not in current language
    if (lang !== 'en' && TRANSLATIONS.en[key]) {
      return TRANSLATIONS.en[key];
    }
    return fallback ?? key;
  }

  private getInitialLanguage(): LanguageCode {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        const saved = localStorage.getItem(STORAGE_KEY) as LanguageCode;
        if (saved === 'en' || saved === 'vi' || saved === 'ja') {
          return saved;
        }
      } catch {
        // Fallback
      }
    }
    return 'en';
  }

  private updateHtmlLang(lang: LanguageCode): void {
    if (typeof document !== 'undefined' && document.documentElement) {
      document.documentElement.lang = lang;
    }
  }
}
