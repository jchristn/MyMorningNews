import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { forbidden, page, renderRoute } from '../test/renderRoute.jsx';
import { endpointBody, formFromEndpoint, validateEndpointForm, withProvider } from './models/EndpointFormModal.jsx';
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

  it('hides the model calls tab from a member without probing the call log', async () => {
    const client = renderRoute('/dashboard/models', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), listModelEndpoints: vi.fn(async () => page([ANTHROPIC])), listLlmCalls: vi.fn(forbidden) });
    expect(await screen.findByRole('tab', { name: 'Assignments' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Model calls' })).not.toBeInTheDocument();
    expect(client.listLlmCalls).not.toHaveBeenCalled();
  });

  it('warns when the chat endpoint has no API key and marks the row', async () => {
    renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([{ ...ANTHROPIC, credentialId: null }])),
      listModelAssignments: vi.fn(async () => page([])),
      listVaultCredentials: vi.fn(async () => page([]))
    });
    const warning = await screen.findByTestId('model-key-warning');
    expect(within(warning).getByText(/has no API key/)).toBeInTheDocument();
    expect(within(warning).getByRole('link', { name: /Add a key to/ })).toHaveAttribute('href', '/dashboard/models?endpoint=mdl_1');
    expect(await screen.findByText('No API key')).toBeInTheDocument();
  });

  it('does not warn about an endpoint that uses the platform key', async () => {
    renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([{ ...ANTHROPIC, credentialId: 'platform:anthropic' }])),
      listVaultCredentials: vi.fn(async () => page([]))
    });
    expect(await screen.findByRole('tab', { name: 'Assignments' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('model-key-warning')).toBeNull());
    expect(screen.queryByText('No API key')).toBeNull();
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

describe('agent CLI endpoints on the Models page', () => {
  const CODEX = { ...ANTHROPIC, id: 'mdl_2', name: 'Codex', provider: 'Codex', authMethod: 'ServerSignIn', model: null, supportsTools: false, credentialId: null, endpoint: null };

  it('creates a Claude Code endpoint with a subscription token and hides the URL, tools, and embeddings', async () => {
    const client = renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([ANTHROPIC])),
      createModelEndpoint: vi.fn(async (b) => ({ id: 'mdl_9', ...b }))
    });
    await userEvent.click((await screen.findAllByRole('button', { name: /Add model/ }))[0]);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Base URL')).toBeInTheDocument();
    await userEvent.selectOptions(within(dialog).getByLabelText('Provider'), 'ClaudeCode');
    expect(within(dialog).queryByLabelText('Base URL')).not.toBeInTheDocument();
    expect(within(dialog).queryByText('Serves')).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Embedding model')).not.toBeInTheDocument();
    expect(within(dialog).getByText(/answers chat prompts only/)).toBeInTheDocument();
    expect(within(dialog).getByRole('radio', { name: 'Subscription token' })).toBeChecked();
    await userEvent.type(within(dialog).getByLabelText('Name'), 'My Claude');
    await userEvent.type(within(dialog).getByLabelText('Token from claude setup-token'), 'sk-ant-oat01-xyz');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createModelEndpoint).toHaveBeenCalled());
    const body = client.createModelEndpoint.mock.calls[0][0];
    expect(body).toMatchObject({ name: 'My Claude', provider: 'ClaudeCode', authMethod: 'AccessToken', apiKey: 'sk-ant-oat01-xyz', endpoint: '', supportsChat: true, supportsEmbeddings: false, model: '' });
    expect(body.temperature).toBeUndefined();
  });

  it('shows the sign-in method in the list and edits a CLI signed in on the server without a secret', async () => {
    const client = renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([CODEX])),
      listVaultCredentials: vi.fn(async () => page([])),
      updateModelEndpoint: vi.fn(async (id, b) => ({ ...CODEX, ...b }))
    });
    const row = (await screen.findByText('Codex', { selector: 'strong' })).closest('tr');
    expect(within(row).getByText('Already signed in on the server')).toBeInTheDocument();
    expect(within(row).queryByText('No API key')).toBeNull();
    await userEvent.click(within(row).getByText('Codex', { selector: 'strong' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('radio', { name: 'Already signed in on the server' })).toBeChecked();
    expect(within(dialog).queryByRole('radio', { name: 'Add a new one' })).not.toBeInTheDocument();
    await userEvent.click(within(dialog).getByText('How to get it'));
    expect(within(dialog).getByText('docker exec -it mymorningnews-server codex login --device-auth')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updateModelEndpoint).toHaveBeenCalledWith('mdl_2', expect.objectContaining({ provider: 'Codex', authMethod: 'ServerSignIn', credentialId: '' })));
  });

  it('marks a CLI endpoint whose sign-in file is missing', async () => {
    renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([{ ...CODEX, authMethod: 'AuthFile' }])),
      listModelAssignments: vi.fn(async () => page([])),
      listVaultCredentials: vi.fn(async () => page([]))
    });
    expect(await screen.findByText('No sign-in file')).toBeInTheDocument();
    const warning = await screen.findByTestId('model-key-warning');
    expect(within(warning).getByText('Codex has no sign-in file, so calls to it fail.')).toBeInTheDocument();
    expect(within(warning).getByRole('link', { name: 'Update the sign-in for Codex' })).toHaveAttribute('href', '/dashboard/models?endpoint=mdl_2');
  });

  it('sends a sign-in file read in the browser as the endpoint secret', async () => {
    const client = renderRoute('/dashboard/models', {
      listModelEndpoints: vi.fn(async () => page([ANTHROPIC])),
      createModelEndpoint: vi.fn(async (b) => ({ id: 'mdl_9', ...b }))
    });
    await userEvent.click((await screen.findAllByRole('button', { name: /Add model/ }))[0]);
    const dialog = await screen.findByRole('dialog');
    await userEvent.selectOptions(within(dialog).getByLabelText('Provider'), 'GeminiCli');
    expect(within(dialog).getByRole('radio', { name: 'Google sign-in file (oauth_creds.json)' })).toBeChecked();
    await userEvent.type(within(dialog).getByLabelText('Name'), 'Gemini CLI');
    const contents = '{"refresh_token":"r"}';
    await userEvent.upload(within(dialog).getByTestId('ep-apikey-file'), new File([contents], 'oauth_creds.json', { type: 'application/json' }));
    expect(await within(dialog).findByText('oauth_creds.json')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createModelEndpoint).toHaveBeenCalledWith(expect.objectContaining({ provider: 'GeminiCli', authMethod: 'AuthFile', apiKey: contents })));
  });
});

describe('endpoint form', () => {
  it('switches the sign-in method with the provider and does not require a model for a CLI', () => {
    const t = i18next.t.bind(i18next);
    const cli = withProvider({ ...formFromEndpoint(null), name: 'C', temperature: '0.4', supportsEmbeddings: true, embeddingModel: 'x' }, 'Codex');
    expect(cli.authMethod).toBe('AuthFile');
    expect(cli.supportsEmbeddings).toBe(false);
    expect(cli.temperature).toBe('');
    expect(validateEndpointForm(cli, t)).toEqual({});
    expect(withProvider(cli, 'Ollama').authMethod).toBe('None');
    expect(withProvider({ ...cli, authMethod: 'ApiKey' }, 'OpenAiCompatible').authMethod).toBe('ApiKey');
    expect(endpointBody({ ...cli, authMethod: 'ServerSignIn', apiKey: 'ignored' })).toMatchObject({ authMethod: 'ServerSignIn', credentialId: '', supportsEmbeddings: false });
    expect(endpointBody({ ...cli, authMethod: 'ServerSignIn', apiKey: 'ignored' }).apiKey).toBeUndefined();
  });

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
