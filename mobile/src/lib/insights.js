// Helpers for the Insights screens (Phase 10): report windows and analytics rows as chart rows. Pure, so the tests
// cover them directly.

/** Report windows offered on every Insights page, in days. The server defaults to 90. */
export const INSIGHT_RANGES = Object.freeze([30, 90, 365]);

/** A report window ending now: `{ from, to }` as ISO strings. */
export function insightWindow(days, now = Date.now()) {
  return { from: new Date(now - days * 86400000).toISOString(), to: new Date(now).toISOString() };
}

const num = (v) => (v === null || v === undefined || !Number.isFinite(Number(v)) ? null : Number(v));

/**
 * BreakdownRows (`{ key, label, count, positive, rate, value }`) as chart rows for one metric (`rate`, `count`,
 * `positive`, or `value`), labeled by `labelFor(row)` (default: label, then key). Rows without the metric are dropped;
 * `limit` keeps the largest.
 */
export function breakdownChartRows(rows, { metric = 'rate', labelFor, limit } = {}) {
  const list = (Array.isArray(rows) ? rows : [])
    .map((r) => ({ label: labelFor ? labelFor(r) : r.label || r.key || '', value: num(r[metric]), count: Number(r.count) || 0 }))
    .filter((r) => r.value !== null);
  if (!limit || list.length <= limit) return list;
  return list.sort((a, b) => b.count - a.count || b.value - a.value).slice(0, limit);
}

/** ByDay (or ByWeek) rows in time order as trend rows for one metric, labeled by `formatKey(key)`. */
export function trendRows(rows, { metric = 'rate', formatKey } = {}) {
  return (Array.isArray(rows) ? rows : [])
    .slice()
    .sort((a, b) => String(a.key).localeCompare(String(b.key)))
    .map((r) => ({ label: formatKey ? formatKey(r.key) : String(r.key), value: num(r[metric]) }));
}

/** Reason theme totals per tag from ReasonReport.ByWeek rows (`key` week, `label` tag), largest first. */
export function reasonTotals(report) {
  if (report && Array.isArray(report.tags) && report.tags.length) return report.tags.map((r) => ({ label: r.key, value: Number(r.count) || 0 }));
  const totals = {};
  for (const r of (report && report.byWeek) || []) totals[r.label || r.key] = (totals[r.label || r.key] || 0) + (Number(r.count) || 0);
  return Object.entries(totals)
    .map(([label, value]) => ({ label, value }))
    .sort((a, b) => b.value - a.value);
}

/** Suggestion outcomes as funnel rows (chart 19) in a fixed order. */
export const SUGGESTION_OUTCOMES = Object.freeze(['total', 'new', 'accepted', 'done', 'snoozed', 'dismissed']);

/** True when a report has nothing to show yet. */
export function isEmptyReport(name, report) {
  if (!report || typeof report !== 'object') return true;
  switch (name) {
    case 'overview':
      return !report.runs && !report.findingsShown && !report.feedbackTotal && !report.predictionsOpen && !report.predictionsResolved;
    case 'relevance':
      return !report.shown && !report.rated;
    case 'accuracy':
      return !report.total;
    case 'feedback':
      return !report.total;
    case 'suggestions':
      return !report.total;
    case 'quality':
      return !report.quotes;
    case 'reasons':
      return !(report.tags && report.tags.length) && !(report.byWeek && report.byWeek.length);
    default:
      return false;
  }
}
