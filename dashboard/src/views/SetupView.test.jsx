import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS, READY_SETUP, SETUP_OPTIONS } from '../test/fakeClient.js';
import { renderRoute } from '../test/renderRoute.jsx';
import { ApiError, SETUP_REQUIRED_EVENT } from '../utils/api.js';

const MODEL_READY = READY_SETUP.checks[0];
const SEARCH_READY = READY_SETUP.checks[1];
const EMBEDDINGS = { key: 'embeddings', kind: 'Embeddings', required: false, state: 'Missing', message: 'No embedding endpoint is set up.', resourceId: null, provider: null, model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: null };
const EMAIL = { key: 'email', kind: 'Email', required: false, state: 'Missing', message: 'Mailgun is not configured.', resourceId: null, provider: 'Mailgun', model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: null };

function status({ ready = false, canConfigure = true, model = {}, search = {}, extra = [] } = {}) {
  return { ready, canConfigure, checkedUtc: '2026-11-20T08:00:00Z', checks: [{ ...MODEL_READY, ...model }, { ...SEARCH_READY, ...search }, ...extra] };
}

// A fresh install: the seeded Anthropic endpoint never got a key, so the provider choice opens.
const FRESH = status({ model: { state: 'Missing', hasApiKey: false, verifiedUtc: null, message: 'Anthropic needs an API key.' }, extra: [EMBEDDINGS, EMAIL] });
const CLAUDE_CODE = { key: 'model:mep_2', resourceId: 'mep_2', resourceName: 'Claude Code', provider: 'ClaudeCode', authMethod: 'AccessToken', model: 'claude-opus-5-5', needsApiKey: true, hasApiKey: true };
const CLAUDE_CODE_READY = status({ ready: true, model: { ...CLAUDE_CODE, state: 'Ready', verifiedUtc: new Date().toISOString() }, extra: [EMBEDDINGS, EMAIL] });
const CLAUDE_CODE_FAILED = status({ model: { ...CLAUDE_CODE, state: 'Failed', message: 'stale check message' } });
const connected = (s, connection) => ({ ...s, connection: { modelEndpointId: 'mep_2', keptPrevious: false, message: null, ...connection } });

function deferred() {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

async function openFresh(overrides = {}) {
  const client = renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => FRESH), ...overrides });
  await screen.findByRole('group', { name: 'Use your AI subscription' });
  return client;
}

describe('setup page for administrators', () => {
  it('shows the title, the two-step checklist, and the provider groups in order', async () => {
    await openFresh();
    expect(screen.getByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
    const progress = screen.getByTestId('setup-progress');
    expect(within(progress).getByText('AI model')).toBeInTheDocument();
    expect(within(progress).getByText('Web search')).toBeInTheDocument();
    expect(within(progress).getByText('To do')).toBeInTheDocument();
    expect(within(progress).getByText('Done')).toBeInTheDocument();
    const legends = screen.getAllByRole('group').map((g) => g.querySelector(':scope > legend')?.textContent);
    expect(legends.filter((l) => ['Use your AI subscription', 'Use an API key', 'Run your own model'].includes(l))).toEqual(['Use your AI subscription', 'Use an API key', 'Run your own model']);
    expect(screen.getByText('API keys are billed per use by the provider.')).toBeInTheDocument();
    const api = screen.getByRole('group', { name: 'Use an API key' });
    expect(within(api).getByTestId('provider-Anthropic')).toHaveTextContent('Recommended');
    expect(within(api).getByRole('radio', { name: /^Google Gemini/ })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /^Codex.*ChatGPT Plus, Pro, or Business/ })).toBeInTheDocument();
    // Optional extras are collapsed and never block.
    expect(screen.getByTestId('setup-extras')).not.toHaveAttribute('open');
    expect(screen.getByText('Finds repeated stories by meaning, not only by matching words.')).toBeInTheDocument();
  });

  it('connects Claude Code with a subscription token: a failed test, then success', async () => {
    const first = deferred();
    const connectSetup = vi
      .fn()
      .mockImplementationOnce(() => first.promise)
      .mockResolvedValueOnce(connected(CLAUDE_CODE_READY, { succeeded: true, message: 'ready' }));
    const client = await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^Claude Code/ }));
    const methods = screen.getByRole('group', { name: 'How should it sign in?' });
    expect(within(methods).getByRole('radio', { name: 'Subscription token' })).toBeChecked();
    expect(within(methods).getByRole('radio', { name: 'Anthropic API key' })).not.toBeChecked();
    expect(within(methods).getByRole('radio', { name: 'Already signed in on the server' })).toBeInTheDocument();
    expect(screen.getByText('claude setup-token')).toBeInTheDocument();
    expect(screen.getByText(/it starts with sk-ant-oat/)).toBeInTheDocument();

    // Nothing pasted yet: the field says what is missing and nothing is sent.
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    expect(await screen.findByText('Paste the token from claude setup-token.')).toBeInTheDocument();
    expect(connectSetup).not.toHaveBeenCalled();

    const token = screen.getByLabelText('Token from claude setup-token');
    expect(token).toHaveAttribute('type', 'password');
    expect(token).toHaveAttribute('autocomplete', 'off');
    await userEvent.type(token, 'sk-ant-oat01-abc');
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    expect(await screen.findByText('Testing the connection. This sends one short prompt and can take up to a minute for a CLI.')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /^Claude Code/ })).toBeDisabled();
    expect(connectSetup).toHaveBeenCalledWith({ provider: 'ClaudeCode', authMethod: 'AccessToken', secret: 'sk-ant-oat01-abc', model: 'claude-opus-5-5' });

    // The result message is the server's connection message, not the model check's.
    await act(async () => first.resolve(connected(CLAUDE_CODE_FAILED, { succeeded: false, message: 'Invalid bearer token' })));
    const result = await screen.findByTestId('setup-result');
    expect(within(result).getByText('The test did not pass')).toBeInTheDocument();
    expect(within(result).getByText('Invalid bearer token')).toBeInTheDocument();
    expect(within(result).queryByText('stale check message')).not.toBeInTheDocument();
    expect(within(result).getByText('If the token was rejected, run claude setup-token again and paste the new token.')).toBeInTheDocument();
    await waitFor(() => expect(result).toHaveFocus());
    // The choices stay so the next try is one click.
    expect(screen.getByRole('radio', { name: /^Claude Code/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Subscription token' })).toBeChecked();
    expect(screen.queryByTestId('setup-ready')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    const success = await screen.findByText('Connected. Claude Code answered the test prompt.');
    await waitFor(() => expect(success.closest('[data-testid="setup-result"]')).toHaveFocus());
    const summary = screen.getByTestId('setup-check-model:mep_2');
    expect(within(summary).getByText('Claude Code')).toBeInTheDocument();
    expect(within(summary).getByText('Subscription token')).toBeInTheDocument();
    expect(within(summary).getByText('claude-opus-5-5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument();

    const ready = screen.getByTestId('setup-ready');
    expect(ready).toHaveTextContent("You're ready");
    expect(within(ready).getByRole('link', { name: 'Create your first topic' })).toHaveAttribute('href', '/dashboard/topics/new');
    await userEvent.click(within(ready).getByRole('link', { name: 'Go to Home' }));
    await waitFor(() => expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument());
    expect(client.connectSetup).toHaveBeenCalledTimes(2);
  });

  it('connects Codex with an auth.json file read in the browser and never shows its contents', async () => {
    const codexReady = status({ ready: true, model: { key: 'model:mep_3', resourceId: 'mep_3', resourceName: 'Codex', provider: 'Codex', authMethod: 'AuthFile', model: 'default', state: 'Ready' } });
    const connectSetup = vi.fn(async () => codexReady);
    await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^Codex/ }));
    expect(screen.getByRole('radio', { name: 'ChatGPT sign-in file (auth.json)' })).toBeChecked();
    expect(screen.getByText('codex login')).toBeInTheDocument();
    expect(screen.getByText('~/.codex/auth.json')).toBeInTheDocument();
    expect(screen.getByText('%USERPROFILE%\\.codex\\auth.json')).toBeInTheDocument();
    expect(screen.getByText('This file signs in as you. It is stored encrypted and only used by this server.')).toBeInTheDocument();
    expect(screen.getByText('Advanced')).toBeInTheDocument();
    expect(screen.getByText("Model: The CLI's default model")).toBeInTheDocument();

    const contents = '{"tokens":{"access_token":"secret-access","refresh_token":"secret-refresh"}}';
    await userEvent.upload(screen.getByTestId('setup-secret-file'), new File([contents], 'auth.json', { type: 'application/json' }));
    const drop = screen.getByTestId('file-drop');
    expect(await within(drop).findByText('auth.json')).toBeInTheDocument();
    expect(within(drop).getByRole('button', { name: 'Replace' })).toBeInTheDocument();
    expect(screen.queryByText(/secret-access/)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    await waitFor(() => expect(connectSetup).toHaveBeenCalledWith({ provider: 'Codex', authMethod: 'AuthFile', secret: contents }));
    expect(await screen.findByText('Connected. Codex answered the test prompt.')).toBeInTheDocument();
    expect(within(screen.getByTestId('setup-check-model:mep_3')).getByText("The CLI's default model")).toBeInTheDocument();
  });

  it('takes a pasted sign-in file and puts a server refusal next to the field it names', async () => {
    const connectSetup = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'Codex could not read this auth.json.', details: { field: 'secret' } });
    });
    await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^Codex/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Paste the contents instead' }));
    const area = screen.getByLabelText('Contents of auth.json');
    await userEvent.click(area);
    await userEvent.paste('{}');
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    expect(await screen.findByText('Codex could not read this auth.json.')).toHaveAttribute('role', 'alert');
    await waitFor(() => expect(area).toHaveFocus());
    expect(screen.queryByTestId('setup-result')).not.toBeInTheDocument();
  });

  it('asks for the model when the provider has no default, and links to the key page', async () => {
    const connectSetup = vi.fn(async () => status({ ready: true, model: { provider: 'OpenAi', model: 'gpt-test', state: 'Ready' } }));
    await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^OpenAI.*GPT models/ }));
    expect(screen.queryByRole('group', { name: 'How should it sign in?' })).not.toBeInTheDocument();
    const link = screen.getByRole('link', { name: /platform\.openai\.com\/api-keys/ });
    expect(link).toHaveAttribute('href', 'https://platform.openai.com/api-keys');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await userEvent.type(screen.getByLabelText('API key', { selector: 'input:not([type=radio])' }), 'sk-test');
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    expect(await screen.findByText('Enter the model to use.')).toBeInTheDocument();
    expect(connectSetup).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.getByLabelText('Model')).toHaveFocus());
    await userEvent.type(screen.getByLabelText('Model'), 'gpt-test');
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    await waitFor(() => expect(connectSetup).toHaveBeenCalledWith({ provider: 'OpenAi', authMethod: 'ApiKey', secret: 'sk-test', model: 'gpt-test' }));
  });

  it('prefills the Ollama address and needs no key by default', async () => {
    const connectSetup = vi.fn(async () => CLAUDE_CODE_FAILED);
    await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^Ollama.*own computer/ }));
    expect(screen.getByRole('radio', { name: 'No key needed' })).toBeChecked();
    expect(screen.getByLabelText('Server address')).toHaveValue('http://host.docker.internal:11434');
    expect(screen.queryByLabelText('API key', { selector: 'input:not([type=radio])' })).not.toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Model'), 'llama3');
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    await waitFor(() => expect(connectSetup).toHaveBeenCalledWith({ provider: 'Ollama', authMethod: 'None', model: 'llama3', baseUrl: 'http://host.docker.internal:11434' }));
  });

  it('disables a CLI the server cannot run and says why', async () => {
    const providers = SETUP_OPTIONS.providers.map((p) => {
      if (p.provider === 'Codex') return { ...p, available: false, installed: false, unavailableReason: 'The Codex CLI (codex) is not installed on the server.' };
      if (p.provider === 'Mux') return { ...p, available: false, installed: true, turnedOff: true, unavailableReason: 'Agent CLIs are turned off on this server (Models.AllowAgentClis).' };
      if (p.provider === 'GeminiCli') return { ...p, available: false, installed: false, turnedOff: true, unavailableReason: 'Agent CLIs are turned off on this server (Models.AllowAgentClis).' };
      return p;
    });
    await openFresh({ getSetupOptions: vi.fn(async () => ({ ...SETUP_OPTIONS, providers })) });
    expect(screen.getByRole('radio', { name: /^Codex/ })).toBeDisabled();
    expect(within(screen.getByTestId('provider-Codex')).getByText('Not installed on this server')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /^Mux/ })).toBeDisabled();
    expect(within(screen.getByTestId('provider-Mux')).getByText('Turned off on this server')).toBeInTheDocument();
    // Turned off wins over not installed: that is what the operator can change.
    expect(screen.getByRole('radio', { name: /^Gemini CLI/ })).toBeDisabled();
    expect(within(screen.getByTestId('provider-GeminiCli')).getByText('Turned off on this server')).toBeInTheDocument();
    expect(within(screen.getByTestId('provider-ClaudeCode')).queryByText('Recommended')).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /^Claude Code/ })).toBeEnabled();
  });

  it('summarizes a working model and reopens the flow with Change, keeping the stored token', async () => {
    const connectSetup = vi.fn(async () => CLAUDE_CODE_READY);
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => CLAUDE_CODE_READY), connectSetup });
    expect(await screen.findByTestId('setup-ready')).toBeInTheDocument();
    const summary = screen.getByTestId('setup-check-model:mep_2');
    expect(within(summary).getByText('Subscription token')).toBeInTheDocument();
    expect(within(summary).getByText(/^Verified /)).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Use your AI subscription' })).not.toBeInTheDocument();
    // The page stays in the System group of the navigation once setup is done.
    expect(screen.getByRole('link', { name: 'Setup' })).toHaveAttribute('href', '/dashboard/setup');

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    expect(screen.getByRole('radio', { name: /^Claude Code/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /^Claude Code/ })).toHaveFocus();
    expect(screen.getByRole('radio', { name: 'Subscription token' })).toBeChecked();
    expect(screen.getByLabelText('Token from claude setup-token')).toHaveValue('');
    expect(screen.getByText('Leave this empty to keep the one already stored.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    await waitFor(() => expect(connectSetup).toHaveBeenCalledWith({ provider: 'ClaudeCode', authMethod: 'AccessToken', model: 'claude-opus-5-5', modelEndpointId: 'mep_2' }));
    expect(await screen.findByText('Connected. Claude Code answered the test prompt.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Change' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument();
  });

  it('shows a failed endpoint with what to try, Test again, and Fix', async () => {
    const failed = status({ model: { key: 'model:mep_3', resourceId: 'mep_3', resourceName: 'Codex', provider: 'Codex', authMethod: 'AuthFile', model: null, state: 'Failed', message: 'codex: not logged in' } });
    const verifySetup = vi.fn(async () => failed);
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => failed), verifySetup });
    const card = await screen.findByTestId('setup-check-model:mep_3');
    expect(within(card).getByText('codex: not logged in')).toBeInTheDocument();
    expect(within(card).getByText('If the sign-in was rejected, run codex login again and choose the new auth.json.')).toBeInTheDocument();
    expect(within(screen.getByTestId('setup-progress')).getByText('Needs attention')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Test again' }));
    await waitFor(() => expect(verifySetup).toHaveBeenCalledTimes(1));
    await userEvent.click(screen.getByRole('button', { name: 'Fix' }));
    expect(screen.getByRole('radio', { name: /^Codex/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'ChatGPT sign-in file (auth.json)' })).toBeChecked();
  });

  it('shows the server sign-in command for a CLI signed in inside the container', async () => {
    await openFresh();
    await userEvent.click(screen.getByRole('radio', { name: /^Claude Code/ }));
    await userEvent.click(screen.getByRole('radio', { name: 'Already signed in on the server' }));
    expect(screen.getByText('docker exec -it mymorningnews-server claude')).toBeInTheDocument();
    expect(screen.getByText('Type /login and finish the sign-in.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Token from claude setup-token')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('radio', { name: /^Mux/ }));
    await userEvent.click(screen.getByRole('radio', { name: 'Already configured on the server' }));
    expect(screen.getByText('docker exec -it mymorningnews-server mux')).toBeInTheDocument();
    expect(screen.getByText('Type /endpoint, choose Add, and save the endpoint you want to use. Mux writes it to ~/.mux/endpoints.json.')).toBeInTheDocument();
  });

  it('keeps the working model when a changed sign-in fails its test', async () => {
    const kept = connected(CLAUDE_CODE_READY, { modelEndpointId: 'mep_3', succeeded: false, keptPrevious: true, message: 'codex: not logged in' });
    const connectSetup = vi.fn(async () => kept);
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => CLAUDE_CODE_READY), connectSetup });
    await userEvent.click(await screen.findByRole('button', { name: 'Change' }));
    await userEvent.click(screen.getByRole('radio', { name: /^Codex/ }));
    await userEvent.upload(screen.getByTestId('setup-secret-file'), new File(['{"tokens":{}}'], 'auth.json', { type: 'application/json' }));
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    const result = await screen.findByTestId('setup-result');
    expect(within(result).getByText('The new sign-in failed its test, so My Morning News kept using Claude Code.')).toBeInTheDocument();
    expect(within(result).getByText('codex: not logged in')).toBeInTheDocument();
    expect(within(result).getByText('If the sign-in was rejected, run codex login again and choose the new auth.json.')).toBeInTheDocument();
    await waitFor(() => expect(result).toHaveFocus());
    // The form stays open with the new choice, and the working model is still shown and still ready.
    expect(screen.getByRole('radio', { name: /^Codex/ })).toBeChecked();
    expect(within(screen.getByTestId('setup-check-model:mep_2')).getByText('Subscription token')).toBeInTheDocument();
    expect(screen.getByTestId('setup-ready')).toBeInTheDocument();
    expect(within(screen.getByTestId('setup-progress')).getAllByText('Done')).toHaveLength(2);
  });

  it('sends a provider refusal to the result area', async () => {
    const connectSetup = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'Codex is not available on this server.', details: { field: 'provider' } });
    });
    await openFresh({ connectSetup });
    await userEvent.click(screen.getByRole('radio', { name: /^Codex/ }));
    await userEvent.click(screen.getByRole('radio', { name: 'Already signed in on the server' }));
    await userEvent.click(screen.getByRole('button', { name: 'Connect and test' }));
    const result = await screen.findByTestId('setup-result');
    expect(within(result).getByText('Codex is not available on this server.')).toBeInTheDocument();
  });

  it('prefills the stored Ollama address when changing it', async () => {
    const ollama = status({ ready: true, model: { key: 'model:mep_4', resourceId: 'mep_4', resourceName: 'Ollama', provider: 'Ollama', authMethod: 'None', model: 'llama3', needsApiKey: false, hasApiKey: false, baseUrl: 'http://192.168.1.20:11434', state: 'Ready' } });
    const client = renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => ollama) });
    await userEvent.click(await screen.findByRole('button', { name: 'Change' }));
    expect(screen.getByLabelText('Server address')).toHaveValue('http://192.168.1.20:11434');
    expect(screen.getByLabelText('Model')).toHaveValue('llama3');
    expect(client.getModelEndpoint).not.toHaveBeenCalled();
  });

  it('verifies once on open when a sign-in is stored but untested', async () => {
    const verifySetup = vi.fn(async () => READY_SETUP);
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => status({ model: { state: 'Unverified', verifiedUtc: null } })), verifySetup });
    expect(await screen.findByTestId('setup-ready')).toBeInTheDocument();
    expect(verifySetup).toHaveBeenCalledTimes(1);
  });

  it('offers Test again and Search providers when web search fails', async () => {
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => status({ search: { state: 'Failed', message: 'SearXNG did not answer.' } })) });
    const search = await screen.findByTestId('setup-check-search');
    expect(within(search).getByText('SearXNG did not answer.')).toBeInTheDocument();
    expect(within(search).getByRole('button', { name: 'Test again' })).toBeInTheDocument();
    expect(within(search).getByRole('link', { name: 'Open search providers' })).toHaveAttribute('href', '/dashboard/search-providers');
  });

  it('says web search works when it does', async () => {
    renderRoute('/dashboard/setup');
    expect(await screen.findByText('Web search works (SearXNG).')).toBeInTheDocument();
  });

  it('redirects product routes to the Setup screen and keeps configuration pages open', async () => {
    renderRoute('/dashboard/topics', { getSetupStatus: vi.fn(async () => FRESH) });
    expect(await screen.findByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
    expect(screen.getByTestId('setup-callout')).toBeInTheDocument();
    // The Configuration hub opens on Models, and its Search providers tab stays open before setup is finished.
    await userEvent.click(screen.getByRole('link', { name: 'Configuration' }));
    expect(await screen.findByRole('heading', { name: 'Models', level: 1 })).toBeInTheDocument();
    await userEvent.click(within(screen.getByRole('navigation', { name: 'Configuration pages' })).getByRole('link', { name: 'Search providers' }));
    expect(await screen.findByRole('heading', { name: 'Search providers', level: 1 })).toBeInTheDocument();
  });
});

describe('setup gate for everyone else', () => {
  it('shows members a full-screen message with Check again and Sign out', async () => {
    const blocked = status({ canConfigure: false, model: { state: 'Missing', hasApiKey: false } });
    const getSetupStatus = vi.fn(async () => blocked);
    const client = renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus });
    expect(await screen.findByTestId('setup-blocked')).toHaveTextContent('This workspace is not ready');
    expect(screen.getByText('An administrator needs to connect an AI model and web search before anyone can use My Morning News.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/API key/)).not.toBeInTheDocument();
    const calls = getSetupStatus.mock.calls.length;
    getSetupStatus.mockResolvedValueOnce(READY_SETUP);
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    await waitFor(() => expect(getSetupStatus.mock.calls.length).toBe(calls + 1));
    expect(await screen.findByRole('navigation')).toBeInTheDocument();
    expect(screen.queryByTestId('setup-blocked')).not.toBeInTheDocument();
    expect(client.verifySetup).not.toHaveBeenCalled();
    expect(client.getSetupOptions).not.toHaveBeenCalled();
  });

  it('signs a member out from the blocked screen', async () => {
    const client = renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus: vi.fn(async () => status({ canConfigure: false, model: { state: 'Failed' } })) });
    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
    await waitFor(() => expect(client.logout).toHaveBeenCalled());
    expect(await screen.findByRole('button', { name: /Sign in/ })).toBeInTheDocument();
  });

  it('keeps the product closed when the setup status cannot be loaded', async () => {
    const getSetupStatus = vi.fn(async () => {
      throw new ApiError(0, null, 'Network error');
    });
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByTestId('setup-load-error')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    getSetupStatus.mockImplementation(async () => READY_SETUP);
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
  });

  it('shows the normal app when setup is ready', async () => {
    renderRoute('/dashboard/topics');
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument();
  });
});

describe('SetupRequired refusals', () => {
  const refusal = () => new ApiError(409, { error: 'SetupRequired', message: 'Connect and test an AI model before starting research.', details: { check: 'model:mep_1' } });

  it('shows the server message and a link to Setup on a one-time request', async () => {
    renderRoute('/dashboard/one-time/new', {
      createResearchRequest: vi.fn(async () => {
        throw refusal();
      })
    });
    await userEvent.type(await screen.findByLabelText('What do you want?'), 'Heat pump rebates');
    await userEvent.click(screen.getByRole('button', { name: 'Plan it' }));
    expect(await screen.findByText('Connect and test an AI model before starting research.')).toBeInTheDocument();
    expect(screen.getByText('Setup is not finished.')).toBeInTheDocument();
    expect(screen.getByTestId('setup-required-link')).toHaveAttribute('href', '/dashboard/setup');
  });

  it('re-reads the setup status and re-engages the gate', async () => {
    const getSetupStatus = vi.fn(async () => READY_SETUP);
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    getSetupStatus.mockImplementation(async () => status({ model: { state: 'Failed', message: 'credit balance is too low' } }));
    act(() => {
      window.dispatchEvent(new CustomEvent(SETUP_REQUIRED_EVENT, { detail: { path: '/v1.0/api/refinement-sessions', message: 'Connect and test an AI model before starting research.' } }));
    });
    expect(await screen.findByTestId('setup-progress')).toBeInTheDocument();
    expect(screen.getByText('Connect and test an AI model before starting research.')).toBeInTheDocument();
    expect(screen.getByText('credit balance is too low')).toBeInTheDocument();
    expect(screen.getByText('Check that the key is right and that the account has billing set up or credit left.')).toBeInTheDocument();
  });
});
