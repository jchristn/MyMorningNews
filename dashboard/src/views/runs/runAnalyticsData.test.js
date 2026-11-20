import { describe, expect, it } from 'vitest';
import {
  MAX_RUN_COLUMNS,
  activityGrid,
  browserTimeZone,
  noveltyRuns,
  providerBars,
  recentRuns,
  runsQuery,
  scoreBuckets,
  sourceParts,
  stageDurations,
  statusDays,
  tierSeries
} from './runAnalyticsData.js';

const NOW = new Date('2026-11-20T12:00:00Z');

describe('run chart shaping', () => {
  it('builds the query for a range, a number of days, a topic, the tenant, and a time zone', () => {
    const week = runsQuery({ rangeId: 'week', topicId: 'top_1', everyone: true, timeZone: 'America/New_York' }, NOW);
    expect(new Date(week.to) - new Date(week.from)).toBe(7 * 86400000);
    expect(week).toMatchObject({ topicId: 'top_1', all: true, timeZone: 'America/New_York' });
    const fallback = runsQuery({ rangeId: 'nope' }, NOW);
    expect(new Date(fallback.to) - new Date(fallback.from)).toBe(30 * 86400000);
    expect(fallback).toMatchObject({ topicId: undefined, all: undefined, timeZone: undefined });
    const days = runsQuery({ rangeId: 'week', days: 180 }, NOW);
    expect(new Date(days.to) - new Date(days.from)).toBe(180 * 86400000);
    expect(typeof browserTimeZone()).toBe('string');
  });

  it('turns days into stacked status series, keeping Completed and Failed and any status that occurs', () => {
    const report = {
      byDay: [
        { day: '2026-11-19', startUtc: '2026-11-19T05:00:00Z', endUtc: '2026-11-20T05:00:00Z', total: 3, counts: { Completed: 2, Failed: 0, Cancelled: 1 } },
        { day: '2026-11-20', startUtc: '2026-11-20T05:00:00Z', endUtc: '2026-11-21T05:00:00Z', total: 1, counts: { Completed: 0, Failed: 1 } }
      ]
    };
    const d = statusDays(report);
    expect(d.total).toBe(4);
    expect(d.buckets.map((b) => b.startUtc)).toEqual(['2026-11-19T05:00:00Z', '2026-11-20T05:00:00Z']);
    expect(d.series.map((s) => s.status)).toEqual(['Completed', 'Failed', 'Cancelled']);
    expect(d.series.find((s) => s.status === 'Completed').values).toEqual([2, 0]);
    expect(statusDays(null)).toEqual({ buckets: [], series: [{ status: 'Completed', color: expect.any(String), values: [] }, { status: 'Failed', color: expect.any(String), values: [] }], total: 0 });
  });

  it('orders stages by pipeline position and never lets p95 fall below p50', () => {
    const rows = stageDurations({
      stages: [
        { stage: 'Fetch', sortOrder: 4, count: 2, p50Ms: 400, p95Ms: 300, meanMs: 380, maxMs: 410, failed: 0 },
        { stage: 'Search', sortOrder: 2, count: 3, p50Ms: 200, p95Ms: 290, meanMs: 210, maxMs: 300, failed: 1 }
      ]
    });
    expect(rows.map((r) => r.stage)).toEqual(['Search', 'Fetch']);
    expect(rows[0]).toMatchObject({ p50: 200, p95: 290, count: 3, failed: 1 });
    expect(rows[1].p95).toBe(400);
  });

  it('keeps the latest runs and splits tiers and sources into stackable parts', () => {
    const runs = Array.from({ length: 25 }, (_, i) => ({ runId: 'run_' + i, tiers: { MustRead: i, Fyi: 1 }, examined: 10, fetched: 6, used: 2 }));
    const recent = recentRuns({ runs });
    expect(recent).toHaveLength(MAX_RUN_COLUMNS);
    expect(recent[0].runId).toBe('run_5');
    const tiers = tierSeries(recent.slice(0, 2));
    expect(tiers.map((s) => s.tier)).toEqual(['MustRead', 'WorthALook', 'Fyi', 'BelowThreshold']);
    expect(tiers[0].values).toEqual([5, 6]);
    expect(tiers[1].values).toEqual([0, 0]);
    expect(sourceParts(runs[0])).toEqual({ used: 2, fetchedOnly: 4, examinedOnly: 4, examined: 10, fetched: 6 });
    expect(sourceParts({ examined: 2, fetched: 0, used: 0 })).toEqual({ used: 0, fetchedOnly: 0, examinedOnly: 2, examined: 2, fetched: 0 });
  });

  it('fills the score histogram and the activity grid', () => {
    const buckets = scoreBuckets({ scoreHistogram: [{ lower: 80, upper: 90, count: 3 }] });
    expect(buckets).toHaveLength(10);
    expect(buckets[8]).toEqual({ lower: 80, upper: 90, count: 3 });
    expect(buckets[9]).toEqual({ lower: 90, upper: 100, count: 0 });
    const grid = activityGrid({ activity: [{ dayOfWeek: 0, hour: 6, count: 2 }, { dayOfWeek: 1, hour: 23, count: 1 }] });
    expect(grid.map((r) => r.day)).toEqual([1, 2, 3, 4, 5, 6, 0]);
    expect(grid[6].values[6]).toBe(2);
    expect(grid[0].values[23]).toBe(1);
    expect(activityGrid(null, 0)[0]).toEqual({ day: 0, values: new Array(24).fill(0) });
  });

  it('keeps runs with a duplicate share and ranks provider yield', () => {
    expect(noveltyRuns({ runs: [{ runId: 'a', duplicateShare: 0.25 }, { runId: 'b', duplicateShare: null }, { runId: 'c', duplicateShare: 0 }] }).map((r) => r.runId)).toEqual(['a', 'c']);
    const bars = providerBars({
      providers: [
        { provider: 'Searxng', results: 40, fetched: 20, used: 8, findings: 9, yield: 0.2 },
        { provider: 'Rss', results: 10, fetched: 5, used: 5, findings: 5, yield: 0.5 },
        { provider: 'Arxiv', results: 0, yield: null }
      ]
    });
    expect(bars.map((b) => b.provider)).toEqual(['Rss', 'Searxng']);
    expect(bars[1]).toMatchObject({ value: 0.2, results: 40, used: 8, findings: 9 });
  });
});
