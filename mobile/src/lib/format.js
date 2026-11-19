import { intlLocale } from '@/i18n/locales';

// Locale-explicit formatters (the dashboard's formatters.js rules): every function takes the UI locale, and the
// pseudo-locales format as English.

export function formatNumber(value, locale, options) {
  const n = Number(value);
  if (!Number.isFinite(n)) return '0';
  try {
    return new Intl.NumberFormat(intlLocale(locale), options).format(n);
  } catch {
    return String(n);
  }
}

/** US dollars with cents, or four decimals for amounts under a cent (model calls are cheap). */
export function formatCost(value, locale) {
  const n = Number(value) || 0;
  const digits = n !== 0 && Math.abs(n) < 0.01 ? 4 : 2;
  return formatNumber(n, locale, { style: 'currency', currency: 'USD', minimumFractionDigits: digits, maximumFractionDigits: digits });
}

/** Compact counts (1.2K, 3.4M) for token totals. */
export function formatCompact(value, locale) {
  return formatNumber(value, locale, { notation: 'compact', maximumFractionDigits: 1 });
}

/** Seconds as m:ss or h:mm:ss (player clock). Negative and non-finite values show 0:00. */
export function formatClock(seconds) {
  const total = Number.isFinite(seconds) && seconds > 0 ? Math.floor(seconds) : 0;
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const ss = String(s).padStart(2, '0');
  return h > 0 ? h + ':' + String(m).padStart(2, '0') + ':' + ss : m + ':' + ss;
}

export function formatBytes(bytes, locale) {
  const n = Number(bytes) || 0;
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = n;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return formatNumber(value, locale, { maximumFractionDigits: unit === 0 ? 0 : 1 }) + ' ' + units[unit];
}

export function formatDate(value, locale, options = { dateStyle: 'medium' }) {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  try {
    return new Intl.DateTimeFormat(intlLocale(locale), options).format(date);
  } catch {
    return date.toISOString().slice(0, 10);
  }
}

export function formatDateTime(value, locale) {
  return formatDate(value, locale, { dateStyle: 'medium', timeStyle: 'short' });
}

/** Full name from a user record, falling back to the email. */
export function displayName(user) {
  if (!user) return '';
  const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return name || user.email || '';
}
