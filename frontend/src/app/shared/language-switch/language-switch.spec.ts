import { TestBed } from '@angular/core/testing';
import { LanguageSwitchComponent } from './language-switch';
import { I18nService } from '../../core/i18n/i18n.service';

describe('LanguageSwitchComponent', () => {
  let component: LanguageSwitchComponent;
  let service: I18nService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LanguageSwitchComponent],
      providers: [I18nService],
    }).compileComponents();

    const fixture = TestBed.createComponent(LanguageSwitchComponent);
    component = fixture.componentInstance;
    service = TestBed.inject(I18nService);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('renders active Vietnamese language item', () => {
    const fixture = TestBed.createComponent(LanguageSwitchComponent);
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('.lang-pill');
    expect(buttons.length).toBe(1);
    expect(service.currentLanguage()).toBe('vi');
  });

  it('toggles dropdown open state and closes on selection', () => {
    expect(component.isOpen()).toBe(false);
    component.toggleDropdown();
    expect(component.isOpen()).toBe(true);
    component.selectLanguage('vi');
    expect(component.isOpen()).toBe(false);
  });

  it('closes dropdown when onEscape is called', () => {
    component.toggleDropdown();
    expect(component.isOpen()).toBe(true);
    component.onEscape();
    expect(component.isOpen()).toBe(false);
  });
});
