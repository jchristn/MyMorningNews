// Shaping for the Insights pages (plan section 20). Pure functions over the analytics DTOs so they are tested
// without rendering.

export const INSIGHT_RANGES = [30, 90, 180, 365];
export const DAY_MS = 86400000;

// The analytics query for a scope: from and to as ISO instants, the topic, and all=true for the whole tenant.
export function insightsQuery({ days = 90, topicId = '', everyone = false } = {}, now = new Date()) {
  const to = new Date(now.getTime());
  const from = new Date(now.getTime() - Math.max(1, Number(days) || 90) * DAY_MS);
  return { from: from.toISOString(), to: to.toISOString(), topicId: topicId || undefined, all: everyone ? true : undefined };
}

// BreakdownRows keyed by day ('yyyy-MM-dd') as LineChart buckets, oldest first.
export function dayBuckets(rows) {
  return [...(rows || [])]
    .filter((r) => /^\d{4}-\d{2}-\d{2}$/.test(String(r.key)))
    .sort((a, b) => String(a.key).localeCompare(String(b.key)))
    .map((r) => {
      const start = new Date(r.key + 'T00:00:00Z');
      return { startUtc: start.toISOString(), endUtc: new Date(start.getTime() + DAY_MS).toISOString(), row: r };
    });
}

// Rate rows as horizontal bars: value is the rate (0 to 1); rows without a rate are dropped. labelOf maps a row
// to its display label.
export function rateBars(rows, labelOf = (r) => r.label || r.key, limit = 20) {
  return [...(rows || [])]
    .filter((r) => r.rate !== null && r.rate !== undefined)
    .sort((a, b) => (b.count || 0) - (a.count || 0))
    .slice(0, limit)
    .map((r) => ({ key: String(r.key), label: labelOf(r), value: Number(r.rate) || 0, count: r.count || 0 }));
}

// Evidence verification failure per model: 1 minus the verified share, worst first.
export function failureBars(rows, limit = 20) {
  return [...(rows || [])]
    .filter((r) => r.count > 0)
    .map((r) => ({ key: String(r.key), label: r.key, value: 1 - (r.rate !== null && r.rate !== undefined ? Number(r.rate) : (r.positive || 0) / r.count), count: r.count }))
    .sort((a, b) => b.value - a.value)
    .slice(0, limit);
}

// The server's calibration buckets in the CalibrationChart shape.
export function calibrationFromReport(buckets) {
  return (buckets || []).map((b, index) => ({
    index,
    from: Number(b.lower) || 0,
    to: Number(b.upper) || 0,
    count: b.count || 0,
    meanConfidence: b.meanConfidence === undefined ? null : b.meanConfidence,
    observedRate: b.observed === undefined ? null : b.observed
  }));
}

// Suggestion outcomes as funnel rows, in lifecycle order, with each share of the total.
export function suggestionOutcomes(report) {
  if (!report) return [];
  const total = report.total || 0;
  return ['new', 'accepted', 'done', 'snoozed', 'dismissed'].map((key) => ({ key, count: report[key] || 0, share: total > 0 ? (report[key] || 0) / total : 0 }));
}

// Reason themes per week as stacked-column input: weeks (oldest first) and the top tags as series.
export function reasonWeeks(report, topTags = 6) {
  const rows = (report && report.byWeek) || [];
  const weeks = [...new Set(rows.map((r) => r.key))].sort();
  const totals = new Map();
  for (const r of rows) totals.set(r.label, (totals.get(r.label) || 0) + (r.count || 0));
  const tags = [...totals.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, topTags)
    .map(([tag]) => tag);
  const series = tags.map((tag) => ({ tag, values: weeks.map((w) => rows.filter((r) => r.key === w && r.label === tag).reduce((s, r) => s + (r.count || 0), 0)) }));
  return { weeks, series };
}

// The topic weight fields a ranking recommendation maps onto.
export const WEIGHT_FIELDS = { llm: 'weightLlm', feedback: 'weightFeedback', novelty: 'weightNovelty', source: 'weightSource', recency: 'weightRecency' };

// The topic update body that applies recommended weights.
export function weightsBody(recommended) {
  const body = {};
  for (const [part, field] of Object.entries(WEIGHT_FIELDS)) {
    if (recommended && recommended[part] !== undefined && recommended[part] !== null) body[field] = Number(recommended[part]);
  }
  return body;
}

// Whether a topic now carries the given weights (within rounding).
export function weightsApplied(topic, body) {
  if (!topic) return false;
  return Object.entries(body).every(([field, value]) => topic[field] !== undefined && topic[field] !== null && Math.abs(Number(topic[field]) - value) < 0.0005);
}

// FeedbackReport.byChannelDay (key is the day, label is the channel) as stacked TimeChart input: day buckets (oldest
// first) and one series per channel, in the given order and then any other channel alphabetically.
export function channelDays(rows, order = []) {
  const valid = (rows || []).filter((r) => /^\d{4}-\d{2}-\d{2}$/.test(String(r.key)) && r.label);
  const days = [...new Set(valid.map((r) => String(r.key)))].sort();
  const seen = [...new Set(valid.map((r) => String(r.label)))];
  const channels = [...order.filter((c) => seen.includes(c)), ...seen.filter((c) => !order.includes(c)).sort()];
  const buckets = days.map((day) => {
    const start = new Date(day + 'T00:00:00Z');
    return { startUtc: start.toISOString(), endUtc: new Date(start.getTime() + DAY_MS).toISOString() };
  });
  const series = channels.map((channel) => ({
    channel,
    values: days.map((day) => valid.filter((r) => String(r.key) === day && String(r.label) === channel).reduce((sum, r) => sum + (r.count || 0), 0))
  }));
  return { buckets, series };
}
