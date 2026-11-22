import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { DIMENSION, TOPIC } from '../../test/researchFixtures.js';
import { ApiError } from '../../utils/api.js';
import { voiceKey, voiceName } from './TopicVoice.jsx';

// The voice per topic on the topic's Settings > Delivery view: the current voice, every available voice with a
// preview, Use this voice, and Use the workspace default.
const DEFAULT_VOICE = { voice: 'af_heart', label: 'Heart (American English, female)', voiceProfileId: 'voi_1', name: 'Kokoro (local)', isDefault: true };
const EMMA = { voice: 'bf_emma', label: 'Emma (British English, female)', voiceProfileId: null, name: null, isDefault: false };
const GEORGE = { voice: 'bm_george', label: 'George (British English, male)', voiceProfileId: null, name: null, isDefault: false };
const VOICES = { enabled: true, listed: true, defaultVoiceProfileId: 'voi_1', voices: [DEFAULT_VOICE, EMMA, GEORGE] };
const EMMA_PROFILE = { ...EMMA, voiceProfileId: 'voi_2', name: 'Emma (British English, female)' };
const detail = (topic = {}) => ({ topic: { ...TOPIC, ...topic }, dimensions: [DIMENSION], constraints: [], recipients: [] });
const sample = () => ({ blob: new Blob(['RIFF'], { type: 'audio/wav' }), contentType: 'audio/wav' });

function open(overrides = {}, topic = {}) {
  return renderRoute('/dashboard/topics/top_1?tab=delivery', {
    getTopic: vi.fn(async () => detail(topic)),
    listSchedules: vi.fn(async () => page([])),
    getAvailableVoices: vi.fn(async () => VOICES),
    previewVoice: vi.fn(async () => sample()),
    ...overrides
  });
}

beforeEach(() => {
  let n = 0;
  URL.createObjectURL = vi.fn(() => 'blob:voice-' + ++n);
  URL.revokeObjectURL = vi.fn();
});

describe('topic voice helpers', () => {
  it('keys a voice by its profile or its server ID and names it by its profile or label', () => {
    expect(voiceKey(DEFAULT_VOICE)).toBe('profile:voi_1');
    expect(voiceKey(EMMA)).toBe('voice:bf_emma');
    expect(voiceName(DEFAULT_VOICE)).toBe('Kokoro (local)');
    expect(voiceName(EMMA)).toBe('Emma (British English, female)');
    expect(voiceName({ voice: 'studio-one' })).toBe('studio-one');
    expect(voiceName(null)).toBe('');
  });
});

describe('topic voice picker', () => {
  it('shows the current voice and every available voice with Play and Use this voice', async () => {
    open();
    const picker = await screen.findByTestId('topic-voice');
    expect(screen.getByRole('group', { name: 'Voice' })).toContainElement(picker);
    expect(await within(picker).findByTestId('topic-voice-current')).toHaveTextContent('This topic uses the workspace default voice, Kokoro (local).');
    expect(within(picker).queryByRole('button', { name: 'Use the workspace default' })).not.toBeInTheDocument();
    const rows = within(within(picker).getByRole('list', { name: 'Available voices' })).getAllByRole('listitem');
    expect(rows).toHaveLength(3);
    expect(rows[0]).toHaveTextContent('Kokoro (local)');
    expect(rows[0]).toHaveTextContent('Heart (American English, female) · af_heart · Workspace default');
    expect(rows[1]).toHaveTextContent('Emma (British English, female)');
    expect(rows[1]).toHaveTextContent('bf_emma');
    for (const row of rows) {
      expect(within(row).getByRole('button', { name: /^Play a sample of / })).toBeEnabled();
      expect(within(row).getByRole('button', { name: /^Use .* for this topic$/ })).toBeEnabled();
    }
  });

  it('plays one sample at a time, with a loading state, and stops', async () => {
    let finish;
    const previewVoice = vi
      .fn()
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finish = () => resolve(sample());
          })
      )
      .mockImplementation(async () => sample());
    open({ previewVoice });
    const emma = await screen.findByTestId('voice-voice:bf_emma');
    await userEvent.click(within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' }));
    // Loading: the button says so and cannot be pressed twice.
    const loading = within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' });
    expect(loading).toBeDisabled();
    expect(loading).toHaveTextContent('Loading');
    expect(previewVoice).toHaveBeenCalledWith({ voice: 'bf_emma' }, undefined);
    finish();
    const audio = await screen.findByTestId('voice-preview-audio');
    expect(audio).toHaveAttribute('src', 'blob:voice-1');
    expect(audio).toHaveAttribute('autoplay');
    expect(within(emma).getByRole('button', { name: 'Stop the sample of Emma (British English, female)' })).toHaveTextContent('Stop');

    // Playing another voice replaces the first: one audio element, the first sample released.
    const first = screen.getByTestId('voice-profile:voi_1');
    await userEvent.click(within(first).getByRole('button', { name: 'Play a sample of Kokoro (local)' }));
    await waitFor(() => expect(screen.getByTestId('voice-preview-audio')).toHaveAttribute('src', 'blob:voice-2'));
    expect(previewVoice).toHaveBeenLastCalledWith({ voiceProfileId: 'voi_1' }, undefined);
    expect(screen.getAllByTestId('voice-preview-audio')).toHaveLength(1);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:voice-1');
    expect(within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' })).toHaveTextContent('Play');

    // Stop, and the end of a sample, both clear the player.
    await userEvent.click(within(first).getByRole('button', { name: 'Stop the sample of Kokoro (local)' }));
    expect(screen.queryByTestId('voice-preview-audio')).not.toBeInTheDocument();
    await userEvent.click(within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' }));
    fireEvent.ended(await screen.findByTestId('voice-preview-audio'));
    expect(screen.queryByTestId('voice-preview-audio')).not.toBeInTheDocument();
  });

  it('says clearly when speech is not available', async () => {
    const previewVoice = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(502, { error: 'TtsError', message: 'Could not reach the speech endpoint at http://tts:8880/v1.' }))
      .mockRejectedValueOnce(new ApiError(409, { error: 'Conflict', message: 'Speech is turned off on this server, so there is nothing to play.' }))
      .mockRejectedValueOnce(new ApiError(429, { error: 'TooManyRequests', message: 'Too many voice previews. Try again in a minute.' }));
    open({ previewVoice });
    const emma = await screen.findByTestId('voice-voice:bf_emma');
    const play = () => userEvent.click(within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' }));
    await play();
    expect(await screen.findByText('The speech service failed: Could not reach the speech endpoint at http://tts:8880/v1.')).toBeInTheDocument();
    expect(screen.queryByTestId('voice-preview-audio')).not.toBeInTheDocument();
    expect(within(emma).getByRole('button', { name: 'Play a sample of Emma (British English, female)' })).toBeEnabled();
    await play();
    expect(await screen.findByText('Speech is turned off on this server, so there is no audio to play. A server administrator can turn it on in Server settings.')).toBeInTheDocument();
    await play();
    expect(await screen.findByText('Too many voice previews. Try again in a minute.')).toBeInTheDocument();
  });

  it('warns up front when speech is turned off on the server', async () => {
    open({ getAvailableVoices: vi.fn(async () => ({ ...VOICES, enabled: false })) });
    const picker = await screen.findByTestId('topic-voice');
    expect(await within(picker).findByText('Speech is turned off on this server, so there is no audio to play. A server administrator can turn it on in Server settings.')).toBeInTheDocument();
    expect(within(picker).getAllByRole('listitem')).toHaveLength(3);
  });

  it('uses a speech server voice for the topic, then goes back to the workspace default', async () => {
    const getAvailableVoices = vi.fn().mockResolvedValueOnce(VOICES).mockResolvedValue({ ...VOICES, voices: [DEFAULT_VOICE, EMMA_PROFILE, GEORGE] });
    const updateTopic = vi.fn(async (id, body) => ({ ...TOPIC, voiceProfileId: body.voice ? 'voi_2' : body.voiceProfileId || null }));
    const client = open({ getAvailableVoices, updateTopic });
    const emma = await screen.findByTestId('voice-voice:bf_emma');
    await userEvent.click(within(emma).getByRole('button', { name: 'Use Emma (British English, female) for this topic' }));
    await waitFor(() => expect(client.updateTopic).toHaveBeenCalledWith('top_1', { voice: 'bf_emma' }));
    expect(await screen.findByText('Emma (British English, female) now reads this topic')).toBeInTheDocument();
    // The voice now has a profile: it is the current one, marked in use, and cannot be chosen again.
    const row = await screen.findByTestId('voice-profile:voi_2');
    expect(screen.getByTestId('topic-voice-current')).toHaveTextContent('This topic uses Emma (British English, female).');
    expect(row).toHaveClass('current');
    expect(row).toHaveTextContent('In use');
    expect(within(row).getByRole('button', { name: 'Use Emma (British English, female) for this topic' })).toBeDisabled();
    expect(getAvailableVoices).toHaveBeenCalledTimes(2);

    await userEvent.click(screen.getByRole('button', { name: 'Use the workspace default' }));
    await waitFor(() => expect(client.updateTopic).toHaveBeenLastCalledWith('top_1', { voiceProfileId: '' }));
    expect(await screen.findByText('This topic uses the workspace default voice again')).toBeInTheDocument();
    expect(screen.getByTestId('topic-voice-current')).toHaveTextContent('This topic uses the workspace default voice, Kokoro (local).');
    expect(screen.queryByRole('button', { name: 'Use the workspace default' })).not.toBeInTheDocument();
  });

  it('uses a voice profile by its ID and shows a refusal from the server', async () => {
    const updateTopic = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(400, { error: 'ValidationFailed', message: 'voiceProfileId: voi_1 is not a voice profile in this workspace.' }))
      .mockResolvedValue({ ...TOPIC, voiceProfileId: 'voi_1' });
    const client = open({ updateTopic }, { voiceProfileId: 'voi_9' });
    // A topic whose voice is not in the list (deleted elsewhere) reads as the default, with the way to clear it.
    expect(await screen.findByTestId('topic-voice-current')).toHaveTextContent('This topic uses the workspace default voice, Kokoro (local).');
    const first = screen.getByTestId('voice-profile:voi_1');
    await userEvent.click(within(first).getByRole('button', { name: 'Use Kokoro (local) for this topic' }));
    expect(await screen.findByText('voiceProfileId: voi_1 is not a voice profile in this workspace.')).toBeInTheDocument();
    await userEvent.click(within(first).getByRole('button', { name: 'Use Kokoro (local) for this topic' }));
    await waitFor(() => expect(client.updateTopic).toHaveBeenLastCalledWith('top_1', { voiceProfileId: 'voi_1' }));
    expect(await screen.findByText('Kokoro (local) now reads this topic')).toBeInTheDocument();
    expect(screen.getByTestId('topic-voice-current')).toHaveTextContent('This topic uses Kokoro (local).');
  });

  it('offers a retry when the voices cannot be listed, and leaves the rest of Delivery working', async () => {
    const getAvailableVoices = vi.fn().mockRejectedValueOnce(new ApiError(500, { error: 'InternalError', message: 'The voices could not be read.' })).mockResolvedValue(VOICES);
    open({ getAvailableVoices });
    const picker = await screen.findByTestId('topic-voice');
    expect(await within(picker).findByText('The voices could not be read.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save delivery options' })).toBeEnabled();
    await userEvent.click(within(picker).getByRole('button', { name: 'Retry' }));
    expect(await screen.findByTestId('voice-voice:bf_emma')).toBeInTheDocument();
  });
});
