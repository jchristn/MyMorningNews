import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ADMIN_DETAILS, MEMBER_DETAILS } from '../test/fakeClient.js';
import { forbidden, page, renderRoute, usageReport, usageRow } from '../test/renderRoute.jsx';
import { coarseBucketMinutes } from '../charts/ranges.js';
import { measureValue, rowLabel, tokenMix, trendSeries } from './usage/usageData.js';
import i18next from '../i18n/index.js';

const SERIES = {
  fromUtc: '2026-11-18T00:00:00Z',
  toUtc: '2026-11-19T00:00:00Z',
  bucketMinutes: 60,
  buckets: [
    { bucketStartUtc: '2026-11-18T00:00:00Z', bucketEndUtc: '2026-11-18T01:00:00Z', calls: 2, failures: 0, inputTokens: 100, outputTokens: 50, cachedInputTokens: 0, costUsd: 0.5 },
    { bucketStartUtc: '2026-11-18T01:00:00Z', bucketEndUtc: '2026-11-18T02:00:00Z', calls: 1, failures: 1, inputTokens: 10, outputTokens: 5, cachedInputTokens: 0, costUsd: 0.25 }
  ]
};

function reportFor(query) {
  switch (query.groupBy) {
    case 'User':
      return usageReport('User', [usageRow('usr_1', { label: 'qa-heavy@example.test', costUsd: 2, costShare: 0.8 }), usageRow('(system)', { costUsd: 0.5, costShare: 0.2 })]);
    case 'Task':
      return usageReport('Task', [usageRow('Scoring', { costUsd: 1.5 }), usageRow('Synthesis', { costUsd: 1 })]);
    case 'Topic':
      return usageReport('Topic', [usageRow('(none)', { costUsd: 2.5 })]);
    case 'Tenant':
      return usageReport('Tenant', [usageRow('default', { label: 'Default', costUsd: 2.5, costShare: 1 })]);
    default:
      return usageReport('Model', [usageRow('claude-opus-5-5', { costUsd: 2.5, costShare: 1 })]);
  }
}

// The same window grouped by task: one row per group in each bucket, with the remainder under "(other)".
const GROUPED = {
  ...SERIES,
  groupBy: 'Task',
  groups: [usageRow('Scoring', { costUsd: 0.5, calls: 2 }), usageRow('(other)', { label: 'Other', costUsd: 0.25, calls: 1 })],
  buckets: [
    { ...SERIES.buckets[0], rows: [usageRow('Scoring', { costUsd: 0.5, calls: 2 })] },
    { ...SERIES.buckets[1], rows: [usageRow('(other)', { costUsd: 0.25, calls: 1 })] }
  ]
};

describe('UsageView', () => {
  it('shows KPIs, cost by user with share, and the breakdown for administrators', async () => {
    const client = renderRoute('/dashboard/insights/usage', {
      getUsageReport: vi.fn(async (q) => reportFor(q)),
      getUsageTimeseries: vi.fn(async () => SERIES),
      listModelEndpoints: vi.fn(async () => page([]))
    });
    expect((await screen.findAllByText('qa-heavy@example.test')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('What does this cost, and who uses it most?').length).toBeGreaterThan(0);
    expect(screen.getAllByText('$2.50').length).toBeGreaterThan(0);
    expect(screen.getAllByText('80%').length).toBeGreaterThan(0);
    expect(screen.getAllByText('System').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeEnabled();
    const groups = client.getUsageReport.mock.calls.map((c) => c[0].groupBy);
    expect(groups).toEqual(expect.arrayContaining(['User', 'Model', 'Task', 'Topic']));
    expect(client.getUsageReport.mock.calls[0][0].from).toBeTruthy();
    expect(client.getUsageTimeseries.mock.calls[0][0].bucketMinutes).toBe(1440);
  });

  it('groups the trend by a chosen dimension, reloading only the time series, and switches the measure', async () => {
    window.localStorage.removeItem('mymorningnews.usage.trendGroup');
    window.localStorage.removeItem('mymorningnews.usage.trendMeasure');
    const client = renderRoute('/dashboard/insights/usage', {
      getUsageReport: vi.fn(async (q) => reportFor(q)),
      getUsageTimeseries: vi.fn(async (q) => (q.groupBy ? GROUPED : SERIES)),
      listModelEndpoints: vi.fn(async () => page([]))
    });
    const chart = await screen.findByRole('img', { name: 'Usage over time' });
    expect(chart.querySelectorAll('[data-series]').length).toBe(1);
    expect(client.getUsageTimeseries.mock.calls[0][0].groupBy).toBeUndefined();
    const reportCalls = client.getUsageReport.mock.calls.length;
    await userEvent.selectOptions(screen.getByLabelText('Group by', { selector: '#usage-trend-group' }), 'Task');
    await waitFor(() => expect(client.getUsageTimeseries).toHaveBeenLastCalledWith(expect.objectContaining({ groupBy: 'Task', top: 6, bucketMinutes: 1440 })));
    await waitFor(() => expect(screen.getByRole('img', { name: 'Usage over time' }).querySelectorAll('[data-series]').length).toBe(2));
    expect(client.getUsageReport.mock.calls.length).toBe(reportCalls);
    // One legend entry per group, with the combined remainder named.
    expect(screen.getAllByText('Other').length).toBeGreaterThan(0);
    await userEvent.selectOptions(screen.getByLabelText('Measure'), 'toolCalls');
    expect(window.localStorage.getItem('mymorningnews.usage.trendMeasure')).toBe('"toolCalls"');
    expect(client.getUsageTimeseries.mock.calls.length).toBe(2);
    window.localStorage.removeItem('mymorningnews.usage.trendGroup');
    window.localStorage.removeItem('mymorningnews.usage.trendMeasure');
  });

  it('shows a member their own usage without requesting the tenant report', async () => {
    const client = renderRoute('/dashboard/insights/usage', {
      getTokenDetails: async () => MEMBER_DETAILS,
      getUsageReport: vi.fn(forbidden),
      getUsageTimeseries: vi.fn(forbidden),
      getMyUsage: vi.fn(async () => usageReport('Task', [usageRow('Refinement', { costUsd: 0.75 })])),
      listModelEndpoints: vi.fn(async () => page([]))
    });
    expect(await screen.findByText(/This is your own usage/)).toBeInTheDocument();
    expect((await screen.findAllByText('Refinement')).length).toBeGreaterThan(0);
    await waitFor(() => expect(client.getMyUsage).toHaveBeenCalledWith(expect.objectContaining({ groupBy: 'Task' })));
    expect(client.getUsageReport).not.toHaveBeenCalled();
    expect(client.getUsageTimeseries).not.toHaveBeenCalled();
  });
});

describe('AdminUsageView', () => {
  it('shows platform cost by tenant for global administrators', async () => {
    const client = renderRoute('/dashboard/admin/usage', {
      getTokenDetails: async () => ADMIN_DETAILS,
      getUsageReport: vi.fn(async (q) => reportFor(q)),
      getUsageTimeseries: vi.fn(async () => SERIES)
    });
    expect(await screen.findByText('Cost by tenant over time')).toBeInTheDocument();
    expect((await screen.findAllByText('Default', { selector: '.truncate' })).length).toBeGreaterThan(0);
    const queries = client.getUsageReport.mock.calls.map((c) => c[0]);
    expect(queries.every((q) => q.allTenants === true)).toBe(true);
    // The per-tenant trend is one grouped time series, not a report per window.
    expect(queries.filter((q) => q.groupBy === 'Tenant').length).toBe(1);
    const seriesQueries = client.getUsageTimeseries.mock.calls.map((c) => c[0]);
    expect(seriesQueries.every((q) => q.allTenants === true)).toBe(true);
    expect(seriesQueries.find((q) => q.groupBy === 'Tenant')).toMatchObject({ top: 6, bucketMinutes: 4320 });
    expect(seriesQueries.some((q) => q.groupBy === undefined && q.bucketMinutes === 1440)).toBe(true);
  });

  it('is hidden from tenant members', async () => {
    renderRoute('/dashboard/admin/usage', { getTokenDetails: async () => MEMBER_DETAILS });
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
  });
});

describe('usage shaping', () => {
  const t = i18next.t.bind(i18next);

  it('splits reasoning out of output and caches out of input', () => {
    expect(tokenMix(usageRow('x'))).toEqual({ uncachedInput: 700, cachedInput: 200, cacheWrite: 100, output: 450, reasoning: 50 });
  });

  it('labels sentinel keys and enum groups', () => {
    expect(rowLabel({ key: '(system)' }, 'User', t)).toBe('System');
    expect(rowLabel({ key: '(none)' }, 'Topic', t)).toBe('No topic');
    expect(rowLabel({ key: 'QueryPlanning' }, 'Task', t)).toBe('Query planning');
    expect(rowLabel({ key: 'usr_1', label: 'a@b.c' }, 'User', t)).toBe('a@b.c');
  });

  it('labels the combined remainder of a grouped series', () => {
    expect(rowLabel({ key: '(other)', label: 'Other' }, 'Task', t)).toBe('Other');
    expect(rowLabel({ key: 'mep_1|claude-opus-5-5', label: 'claude-opus-5-5 (Anthropic)' }, 'Model', t)).toBe('claude-opus-5-5 (Anthropic)');
  });

  it('reads each measure from a bucket or a group row', () => {
    const bucket = { costUsd: 1.5, calls: 3, inputTokens: 100, outputTokens: 40, cacheWriteTokens: 7, reasoningTokens: 5, toolCalls: 2, p95TimeToFirstTokenMs: 900 };
    expect(['cost', 'calls', 'tokens', 'cacheWriteTokens', 'reasoningTokens', 'toolCalls', 'ttft'].map((m) => measureValue(bucket, m))).toEqual([1.5, 3, 140, 7, 5, 2, 900]);
    expect(measureValue(undefined, 'cost')).toBe(0);
    expect(measureValue({ p95TimeToFirstTokenMs: null }, 'ttft')).toBe(0);
  });

  it('draws one series per group, zero where a group has no calls, and average with p95 for time to first token', () => {
    const grouped = trendSeries(GROUPED, 'cost', t);
    expect(grouped.map((s) => [s.key, s.label, s.values])).toEqual([
      ['Scoring', 'Scoring', [0.5, 0]],
      ['(other)', 'Other', [0, 0.25]]
    ]);
    expect(grouped[0].color).not.toBe(grouped[1].color);
    expect(trendSeries(SERIES, 'calls', t).map((s) => s.values)).toEqual([[2, 1]]);
    const timing = trendSeries({ ...SERIES, buckets: SERIES.buckets.map((b, i) => ({ ...b, averageTimeToFirstTokenMs: 100 * (i + 1), p95TimeToFirstTokenMs: 400 * (i + 1) })) }, 'ttft', t);
    expect(timing.map((s) => [s.key, s.values])).toEqual([
      ['avg', [100, 200]],
      ['p95', [400, 800]]
    ]);
  });

  it('widens the bucket so a range fits a column budget', () => {
    expect(coarseBucketMinutes({ bucketMinutes: 1440, count: 30 }, 12)).toBe(4320);
    expect(coarseBucketMinutes({ bucketMinutes: 60, count: 24 }, 12)).toBe(120);
    expect(coarseBucketMinutes({ bucketMinutes: 10080, count: 13 }, 12)).toBe(20160);
    expect(coarseBucketMinutes({ bucketMinutes: 60, count: 6 }, 12)).toBe(60);
  });
});
