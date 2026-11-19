import fs from 'fs';
import path from 'path';
import { IntlMessageFormat } from 'intl-messageformat';
import { en, mobileCatalogs } from '@/i18n/catalog';
import { SUPPORTED_LOCALES, isRtl, normalizeLocale } from '@/i18n/locales';
import { flattenEntries, flattenKeys } from '@/i18n/pseudo';
import { initI18n } from '@/i18n/runtime';

const EM_DASH = String.fromCharCode(0x2014);
const srcDir = path.join(__dirname, '..');
const enKeys = new Set(flattenKeys(en).map((k) => 'mobile.' + k));

function sourceFiles(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((d) => {
    const full = path.join(dir, d.name);
    if (d.isDirectory()) return d.name === '__tests__' ? [] : sourceFiles(full);
    return /\.(js|jsx)$/.test(d.name) ? [full] : [];
  });
}

/** Literal keys ('mobile.x.y') and dynamic prefixes (t('mobile.x.' + name)) used anywhere in the app. */
function usage() {
  const literals = new Set();
  const prefixes = new Set();
  for (const file of sourceFiles(srcDir)) {
    if (file.includes(path.join('i18n', 'catalog'))) continue;
    const text = fs.readFileSync(file, 'utf8');
    for (const m of text.matchAll(/'(mobile\.[A-Za-z0-9_.]+)'/g)) {
      if (m[1].endsWith('.')) prefixes.add(m[1]);
      else literals.add(m[1]);
    }
  }
  return { literals: [...literals], prefixes: [...prefixes] };
}

/** ICU argument names of a message ({name}, {count, plural, ...}). */
function argumentsOf(message) {
  const names = new Set();
  const walk = (elements) => {
    for (const el of elements) {
      if (el.value && typeof el.value === 'string' && el.type !== 0) names.add(el.value);
      if (el.options) for (const option of Object.values(el.options)) walk(option.value);
    }
  };
  walk(new IntlMessageFormat(message, 'en').getAst());
  return [...names].sort();
}

describe('mobile i18n catalog', () => {
  const { literals, prefixes } = usage();

  it('has a catalog for every supported locale, including the pseudo-locales', () => {
    expect(Object.keys(mobileCatalogs).sort()).toEqual(SUPPORTED_LOCALES.map((l) => l.code).sort());
  });

  it('defines every key the app uses (no missing keys)', () => {
    expect(literals.filter((k) => !enKeys.has(k))).toEqual([]);
    for (const prefix of prefixes) expect([prefix, [...enKeys].some((k) => k.startsWith(prefix))]).toEqual([prefix, true]);
  });

  it('uses every English key (no orphan keys)', () => {
    const used = new Set(literals);
    const orphans = [...enKeys].filter((k) => !used.has(k) && !prefixes.some((p) => k.startsWith(p)));
    expect(orphans).toEqual([]);
  });

  it.each(Object.keys(mobileCatalogs).filter((c) => c !== 'en' && !c.startsWith('qps')))('%s has no keys English lacks', (code) => {
    expect(flattenKeys(mobileCatalogs[code]).filter((k) => !enKeys.has('mobile.' + k))).toEqual([]);
  });

  it('writes valid ICU messages in every catalog', () => {
    for (const [code, catalog] of Object.entries(mobileCatalogs)) {
      for (const [key, value] of flattenEntries(catalog)) {
        expect(() => argumentsOf(value)).not.toThrow(code + ' ' + key);
      }
    }
  });

  it.each(Object.keys(mobileCatalogs).filter((c) => c !== 'en'))('%s keeps every placeholder of the English message', (code) => {
    const english = new Map(flattenEntries(en));
    for (const [key, value] of flattenEntries(mobileCatalogs[code])) {
      expect([key, argumentsOf(value)]).toEqual([key, argumentsOf(english.get(key))]);
    }
  });

  it('uses no em-dashes in any catalog', () => {
    for (const [code, catalog] of Object.entries(mobileCatalogs)) {
      expect([code, JSON.stringify(catalog).includes(EM_DASH)]).toEqual([code, false]);
    }
  });

  it('resolves locales and directions like the dashboard registry', () => {
    expect(normalizeLocale('pt-BR')).toBe('pt');
    expect(normalizeLocale('zh-Hans')).toBe('zh');
    expect(normalizeLocale('qps-ploc')).toBe('qps-ploc');
    expect(normalizeLocale('xx')).toBe('en');
    expect(isRtl('ar')).toBe(true);
    expect(isRtl('qps-rtl')).toBe(true);
    expect(isRtl('en')).toBe(false);
  });

  it('formats ICU plurals, falls back to English, and builds the pseudo-locales', async () => {
    const i18n = initI18n('en');
    expect(i18n.t('mobile.usage.lastDays', { count: 1 })).toBe('Last 1 day');
    expect(i18n.t('mobile.usage.lastDays', { count: 30 })).toBe('Last 30 days');
    expect(i18n.t('mobile.states.notAvailableFeature', { feature: 'Briefings' })).toBe('Briefings will be available after a later server update.');
    await i18n.changeLanguage('es');
    expect(i18n.t('mobile.tabs.today')).toBe('Hoy');
    expect(i18n.t('mobile.signIn.submit')).toBe('Sign in');
    await i18n.changeLanguage('qps-ploc');
    expect(i18n.t('mobile.tabs.today')).toMatch(/^\[Ŧǿḓȧẏ~+\]$/);
    expect(i18n.t('mobile.usage.lastDays', { count: 2 })).toContain('2');
    await i18n.changeLanguage('qps-rtl');
    expect(i18n.t('mobile.tabs.today')).toBe('‫Today‬');
    await i18n.changeLanguage('en');
  });
});
