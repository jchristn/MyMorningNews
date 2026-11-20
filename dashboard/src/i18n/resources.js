import en from './locales/en.json';
import { buildExpansionPseudo, buildRtlPseudo } from './pseudo.js';
import { SUPPORTED_LOCALES } from './localeRegistry.js';

export const NAMESPACE = 'translation';

// Translated catalogs, one file per locale under ./locales. Vite splits each into its own chunk, loaded the first
// time the locale is selected. English is bundled; the pseudo-locales are generated from English.
const translated = import.meta.glob(['./locales/*.json', '!./locales/en.json'], { import: 'default' });

const loaders = {
  en: async () => en,
  'qps-ploc': async () => buildExpansionPseudo(en),
  'qps-rtl': async () => buildRtlPseudo(en)
};

for (const locale of SUPPORTED_LOCALES) {
  if (loaders[locale.code]) continue;
  const load = translated['./locales/' + locale.code + '.json'];
  // A locale without a catalog falls back to English key by key; `npm run i18n:verify` fails when one is missing.
  loaders[locale.code] = load || (async () => ({}));
}

export async function loadCatalog(code) {
  const loader = loaders[code] || loaders.en;
  return loader();
}

export const bundledResources = { en: { [NAMESPACE]: en } };

export const sourceCatalog = en;
