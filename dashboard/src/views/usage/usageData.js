// Shaping helpers for usage reports (GET /v1.0/api/usage/report rows and totals) and the usage time series.

// The key the server gives the combined remainder of a grouped time series (groups beyond the requested top).
export const OTHER_KEY = '(other)';

// What the trend chart can plot. Each is a field of a time bucket and of the per-group rows inside it.
export const TREND_MEASURES = ['cost', 'calls', 'tokens', 'cacheWriteTokens', 'reasoningTokens', 'toolCalls', 'ttft'];

// Groupings a tenant's trend chart offers; an empty value is a single line for everything.
export const TREND_GROUPS = ['', 'User', 'Model', 'Task', 'Topic', 'Provider', 'PromptKey'];

// How many groups a grouped trend draws before the server combines the rest as "other".
export const TREND_TOP = 6;

export const SERIES_COLORS = ['var(--chart-series-1)', 'var(--chart-series-2)', 'var(--chart-series-3)', 'var(--chart-series-5)', 'var(--chart-series-6)', 'var(--chart-series-7)'];
export const OTHER_COLOR = 'var(--chart-series-8)';

// One measure of a bucket or of a group row. Time to first token is the 95th percentile, the figure that shows a slow tail.
export function measureValue(row, measure) {
  if (!row) return 0;
  switch (measure) {
    case 'calls':
      return Number(row.calls) || 0;
    case 'tokens':
      return (Number(row.inputTokens) || 0) + (Number(row.outputTokens) || 0);
    case 'cacheWriteTokens':
      return Number(row.cacheWriteTokens) || 0;
    case 'reasoningTokens':
      return Number(row.reasoningTokens) || 0;
    case 'toolCalls':
      return Number(row.toolCalls) || 0;
    case 'ttft':
      return Number(row.p95TimeToFirstTokenMs) || 0;
    default:
      return Number(row.costUsd) || 0;
  }
}

// The lines (or stacks) of a time series for one measure: one per group when the series is grouped, in the server's
// order with "other" last; otherwise a single series, or average and p95 for time to first token.
export function trendSeries(series, measure, t) {
  const buckets = (series && series.buckets) || [];
  const groups = (series && series.groups) || [];
  if (series && series.groupBy && groups.length > 0) {
    return groups.map((g, i) => ({
      key: g.key,
      label: rowLabel(g, series.groupBy, t),
      color: g.key === OTHER_KEY ? OTHER_COLOR : SERIES_COLORS[i % SERIES_COLORS.length],
      values: buckets.map((b) => measureValue((b.rows || []).find((r) => r.key === g.key), measure))
    }));
  }
  if (measure === 'ttft') {
    return [
      { key: 'avg', label: t('usage.series.average'), color: 'var(--chart-series-1)', values: buckets.map((b) => Number(b.averageTimeToFirstTokenMs) || 0) },
      { key: 'p95', label: t('usage.series.p95'), color: 'var(--chart-series-3)', values: buckets.map((b) => Number(b.p95TimeToFirstTokenMs) || 0) }
    ];
  }
  return [{ key: measure, label: measureLabel(measure, t), color: 'var(--chart-series-1)', values: buckets.map((b) => measureValue(b, measure)) }];
}

export function measureLabel(measure, t) {
  if (measure === 'ttft') return t('usage.trend.ttft');
  return t('usage.columns.' + measure);
}

// The display label of a report row. The server labels users (email) and tenants (name); other groups use the key,
// with placeholders for the server's sentinel keys.
export function rowLabel(row, groupBy, t) {
  const key = row.key;
  if (key === OTHER_KEY) return t('usage.labels.other');
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
