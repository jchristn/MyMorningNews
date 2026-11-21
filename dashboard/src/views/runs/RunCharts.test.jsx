import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../../test/fakeClient.js';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { TOPIC } from '../../test/researchFixtures.js';

const zeroCounts = { Queued: 0, Running: 0, Completed: 0, PartiallySucceeded: 0, Failed: 0, Cancelled: 0, Skipped: 0 };
const tiers = (mustRead, fyi) => ({ MustRead: mustRead, WorthALook: 1, Fyi: fyi, BelowThreshold: 0 });

const RUNS_REPORT = {
  fromUtc: '2026-10-21T12:00:00Z',
  toUtc: '2026-11-20T12:00:00Z',
  timeZone: 'America/New_York',
  total: 3,
  byStatus: { ...zeroCounts, Completed: 2, Failed: 1 },
  byDay: [
    { day: '2026-11-19', startUtc: '2026-11-19T05:00:00Z', endUtc: '2026-11-20T05:00:00Z', total: 2, counts: { ...zeroCounts, Completed: 1, Failed: 1 } },
    { day: '2026-11-20', startUtc: '2026-11-20T05:00:00Z', endUtc: '2026-11-21T05:00:00Z', total: 1, counts: { ...zeroCounts, Completed: 1 } }
  ],
  stages: [
    { stage: 'Search', sortOrder: 2, count: 3, failed: 1, p50Ms: 1200, p95Ms: 4000, meanMs: 1500, maxMs: 4200 },
    { stage: 'Fetch', sortOrder: 4, count: 2, failed: 0, p50Ms: 8000, p95Ms: 9000, meanMs: 8200, maxMs: 9100 },
    { stage: 'Extract', sortOrder: 6, count: 3, failed: 0, p50Ms: 3000, p95Ms: 5000, meanMs: 3200, maxMs: 5100, totalCostUsd: 0.9, meanCostUsd: 0.3, llmCallCount: 12 }
  ],
  runs: [
    { runId: 'run_1', topicId: 'top_1', topicName: 'AI developer tools', createdUtc: '2026-11-19T14:00:00Z', status: 'Completed', durationMs: 60000, tiers: tiers(2, 3), newFindings: 6, duplicates: 2, duplicateShare: 0.25, examined: 40, fetched: 12, used: 5 },
    { runId: 'run_2', topicId: 'top_1', topicName: 'AI developer tools', createdUtc: '2026-11-20T14:00:00Z', status: 'Completed', durationMs: 50000, tiers: tiers(1, 0), newFindings: 2, duplicates: 6, duplicateShare: 0.75, examined: 30, fetched: 10, used: 2 }
  ],
  runsTruncated: false,
  scoreHistogram: Array.from({ length: 10 }, (_, i) => ({ lower: i * 10, upper: i * 10 + 10, count: i === 8 ? 4 : i === 5 ? 2 : 0 })),
  activity: Array.from({ length: 168 }, (_, i) => ({ dayOfWeek: Math.floor(i / 24), hour: i % 24, count: i === 4 * 24 + 9 ? 3 : 0 })),
  providers: [
    { provider: 'Searxng', results: 40, fetched: 20, used: 8, findings: 9, yield: 0.2 },
    { provider: 'Rss', results: 10, fetched: 5, used: 5, findings: 5, yield: 0.5 }
  ]
};

const report = () => vi.fn(async () => RUNS_REPORT);
const lastQuery = (client) => client.getRunsAnalytics.mock.calls[client.getRunsAnalytics.mock.calls.length - 1][0];

describe('Run charts', () => {
  it('shows runs by status, stage durations, and cost by stage on Runs, for the tenant, in the browser time zone, under one range control', async () => {
    const client = renderRoute('/dashboard/runs', { getRunsAnalytics: report(), listTopics: vi.fn(async () => page([TOPIC])) });
    const status = await screen.findByRole('img', { name: 'Runs over time by status' });
    expect(status).toBeInTheDocument();
    expect(screen.getByText('Total runs: 3')).toBeInTheDocument();
    const stages = screen.getByRole('img', { name: 'Run duration by stage' });
    expect(stages.querySelector('[data-whisker="Search"]')).not.toBeNull();
    expect(within(stages).getByText('1.2 sec / 4 sec')).toBeInTheDocument();
    const q = lastQuery(client);
    expect(q).toMatchObject({ all: true, timeZone: expect.any(String) });
    expect(new Date(q.to) - new Date(q.from)).toBe(30 * 86400000);
    // One range control above every chart, none inside a card.
    const bar = screen.getByTestId('chart-range-bar');
    expect(within(status.closest('.chart-card')).queryByRole('button', { name: 'Last week' })).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Last week' })).toHaveLength(1);
    expect(status.closest('.chart-card')).toHaveClass('wide');
    expect(stages.closest('.chart-card')).toHaveClass('wide');
    await userEvent.click(within(bar).getByRole('button', { name: 'Last week' }));
    await waitFor(() => {
      const next = lastQuery(client);
      expect(new Date(next.to) - new Date(next.from)).toBe(7 * 86400000);
    });
    const costs = screen.getByRole('list', { name: 'Cost by stage' });
    expect(costs.closest('.chart-card')).toHaveClass('wide');
    expect(within(costs).getByText('$0.90')).toBeInTheDocument();
    expect(within(costs).getByText('Extract ($0.30 per run)')).toBeInTheDocument();
    expect(within(costs).queryByText(/Search/)).toBeNull();
    const slot = stages.querySelector('[data-stage="Search"]');
    fireEvent.focus(slot);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('95th percentile (p95)');
  });

  it('shows findings by tier and the score distribution on Findings, and a tier column opens the run findings', async () => {
    const client = renderRoute('/dashboard/findings', { getRunsAnalytics: report(), listFindings: vi.fn(async () => page([])) });
    const tierChart = await screen.findByRole('img', { name: 'Findings per run by tier' });
    expect(screen.getByRole('img', { name: 'Relevance score distribution' })).toBeInTheDocument();
    expect(screen.getByText('80 to 90')).toBeInTheDocument();
    expect(lastQuery(client).all).toBeUndefined();
    const columns = tierChart.querySelectorAll('.chart-slot');
    expect(columns).toHaveLength(2);
    expect(columns[0].getAttribute('aria-label')).toMatch(/Must read 2/);
    fireEvent.click(columns[1]);
    await waitFor(() => expect(client.listFindings).toHaveBeenLastCalledWith(expect.objectContaining({ runId: 'run_2' })));
  });

  it('shows sources examined, fetched, and used on Sources for the selected topic', async () => {
    const client = renderRoute('/dashboard/sources', { getRunsAnalytics: report(), listTopics: vi.fn(async () => page([TOPIC])), listSources: vi.fn(async () => page([])) });
    const chart = await screen.findByRole('img', { name: 'Sources examined, fetched, and used' });
    expect(screen.getByText('Used in a finding')).toBeInTheDocument();
    expect(screen.getByText('Examined, not fetched')).toBeInTheDocument();
    expect(chart.querySelectorAll('.chart-slot')[0].getAttribute('aria-label')).toMatch(/Used in a finding 5, Fetched, not used 7, Examined, not fetched 28/);
    await waitFor(() => expect(screen.getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'top_1' } });
    await waitFor(() => expect(lastQuery(client)).toMatchObject({ topicId: 'top_1' }));
  });

  it('shows provider yield on Search Providers, best first', async () => {
    renderRoute('/dashboard/search-providers', { getRunsAnalytics: report(), listSearchProviders: vi.fn(async () => page([])) });
    const list = await screen.findByRole('list', { name: 'Search provider yield' });
    const rows = within(list).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('RSS feeds (5 of 10)');
    expect(rows[0]).toHaveTextContent('50%');
    expect(rows[1]).toHaveTextContent('SearXNG (8 of 40)');
  });

  it('shows runs by status and the activity heatmap on Home for a member, with keyboard tooltips', async () => {
    const client = renderRoute('/dashboard', { getRunsAnalytics: report(), getTokenDetails: vi.fn(async () => MEMBER_DETAILS) });
    expect(await screen.findByRole('img', { name: 'Runs over time by status' })).toBeInTheDocument();
    const heatmap = screen.getByRole('table', { name: 'Activity by day and hour' });
    expect(screen.getByText('Runs started in each hour of the week, in America/New_York.')).toBeInTheDocument();
    const rows = within(heatmap).getAllByRole('row');
    expect(rows).toHaveLength(8);
    const thursday = within(heatmap).getByRole('cell', { name: /, 9 AM: 3$/ });
    fireEvent.focus(thursday);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('3');
    expect(lastQuery(client).all).toBeUndefined();
  });

  it('shows stage durations on Health', async () => {
    renderRoute('/dashboard/health', { getRunsAnalytics: report() });
    expect(await screen.findByRole('img', { name: 'Run duration by stage' })).toBeInTheDocument();
  });

  it('draws tiers, novelty, and activity for one topic on its Analytics tab', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=analytics', {
      getTopic: vi.fn(async () => ({ topic: { ...TOPIC, userId: 'usr_admin', mcpToolIds: [] }, dimensions: [], constraints: [], recipients: [] })),
      getRunsAnalytics: report(),
      getAnalytics: vi.fn(async () => ({})),
      getRankingRecommendation: vi.fn(async () => ({ enough: false, ratedFindings: 0, current: {}, recommended: {} }))
    });
    expect(await screen.findByRole('img', { name: 'Novelty trend' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Findings per run by tier' })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Activity by day and hour' })).toBeInTheDocument();
    const q = lastQuery(client);
    expect(q.topicId).toBe(TOPIC.id);
    expect(new Date(q.to) - new Date(q.from)).toBe(90 * 86400000);
  });

  it('follows the Insights scope on Relevance and shows the empty state without runs', async () => {
    const client = renderRoute('/dashboard/insights/relevance', { getRunsAnalytics: vi.fn(async () => ({ ...RUNS_REPORT, scoreHistogram: [] })), getAnalytics: vi.fn(async () => ({})), listTopics: vi.fn(async () => page([TOPIC])) });
    const card = (await screen.findByRole('heading', { name: 'Relevance score distribution' })).closest('.chart-card');
    expect(await within(card).findByText('No data for this range yet.')).toBeInTheDocument();
    expect(within(card).queryByRole('button', { name: 'Last week' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: '30 days' }));
    await waitFor(() => {
      const q = lastQuery(client);
      expect(new Date(q.to) - new Date(q.from)).toBe(30 * 86400000);
    });
  });
});
