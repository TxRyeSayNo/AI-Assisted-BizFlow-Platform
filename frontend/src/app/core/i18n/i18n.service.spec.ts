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

  it('supports pure Vietnamese (vi) language', () => {
    const codes = service.supportedLanguages.map((l) => l.code);
    expect(codes).toEqual(['vi']);
    expect(service.currentLanguage()).toBe('vi');
    expect(service.currentLanguageOption().label).toBe('Tiếng Việt');
  });

  it('translates keys accurately in Vietnamese', () => {
    service.setLanguage('vi');
    expect(service.t('nav.tasks')).toBe('Nhiệm vụ');
    expect(service.t('auth.signIn')).toBe('Đăng nhập');
  });

  it('TranslatePipe transforms keys using I18nService', () => {
    TestBed.runInInjectionContext(() => {
      const pipe = new TranslatePipe();
      service.setLanguage('vi');
      expect(pipe.transform('common.search')).toBe('Tìm kiếm');
    });
  });

  it('falls back to default fallback text when key is not found', () => {
    expect(service.t('unknown.key.random', 'Fallback Text')).toBe('Fallback Text');
  });

  it('covers all feature modules in Vietnamese', () => {
    const moduleKeys = [
      'tasks.title',
      'taskCreate.title',
      'taskDetail.breadcrumb',
      'requests.title',
      'requestCreate.title',
      'requestDetail.submit',
      'approvals.title',
      'slaMonitor.title',
      'reports.title',
      'notifications.title',
      'audit.workspaceTitle',
      'search.title',
      'settings.title',
      'departments.title',
      'users.title',
      'roles.title',
      'services.title',
      'workflows.title',
      'sla.title',
      'platform.title',
      'comments.title',
      'attachments.title',
      'accessDenied.title',
      'ai.drawerTitle',
    ];

    service.setLanguage('vi');
    for (const key of moduleKeys) {
      const translated = service.t(key, '__MISSING__');
      expect(translated).not.toBe('__MISSING__');
      expect(translated.length).toBeGreaterThan(0);
    }
  });
});
