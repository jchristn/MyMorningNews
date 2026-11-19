import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { EPISODE, audioResult, installMediaShims, mediaDetail } from '../test/mediaFixtures.js';

beforeEach(() => installMediaShims());
afterEach(() => vi.restoreAllMocks());

const FAILED = { ...EPISODE, id: 'med_2', title: 'Run briefing: AI tools', status: 'Failed', artifactId: undefined, durationSeconds: 0, bytes: 0, errorMessage: 'The speech endpoint did not answer.' };

async function openActions(name) {
  const row = (await screen.findByText(name)).closest('tr');
  await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
}

describe('ListenView', () => {
  it('lists episodes with status, duration, and size, and filters by status', async () => {
    const listMedia = vi.fn(async () => page([EPISODE, FAILED]));
    renderRoute('/dashboard/listen', { listMedia });
    const row = (await screen.findByText(EPISODE.title)).closest('tr');
    expect(within(row).getByText('Ready', { selector: '.badge' })).toBeInTheDocument();
    expect(within(row).getByText('Briefing audio', { selector: '.badge' })).toBeInTheDocument();
    expect(within(row).getByText(/4 min|4m/)).toBeInTheDocument();
    expect(within(row).getByText(/2\.3 MB/)).toBeInTheDocument();
    expect(screen.getByText('The speech endpoint did not answer.')).toBeInTheDocument();
    expect(listMedia).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'createdUtc', sortDirection: 'Descending' }));
    expect(screen.getByRole('heading', { name: 'Listen' })).toBeInTheDocument();
  });

  it('plays a ready episode with its chapters', async () => {
    const getMedia = vi.fn(async () => mediaDetail());
    const getArtifactContent = vi.fn(async () => audioResult());
    renderRoute('/dashboard/listen', { listMedia: vi.fn(async () => page([EPISODE, FAILED])), getMedia, getArtifactContent });
    await openActions(FAILED.title);
    expect(screen.getByRole('menuitem', { name: 'Play' })).toBeDisabled();
    await userEvent.keyboard('{Escape}');
    await openActions(EPISODE.title);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Play' }));
    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByTestId('audio-element')).toBeInTheDocument();
    expect(within(dialog).getByRole('list', { name: 'Chapters' })).toBeInTheDocument();
    expect(getMedia).toHaveBeenCalledWith('med_1');
    expect(getArtifactContent).toHaveBeenCalledWith('art_mp3', expect.anything());
  });

  it('remakes audio with the same voice and deletes after a confirm', async () => {
    const generateBriefingAudio = vi.fn(async () => ({ ...EPISODE, id: 'med_3', status: 'Queued' }));
    const deleteMedia = vi.fn(async () => null);
    const listMedia = vi.fn(async () => page([EPISODE]));
    renderRoute('/dashboard/listen', { listMedia, generateBriefingAudio, deleteMedia });
    await openActions(EPISODE.title);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Remake audio' }));
    await waitFor(() => expect(generateBriefingAudio).toHaveBeenCalledWith('brf_1', 'voi_1'));
    expect(await screen.findByText('Audio queued')).toBeInTheDocument();
    await openActions(EPISODE.title);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(deleteMedia).toHaveBeenCalledWith('med_1'));
    expect(await screen.findByText('Episode deleted')).toBeInTheDocument();
  });

  it('shows the empty state', async () => {
    renderRoute('/dashboard/listen', { listMedia: vi.fn(async () => page([])) });
    expect(await screen.findByText('No audio yet')).toBeInTheDocument();
  });
});
