import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { VOICE, installMediaShims } from '../test/mediaFixtures.js';
import { clampSpeed, voiceBody } from './VoicesView.jsx';

beforeEach(() => installMediaShims());
afterEach(() => vi.restoreAllMocks());

const SECOND = { ...VOICE, id: 'voi_2', name: 'OpenAI alloy', voice: 'alloy', secondaryVoice: undefined, model: 'tts-1', endpoint: 'https://api.openai.com/v1', speed: 1.25, isDefault: false, credentialId: 'vlt_1' };

async function openActions(name) {
  const row = (await screen.findByText(name)).closest('tr');
  await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
}

describe('voice form helpers', () => {
  it('clamps the speed and builds the request body', () => {
    expect(clampSpeed(3)).toBe(2);
    expect(clampSpeed(0.1)).toBe(0.5);
    expect(clampSpeed('x')).toBe(1);
    expect(voiceBody({ name: ' A ', provider: 'OpenAiCompatibleSpeech', endpoint: ' ', model: '', voice: 'af_heart', secondaryVoice: '', speed: '1.5', credentialId: '', isDefault: true })).toEqual({
      name: 'A',
      provider: 'OpenAiCompatibleSpeech',
      endpoint: '',
      model: undefined,
      voice: 'af_heart',
      secondaryVoice: '',
      speed: 1.5,
      credentialId: '',
      isDefault: true
    });
  });
});

describe('VoicesView', () => {
  it('lists voices with the default badge, second voice, and server default endpoint', async () => {
    const api = renderRoute('/dashboard/voices', { listVoices: vi.fn(async () => page([VOICE, SECOND])) });
    const row = (await screen.findByText('Kokoro narrator')).closest('tr');
    expect(within(row).getByText('Default', { selector: '.badge' })).toBeInTheDocument();
    expect(within(row).getByText('Second host: am_adam')).toBeInTheDocument();
    expect(within(row).getByText('Server default')).toBeInTheDocument();
    expect(screen.getByText('https://api.openai.com/v1')).toBeInTheDocument();
    expect(screen.getByText(/HOST_B uses the second voice/)).toBeInTheDocument();
    expect(api.listVoices).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'name', sortDirection: 'Ascending' }));
  });

  it('creates a voice with a second voice and a clamped speed', async () => {
    const createVoice = vi.fn(async (body) => ({ ...VOICE, ...body, id: 'voi_3' }));
    renderRoute('/dashboard/voices', { listVoices: vi.fn(async () => page([])), createVoice, listVaultCredentials: vi.fn(async () => page([{ id: 'vlt_1', name: 'openai-key' }])) });
    expect(await screen.findByText('No voices yet')).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: /Add/ })[0]);
    await userEvent.type(screen.getByLabelText('Name'), 'Two hosts');
    await userEvent.type(screen.getByLabelText('Voice', { selector: 'input' }), 'af_heart');
    await userEvent.type(screen.getByLabelText('Second voice'), 'am_adam');
    const speed = screen.getByLabelText('Speed', { selector: 'input[type="number"]' });
    await userEvent.clear(speed);
    await userEvent.type(speed, '5');
    await userEvent.tab();
    await waitFor(() => expect(screen.getByRole('option', { name: 'openai-key' })).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText('Credential'), 'vlt_1');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(createVoice).toHaveBeenCalled());
    expect(createVoice.mock.calls[0][0]).toMatchObject({ name: 'Two hosts', voice: 'af_heart', secondaryVoice: 'am_adam', speed: 2, credentialId: 'vlt_1', provider: 'OpenAiCompatibleSpeech', endpoint: '' });
  });

  it('makes a voice the default and deletes one after a confirm', async () => {
    const updateVoice = vi.fn(async () => ({ ...SECOND, isDefault: true }));
    const deleteVoice = vi.fn(async () => null);
    renderRoute('/dashboard/voices', { listVoices: vi.fn(async () => page([VOICE, SECOND])), updateVoice, deleteVoice });
    await openActions('OpenAI alloy');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Make default' }));
    await waitFor(() => expect(updateVoice).toHaveBeenCalledWith('voi_2', { isDefault: true }));
    expect(await screen.findByText('OpenAI alloy is now the default voice')).toBeInTheDocument();
    await openActions('Kokoro narrator');
    expect(screen.getByRole('menuitem', { name: 'Make default' })).toBeDisabled();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(deleteVoice).toHaveBeenCalledWith('voi_1'));
  });

  it('tests a voice with custom text and plays the sample', async () => {
    const testVoice = vi.fn(async () => ({ blob: new Blob(['RIFF'], { type: 'audio/wav' }), contentType: 'audio/wav' }));
    renderRoute('/dashboard/voices', { listVoices: vi.fn(async () => page([VOICE])), testVoice });
    await openActions('Kokoro narrator');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Test' }));
    const dialog = screen.getByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Sample text'), 'Hello from the test.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Speak' }));
    await waitFor(() => expect(testVoice).toHaveBeenCalledWith('voi_1', 'Hello from the test.'));
    expect(await within(dialog).findByTestId('voice-sample')).toHaveAttribute('src', 'blob:audio-1');
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Close' }).at(-1));
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:audio-1');
  });

  it('shows the speech error when the test fails with 502', async () => {
    const testVoice = vi.fn(async () => {
      throw new ApiError(502, { error: 'TtsError', message: 'Connection refused.' });
    });
    renderRoute('/dashboard/voices', { listVoices: vi.fn(async () => page([VOICE])), testVoice });
    await openActions('Kokoro narrator');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Test' }));
    await userEvent.click(screen.getByRole('button', { name: 'Speak' }));
    await waitFor(() => expect(testVoice).toHaveBeenCalledWith('voi_1', 'Good morning. Here is what moved in your topics.'));
    expect(await screen.findByText('The speech service failed: Connection refused.')).toBeInTheDocument();
  });

  it('is read-only for members, who can still test', async () => {
    renderRoute('/dashboard/voices', { getTokenDetails: async () => MEMBER_DETAILS, listVoices: vi.fn(async () => page([VOICE])) });
    expect(await screen.findByText(/requires a tenant administrator/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Add/ })).toBeDisabled();
    await openActions('Kokoro narrator');
    expect(screen.getByRole('menuitem', { name: 'Edit' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Delete' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Make default' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Test' })).toBeEnabled();
  });
});
