// Shaping for the run charts (plan section 25.6, charts 2 to 5 and 21 to 24) over GET /v1.0/api/analytics/runs.
// Pure functions so they are tested without rendering. Every function accepts a missing or partial report.
import { FINDING_TIERS, RUN_STATUSES } from '../../utils/enums.js';
import { DAY_MS, insightsQuery } from '../insights/insightsData.js';

// Range ids (ChartCard labels them charts.ranges.<id>) and the days each covers.
export const RUN_RANGE_DAYS = { week: 7, month: 30, quarter: 90 };
export const RUN_RANGE_IDS = Object.keys(RUN_RANGE_DAYS);

// Per-run column charts show at most this many of the most recent runs, so the labels stay readable.
export const MAX_RUN_COLUMNS = 20;

// Series colors for run statuses and finding tiers, theme-aware through the chart tokens.
export const RUN_STATUS_COLORS = {
  Completed: 'var(--chart-series-2)',
  PartiallySucceeded: 'var(--chart-series-3)',
  Failed: 'var(--chart-series-4)',
  Cancelled: 'var(--chart-series-8)',
  Skipped: 'var(--chart-series-7)',
  Running: 'var(--chart-series-1)',
  Queued: 'var(--chart-series-6)'
};
export const TIER_COLORS = { MustRead: 'var(--chart-series-5)', WorthALook: 'var(--chart-series-2)', Fyi: 'var(--chart-series-1)', BelowThreshold: 'var(--chart-series-8)' };
export const SOURCE_PARTS = [
  { key: 'used', color: 'var(--chart-series-2)' },
  { key: 'fetchedOnly', color: 'var(--chart-series-1)' },
  { key: 'examinedOnly', color: 'var(--chart-series-8)' }
];

// The browser's IANA time zone, or undefined when the runtime cannot say.
export function browserTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    return undefined;
  }
}

// The runs report query for a range id (or an explicit number of days), a topic, and the whole tenant for
// administrators, in a time zone.
export function runsQuery({ rangeId = 'month', days, topicId = '', everyone = false, timeZone } = {}, now = new Date()) {
  const span = Number(days) > 0 ? Number(days) : RUN_RANGE_DAYS[rangeId] || RUN_RANGE_DAYS.month;
  return { ...insightsQuery({ days: span, topicId, everyone }, now), timeZone: timeZone || undefined };
}

const count = (v) => Math.max(0, Number(v) || 0);

// Runs per day as TimeChart input: day buckets (oldest first) and one series per status that occurs, in lifecycle
// order. Completed and Failed are always present so the legend is stable.
export function statusDays(report) {
  const rows = (report && report.byDay) || [];
  const buckets = rows.map((r) => ({
    startUtc: r.startUtc || new Date(r.day + 'T00:00:00Z').toISOString(),
    endUtc: r.endUtc || new Date(new Date(r.day + 'T00:00:00Z').getTime() + DAY_MS).toISOString(),
    day: r.day
  }));
  const used = RUN_STATUSES.filter((s) => s === 'Completed' || s === 'Failed' || rows.some((r) => count(r.counts && r.counts[s]) > 0));
  const series = used.map((status) => ({ status, color: RUN_STATUS_COLORS[status], values: rows.map((r) => count(r.counts && r.counts[status])) }));
  return { buckets, series, total: rows.reduce((sum, r) => sum + count(r.total), 0) };
}

// Stage duration rows in pipeline order, with the p95 never below the p50.
export function stageDurations(report) {
  return [...((report && report.stages) || [])]
    .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0) || String(a.stage).localeCompare(String(b.stage)))
    .map((s) => {
      const p50 = count(s.p50Ms);
      return { stage: String(s.stage), count: count(s.count), failed: count(s.failed), p50, p95: Math.max(p50, count(s.p95Ms)), mean: count(s.meanMs), max: count(s.maxMs) };
    });
}

// The most recent runs (at most limit), oldest first.
export function recentRuns(report, limit = MAX_RUN_COLUMNS) {
  const runs = (report && report.runs) || [];
  return runs.slice(Math.max(0, runs.length - limit));
}

// Findings per run by tier as StackedColumnChart series values, one per tier.
export function tierSeries(runs) {
  return FINDING_TIERS.map((tier) => ({ tier, color: TIER_COLORS[tier], values: runs.map((r) => count(r.tiers && r.tiers[tier])) }));
}

// Sources per run split into disjoint parts that stack to the examined total: used, fetched but not used, and
// examined but not fetched.
export function sourceParts(run) {
  const examined = count(run && run.examined);
  const fetched = count(run && run.fetched);
  const used = count(run && run.used);
  return { used, fetchedOnly: Math.max(0, fetched - used), examinedOnly: Math.max(0, examined - Math.max(fetched, used)), examined, fetched };
}

// Ten score buckets of ten points; missing buckets are zero.
export function scoreBuckets(report) {
  const rows = (report && report.scoreHistogram) || [];
  const out = [];
  for (let i = 0; i < 10; i++) {
    const row = rows.find((r) => Number(r.lower) === i * 10);
    out.push({ lower: i * 10, upper: i * 10 + 10, count: count(row && row.count) });
  }
  return out;
}

// Activity cells as a 7 by 24 grid, rows starting on firstDay (0 is Sunday, 1 is Monday).
export function activityGrid(report, firstDay = 1) {
  const cells = (report && report.activity) || [];
  const rows = [];
  for (let i = 0; i < 7; i++) {
    const day = (firstDay + i) % 7;
    const values = new Array(24).fill(0);
    for (const c of cells) if (Number(c.dayOfWeek) === day && c.hour >= 0 && c.hour < 24) values[c.hour] += count(c.count);
    rows.push({ day, values });
  }
  return rows;
}

// Runs with a duplicate share (runs that produced or repeated at least one finding), oldest first.
export function noveltyRuns(report) {
  return ((report && report.runs) || []).filter((r) => r.duplicateShare !== null && r.duplicateShare !== undefined);
}

// Provider yield bars: value is the share of results that led to a finding, best first, then by volume.
export function providerBars(report) {
  return ((report && report.providers) || [])
    .filter((p) => count(p.results) > 0)
    .map((p) => ({ key: String(p.provider), provider: String(p.provider), value: Number(p.yield) || 0, results: count(p.results), fetched: count(p.fetched), used: count(p.used), findings: count(p.findings) }))
    .sort((a, b) => b.value - a.value || b.results - a.results || a.provider.localeCompare(b.provider));
}
