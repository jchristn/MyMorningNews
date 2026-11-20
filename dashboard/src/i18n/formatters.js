import { intlLocale } from './localeRegistry.js';

// Every formatter takes an explicit locale. Never call toLocaleString or Intl without one.
const cache = new Map();

function formatterFor(kind, locale, options) {
  const key = kind + '|' + locale + '|' + JSON.stringify(options);
  if (!cache.has(key)) {
    const Ctor = { number: Intl.NumberFormat, date: Intl.DateTimeFormat, relative: Intl.RelativeTimeFormat, list: Intl.ListFormat }[kind];
    cache.set(key, new Ctor(intlLocale(locale), options));
  }
  return cache.get(key);
}

function toDate(value) {
  if (value === null || value === undefined || value === '') return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatNumber(value, locale, options = {}) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '';
  return formatterFor('number', locale, options).format(Number(value));
}

export function formatPercent(value, locale, digits = 1) {
  return formatNumber(value, locale, { style: 'percent', maximumFractionDigits: digits, minimumFractionDigits: 0 });
}

export const DATE_DEFAULTS = { dateStyle: 'medium' };
export const TIME_DEFAULTS = { timeStyle: 'medium' };
export const DATE_TIME_DEFAULTS = { dateStyle: 'short', timeStyle: 'medium' };

// Options with a time zone added when one is given and the caller did not choose one.
export function withTimeZone(options, defaults, timeZone) {
  const base = options || defaults;
  return timeZone && !base.timeZone ? { ...base, timeZone } : base;
}

export function formatDate(value, locale, options = DATE_DEFAULTS) {
  const d = toDate(value);
  return d ? formatterFor('date', locale, options).format(d) : '';
}

export function formatTime(value, locale, options = TIME_DEFAULTS) {
  const d = toDate(value);
  return d ? formatterFor('date', locale, options).format(d) : '';
}

export function formatDateTime(value, locale, options = DATE_TIME_DEFAULTS) {
  const d = toDate(value);
  return d ? formatterFor('date', locale, options).format(d) : '';
}

const RELATIVE_UNITS = [
  ['year', 31536000],
  ['month', 2592000],
  ['week', 604800],
  ['day', 86400],
  ['hour', 3600],
  ['minute', 60],
  ['second', 1]
];

export function formatRelativeTime(value, locale, now = Date.now()) {
  const d = toDate(value);
  if (!d) return '';
  const seconds = Math.round((d.getTime() - now) / 1000);
  const abs = Math.abs(seconds);
  for (const [unit, size] of RELATIVE_UNITS) {
    if (abs >= size || unit === 'second') {
      return formatterFor('relative', locale, { numeric: 'auto' }).format(Math.round(seconds / size), unit);
    }
  }
  return '';
}

export function formatDuration(ms, locale) {
  if (ms === null || ms === undefined || ms === '') return '';
  const value = Number(ms);
  const unit = (u, v, digits) => formatNumber(v, locale, { style: 'unit', unit: u, unitDisplay: 'short', maximumFractionDigits: digits });
  if (value < 1000) return unit('millisecond', value, 1);
  if (value < 60000) return unit('second', value / 1000, 2);
  if (value < 3600000) return unit('minute', value / 60000, 1);
  if (value < 86400000) return unit('hour', value / 3600000, 1);
  return unit('day', value / 86400000, 1);
}

export function formatSeconds(seconds, locale) {
  return formatDuration(Number(seconds || 0) * 1000, locale);
}

const BYTE_UNITS = ['byte', 'kilobyte', 'megabyte', 'gigabyte', 'terabyte'];

export function formatBytes(bytes, locale) {
  let value = Number(bytes || 0);
  let index = 0;
  while (value >= 1024 && index < BYTE_UNITS.length - 1) {
    value /= 1024;
    index += 1;
  }
  return formatNumber(value, locale, { style: 'unit', unit: BYTE_UNITS[index], unitDisplay: 'short', maximumFractionDigits: index === 0 ? 0 : 1 });
}

export function formatList(items, locale, type = 'conjunction') {
  return formatterFor('list', locale, { style: 'long', type }).format((items || []).map(String));
}

// US dollars with the plain $ sign in every locale (some locales would print US$ or $US). Model costs are often
// fractions of a cent, so small values keep up to four decimals.
export function formatUsd(value, locale) {
  if (value === null || value === undefined || value === '' || Number.isNaN(Number(value))) return '';
  const n = Number(value);
  const small = n !== 0 && Math.abs(n) < 1;
  const text = formatNumber(n, locale, { style: 'currency', currency: 'USD', currencyDisplay: 'narrowSymbol', minimumFractionDigits: 2, maximumFractionDigits: small ? 4 : 2 });
  // Some locales (Arabic) keep "US$" even for the narrow symbol, which right-to-left text then shows as "$US".
  return text.replace('US$', '$');
}

// Large counts (tokens) in compact form: 12.3K, 4.5M.
export function formatCompact(value, locale) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '';
  return formatNumber(value, locale, { notation: 'compact', maximumFractionDigits: 1 });
}
