import { useMemo } from 'react';
import { useLocale } from '../i18n/index.js';
import {
  formatBytes,
  formatDate,
  formatDateTime,
  formatDuration,
  formatList,
  formatNumber,
  formatPercent,
  formatRelativeTime,
  formatSeconds,
  formatTime
} from '../i18n/formatters.js';

// Formatters bound to the active locale.
export default function useFormat() {
  const { locale } = useLocale();
  return useMemo(
    () => ({
      locale,
      number: (v, o) => formatNumber(v, locale, o),
      percent: (v, d) => formatPercent(v, locale, d),
      date: (v, o) => formatDate(v, locale, o),
      time: (v, o) => formatTime(v, locale, o),
      dateTime: (v, o) => formatDateTime(v, locale, o),
      relative: (v) => formatRelativeTime(v, locale),
      duration: (ms) => formatDuration(ms, locale),
      seconds: (s) => formatSeconds(s, locale),
      bytes: (b) => formatBytes(b, locale),
      list: (items, type) => formatList(items, locale, type)
    }),
    [locale]
  );
}
