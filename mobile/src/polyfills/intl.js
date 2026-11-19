/**
 * Intl APIs that ICU message formatting (i18next-icu, intl-messageformat) needs and Hermes may not implement:
 * Intl.PluralRules and its prerequisites Intl.getCanonicalLocales and Intl.Locale. Each polyfill installs only when
 * the engine lacks the feature, so the cost is zero where it is native. Imported first by src/app/_layout.jsx.
 * Locale data covers every catalog locale; the pseudo-locales format as English.
 */
import '@formatjs/intl-getcanonicallocales/polyfill.js';
import '@formatjs/intl-locale/polyfill.js';
import '@formatjs/intl-pluralrules/polyfill.js';
import '@formatjs/intl-pluralrules/locale-data/en.js';
import '@formatjs/intl-pluralrules/locale-data/es.js';
import '@formatjs/intl-pluralrules/locale-data/pt.js';
import '@formatjs/intl-pluralrules/locale-data/fr.js';
import '@formatjs/intl-pluralrules/locale-data/it.js';
import '@formatjs/intl-pluralrules/locale-data/de.js';
import '@formatjs/intl-pluralrules/locale-data/zh.js';
import '@formatjs/intl-pluralrules/locale-data/ar.js';
import '@formatjs/intl-pluralrules/locale-data/ru.js';
import '@formatjs/intl-pluralrules/locale-data/ms.js';
import '@formatjs/intl-pluralrules/locale-data/hi.js';
import '@formatjs/intl-pluralrules/locale-data/ja.js';
