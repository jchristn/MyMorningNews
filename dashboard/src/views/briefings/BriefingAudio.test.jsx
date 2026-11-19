import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../../utils/api.js';
import { EMPTY_PAGE } from '../../test/fakeClient.js';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { BRIEFING, EDITION, detail } from '../../test/briefingFixtures.js';
import { EPISODE, VOICE, audioResult, installMediaShims, mediaDetail, setMediaProperty } from '../../test/mediaFixtures.js';
import { playableEpisode } from './BriefingAudio.jsx';

beforeEach(() => installMediaShims());
afterEach(() => vi.restoreAllMocks());

function readerApi(overrides = {}) {
  return {
    getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, mediaAssetId: 'med_1' } })),
    listMedia: vi.fn(async () => page([EPISODE])),
    getMedia: vi.fn(async () => mediaDetail()),
    getArtifactContent: vi.fn(async () => audioResult()),
    listVoices: vi.fn(async () => page([VOICE])),
    ...overrides
  };
}

describe('playableEpisode', () => {
  it('prefers the briefing episode, then the newest ready one', () => {
    const older = { ...EPISODE, id: 'med_0' };
    const queued = { ...EPISODE, id: 'med_2', status: 'Queued' };
    expect(playableEpisode([queued, EPISODE, older], { mediaAssetId: 'med_0' }).id).toBe('med_0');
    expect(playableEpisode([queued, EPISODE, older], {}).id).toBe('med_1');
    expect(playableEpisode([queued], {})).toBeNull();
    expect(playableEpisode(null, {})).toBeNull();
  });
});

describe('BriefingReader audio', () => {
  it('plays a ready episode and highlights the section being read in the outline and the article', async () => {
    const api = renderRoute('/dashboard/briefings/brf_1', readerApi());
    const audio = await screen.findByTestId('audio-element');
    expect(api.listMedia).toHaveBeenCalledWith(expect.objectContaining({ briefingId: 'brf_1', sortBy: 'createdUtc', sortDirection: 'Descending' }));
    expect(api.getMedia).toHaveBeenCalledWith('med_1');
    expect(api.getArtifactContent).toHaveBeenCalledWith('art_mp3', expect.anything());
    expect(document.querySelector('[data-slot="audio-player"]')).toBeInTheDocument();
    setMediaProperty(audio, 'currentTime', 0);
    setMediaProperty(audio, 'duration', 240);
    fireEvent.loadedMetadata(audio);

    const top = screen.getByRole('region', { name: 'Top of the news' });
    await waitFor(() => expect(top).toHaveAttribute('data-speaking', 'true'));
    const outline = screen.getByRole('navigation', { name: 'Outline' });
    expect(within(outline).getByRole('link', { name: /Top of the news/ })).toHaveAttribute('aria-current', 'true');

    audio.currentTime = 130;
    fireEvent.timeUpdate(audio);
    await waitFor(() => expect(screen.getByRole('region', { name: 'Sources' })).toHaveAttribute('data-speaking', 'true'));
    expect(top).not.toHaveAttribute('data-speaking');

    await userEvent.click(within(outline).getByRole('button', { name: 'Listen from Top of the news' }));
    expect(audio.currentTime).toBe(0);
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalled();
  });

  it('shows the status while audio is being made, polls, and then plays it', async () => {
    let calls = 0;
    const listMedia = vi.fn(async () => {
      calls += 1;
      return page([calls === 1 ? { ...EPISODE, status: 'Synthesizing', artifactId: undefined } : EPISODE]);
    });
    renderRoute('/dashboard/briefings/brf_1', readerApi({ getBriefing: vi.fn(async () => detail()), listMedia }));
    expect(await screen.findByTestId('audio-status')).toHaveTextContent('Synthesizing speech.');
    expect(screen.queryByRole('button', { name: /Make audio/ })).not.toBeInTheDocument();
    expect(await screen.findByTestId('audio-element', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByTestId('audio-status')).not.toBeInTheDocument();
  }, 10000);

  it('shows the error when audio failed and makes it again', async () => {
    const failed = { ...EPISODE, status: 'Failed', artifactId: undefined, errorMessage: 'The speech endpoint did not answer.' };
    const generateBriefingAudio = vi.fn(async () => ({ ...EPISODE, id: 'med_2', status: 'Queued' }));
    const api = renderRoute('/dashboard/briefings/brf_1', readerApi({ getBriefing: vi.fn(async () => detail()), listMedia: vi.fn(async () => page([failed])), generateBriefingAudio }));
    expect(await screen.findByTestId('audio-failed')).toHaveTextContent('Making audio failed. The speech endpoint did not answer.');
    await userEvent.click(screen.getByRole('button', { name: 'Make audio again' }));
    await waitFor(() => expect(generateBriefingAudio).toHaveBeenCalledWith('brf_1', null));
    expect(await screen.findByText('Audio queued')).toBeInTheDocument();
    expect(api.listMedia.mock.calls.length).toBeGreaterThan(1);
  });

  it('offers Make audio with a voice picker when there is none, and explains a conflict', async () => {
    const generateBriefingAudio = vi.fn(async () => {
      throw new ApiError(409, { error: 'Conflict', message: 'Audio is turned off on this server (tts.enabled).' });
    });
    renderRoute('/dashboard/briefings/brf_1', readerApi({ getBriefing: vi.fn(async () => detail()), listMedia: vi.fn(async () => EMPTY_PAGE), generateBriefingAudio }));
    expect(await screen.findByText('This briefing has no audio yet.')).toBeInTheDocument();
    const picker = screen.getByRole('combobox', { name: 'Voice' });
    await waitFor(() => expect(within(picker).getByRole('option', { name: 'Kokoro narrator (default)' })).toBeInTheDocument());
    await userEvent.selectOptions(picker, 'voi_1');
    await userEvent.click(screen.getByRole('button', { name: 'Make audio' }));
    await waitFor(() => expect(generateBriefingAudio).toHaveBeenCalledWith('brf_1', 'voi_1'));
    expect(await screen.findByText(/Audio is turned off on this server/)).toBeInTheDocument();
  });

  it('does not look for audio while the briefing is still composing', async () => {
    const api = renderRoute('/dashboard/briefings/brf_1', readerApi({ getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, status: 'Composing', markdown: '' } })) }));
    expect(await screen.findByTestId('briefing-progress')).toBeInTheDocument();
    expect(api.listMedia).not.toHaveBeenCalled();
    expect(document.querySelector('[data-slot="audio-player"]')).toBeNull();
  });
});

describe('Today audio', () => {
  it('shows a compact player for the latest edition episode', async () => {
    renderRoute('/dashboard/today', {
      ...readerApi(),
      listEditions: vi.fn(async () => page([EDITION])),
      listBriefings: vi.fn(async (q) => (q.kind === 'Edition' && q.status === 'Ready' ? page([BRIEFING]) : EMPTY_PAGE))
    });
    const panel = await screen.findByTestId('today-briefing');
    expect(await within(panel).findByTestId('audio-element')).toBeInTheDocument();
    expect(within(panel).getByRole('slider', { name: 'Playback position' })).toBeInTheDocument();
    expect(within(panel).queryByRole('list', { name: 'Chapters' })).not.toBeInTheDocument();
  });
});
