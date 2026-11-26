import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '../utils/api.js';
import { historyPreview } from '../test/comparisonFixtures.js';
import HistoryPreview from './HistoryPreview.jsx';

const FORM = { historyMode: 'RecentIssues', historyCount: 3, historySinceUtc: '', historyBriefingId: '', historyTokenBudget: 8000 };

function show(props) {
  return render(
    <MemoryRouter>
      <HistoryPreview form={FORM} baselineHref={(id) => '/dashboard/briefings/' + id} {...props} />
    </MemoryRouter>
  );
}

describe('HistoryPreview', () => {
  it('previews only on request, with the unsaved values, and lists the issues and context size', async () => {
    const load = vi.fn(async () => historyPreview());
    show({ load });
    expect(screen.getByText(/See which earlier issues the next briefing would compare with/)).toBeInTheDocument();
    expect(load).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    await waitFor(() => expect(load).toHaveBeenCalled());
    expect(load.mock.calls[0][0]).toEqual({ historyMode: 'RecentIssues', historyCount: 3, historySinceUtc: null, historyBriefingId: null, historyTokenBudget: 8000 });
    expect(await screen.findByText('The next briefing would compare with 2 earlier issues:')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Heat pumps weekly' })).toHaveAttribute('href', '/dashboard/briefings/brf_2');
    expect(screen.getByText(/^Comparison context: \S.* of \S.*$/)).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '50 percent of the comparison budget' })).toBeInTheDocument();
    expect(screen.queryByText(/shortened version/)).toBeNull();
  });

  it('warns about truncation and explains a first run with no baselines', async () => {
    const { unmount } = show({ load: vi.fn(async () => historyPreview({ truncated: true, contextBytes: 9000 })) });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    expect(await screen.findByText(/shortened version/)).toBeInTheDocument();
    unmount();
    show({ load: vi.fn(async () => historyPreview({ baselines: [], contextBytes: 0, note: 'No earlier ready issue exists yet.' })) });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    expect(await screen.findByText(/No earlier ready issue matches these settings yet/)).toBeInTheDocument();
    expect(screen.getByText('No earlier ready issue exists yet.')).toBeInTheDocument();
  });

  it('shows the server note when comparison is off', async () => {
    show({ form: { ...FORM, historyMode: 'Off' }, load: vi.fn(async () => historyPreview({ mode: 'Off', baselines: [], note: 'Comparison is off; the briefing stands alone.' })) });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    expect(await screen.findByText('Comparison is off; the briefing stands alone.')).toBeInTheDocument();
    expect(screen.queryByText(/No earlier ready issue matches/)).toBeNull();
  });

  it('asks for a date or issue before previewing those modes', () => {
    const load = vi.fn();
    const { unmount } = show({ form: { ...FORM, historyMode: 'SinceDate' }, load });
    expect(screen.getByRole('button', { name: 'Show baseline' })).toBeDisabled();
    unmount();
    show({ form: { ...FORM, historyMode: 'SpecificIssue', historyBriefingId: '' }, load });
    expect(screen.getByRole('button', { name: 'Show baseline' })).toBeDisabled();
    expect(load).not.toHaveBeenCalled();
  });

  it('shows 409 and 400 refusals inline', async () => {
    const conflict = vi.fn(async () => {
      throw new ApiError(409, { error: 'Conflict', message: 'The pinned issue brf_9 is unavailable.' });
    });
    const { unmount } = show({ form: { ...FORM, historyMode: 'SpecificIssue', historyBriefingId: 'brf_9' }, load: conflict });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    expect(await screen.findByText('The pinned issue brf_9 is unavailable.')).toBeInTheDocument();
    expect(screen.getByText('The pinned issue is no longer available. Choose another issue or a different mode.')).toBeInTheDocument();
    expect(conflict.mock.calls[0][0]).toMatchObject({ historyMode: 'SpecificIssue', historyBriefingId: 'brf_9' });
    unmount();
    const invalid = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'historyCount must be between 1 and 20.' });
    });
    show({ form: { ...FORM, historyCount: 50 }, load: invalid });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('historyCount must be between 1 and 20.');
  });

  it('refreshes after edits once it is open', async () => {
    const load = vi.fn(async () => historyPreview());
    const { rerender } = show({ load });
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    await waitFor(() => expect(load).toHaveBeenCalledTimes(1));
    rerender(
      <MemoryRouter>
        <HistoryPreview form={{ ...FORM, historyCount: 7 }} load={load} />
      </MemoryRouter>
    );
    await waitFor(() => expect(load).toHaveBeenCalledTimes(2));
    expect(load.mock.calls[1][0]).toMatchObject({ historyCount: 7 });
  });
});
