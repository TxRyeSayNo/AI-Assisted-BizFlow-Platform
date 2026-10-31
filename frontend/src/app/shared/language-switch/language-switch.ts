import { Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nService } from '../../core/i18n/i18n.service';
import { LanguageCode } from '../../core/i18n/i18n.types';

@Component({
  selector: 'bf-language-switch',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './language-switch.html',
  styleUrls: ['./language-switch.scss'],
})
export class LanguageSwitchComponent {
  readonly i18n = inject(I18nService);
  private readonly elementRef = inject(ElementRef);

  readonly isOpen = signal(false);

  toggleDropdown(): void {
    this.isOpen.update((v) => !v);
  }

  closeDropdown(): void {
    this.isOpen.set(false);
  }

  selectLanguage(code: LanguageCode): void {
    this.i18n.setLanguage(code);
    this.closeDropdown();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target as Node)) {
      this.closeDropdown();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeDropdown();
  }
}
