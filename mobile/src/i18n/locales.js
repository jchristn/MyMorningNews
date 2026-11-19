// Supported locales, mirroring dashboard/src/i18n/localeRegistry.js (the 12 baseline locales plus the two
// pseudo-locales). When the dashboard's catalog moves to dashboard/src/shared (sharing.config.js), this file imports
// the registry from there instead.
export const DEFAULT_LOCALE = 'en';

export const SUPPORTED_LOCALES = [
  { code: 'en', englishName: 'English', nativeName: 'English', dir: 'ltr' },
  { code: 'es', englishName: 'Spanish', nativeName: 'Español', dir: 'ltr' },
  { code: 'pt', englishName: 'Portuguese', nativeName: 'Português', dir: 'ltr' },
  { code: 'fr', englishName: 'French', nativeName: 'Français', dir: 'ltr' },
  { code: 'it', englishName: 'Italian', nativeName: 'Italiano', dir: 'ltr' },
  { code: 'de', englishName: 'German', nativeName: 'Deutsch', dir: 'ltr' },
  { code: 'zh', englishName: 'Chinese (Simplified)', nativeName: '中文', dir: 'ltr' },
  { code: 'ar', englishName: 'Arabic', nativeName: 'العربية', dir: 'rtl' },
  { code: 'ru', englishName: 'Russian', nativeName: 'Русский', dir: 'ltr' },
  { code: 'ms', englishName: 'Malay', nativeName: 'Bahasa Melayu', dir: 'ltr' },
  { code: 'hi', englishName: 'Hindi', nativeName: 'हिन्दी', dir: 'ltr' },
  { code: 'ja', englishName: 'Japanese', nativeName: '日本語', dir: 'ltr' },
  { code: 'qps-ploc', englishName: 'Pseudo (expansion)', nativeName: 'Ƥşḗŭḓǿ ḗẋƥȧƞşīǿƞ', dir: 'ltr', pseudo: true },
  { code: 'qps-rtl', englishName: 'Pseudo (right to left)', nativeName: 'Pseudo RTL', dir: 'rtl', pseudo: true },
];

const ALIASES = {
  'zh-cn': 'zh',
  'zh-hans': 'zh',
  'zh-sg': 'zh',
  'pt-br': 'pt',
  'pt-pt': 'pt',
};

/** Canonicalize any BCP 47 tag or alias to a supported locale code. */
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

export function isRtl(code) {
  const entry = SUPPORTED_LOCALES.find((l) => l.code === normalizeLocale(code));
  return Boolean(entry && entry.dir === 'rtl');
}

/** The locale handed to Intl formatters: pseudo-locales format like English. */
export function intlLocale(code) {
  const normalized = normalizeLocale(code);
  return normalized.startsWith('qps-') ? 'en' : normalized;
}
