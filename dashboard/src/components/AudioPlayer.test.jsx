import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createRef } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthProvider } from '../context/AuthContext.jsx';
import { ApiError } from '../utils/api.js';
import { fakeClient } from '../test/fakeClient.js';
import { CHAPTERS, audioResult, installMediaShims, setMediaProperty } from '../test/mediaFixtures.js';
import AudioPlayer from './AudioPlayer.jsx';

function renderPlayer(props = {}, overrides = {}) {
  const client = fakeClient({ getArtifactContent: vi.fn(async () => audioResult()), ...overrides });
  const utils = render(
    <AuthProvider clientFactory={() => client}>
      <AudioPlayer artifactId="art_mp3" chapters={CHAPTERS} durationSeconds={240} bytes={2400000} title="Morning News" {...props} />
    </AuthProvider>
  );
  return { client, ...utils };
}

async function ready() {
  const audio = await screen.findByTestId('audio-element');
  setMediaProperty(audio, 'currentTime', 0);
  setMediaProperty(audio, 'duration', 240);
  fireEvent.loadedMetadata(audio);
  return audio;
}

function tick(audio, seconds) {
  audio.currentTime = seconds;
  fireEvent.timeUpdate(audio);
}

beforeEach(() => installMediaShims());
afterEach(() => vi.restoreAllMocks());

describe('AudioPlayer', () => {
  it('fetches the audio with auth into a blob URL, shows a loading state first, and revokes the URL on unmount', async () => {
    let resolve;
    const getArtifactContent = vi.fn(() => new Promise((r) => (resolve = r)));
    const { unmount } = renderPlayer({}, { getArtifactContent });
    expect(screen.getByText(/Loading audio \(2\.3 MB\)/)).toBeInTheDocument();
    expect(getArtifactContent).toHaveBeenCalledWith('art_mp3', expect.objectContaining({ signal: expect.anything() }));
    await act(async () => resolve(audioResult()));
    const audio = await screen.findByTestId('audio-element');
    expect(audio).toHaveAttribute('src', 'blob:audio-1');
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    unmount();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:audio-1');
  });

  it('draws a chapter tick on the progress bar and lists chapters', async () => {
    renderPlayer();
    await ready();
    const ticks = screen.getByTestId('audio-progress').querySelectorAll('.audio-progress-tick');
    expect(ticks).toHaveLength(1);
    expect(ticks[0].getAttribute('data-fraction')).toBe('0.5000');
    expect(ticks[0].getAttribute('x')).toBe('50.000%');
    const list = screen.getByRole('list', { name: 'Chapters' });
    expect(within(list).getAllByRole('button')).toHaveLength(2);
    expect(screen.getByTestId('audio-clock')).toHaveTextContent('0:00 / 4:00');
  });

  it('highlights the chapter playing and reports its briefing section', async () => {
    const onSectionChange = vi.fn();
    renderPlayer({ onSectionChange });
    const audio = await ready();
    await waitFor(() => expect(onSectionChange).toHaveBeenLastCalledWith('bsc_1'));
    expect(screen.getByRole('button', { name: /Top of the news/ })).toHaveAttribute('aria-current', 'true');
    tick(audio, 130);
    await waitFor(() => expect(onSectionChange).toHaveBeenLastCalledWith('bsc_2'));
    expect(screen.getByRole('button', { name: /Sources/ })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('button', { name: /Top of the news/ })).not.toHaveAttribute('aria-current');
    expect(screen.getByText('Now playing: Sources')).toBeInTheDocument();
    expect(screen.getByRole('slider', { name: 'Playback position' })).toHaveAttribute('aria-valuetext', '2:10 of 4:00, Sources');
  });

  it('seeks and plays from a chapter, skips back 15 and forward 30 within bounds, and toggles play', async () => {
    renderPlayer();
    const audio = await ready();
    await userEvent.click(screen.getByRole('button', { name: /Sources/ }));
    expect(audio.currentTime).toBe(120);
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Pause' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Back 15 seconds' }));
    expect(audio.currentTime).toBe(105);
    await userEvent.click(screen.getByRole('button', { name: 'Forward 30 seconds' }));
    expect(audio.currentTime).toBe(135);
    tick(audio, 230);
    await userEvent.click(screen.getByRole('button', { name: 'Forward 30 seconds' }));
    expect(audio.currentTime).toBe(240);
    tick(audio, 5);
    await userEvent.click(screen.getByRole('button', { name: 'Back 15 seconds' }));
    expect(audio.currentTime).toBe(0);
    await userEvent.click(screen.getByRole('button', { name: 'Pause' }));
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Play' })).toBeInTheDocument();
  });

  it('changes the speed from 0.75x to 2x and moves with the keyboard on the progress bar', async () => {
    renderPlayer();
    const audio = await ready();
    const speed = screen.getByRole('combobox', { name: 'Playback speed' });
    expect(within(speed).getAllByRole('option').map((o) => o.textContent)).toEqual(['0.75x', '1x', '1.25x', '1.5x', '1.75x', '2x']);
    await userEvent.selectOptions(speed, '1.5');
    expect(audio.playbackRate).toBe(1.5);
    const slider = screen.getByRole('slider', { name: 'Playback position' });
    fireEvent.keyDown(slider, { key: 'ArrowRight' });
    expect(audio.currentTime).toBe(5);
    fireEvent.keyDown(slider, { key: 'End' });
    expect(audio.currentTime).toBe(240);
    fireEvent.keyDown(slider, { key: 'Home' });
    expect(audio.currentTime).toBe(0);
  });

  it('seeks to a section through apiRef and reports when a section has no chapter', async () => {
    const apiRef = createRef();
    renderPlayer({ apiRef });
    const audio = await ready();
    let found;
    act(() => {
      found = apiRef.current.seekToSection('bsc_2');
    });
    expect(found).toBe(true);
    expect(audio.currentTime).toBe(120);
    act(() => {
      found = apiRef.current.seekToSection('bsc_missing');
    });
    expect(found).toBe(false);
  });

  it('hides the chapter list in compact mode', async () => {
    renderPlayer({ compact: true });
    await ready();
    expect(screen.queryByRole('list', { name: 'Chapters' })).not.toBeInTheDocument();
    expect(screen.getByRole('slider', { name: 'Playback position' })).toBeInTheDocument();
  });

  it('shows an error with retry when the audio cannot be fetched', async () => {
    const getArtifactContent = vi.fn().mockRejectedValueOnce(new ApiError(410, { error: 'ArtifactExpired', message: 'The audio file has expired.' })).mockResolvedValueOnce(audioResult());
    renderPlayer({}, { getArtifactContent });
    expect(await screen.findByText('The audio file has expired.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByTestId('audio-element')).toBeInTheDocument();
    expect(getArtifactContent).toHaveBeenCalledTimes(2);
  });
});
