import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';
import { TranslatePipe } from './translate.pipe';

describe('I18nService & TranslatePipe', () => {
  let service: I18nService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [I18nService],
    });
    service = TestBed.inject(I18nService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('supports en, vi, and ja languages', () => {
    const codes = service.supportedLanguages.map((l) => l.code);
    expect(codes).toContain('vi');
    expect(codes).toContain('en');
    expect(codes).toContain('ja');
  });

  it('allows switching languages and updates signal', () => {
    service.setLanguage('en');
    expect(service.currentLanguage()).toBe('en');
    expect(service.currentLanguageOption().label).toBe('English');

    service.setLanguage('ja');
    expect(service.currentLanguage()).toBe('ja');
    expect(service.currentLanguageOption().label).toBe('Japanese');

    service.setLanguage('vi');
    expect(service.currentLanguage()).toBe('vi');
    expect(service.currentLanguageOption().label).toBe('Tiếng Việt');
  });

  it('translates keys across languages accurately', () => {
    service.setLanguage('en');
    expect(service.t('nav.tasks')).toBe('Tasks');
    expect(service.t('auth.signIn')).toBe('Sign in');

    service.setLanguage('vi');
    expect(service.t('nav.tasks')).toBe('Nhiệm vụ & Công việc');
    expect(service.t('auth.signIn')).toBe('Đăng nhập');

    service.setLanguage('ja');
    expect(service.t('nav.tasks')).toBe('タスク一覧');
    expect(service.t('auth.signIn')).toBe('サインイン');
  });

  it('TranslatePipe transforms keys using I18nService', () => {
    TestBed.runInInjectionContext(() => {
      const pipe = new TranslatePipe();
      service.setLanguage('vi');
      expect(pipe.transform('common.search')).toBe('Tìm kiếm');

      service.setLanguage('en');
      expect(pipe.transform('common.search')).toBe('Search');

      service.setLanguage('ja');
      expect(pipe.transform('common.search')).toBe('検索');
    });
  });

  it('falls back to default fallback text when key is not found', () => {
    expect(service.t('unknown.key.random', 'Fallback Text')).toBe('Fallback Text');
  });
});
