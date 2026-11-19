// CSV export. RFC 4180 quoting: fields containing a comma, quote, or line break are quoted and quotes doubled.
// Values that a spreadsheet would treat as a formula are prefixed with a single quote.
export function csvField(value) {
  if (value === null || value === undefined) return '';
  let text = value instanceof Date ? value.toISOString() : String(value);
  if (/^[=+\-@\t\r]/.test(text) && !/^-?\d+(\.\d+)?$/.test(text)) text = "'" + text;
  return /[",\r\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
}

// columns: [{ key, header, value?: (row) => any }]
export function toCsv(rows, columns) {
  const header = columns.map((c) => csvField(c.header)).join(',');
  const body = (rows || []).map((row) => columns.map((c) => csvField(c.value ? c.value(row) : row[c.key])).join(','));
  return [header, ...body].join('\r\n') + '\r\n';
}

export function downloadText(filename, text, type = 'text/csv;charset=utf-8') {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

// The columns of a usage report row, in export order. Header text is stable English so exported files are
// comparable across locales.
export const USAGE_CSV_COLUMNS = [
  { key: 'key', header: 'key' },
  { key: 'label', header: 'label' },
  { key: 'calls', header: 'calls' },
  { key: 'failures', header: 'failures' },
  { key: 'inputTokens', header: 'inputTokens' },
  { key: 'cachedInputTokens', header: 'cachedInputTokens' },
  { key: 'cacheWriteTokens', header: 'cacheWriteTokens' },
  { key: 'outputTokens', header: 'outputTokens' },
  { key: 'reasoningTokens', header: 'reasoningTokens' },
  { key: 'totalTokens', header: 'totalTokens' },
  { key: 'toolCalls', header: 'toolCalls' },
  { key: 'costUsd', header: 'costUsd' },
  { key: 'costShare', header: 'costShare' },
  { key: 'averageDurationMs', header: 'averageDurationMs' },
  { key: 'averageTimeToFirstTokenMs', header: 'averageTimeToFirstTokenMs' },
  { key: 'p95TimeToFirstTokenMs', header: 'p95TimeToFirstTokenMs' }
];

export function usageReportCsv(report) {
  if (!report) return toCsv([], USAGE_CSV_COLUMNS);
  const rows = [...(report.rows || []), { ...(report.totals || {}), key: 'total', label: 'Total' }];
  return toCsv(rows, USAGE_CSV_COLUMNS);
}

export function usageCsvFilename(prefix, groupBy, fromUtc, toUtc) {
  const day = (v) => (v ? String(v).slice(0, 10) : '');
  return [prefix, String(groupBy || '').toLowerCase(), day(fromUtc), day(toUtc)].filter(Boolean).join('-') + '.csv';
}
