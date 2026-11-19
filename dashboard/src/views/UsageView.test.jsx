import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { ADMIN_DETAILS, MEMBER_DETAILS } from '../test/fakeClient.js';
import { forbidden, page, renderRoute, usageReport, usageRow } from '../test/renderRoute.jsx';
import { tenantTrend } from './AdminUsageView.jsx';
import { rowLabel, tokenMix } from './usage/usageData.js';
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

  it('falls back to the member view of their own usage when the tenant report is forbidden', async () => {
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
    expect(queries.filter((q) => q.groupBy === 'Tenant').length).toBeGreaterThan(1);
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

  it('stacks the top tenants per window and folds the rest into other', () => {
    const windows = [
      { from: 'a', to: 'b', report: { rows: [{ key: 't1', costUsd: 1 }, { key: 't2', costUsd: 2 }, { key: 't3', costUsd: 0.5 }] } },
      { from: 'b', to: 'c', report: { rows: [] } }
    ];
    expect(tenantTrend(windows, ['t1', 't2'])).toEqual([
      { from: 'a', to: 'b', values: { t1: 1, t2: 2 }, other: 0.5 },
      { from: 'b', to: 'c', values: {}, other: 0 }
    ]);
  });
});
