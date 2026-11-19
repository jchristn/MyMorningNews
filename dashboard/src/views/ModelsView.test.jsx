import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { forbidden, page, renderRoute } from '../test/renderRoute.jsx';
import { endpointBody, formFromEndpoint, validateEndpointForm } from './models/EndpointFormModal.jsx';
import i18next from '../i18n/index.js';

const ANTHROPIC = {
  id: 'mdl_1',
  tenantId: 'default',
  name: 'Anthropic',
  provider: 'Anthropic',
  model: 'claude-opus-5-5',
  supportsChat: true,
  supportsTools: true,
  supportsEmbeddings: false,
  maxTokens: 16000,
  timeoutMs: 300000,
  inputCostPerMTok: 4,
  outputCostPerMTok: 20,
  active: true,
  createdUtc: '2026-11-19T10:00:00Z',
  lastUpdateUtc: '2026-11-19T10:00:00Z'
};

const CALL = {
  id: 'llm_1',
  task: 'Scoring',
  provider: 'Anthropic',
  model: 'claude-opus-5-5',
  modelEndpointId: 'mdl_1',
  inputTokens: 1200,
  cachedInputTokens: 200,
  cacheWriteTokens: 0,
  outputTokens: 300,
  reasoningTokens: 40,
  totalTokens: 1500,
  durationMs: 2100,
  timeToFirstTokenMs: 450,
  streamingDurationMs: 1600,
  chunkCount: 42,
  outputTokensPerSecond: 187.5,
  toolCallCount: 1,
  toolNames: ['submit_result'],
  attempts: 1,
  streamed: true,
  success: true,
  finishReason: 'tool_use',
  structuredOutputRepairs: 0,
  inputCostUsd: 0.0048,
  outputCostUsd: 0.006,
  estimatedCostUsd: 0.0108,
  isDryRun: false,
  promptText: '{"system":"x"}',
  responseText: 'ok',
  createdUtc: '2026-11-19T10:00:00Z'
};

describe('ModelsView', () => {
  it('lists endpoints with capabilities and prices', async () => {
    renderRoute('/dashboard/models', { listModelEndpoints: vi.fn(async () => page([ANTHROPIC])) });
    const cells = await screen.findAllByText('claude-opus-5-5');
    expect(cells.length).toBeGreaterThan(0);
    expect(screen.getByText('Tools')).toBeInTheDocument();
    expect(screen.getByText('$4.00 in, $20.00 out')).toBeInTheDocument();
  });

  it('invites the first model when there are none', async () => {
    renderRoute('/dashboard/models', { listModelEndpoints: vi.fn(async () => page([])) });
    expect(await screen.findByText('Add your first model')).toBeInTheDocument();
  });

  it('tests an endpoint and shows the reply, tokens, and duration', async () => {
    const client = renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([ANTHROPIC])),
      testModelEndpoint: vi.fn(async () => ({ success: true, text: 'ready', model: 'claude-opus-5-5', inputTokens: 12, outputTokens: 2, durationMs: 840 }))
    });
    const row = (await screen.findByText('Anthropic', { selector: 'strong' })).closest('tr');
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Test' }));
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));
    const result = await screen.findByTestId('test-result');
    expect(within(result).getByText('ready')).toBeInTheDocument();
    expect(within(result).getByText('12 in, 2 out')).toBeInTheDocument();
    expect(client.testModelEndpoint).toHaveBeenCalledWith('mdl_1', { prompt: 'Reply with the single word: ready', model: undefined });
  });

  it('hides the model calls tab when the call log is forbidden', async () => {
    const client = renderRoute('/dashboard/models', { listModelEndpoints: vi.fn(async () => page([ANTHROPIC])), listLlmCalls: vi.fn(forbidden) });
    expect(await screen.findByRole('tab', { name: 'Assignments' })).toBeInTheDocument();
    await waitFor(() => expect(client.listLlmCalls).toHaveBeenCalled());
    expect(screen.queryByRole('tab', { name: 'Model calls' })).not.toBeInTheDocument();
  });

  it('shows one row per task role on the assignments tab and saves a change', async () => {
    const client = renderRoute('/dashboard/models?tab=assignments', {
      listModelEndpoints: vi.fn(async () => page([ANTHROPIC])),
      listModelAssignments: vi.fn(async () => page([{ id: 'mda_1', task: 'Scoring', modelEndpointId: 'mdl_1', reasoningEffort: 'Medium' }])),
      upsertModelAssignment: vi.fn(async (b) => ({ id: 'mda_1', ...b }))
    });
    const row = await screen.findByTestId('assignment-Scoring');
    expect(screen.getByTestId('assignment-Embedding')).toBeInTheDocument();
    await userEvent.selectOptions(within(row).getByLabelText('Reasoning effort for Scoring'), 'High');
    await userEvent.click(within(row).getByRole('button', { name: /Save/ }));
    await waitFor(() => expect(client.upsertModelAssignment).toHaveBeenCalledWith({ task: 'Scoring', modelEndpointId: 'mdl_1', modelOverride: null, reasoningEffort: 'High' }));
  });

  it('opens a call from the log with every accounting field and the payloads', async () => {
    renderRoute('/dashboard/models?tab=calls', {
      listModelEndpoints: vi.fn(async () => page([ANTHROPIC])),
      listLlmCalls: vi.fn(async () => page([CALL])),
      getLlmCall: vi.fn(async () => CALL)
    });
    const cost = await screen.findByText('$0.0108');
    await userEvent.click(cost);
    const drawer = await screen.findByTestId('call-detail');
    expect(within(drawer).getByText('Time to first token')).toBeInTheDocument();
    expect(within(drawer).getByText('submit_result')).toBeInTheDocument();
    expect(within(drawer).getByText('tool_use')).toBeInTheDocument();
    expect(within(drawer).getByText('Uncached input')).toBeInTheDocument();
    expect(within(drawer).getByText(/"system": "x"/)).toBeInTheDocument();
  });
});

describe('endpoint form', () => {
  const t = i18next.t.bind(i18next);

  it('never sends temperature for Anthropic and sends a new key as apiKey', () => {
    const form = { ...formFromEndpoint(null), name: 'A', model: 'claude-opus-5-5', temperature: '0.7', apiKey: 'sk-1' };
    const body = endpointBody(form);
    expect(body.temperature).toBeUndefined();
    expect(body.apiKey).toBe('sk-1');
    expect(body.credentialId).toBeUndefined();
    expect(body.maxTokens).toBe(16000);
  });

  it('links a stored credential or clears it', () => {
    const base = formFromEndpoint({ ...ANTHROPIC, credentialId: 'vlt_1' });
    expect(base.keyMode).toBe('vault');
    expect(endpointBody(base).credentialId).toBe('vlt_1');
    expect(endpointBody({ ...base, keyMode: 'none' }).credentialId).toBe('');
    expect(endpointBody({ ...base, provider: 'OpenAi', temperature: '0.2' }).temperature).toBe(0.2);
  });

  it('requires a base URL for OpenAI-compatible endpoints and a model for chat', () => {
    const form = { ...formFromEndpoint(null), name: 'Local', provider: 'OpenAiCompatible', model: '' };
    const errors = validateEndpointForm(form, t);
    expect(errors.endpoint).toBeTruthy();
    expect(errors.model).toBeTruthy();
    expect(validateEndpointForm({ ...form, endpoint: 'http://host.docker.internal:8000/v1', model: 'llama' }, t)).toEqual({});
  });
});
