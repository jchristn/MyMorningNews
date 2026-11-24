import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { page, renderRoute } from '../test/renderRoute.jsx';

const TEMPLATE = {
  id: 'pmt_1',
  tenantId: 'default',
  key: 'research.score',
  name: 'Score candidates',
  role: 'Scoring',
  currentVersion: 2,
  isCustomized: true,
  active: true,
  createdUtc: '2026-11-19T10:00:00Z',
  lastUpdateUtc: '2026-11-19T11:00:00Z'
};

const V1 = '---\nkey: research.score\nrole: Scoring\nvariables: [topic.brief]\n---\n## System\nRank items.\n\n## User\n{{topic.brief}}\n';
const V2 = '---\nkey: research.score\nrole: Scoring\nvariables: [topic.brief, candidate.title]\n---\n## System\nRank items strictly.\n\n## User\n{{topic.brief}}\n{{candidate.title}}\n';

function promptApi(overrides = {}) {
  return {
    listPrompts: vi.fn(async () => page([TEMPLATE, { ...TEMPLATE, id: 'pmt_2', key: 'research.score', topicId: 'top_1', isCustomized: false, currentVersion: 1 }])),
    getPrompt: vi.fn(async () => ({ template: TEMPLATE, content: V2, variables: ['topic.brief', 'candidate.title'], defaultContent: V1 })),
    listPromptVersions: vi.fn(async () => enumerationResult([
      { id: 'pmv_1', version: 1, content: V1, changeNote: null, createdUtc: '2026-11-19T10:00:00Z' },
      { id: 'pmv_2', version: 2, content: V2, changeNote: 'Stricter', createdUtc: '2026-11-19T11:00:00Z' }
    ])),
    listModelEndpoints: vi.fn(async () => page([{ id: 'mdl_1', name: 'Anthropic', model: 'claude-opus-5-5', supportsChat: true }])),
    ...overrides
  };
}

describe('PromptsView list', () => {
  it('lists prompts with scope, version, and customized flag', async () => {
    renderRoute('/dashboard/prompts', promptApi());
    expect((await screen.findAllByText('research.score')).length).toBe(2);
    expect(screen.getByText('Topic override')).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
    expect(screen.getByText('Customized', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getByText(/scope guardrail/)).toBeInTheDocument();
  });
});

describe('PromptEditor', () => {
  it('loads the template, its variables, and versions', async () => {
    renderRoute('/dashboard/prompts/pmt_1', promptApi());
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    expect(editor.value).toBe(V2);
    expect(screen.getByText('{{candidate.title}}')).toBeInTheDocument();
    expect(screen.getByText('Stricter')).toBeInTheDocument();
    expect(screen.getByText(/Editing a prompt cannot remove it/)).toBeInTheDocument();
  });

  it('shows server validation errors inline when a save is rejected', async () => {
    const savePrompt = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'The prompt is not valid.', details: { variables: 'Undeclared variables: foo.' } });
    });
    renderRoute('/dashboard/prompts/pmt_1', promptApi({ savePrompt }));
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    fireEvent.change(editor, { target: { value: V2 + '{{foo}}\n' } });
    await userEvent.type(screen.getByLabelText('Change note'), 'Add foo');
    await userEvent.click(screen.getByRole('button', { name: 'Save version' }));
    expect(await screen.findByText('The server rejected this version.')).toBeInTheDocument();
    expect(screen.getByText(/Undeclared variables: foo/)).toBeInTheDocument();
    expect(savePrompt).toHaveBeenCalledWith('pmt_1', V2 + '{{foo}}\n', 'Add foo');
  });

  it('diffs two versions', async () => {
    renderRoute('/dashboard/prompts/pmt_1', promptApi());
    await screen.findByLabelText('Template', { selector: 'textarea' });
    await userEvent.click(screen.getByRole('tab', { name: 'Compare' }));
    await userEvent.selectOptions(screen.getByLabelText('From'), 'v1');
    await userEvent.selectOptions(screen.getByLabelText('To'), 'v2');
    expect(await screen.findByText('3 lines added')).toBeInTheDocument();
    expect(screen.getByText('2 lines removed')).toBeInTheDocument();
    expect(screen.getByText('Rank items strictly.')).toBeInTheDocument();
  });

  it('rolls back to an earlier version after confirmation', async () => {
    const rollbackPrompt = vi.fn(async () => ({ template: { ...TEMPLATE, currentVersion: 3 }, content: V1, variables: ['topic.brief'], defaultContent: V1 }));
    renderRoute('/dashboard/prompts/pmt_1', promptApi({ rollbackPrompt }));
    await userEvent.click(await screen.findByRole('button', { name: 'Roll back to v1' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Roll back' }));
    await waitFor(() => expect(rollbackPrompt).toHaveBeenCalledWith('pmt_1', 1));
  });

  it('runs the prompt lab and shows the rendered prompts and output', async () => {
    const runPromptLab = vi.fn(async () => ({
      renderedSystem: 'Rank items strictly.',
      renderedUser: 'Solar\nPanel news',
      success: true,
      text: '{"score":0.9}',
      model: 'claude-opus-5-5',
      inputTokens: 100,
      outputTokens: 12,
      durationMs: 900,
      llmCallId: 'llm_9',
      unknownVariables: []
    }));
    renderRoute('/dashboard/prompts/pmt_1', promptApi({ runPromptLab, getLlmCall: vi.fn(async () => ({ id: 'llm_9', estimatedCostUsd: 0.0012 })) }));
    await screen.findByLabelText('Template', { selector: 'textarea' });
    await userEvent.click(screen.getByRole('tab', { name: 'Prompt lab' }));
    await userEvent.type(screen.getByLabelText('topic.brief'), 'Solar');
    await userEvent.click(screen.getByRole('button', { name: 'Run' }));
    const result = await screen.findByTestId('lab-result');
    expect(within(result).getByText('{"score":0.9}')).toBeInTheDocument();
    expect(within(result).getByText('Rendered user prompt')).toBeInTheDocument();
    expect(await within(result).findByText('Cost $0.0012')).toBeInTheDocument();
    expect(within(result).getByRole('link', { name: 'Open the logged call' })).toHaveAttribute('href', '/dashboard/models?tab=calls&call=llm_9');
    expect(runPromptLab).toHaveBeenCalledWith(expect.objectContaining({ key: 'research.score', variables: { 'topic.brief': 'Solar' } }));
  });
});
