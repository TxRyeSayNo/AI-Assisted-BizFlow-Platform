export type LanguageCode = 'vi';

export interface LanguageOption {
  code: LanguageCode;
  label: string;
  nativeName: string;
  flag: string;
}

export const SUPPORTED_LANGUAGES: LanguageOption[] = [
  { code: 'vi', label: 'Tiếng Việt', nativeName: 'Tiếng Việt', flag: '🇻🇳' },
];
