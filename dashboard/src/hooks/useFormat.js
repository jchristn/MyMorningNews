import { useMemo } from 'react';
import { useOptionalAuth } from '../context/AuthContext.jsx';
import { useLocale } from '../i18n/index.js';
import {
  DATE_DEFAULTS,
  DATE_TIME_DEFAULTS,
  formatBytes,
  formatCompact,
  formatDate,
  formatDateTime,
  formatDuration,
  formatList,
  formatNumber,
  formatPercent,
  formatRelativeTime,
  formatSeconds,
  formatTime,
  formatUsd,
  TIME_DEFAULTS,
  withTimeZone
} from '../i18n/formatters.js';

// Formatters bound to the active locale. Dates and times show in the signed-in user's profile time zone when one is
// set (the zone emails use), else in the browser's zone.
export default function useFormat() {
  const { locale } = useLocale();
  const auth = useOptionalAuth();
  const timeZone = auth ? auth.timeZone : null;
  return useMemo(
    () => ({
      locale,
      timeZone,
      number: (v, o) => formatNumber(v, locale, o),
      percent: (v, d) => formatPercent(v, locale, d),
      date: (v, o) => formatDate(v, locale, withTimeZone(o, DATE_DEFAULTS, timeZone)),
      time: (v, o) => formatTime(v, locale, withTimeZone(o, TIME_DEFAULTS, timeZone)),
      dateTime: (v, o) => formatDateTime(v, locale, withTimeZone(o, DATE_TIME_DEFAULTS, timeZone)),
      relative: (v) => formatRelativeTime(v, locale),
      duration: (ms) => formatDuration(ms, locale),
      seconds: (s) => formatSeconds(s, locale),
      bytes: (b) => formatBytes(b, locale),
      list: (items, type) => formatList(items, locale, type),
      usd: (v) => formatUsd(v, locale),
      compact: (v) => formatCompact(v, locale)
    }),
    [locale, timeZone]
  );
}
