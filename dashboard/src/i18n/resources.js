import en from './locales/en.json';
import { buildExpansionPseudo, buildRtlPseudo } from './pseudo.js';
import { SUPPORTED_LOCALES } from './localeRegistry.js';

export const NAMESPACE = 'translation';

// Catalog loaders. English is bundled. Pseudo-locales are generated from English. Other locales load lazily; until
// they are translated their catalogs are empty and i18next falls back to English key by key.
// TODO(Phase 12): add translated catalogs for es, pt, fr, it, de, zh, ar, ru, ms, hi, and ja under ./locales.
const loaders = {
  en: async () => en,
  'qps-ploc': async () => buildExpansionPseudo(en),
  'qps-rtl': async () => buildRtlPseudo(en)
};

for (const locale of SUPPORTED_LOCALES) {
  if (!loaders[locale.code]) loaders[locale.code] = async () => ({});
}

export async function loadCatalog(code) {
  const loader = loaders[code] || loaders.en;
  return loader();
}

export const bundledResources = { en: { [NAMESPACE]: en } };

export const sourceCatalog = en;
