import { createInstance } from 'i18next';
import ICU from 'i18next-icu';
import { initReactI18next } from 'react-i18next';
import { I18nManager } from 'react-native';
import { getLocales } from 'expo-localization';
import { DEFAULT_LOCALE, intlLocale, isRtl, normalizeLocale } from './locales';
import { buildResources } from './catalog';

/**
 * The layout direction the app launched with. `I18nManager.isRTL` reflects a `forceRTL` call at once, but the layout
 * only flips on the next launch, so a pending change is measured against this.
 */
const LAUNCH_RTL = I18nManager.isRTL;

/** The device's preferred supported locale (resolution: explicit choice, then device, then English). */
export function deviceLocale() {
  const first = getLocales()[0];
  return normalizeLocale(first && first.languageTag ? first.languageTag : DEFAULT_LOCALE);
}

let instance = null;

/**
 * Initialize the app's i18next instance once, or switch its language. Messages use ICU syntax (`{name}`,
 * `{count, plural, ...}`) like the dashboard, so the shared dashboard catalog can join the same instance later.
 */
export function initI18n(locale) {
  const code = normalizeLocale(locale);
  if (!instance) {
    instance = createInstance();
    void instance
      .use(ICU)
      .use(initReactI18next)
      .init({
        resources: buildResources(),
        lng: code,
        fallbackLng: DEFAULT_LOCALE,
        // i18next would read qps-ploc as language plus script (qps-Ploc) and miss the catalog; every code is lowercase.
        lowerCaseLng: true,
        // ICU formats plurals and numbers through Intl, which rejects the pseudo-locale tags; format them as English.
        i18nFormat: { parseLngForICU: (lng) => intlLocale(lng) },
        interpolation: { escapeValue: false },
        react: { useSuspense: false },
        returnNull: false,
        initAsync: false,
      });
  } else if (instance.language !== code) {
    void instance.changeLanguage(code);
  }
  return instance;
}

/** Active UI language for Accept-Language, or null before initialization. */
export function currentLanguage() {
  return instance ? instance.language : null;
}

/**
 * Layout direction follows the locale (Arabic and the RTL pseudo-locale). React Native applies a direction change on
 * the next launch, so this returns true when a restart is needed.
 */
export function applyDirection(locale) {
  const rtl = isRtl(locale);
  I18nManager.allowRTL(true);
  if (I18nManager.isRTL !== rtl) {
    I18nManager.forceRTL(rtl);
    return true;
  }
  return false;
}

/** True when the launched layout direction differs from the one the locale asks for (restart pending). */
export function directionPending(locale) {
  return LAUNCH_RTL !== isRtl(locale);
}
