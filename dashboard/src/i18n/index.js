import i18next from 'i18next';
import ICU from 'i18next-icu';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next, useTranslation } from 'react-i18next';
import { bundledResources, loadCatalog, NAMESPACE } from './resources.js';
import {
  DEFAULT_LOCALE,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  intlLocale,
  localeDirection,
  normalizeLocale,
  readStoredLocale,
  storeLocale
} from './localeRegistry.js';

function applyDocumentLocale(code) {
  if (typeof document === 'undefined') return;
  document.documentElement.lang = intlLocale(code);
  document.documentElement.dir = localeDirection(code);
}

async function ensureCatalog(code) {
  if (i18next.hasResourceBundle(code, NAMESPACE)) return;
  const catalog = await loadCatalog(code);
  i18next.addResourceBundle(code, NAMESPACE, catalog, true, true);
}

// Resolution order: explicit selection, persisted preference, browser preference, default.
export function resolveInitialLocale() {
  const stored = readStoredLocale();
  if (stored) return normalizeLocale(stored);
  if (typeof navigator !== 'undefined' && navigator.language) return normalizeLocale(navigator.language);
  return DEFAULT_LOCALE;
}

export async function initI18n(locale) {
  const initial = normalizeLocale(locale || resolveInitialLocale());
  if (!i18next.isInitialized) {
    await i18next
      .use(ICU)
      .use(LanguageDetector)
      .use(initReactI18next)
      .init({
        resources: bundledResources,
        lng: initial,
        fallbackLng: DEFAULT_LOCALE,
        // Without this i18next reads qps-ploc as language plus script (qps-Ploc) and misses the catalog.
        lowerCaseLng: true,
        // ICU formats plurals and numbers through Intl, which rejects the pseudo-locale tags; format them as English.
        i18nFormat: { parseLngForICU: (lng) => intlLocale(lng) },
        supportedLngs: SUPPORTED_LOCALES.map((l) => l.code),
        ns: [NAMESPACE],
        defaultNS: NAMESPACE,
        interpolation: { escapeValue: false },
        detection: { order: ['localStorage', 'navigator'], lookupLocalStorage: LOCALE_STORAGE_KEY, caches: [] },
        react: { useSuspense: false },
        returnNull: false
      });
  }
  await ensureCatalog(initial);
  await i18next.changeLanguage(initial);
  applyDocumentLocale(initial);
  return i18next;
}

export async function setActiveLocale(code) {
  const normalized = normalizeLocale(code);
  await ensureCatalog(normalized);
  await i18next.changeLanguage(normalized);
  storeLocale(normalized);
  applyDocumentLocale(normalized);
  return normalized;
}

export function useLocale() {
  const { i18n } = useTranslation();
  const locale = normalizeLocale(i18n.resolvedLanguage || i18n.language);
  return { locale, intl: intlLocale(locale), dir: localeDirection(locale), setLocale: setActiveLocale };
}

export default i18next;
