// Shaping helpers for usage reports (GET /v1.0/api/usage/report rows and totals).

// The display label of a report row. The server labels users (email) and tenants (name); other groups use the key,
// with placeholders for the server's sentinel keys.
export function rowLabel(row, groupBy, t) {
  const key = row.key;
  if (groupBy === 'User' && key === '(system)') return t('usage.labels.system');
  if (key === '(none)') return groupBy === 'Topic' ? t('usage.labels.noTopic') : t('usage.labels.none');
  if (key === '' || key === null || key === undefined) return t('usage.labels.unknown');
  if (groupBy === 'Task') return t('enums.task.' + key, { defaultValue: key });
  if (groupBy === 'Provider') return t('enums.provider.' + key, { defaultValue: key });
  return row.label || key;
}

// Token types for the mix chart. Output includes reasoning on the server, so reasoning is split out; uncached input
// is what remains after cache reads and cache writes.
export function tokenMix(row) {
  const input = Number(row.inputTokens) || 0;
  const cached = Number(row.cachedInputTokens) || 0;
  const cacheWrite = Number(row.cacheWriteTokens) || 0;
  const output = Number(row.outputTokens) || 0;
  const reasoning = Number(row.reasoningTokens) || 0;
  return {
    uncachedInput: Math.max(0, input - cached - cacheWrite),
    cachedInput: cached,
    cacheWrite,
    output: Math.max(0, output - reasoning),
    reasoning
  };
}

export const TOKEN_TYPES = [
  { key: 'uncachedInput', color: 'var(--chart-series-1)' },
  { key: 'cachedInput', color: 'var(--chart-series-6)' },
  { key: 'cacheWrite', color: 'var(--chart-series-3)' },
  { key: 'output', color: 'var(--chart-series-2)' },
  { key: 'reasoning', color: 'var(--chart-series-5)' }
];

export function topRows(report, limit) {
  return ((report && report.rows) || []).slice(0, limit);
}

// Remove empty filter values so the query string stays short.
export function cleanQuery(query) {
  const out = {};
  for (const [k, v] of Object.entries(query || {})) {
    if (v === '' || v === null || v === undefined || v === false) continue;
    out[k] = v;
  }
  return out;
}

export function failureRate(row) {
  const calls = Number(row && row.calls) || 0;
  return calls > 0 ? (Number(row.failures) || 0) / calls : 0;
}
