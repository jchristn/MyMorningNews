import { describe, expect, it } from 'vitest';
import { ApiError } from '../../utils/api.js';
import { authMethodsFor, effectiveAuthMethod, isCliDefaultModel, needsSecret } from '../../utils/modelAuth.js';
import { keyProblem } from '../../utils/modelKeys.js';
import { errorField, failureHint, methodLabel, serverCommand, signInNotes, signInSteps } from '../../components/modelSignIn/signInCatalog.js';
import { connectedCheck, isBrokenCheck, isHttpUrl, modelStepState, progressState } from './setupState.js';

const t = (key, values) => key + (values ? JSON.stringify(values) : '');
const bad = (message, code = 'ValidationFailed') => new ApiError(400, { error: code, message });

describe('sign-in methods', () => {
  it('mirror the server: recommended first, and which ones need a stored secret', () => {
    expect(authMethodsFor('ClaudeCode')).toEqual(['AccessToken', 'ApiKey', 'ServerSignIn']);
    expect(authMethodsFor('Mux')).toEqual(['AuthFile', 'ServerSignIn']);
    expect(authMethodsFor('Anthropic')).toEqual(['ApiKey']);
    expect(['ApiKey', 'AccessToken', 'AuthFile'].every(needsSecret)).toBe(true);
    expect(needsSecret('ServerSignIn') || needsSecret('None')).toBe(false);
  });

  it('infer the method of an endpoint that has none stored', () => {
    expect(effectiveAuthMethod({ provider: 'Codex' })).toBe('ServerSignIn');
    expect(effectiveAuthMethod({ provider: 'Codex', credentialId: 'vlt_1' })).toBe('AuthFile');
    expect(effectiveAuthMethod({ provider: 'Ollama' })).toBe('None');
    expect(effectiveAuthMethod({ provider: 'OpenAiCompatible', credentialId: 'vlt_1' })).toBe('ApiKey');
    expect(effectiveAuthMethod({ provider: 'Anthropic' })).toBe('ApiKey');
    expect(effectiveAuthMethod({ provider: 'ClaudeCode', authMethod: 'AccessToken' })).toBe('AccessToken');
    expect(isCliDefaultModel('default') && isCliDefaultModel(null) && !isCliDefaultModel('claude-opus-5-5')).toBe(true);
  });

  it('report a missing secret only for methods that store one', () => {
    expect(keyProblem({ provider: 'ClaudeCode', authMethod: 'ServerSignIn' }, [])).toBeNull();
    expect(keyProblem({ provider: 'Codex', authMethod: 'AuthFile' }, [])).toBe('missing');
    expect(keyProblem({ provider: 'ClaudeCode', authMethod: 'AccessToken', credentialId: 'vlt_1' }, [{ id: 'vlt_1', active: false }])).toBe('inactive');
    expect(keyProblem({ provider: 'Ollama', authMethod: 'ApiKey' }, [])).toBe('missing');
    expect(keyProblem({ provider: 'Ollama' }, [])).toBeNull();
  });
});

describe('sign-in instructions', () => {
  it('label methods per provider', () => {
    expect(methodLabel(t, 'Codex', 'AuthFile')).toBe('setup.method.chatGptFile{"file":"auth.json"}');
    expect(methodLabel(t, 'Anthropic', 'ApiKey')).toBe('enums.authMethod.ApiKey{"defaultValue":"ApiKey"}');
  });

  it('give each provider and method its own steps and commands', () => {
    expect(signInSteps('ClaudeCode', 'AccessToken')[0].commands).toEqual(['claude setup-token']);
    expect(signInSteps('GeminiCli', 'AuthFile')[1].paths.unix).toBe('~/.gemini/oauth_creds.json');
    expect(signInSteps('XAi', 'ApiKey')[0].link).toBe('https://console.x.ai');
    expect(signInSteps('Codex', 'ServerSignIn', 'mmn')[0].commands).toEqual(['docker exec -it mmn codex login --device-auth']);
    expect(signInSteps('Ollama', 'None')[0].values.url).toBe('http://host.docker.internal:11434');
    expect(signInSteps('Anthropic', 'None')).toEqual([]);
    expect(serverCommand('GeminiCli')).toBe('docker exec -it mymorningnews-server gemini');
    expect(signInNotes('Codex', 'AuthFile')).toMatchObject({ warning: 'setup.steps.fileWarning', install: 'npm install -g @openai/codex' });
  });

  it('say what to try when a test fails', () => {
    expect(failureHint('ClaudeCode', 'AccessToken').values.command).toBe('claude setup-token');
    expect(failureHint('GeminiCli', 'AuthFile').values).toEqual({ command: 'gemini', file: 'oauth_creds.json' });
    expect(failureHint('Mux', 'ServerSignIn', 'box').command).toBe('docker exec -it box mux');
    expect(signInSteps('Mux', 'ServerSignIn', 'box').map((s) => s.key)).toEqual(['setup.steps.serverOpen', 'setup.steps.serverMuxConfigure', 'setup.steps.serverReturn']);
    expect(failureHint('OpenAi', 'ApiKey').key).toBe('setup.hint.apiKey');
    expect(failureHint('Ollama', 'None').key).toBe('setup.hint.selfHosted');
  });

  it('place a server refusal next to the field the server names', () => {
    const named = (field, message = 'Anything at all.') => new ApiError(400, { error: 'ValidationFailed', message, details: { field } });
    expect(['secret', 'model', 'baseUrl'].map((f) => errorField(named(f)))).toEqual(['secret', 'model', 'baseUrl']);
    expect(errorField(named('provider'))).toBeNull();
    expect(errorField(named('authMethod', 'Paste the API key.'))).toBeNull();
  });

  it('fall back to reading the message when the server names no field', () => {
    expect(errorField(bad('That looks like an Anthropic API key, not a claude setup-token token.'))).toBe('secret');
    expect(errorField(bad('Paste a token from claude setup-token.'))).toBe('secret');
    expect(errorField(bad('This oauth_creds.json has no Google sign-in in it.'))).toBe('secret');
    expect(errorField(bad('Name the model to use, for example one listed in the provider\'s console.'))).toBe('model');
    expect(errorField(bad("Enter the server's address, such as http://host.docker.internal:11434."))).toBe('baseUrl');
    expect(errorField(bad('Blocked', 'EgressBlocked'))).toBe('baseUrl');
    expect(errorField(bad('Agent CLIs are turned off on this server.'))).toBeNull();
  });
});

describe('setup steps', () => {
  const check = (extra) => ({ key: 'model:mep_1', kind: 'Model', required: true, resourceId: 'mep_1', state: 'Missing', needsApiKey: true, hasApiKey: false, ...extra });

  it('open the provider choice for a fresh install and show the problem for a configured endpoint', () => {
    expect(modelStepState([check()])).toBe('empty');
    expect(modelStepState([{ key: 'model:none', state: 'Missing' }])).toBe('empty');
    expect(modelStepState([check({ state: 'Failed', hasApiKey: true })])).toBe('broken');
    expect(modelStepState([check({ needsApiKey: false, message: 'The Codex CLI is not installed.' })])).toBe('broken');
    expect(modelStepState([check({ usesPlatformKey: true, needsApiKey: true })])).toBe('broken');
    expect(modelStepState([check({ state: 'Unverified', hasApiKey: true })])).toBe('checking');
    expect(modelStepState([check({ state: 'Ready' })])).toBe('ready');
    expect(isBrokenCheck(check({ state: 'Ready' }))).toBe(false);
  });

  it('pick the check a connect attempt is about and read the checklist state', () => {
    const list = [check({ provider: 'Anthropic', state: 'Ready' }), check({ key: 'model:mep_2', provider: 'Codex', state: 'Failed' })];
    expect(connectedCheck(list, 'Codex').key).toBe('model:mep_2');
    expect(connectedCheck(list, 'Mux').key).toBe('model:mep_2');
    expect([progressState('ready'), progressState('Failed'), progressState('empty'), progressState('Unverified')]).toEqual(['done', 'attention', 'todo', 'todo']);
    expect(isHttpUrl('http://host.docker.internal:11434') && isHttpUrl('https://x.example/v1')).toBe(true);
    expect(isHttpUrl('ftp://x') || isHttpUrl('host:11434') || isHttpUrl('')).toBe(false);
  });
});
