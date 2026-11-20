import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { forbidden, page, renderRoute } from '../../test/renderRoute.jsx';
import { TOPIC } from '../../test/researchFixtures.js';
import { channelDays } from './insightsData.js';

const OVERVIEW = {
  fromUtc: '2026-08-21T00:00:00Z',
  toUtc: '2026-11-19T00:00:00Z',
  activeTopics: 3,
  runs: 40,
  runsCompleted: 35,
  runsPartiallySucceeded: 3,
  runsFailed: 2,
  findingsShown: 120,
  findingsRated: 30,
  relevancePrecision: 0.7,
  briefingsReady: 12,
  emailsSent: 14,
  emailsFailed: 1,
  predictionsOpen: 5,
  predictionsResolved: 4,
  predictionAccuracy: 0.75,
  meanBrier: 0.182,
  feedbackTotal: 33,
  feedbackWithReason: 9,
  suggestionsNew: 2,
  suggestionsAccepted: 6,
  quoteFailureRate: 0.05,
  modelCalls: 900,
  modelCostUsd: 12.5
};

const EMAIL = {
  total: 20,
  sent: 16,
  failed: 2,
  pending: 1,
  skipped: 1,
  deliveryRate: 0.8889,
  meanSecondsToSend: 4.5,
  byTemplate: [
    { key: 'RunDigest', count: 15, positive: 13, rate: 0.87 },
    { key: 'BriefingPublished', count: 5, positive: 3, rate: 0.6 }
  ],
  byDay: [
    { key: '2026-11-17', count: 8, positive: 7, rate: 0.875 },
    { key: '2026-11-18', count: 12, positive: 9, rate: 0.75 }
  ],
  byStatus: [
    { key: 'Sent', count: 16 },
    { key: 'Failed', count: 2 },
    { key: 'Queued', count: 1 },
    { key: 'Skipped', count: 1 }
  ],
  feedbackLinkOpens: 10,
  feedbackLinkSubmits: 4,
  unsubscribes: 1
};

const FEEDBACK = {
  total: 6,
  withReason: 2,
  byChannel: [
    { key: 'Dashboard', count: 3 },
    { key: 'Mcp', count: 3 }
  ],
  byTargetType: [{ key: 'Finding', count: 6 }],
  byDay: [{ key: '2026-11-18', count: 6, positive: 2 }],
  byChannelDay: [
    { key: '2026-11-18', label: 'Dashboard', count: 3, positive: 1 },
    { key: '2026-11-18', label: 'Mcp', count: 2, positive: 1 },
    { key: '2026-11-17', label: 'Mcp', count: 1, positive: 0 }
  ],
  emailLinkOpens: 0,
  emailLinkSubmits: 0
};

const reports = { overview: OVERVIEW, email: EMAIL, feedback: FEEDBACK, reasons: { tags: [], byWeek: [] }, suggestions: { total: 0 } };
const getAnalytics = () => vi.fn(async (report) => reports[report] || {});
const topics = () => vi.fn(async () => page([TOPIC]));

describe('channelDays', () => {
  it('builds day buckets oldest first and one series per channel in the given order', () => {
    const result = channelDays(FEEDBACK.byChannelDay, ['Dashboard', 'Email', 'Mcp']);
    expect(result.buckets.map((b) => b.startUtc)).toEqual(['2026-11-17T00:00:00.000Z', '2026-11-18T00:00:00.000Z']);
    expect(result.buckets[0].endUtc).toBe('2026-11-18T00:00:00.000Z');
    expect(result.series).toEqual([
      { channel: 'Dashboard', values: [0, 3] },
      { channel: 'Mcp', values: [1, 2] }
    ]);
  });

  it('appends unknown channels alphabetically and drops rows without a day or a channel', () => {
    const result = channelDays([{ key: '2026-11-18', label: 'Zeta', count: 1 }, { key: '2026-11-18', label: 'Alpha', count: 2 }, { key: 'bad', label: 'Mcp', count: 5 }, { key: '2026-11-18', count: 9 }], ['Mcp']);
    expect(result.series.map((s) => s.channel)).toEqual(['Alpha', 'Zeta']);
    expect(channelDays(null)).toEqual({ buckets: [], series: [] });
  });
});

describe('Insights overview', () => {
  it('shows headline tiles for the scope, each linking to its page', async () => {
    const client = renderRoute('/dashboard/insights/overview', { getAnalytics: getAnalytics(), listTopics: topics() });
    const tiles = await screen.findByTestId('overview-tiles');
    await waitFor(() => expect(within(tiles).getByText('120')).toBeInTheDocument());
    expect(client.getAnalytics).toHaveBeenCalledWith('overview', expect.objectContaining({ from: expect.any(String), to: expect.any(String) }));
    expect(within(tiles).getByText('35 completed, 3 partial, 2 failed')).toBeInTheDocument();
    expect(within(tiles).getByText('70%')).toBeInTheDocument();
    expect(within(tiles).getByText('75%')).toBeInTheDocument();
    expect(within(tiles).getByText('Mean Brier 0.182')).toBeInTheDocument();
    expect(within(tiles).getByText('$12.50')).toBeInTheDocument();
    expect(within(tiles).getByText('900 model calls')).toBeInTheDocument();
    expect(within(tiles).getByText('5%')).toBeInTheDocument();
    await userEvent.selectOptions(await screen.findByLabelText('Topic'), 'top_1');
    await waitFor(() => expect(client.getAnalytics).toHaveBeenLastCalledWith('overview', expect.objectContaining({ topicId: 'top_1' })));
    await userEvent.click(within(tiles).getByRole('button', { name: /Emails sent/ }));
    expect(await screen.findByRole('heading', { name: 'Email delivery', level: 1 })).toBeInTheDocument();
  });

  it('redirects /insights to the overview and lists it first in the Insights group', async () => {
    renderRoute('/dashboard/insights', { getAnalytics: getAnalytics(), listTopics: topics() });
    expect(await screen.findByTestId('overview-tiles')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Overview', level: 1 })).toBeInTheDocument();
  });
});

describe('Email insights', () => {
  it('shows delivery totals, the rate, mean time to send, link use, and the charts', async () => {
    const client = renderRoute('/dashboard/insights/email', { getAnalytics: getAnalytics(), listTopics: topics() });
    const kpis = await screen.findByTestId('email-kpis');
    await waitFor(() => expect(within(kpis).getByText('16')).toBeInTheDocument());
    expect(client.getAnalytics).toHaveBeenCalledWith('email', expect.any(Object));
    expect(within(kpis).getByText('1 pending, 1 skipped')).toBeInTheDocument();
    expect(within(kpis).getByText('88.9%')).toBeInTheDocument();
    expect(within(kpis).getByText('4.5 sec')).toBeInTheDocument();
    expect(within(kpis).getByText('40% of opens')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Messages by day' })).toBeInTheDocument();
    expect(screen.getByText('Run digest (15)')).toBeInTheDocument();
    expect(screen.getByText('Briefing published (5)')).toBeInTheDocument();
    const statuses = screen.getByRole('list', { name: 'Messages by status' });
    expect(within(statuses).getByText('Sent')).toBeInTheDocument();
    expect(within(statuses).getByText('Queued')).toBeInTheDocument();
  });

  it('shows the empty state for a window without email', async () => {
    renderRoute('/dashboard/insights/email', { getAnalytics: vi.fn(async () => ({ total: 0, byDay: [], byTemplate: [], byStatus: [] })), listTopics: topics() });
    await screen.findByTestId('email-kpis');
    await waitFor(() => expect(screen.getAllByText('No data for this range yet.').length).toBe(3));
  });
});

describe('Feedback by day and channel', () => {
  it('stacks feedback per day by channel and labels the MCP channel', async () => {
    renderRoute('/dashboard/insights/feedback', { getAnalytics: getAnalytics(), listTopics: topics(), listFeedback: vi.fn(async () => page([])) });
    expect(await screen.findByRole('img', { name: 'Feedback by day and channel' })).toBeInTheDocument();
    expect(screen.getAllByText('MCP').length).toBeGreaterThan(0);
    expect(screen.getByRole('option', { name: 'MCP' })).toBeInTheDocument();
  });
});

describe('Home overview', () => {
  it('shows headline numbers for the last 30 days with a link to Insights', async () => {
    const client = renderRoute('/dashboard/home', { getAnalytics: getAnalytics() });
    const section = await screen.findByTestId('home-overview');
    expect(within(section).getByText('Last 30 days')).toBeInTheDocument();
    await waitFor(() => expect(within(section).getByText('120')).toBeInTheDocument());
    expect(within(section).getByRole('link', { name: 'Open Insights' })).toHaveAttribute('href', '/dashboard/insights/overview');
    const [report, query] = client.getAnalytics.mock.calls[0];
    expect(report).toBe('overview');
    const days = (new Date(query.to) - new Date(query.from)) / 86400000;
    expect(Math.round(days)).toBe(30);
  });

  it('leaves the overview out when the report is unavailable', async () => {
    renderRoute('/dashboard/home', { getAnalytics: vi.fn(forbidden) });
    expect(await screen.findByTestId('home-request')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('home-overview')).toBeNull());
  });
});
