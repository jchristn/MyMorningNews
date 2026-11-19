export const LOCALE_STORAGE_KEY = 'mymorningnews.locale';
export const SOURCE_LOCALE = 'en';
export const DEFAULT_LOCALE = 'en';

// Supported locales. Adding a locale means adding an entry here and a catalog in ./locales; no logic changes.
export const SUPPORTED_LOCALES = [
  { code: 'en', englishName: 'English', nativeName: 'English', dir: 'ltr', fallback: 'en' },
  { code: 'es', englishName: 'Spanish', nativeName: 'Español', dir: 'ltr', fallback: 'en' },
  { code: 'pt', englishName: 'Portuguese', nativeName: 'Português', dir: 'ltr', fallback: 'en' },
  { code: 'fr', englishName: 'French', nativeName: 'Français', dir: 'ltr', fallback: 'en' },
  { code: 'it', englishName: 'Italian', nativeName: 'Italiano', dir: 'ltr', fallback: 'en' },
  { code: 'de', englishName: 'German', nativeName: 'Deutsch', dir: 'ltr', fallback: 'en' },
  { code: 'zh', englishName: 'Chinese (Simplified)', nativeName: '中文', dir: 'ltr', fallback: 'en', script: 'Hans' },
  { code: 'ar', englishName: 'Arabic', nativeName: 'العربية', dir: 'rtl', fallback: 'en' },
  { code: 'ru', englishName: 'Russian', nativeName: 'Русский', dir: 'ltr', fallback: 'en' },
  { code: 'ms', englishName: 'Malay', nativeName: 'Bahasa Melayu', dir: 'ltr', fallback: 'en' },
  { code: 'hi', englishName: 'Hindi', nativeName: 'हिन्दी', dir: 'ltr', fallback: 'en' },
  { code: 'ja', englishName: 'Japanese', nativeName: '日本語', dir: 'ltr', fallback: 'en' },
  { code: 'qps-ploc', englishName: 'Pseudo (expansion)', nativeName: 'Ƥşḗŭḓǿ ḗẋƥȧƞşīǿƞ', dir: 'ltr', fallback: 'en', pseudo: true },
  { code: 'qps-rtl', englishName: 'Pseudo (right to left)', nativeName: 'Pseudo RTL', dir: 'rtl', fallback: 'en', pseudo: true }
];

const ALIASES = {
  'zh-cn': 'zh',
  'zh-hans': 'zh',
  'zh-sg': 'zh',
  'pt-br': 'pt',
  'pt-pt': 'pt',
  kanji: 'ja'
};

// Canonicalize any BCP 47 tag or alias to a supported locale code.
export function normalizeLocale(value) {
  if (!value || typeof value !== 'string') return DEFAULT_LOCALE;
  const lower = value.trim().toLowerCase().replace('_', '-');
  if (ALIASES[lower]) return ALIASES[lower];
  const exact = SUPPORTED_LOCALES.find((l) => l.code === lower);
  if (exact) return exact.code;
  const base = lower.split('-')[0];
  if (ALIASES[base]) return ALIASES[base];
  const match = SUPPORTED_LOCALES.find((l) => l.code === base);
  return match ? match.code : DEFAULT_LOCALE;
}

export function localeDirection(code) {
  const entry = SUPPORTED_LOCALES.find((l) => l.code === normalizeLocale(code));
  return entry ? entry.dir : 'ltr';
}

// The locale handed to Intl formatters: pseudo-locales format like English.
export function intlLocale(code) {
  const normalized = normalizeLocale(code);
  return normalized.startsWith('qps-') ? 'en' : normalized;
}

export function readStoredLocale() {
  try {
    return window.localStorage.getItem(LOCALE_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function storeLocale(code) {
  try {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, code);
  } catch {
    // Storage may be unavailable (private mode); the locale still applies for this session.
  }
}
