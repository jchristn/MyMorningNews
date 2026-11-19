import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { EMPTY_PAGE } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { BRIEFING, EDITION, detail } from '../test/briefingFixtures.js';

// listBriefings answers by kind and status, the way the Today page asks.
function briefingLists({ ready = [BRIEFING], editions = [], runs = [], oneTime = [] } = {}) {
  return vi.fn(async (q) => {
    if (q.kind === 'Edition' && q.status === 'Ready') return page(ready);
    if (q.kind === 'Edition') return page(editions);
    if (q.kind === 'Run') return page(runs);
    if (q.kind === 'OneTime') return page(oneTime);
    return EMPTY_PAGE;
  });
}

describe('TodayView', () => {
  it('renders the latest ready edition briefing inline with recent run and one-time briefings', async () => {
    const runBriefing = { ...BRIEFING, id: 'brf_run', kind: 'Run', title: 'Run briefing: AI tools' };
    const oneTime = { ...BRIEFING, id: 'brf_once', kind: 'OneTime', title: 'Report: heat pumps', status: 'Failed' };
    renderRoute('/dashboard/today', {
      listEditions: vi.fn(async () => page([EDITION])),
      listBriefings: briefingLists({ runs: [runBriefing], oneTime: [oneTime] }),
      getBriefing: vi.fn(async () => detail())
    });
    const panel = await screen.findByTestId('today-briefing');
    expect(within(panel).getByRole('heading', { name: BRIEFING.title })).toBeInTheDocument();
    expect(within(panel).getByRole('region', { name: 'Top of the news' })).toHaveTextContent('Coding agents reached general availability.');
    expect(within(panel).getByRole('link', { name: 'Open in reader' })).toHaveAttribute('href', '/dashboard/briefings/brf_1');
    expect(screen.getByRole('link', { name: 'Run briefing: AI tools' })).toHaveAttribute('href', '/dashboard/briefings/brf_run');
    expect(screen.getByRole('link', { name: 'Report: heat pumps' })).toBeInTheDocument();
  });

  it('shows the empty state with Compose now, then follows the queued briefing until it is ready', async () => {
    const queued = { ...BRIEFING, id: 'brf_new', status: 'Queued', researchDeadlineUtc: undefined, composedUtc: undefined };
    let state = 'none';
    const listBriefings = vi.fn(async (q) => {
      if (q.kind === 'Edition' && q.status === 'Ready') return page(state === 'ready' ? [{ ...queued, status: 'Ready' }] : []);
      if (q.kind === 'Edition') return page(state === 'queued' ? [queued] : state === 'ready' ? [{ ...queued, status: 'Ready' }] : []);
      return EMPTY_PAGE;
    });
    const composeEdition = vi.fn(async () => {
      state = 'queued';
      return queued;
    });
    renderRoute('/dashboard/today', { listEditions: vi.fn(async () => page([EDITION])), listBriefings, composeEdition, getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, id: 'brf_new' } })) });
    expect(await screen.findByText('No briefing yet')).toBeInTheDocument();
    const buttons = await screen.findAllByRole('button', { name: 'Compose now' });
    await waitFor(() => expect(buttons[0]).toBeEnabled());
    await userEvent.click(buttons[0]);
    await waitFor(() => expect(composeEdition).toHaveBeenCalledWith('edn_1'));
    expect(await screen.findByTestId('compose-progress')).toHaveTextContent('Queued. It will be composed shortly.');
    expect(screen.getAllByRole('button', { name: 'Compose now' })[0]).toBeDisabled();
    state = 'ready';
    expect(await screen.findByTestId('today-briefing', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByTestId('compose-progress')).not.toBeInTheDocument();
  }, 10000);

  it('explains when a briefing is already on the way', async () => {
    const composeEdition = vi.fn(async () => {
      throw new ApiError(409, { error: 'Conflict', message: 'This edition already has a briefing on the way.' });
    });
    renderRoute('/dashboard/today', { listEditions: vi.fn(async () => page([EDITION])), listBriefings: briefingLists(), getBriefing: vi.fn(async () => detail()), composeEdition });
    await screen.findByTestId('today-briefing');
    await userEvent.click(screen.getByRole('button', { name: 'Compose now' }));
    expect(await screen.findByText('A briefing for this edition is already on the way.')).toBeInTheDocument();
  });
});
