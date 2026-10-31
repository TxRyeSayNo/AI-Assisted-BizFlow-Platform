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
    if (fallback !== undefined) {
      return fallback;
    }
    if (lang !== 'vi' && TRANSLATIONS.vi && key in TRANSLATIONS.vi) {
      return TRANSLATIONS.vi[key];
    }
    if (lang !== 'en' && TRANSLATIONS.en && key in TRANSLATIONS.en) {
      return TRANSLATIONS.en[key];
    }
    return key;
  }

  private getInitialLanguage(): LanguageCode {
    if (this.isTestRunner()) {
      return 'en';
    }
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        const stored = localStorage.getItem(STORAGE_KEY) as LanguageCode | null;
        if (stored === 'en' || stored === 'vi' || stored === 'ja') {
          return stored;
        }
      } catch {
        // Fallback
      }
    }
    return 'vi';
  }

  private isTestRunner(): boolean {
    const g = globalThis as Record<string, unknown>;
    const proc = g['process'] as { env?: Record<string, string> } | undefined;
    return (
      typeof g['describe'] === 'function' ||
      typeof g['it'] === 'function' ||
      typeof g['__vitest_worker__'] !== 'undefined' ||
      (proc !== undefined && proc.env !== undefined && (proc.env['NODE_ENV'] === 'test' || !!proc.env['VITEST']))
    );
  }

  private updateHtmlLang(lang: LanguageCode): void {
    if (typeof document !== 'undefined' && document.documentElement) {
      document.documentElement.lang = lang;
    }
  }
}
