import { afterAll, describe, expect, it } from 'vitest';
import i18next, { initI18n, setActiveLocale } from './index.js';
import { LOCALE_STORAGE_KEY, SUPPORTED_LOCALES, intlLocale, localeDirection, normalizeLocale } from './localeRegistry.js';
import { formatBytes, formatDuration, formatList, formatNumber, formatPercent, formatRelativeTime } from './formatters.js';
import { buildExpansionPseudo, buildRtlPseudo, transformLiterals } from './pseudo.js';
import en from './locales/en.json';

afterAll(async () => {
  await setActiveLocale('en');
});

describe('locale registry', () => {
  it('lists the 12 baseline locales and two pseudo-locales', () => {
    const codes = SUPPORTED_LOCALES.map((l) => l.code);
    expect(codes).toEqual(['en', 'es', 'pt', 'fr', 'it', 'de', 'zh', 'ar', 'ru', 'ms', 'hi', 'ja', 'qps-ploc', 'qps-rtl']);
  });

  it('normalizes regional tags and aliases', () => {
    expect(normalizeLocale('en-US')).toBe('en');
    expect(normalizeLocale('pt_BR')).toBe('pt');
    expect(normalizeLocale('zh-Hans')).toBe('zh');
    expect(normalizeLocale('kanji')).toBe('ja');
    expect(normalizeLocale('qps-ploc')).toBe('qps-ploc');
    expect(normalizeLocale('xx')).toBe('en');
    expect(normalizeLocale('')).toBe('en');
  });

  it('knows text direction and the Intl locale', () => {
    expect(localeDirection('ar')).toBe('rtl');
    expect(localeDirection('qps-rtl')).toBe('rtl');
    expect(localeDirection('de')).toBe('ltr');
    expect(intlLocale('qps-ploc')).toBe('en');
    expect(intlLocale('ja')).toBe('ja');
  });
});

describe('formatters', () => {
  it('format numbers with the given locale', () => {
    expect(formatNumber(1234.5, 'en')).toBe('1,234.5');
    expect(formatNumber(1234.5, 'de')).toBe('1.234,5');
    expect(formatNumber(null, 'en')).toBe('');
    expect(formatPercent(0.25, 'en')).toBe('25%');
  });

  it('format durations, bytes, lists, and relative times', () => {
    expect(formatDuration(250, 'en')).toBe('250 ms');
    expect(formatDuration(1500, 'en')).toBe('1.5 sec');
    expect(formatBytes(2048, 'en')).toBe('2 kB');
    expect(formatList(['a', 'b', 'c'], 'en')).toBe('a, b, and c');
    const now = Date.parse('2026-01-01T12:00:00Z');
    expect(formatRelativeTime('2026-01-01T11:00:00Z', 'en', now)).toBe('1 hour ago');
  });
});

describe('pseudo-locales', () => {
  it('accent literals but keep placeholders and ICU blocks', () => {
    expect(transformLiterals('Hi {name}!', (s) => s.toUpperCase())).toBe('HI {name}!');
    const ploc = buildExpansionPseudo({ a: 'Copy of {name}', b: { c: '{count, plural, one {# item} other {# items}}' } });
    expect(ploc.a.startsWith('[')).toBe(true);
    expect(ploc.a).toContain('{name}');
    expect(ploc.a).not.toContain('Copy');
    expect(ploc.b.c).toContain('{count, plural, one {# item} other {# items}}');
    expect(buildRtlPseudo({ a: 'x' }).a).toBe('‫x‬');
  });
});

describe('i18n runtime', () => {
  it('formats ICU messages with plurals', () => {
    expect(i18next.t('json.items', { count: 1 })).toBe('1 item');
    expect(i18next.t('json.items', { count: 3 })).toBe('3 items');
    expect(i18next.t('admin.copyOf', { name: 'Acme' })).toBe('Copy of Acme');
  });

  it('switches to the RTL pseudo-locale and sets the document direction', async () => {
    await setActiveLocale('qps-rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(window.localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('qps-rtl');
    expect(i18next.t('common.save')).toBe('‫' + en.common.save + '‬');
    expect(i18next.t('admin.copyOf', { name: 'Acme' })).toBe('‫Copy of Acme‬');
    expect(i18next.t('json.items', { count: 2 })).toBe('‫2 items‬');
    await setActiveLocale('qps-ploc');
    expect(document.documentElement.dir).toBe('ltr');
    expect(i18next.t('common.save')).not.toBe(en.common.save);
    expect(i18next.t('admin.copyOf', { name: 'Acme' })).toContain('Acme');
  });

  it('loads translated catalogs lazily and sets the document language', async () => {
    await setActiveLocale('ja');
    expect(document.documentElement.lang).toBe('ja');
    expect(document.documentElement.dir).toBe('ltr');
    expect(i18next.t('common.save')).not.toBe(en.common.save);
    expect(i18next.t('admin.copyOf', { name: 'Acme' })).toContain('Acme');
    await setActiveLocale('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(i18next.t('common.save')).toMatch(/\p{Script=Arabic}/u);
    await initI18n('en');
    expect(i18next.language).toBe('en');
  });

  it('formats plurals in every translated locale', async () => {
    for (const locale of SUPPORTED_LOCALES.filter((l) => l.code !== 'en' && !l.pseudo)) {
      await setActiveLocale(locale.code);
      for (const count of [0, 1, 2, 3, 5, 11, 21, 100]) {
        const text = i18next.t('json.items', { count });
        expect([locale.code, count, text.includes(new Intl.NumberFormat(locale.code).format(count)) || text.includes(String(count)) || count < 3]).toEqual([locale.code, count, true]);
        expect(text).not.toMatch(/[{}]/);
      }
      expect(i18next.t('common.save')).not.toBe(en.common.save);
    }
    await setActiveLocale('en');
  });
});
