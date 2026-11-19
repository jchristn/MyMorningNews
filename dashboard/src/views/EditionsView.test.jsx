import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { EDITION, MARKDOWN, TEMPLATE, TOPICS, editionDetail } from '../test/briefingFixtures.js';
import { editionBody, formFromEdition } from './editions/EditionEditor.jsx';
import { moveItem } from './editions/EditionTopicPicker.jsx';

function editionApi(overrides = {}) {
  return {
    listEditions: vi.fn(async () => page([EDITION])),
    getEdition: vi.fn(async () => editionDetail()),
    listBriefingTemplates: vi.fn(async () => page([TEMPLATE, { ...TEMPLATE, id: 'btp_2', name: 'Evening', userId: 'usr_admin', isDefault: false, defaultKey: undefined }])),
    listTopics: vi.fn(async () => page(TOPICS)),
    ...overrides
  };
}

describe('EditionsView list', () => {
  it('lists editions with delivery badges and look-back', async () => {
    renderRoute('/dashboard/editions', editionApi());
    expect(await screen.findByText('Morning News')).toBeInTheDocument();
    expect(screen.getByText('Fresh research (45 min)')).toBeInTheDocument();
    expect(screen.getByText('Since the last edition')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /New edition/ })).toBeInTheDocument();
  });
});

describe('EditionEditor', () => {
  it('loads the edition and saves changed settings', async () => {
    const updateEdition = vi.fn(async (id, body) => editionDetail({ edition: { ...EDITION, ...body } }));
    renderRoute('/dashboard/editions/edn_1', editionApi({ updateEdition }));
    const name = await screen.findByLabelText('Name');
    expect(name).toHaveValue('Morning News');
    expect(screen.getByRole('button', { name: /Save edition/ })).toBeDisabled();
    await userEvent.selectOptions(screen.getByLabelText('Template'), 'btp_2');
    await userEvent.selectOptions(screen.getByLabelText('Look-back'), 'FixedHours');
    await userEvent.clear(screen.getByLabelText('Look-back window (hours)'));
    await userEvent.type(screen.getByLabelText('Look-back window (hours)'), '5000');
    await userEvent.click(screen.getByLabelText('Audio'));
    await userEvent.click(screen.getByRole('button', { name: /Save edition/ }));
    await waitFor(() => expect(updateEdition).toHaveBeenCalled());
    expect(updateEdition.mock.calls[0][0]).toBe('edn_1');
    expect(updateEdition.mock.calls[0][1]).toMatchObject({ briefingTemplateId: 'btp_2', lookbackMode: 'FixedHours', lookbackHours: 2160, audioEnabled: false });
  });

  it('chooses topics in order and saves them', async () => {
    const setEditionTopics = vi.fn(async () => editionDetail({ topics: [{ id: 'etp_1', topicId: 'top_2', sortOrder: 0 }] }));
    renderRoute('/dashboard/editions/edn_1', editionApi({ setEditionTopics }));
    expect(await screen.findByText('Covers every active recurring topic, including topics you add later.')).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: 'Topics covered' })).getByText('Solar power')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('radio', { name: 'Choose topics' }));
    await userEvent.selectOptions(screen.getByLabelText('Topic to add'), 'top_1');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.selectOptions(screen.getByLabelText('Topic to add'), 'top_2');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move Solar power up' }));
    await userEvent.click(screen.getByRole('button', { name: /Save topics/ }));
    await waitFor(() => expect(setEditionTopics).toHaveBeenCalledWith('edn_1', ['top_2', 'top_1']));
  });

  it('previews the edition and renders the Markdown', async () => {
    const previewEdition = vi.fn(async () => ({ title: 'Morning News', markdown: MARKDOWN, modelFallbacks: 1, isDryRun: true }));
    renderRoute('/dashboard/editions/edn_1', editionApi({ previewEdition }));
    await userEvent.click(await screen.findByRole('button', { name: /Preview/ }));
    const preview = await screen.findByTestId('edition-preview');
    expect(within(preview).getByRole('heading', { level: 1, name: 'Morning News for November 19, 2026' })).toBeInTheDocument();
    expect(within(preview).getByText('1 section was written as a plain list because no model answered.')).toBeInTheDocument();
    expect(previewEdition).toHaveBeenCalledWith('edn_1', expect.anything());
  });

  it('creates a new edition and opens it', async () => {
    const createEdition = vi.fn(async () => editionDetail({ edition: { ...EDITION, id: 'edn_2', name: 'Evening' } }));
    renderRoute('/dashboard/editions/new', editionApi({ createEdition, getEdition: vi.fn(async () => editionDetail({ edition: { ...EDITION, id: 'edn_2', name: 'Evening' } })) }));
    await userEvent.type(await screen.findByLabelText('Name'), 'Evening');
    await userEvent.click(screen.getByRole('button', { name: /Create edition/ }));
    await waitFor(() => expect(createEdition).toHaveBeenCalledWith(expect.objectContaining({ name: 'Evening', briefingTemplateId: null })));
    expect(await screen.findByRole('heading', { level: 1, name: 'Evening' })).toBeInTheDocument();
  });
});

describe('edition helpers', () => {
  it('clamps values and sends an empty template id to clear it on update', () => {
    const body = editionBody({ ...formFromEdition(EDITION), freshResearchLeadMinutes: '1', lookbackHours: '0' }, false);
    expect(body).toMatchObject({ freshResearchLeadMinutes: 5, lookbackHours: 1, briefingTemplateId: '' });
    expect(moveItem(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
    expect(moveItem(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
  });
});
