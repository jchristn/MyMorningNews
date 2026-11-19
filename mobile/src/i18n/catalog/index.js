import { ar } from './ar';
import { de } from './de';
import { en } from './en';
import { es } from './es';
import { fr } from './fr';
import { hi } from './hi';
import { it } from './it';
import { ja } from './ja';
import { ms } from './ms';
import { pt } from './pt';
import { ru } from './ru';
import { zh } from './zh';
import { buildExpansionPseudo, buildRtlPseudo } from '../pseudo';

export { en };

/** The app's own strings per locale (the `mobile` subtree), including the two pseudo-locales derived from English. */
export const mobileCatalogs = {
  en,
  es,
  pt,
  fr,
  it,
  de,
  zh,
  ar,
  ru,
  ms,
  hi,
  ja,
  'qps-ploc': buildExpansionPseudo(en),
  'qps-rtl': buildRtlPseudo(en),
};

/**
 * i18next resources: every locale's strings under `mobile.*` in the default namespace. When the dashboard catalog is
 * shared (sharing.config.js), its translations merge in beside `mobile`.
 */
export function buildResources() {
  const out = {};
  for (const [code, catalog] of Object.entries(mobileCatalogs)) out[code] = { translation: { mobile: catalog } };
  return out;
}
