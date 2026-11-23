import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { DIMENSION, TOPIC } from '../../test/researchFixtures.js';
import { ApiError } from '../../utils/api.js';

// Automatic audio per topic: a switch beside the schedule (where runs are planned) and with the voice on Delivery.
const detail = (topic = {}) => ({ topic: { ...TOPIC, ...topic }, dimensions: [DIMENSION], constraints: [], recipients: [] });

function open(tab, overrides = {}, topic = {}) {
  return renderRoute('/dashboard/topics/top_1?tab=' + tab, {
    getTopic: vi.fn(async () => detail(topic)),
    listSchedules: vi.fn(async () => page([])),
    getAvailableVoices: vi.fn(async () => ({ enabled: true, defaultVoiceProfileId: null, voices: [] })),
    ...overrides
  });
}

describe('topic automatic audio', () => {
  it('is off by default on the Schedule tab and saves as soon as it is switched on', async () => {
    const updateTopic = vi.fn(async (id, body) => ({ ...TOPIC, ...body }));
    open('schedule', { updateTopic });
    const group = await screen.findByRole('group', { name: 'Audio' });
    const box = within(group).getByRole('checkbox');
    expect(box).not.toBeChecked();
    expect(group).toHaveTextContent('Make audio automatically');
    expect(group).toHaveTextContent('Audio counts toward the audio minutes of your plan.');
    await userEvent.click(box);
    await waitFor(() => expect(updateTopic).toHaveBeenCalledWith('top_1', { audioEnabled: true }));
    await waitFor(() => expect(within(screen.getByRole('group', { name: 'Audio' })).getByRole('checkbox')).toBeChecked());
  });

  it('shows the saved choice with the voice on Delivery and switches it off', async () => {
    const updateTopic = vi.fn(async (id, body) => ({ ...TOPIC, audioEnabled: true, ...body }));
    open('delivery', { updateTopic }, { audioEnabled: true });
    const box = within(await screen.findByRole('group', { name: 'Audio' })).getByRole('checkbox');
    expect(box).toBeChecked();
    expect(screen.getByRole('group', { name: 'Voice' })).toBeInTheDocument();
    await userEvent.click(box);
    await waitFor(() => expect(updateTopic).toHaveBeenCalledWith('top_1', { audioEnabled: false }));
  });

  it('keeps the choice and shows the reason when the save fails', async () => {
    const updateTopic = vi.fn(async () => {
      throw new ApiError(403, { error: 'Forbidden', message: 'You cannot change this topic.' }, 'You cannot change this topic.');
    });
    open('schedule', { updateTopic });
    const box = within(await screen.findByRole('group', { name: 'Audio' })).getByRole('checkbox');
    await userEvent.click(box);
    expect(await screen.findByText('You cannot change this topic.')).toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Audio' })).getByRole('checkbox')).not.toBeChecked();
  });
});
