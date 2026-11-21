// What the Setup page and the Models page editor tell an administrator about each provider and sign-in method:
// labels, step-by-step instructions, commands to copy, where to create keys, and what to try when a test fails.
// Commands, paths, and URLs are not translated; the sentences around them are catalog keys.

import { authFileName, isAgentCli } from '../../utils/modelAuth.js';

// The container name the server reports when it does not say otherwise (docker/compose.yaml).
export const DEFAULT_SERVER_CONTAINER = 'mymorningnews-server';

// Provider groups on the Setup page, in display order.
export const PROVIDER_GROUPS = ['AgentCli', 'Api', 'SelfHosted'];

// Where each hosted provider's API keys are created.
const KEY_PAGES = {
  Anthropic: 'https://console.anthropic.com/settings/keys',
  ClaudeCode: 'https://console.anthropic.com/settings/keys',
  OpenAi: 'https://platform.openai.com/api-keys',
  Codex: 'https://platform.openai.com/api-keys',
  Gemini: 'https://aistudio.google.com/apikey',
  GeminiCli: 'https://aistudio.google.com/apikey',
  XAi: 'https://console.x.ai',
  OllamaCloud: 'https://ollama.com/settings/keys'
};

// How to install each CLI on a computer, for the person who does not have it yet.
const INSTALL_COMMANDS = {
  ClaudeCode: 'npm install -g @anthropic-ai/claude-code',
  Codex: 'npm install -g @openai/codex',
  GeminiCli: 'npm install -g @google/gemini-cli'
};

// Where each CLI saves the sign-in file the AuthFile method uploads.
const FILE_PATHS = {
  Codex: { unix: '~/.codex/auth.json', windows: '%USERPROFILE%\\.codex\\auth.json' },
  GeminiCli: { unix: '~/.gemini/oauth_creds.json', windows: '%USERPROFILE%\\.gemini\\oauth_creds.json' },
  Mux: { unix: '~/.mux/endpoints.json', windows: '%USERPROFILE%\\.mux\\endpoints.json' }
};

// The usual Ollama address from inside the server container on Docker Desktop.
export const OLLAMA_DOCKER_URL = 'http://host.docker.internal:11434';
export const COMPATIBLE_URL_EXAMPLE = 'http://host.docker.internal:8000/v1';

// Setup names that differ from the provider's enum label.
const NAME_KEYS = { Gemini: 'setup.providers.geminiName', OpenAiCompatible: 'setup.providers.compatibleName' };

const TAGLINE_KEYS = {
  ClaudeCode: 'setup.providers.ClaudeCode',
  Codex: 'setup.providers.Codex',
  GeminiCli: 'setup.providers.GeminiCli',
  Mux: 'setup.providers.Mux',
  Anthropic: 'setup.providers.Anthropic',
  OpenAi: 'setup.providers.OpenAi',
  Gemini: 'setup.providers.Gemini',
  XAi: 'setup.providers.XAi',
  OllamaCloud: 'setup.providers.OllamaCloud',
  Ollama: 'setup.providers.Ollama',
  OpenAiCompatible: 'setup.providers.OpenAiCompatible'
};

// Plain labels for a provider's sign-in methods. Methods not listed use the enum label.
const METHOD_LABELS = {
  ClaudeCode: {
    AccessToken: 'setup.method.subscriptionToken',
    ApiKey: 'setup.method.anthropicKey',
    ServerSignIn: 'setup.method.serverSignIn'
  },
  Codex: { AuthFile: 'setup.method.chatGptFile', ApiKey: 'setup.method.openAiKey', ServerSignIn: 'setup.method.serverSignIn' },
  GeminiCli: { AuthFile: 'setup.method.googleFile', ApiKey: 'setup.method.geminiKey', ServerSignIn: 'setup.method.serverSignIn' },
  Mux: { AuthFile: 'setup.method.muxFile', ServerSignIn: 'setup.method.serverConfigured' },
  Ollama: { None: 'setup.method.noKey', ApiKey: 'setup.method.apiKey' },
  OpenAiCompatible: { ApiKey: 'setup.method.apiKey', None: 'setup.method.noKey' }
};

export function providerName(t, provider) {
  return t(NAME_KEYS[provider] || 'enums.provider.' + provider, { defaultValue: provider || '' });
}

export function providerTagline(t, provider) {
  return TAGLINE_KEYS[provider] ? t(TAGLINE_KEYS[provider]) : '';
}

export function methodLabel(t, provider, method) {
  const key = (METHOD_LABELS[provider] || {})[method];
  if (key) return t(key, { file: authFileName(provider) || '' });
  return t('enums.authMethod.' + method, { defaultValue: method || '' });
}

// The label of the field that takes the method's secret.
export function secretLabel(t, provider, method) {
  if (method === 'AccessToken') return t('setup.field.token');
  if (method === 'AuthFile') return t('setup.field.file', { file: authFileName(provider) || '' });
  return t('setup.field.apiKey');
}

// The command that signs a CLI in inside the server container.
export function serverCommand(provider, container) {
  const c = container || DEFAULT_SERVER_CONTAINER;
  switch (provider) {
    case 'ClaudeCode':
      return 'docker exec -it ' + c + ' claude';
    case 'Codex':
      return 'docker exec -it ' + c + ' codex login --device-auth';
    case 'GeminiCli':
      return 'docker exec -it ' + c + ' gemini';
    case 'Mux':
      return 'docker exec -it ' + c + ' mux';
    default:
      return null;
  }
}

function keySteps(provider) {
  const page = KEY_PAGES[provider];
  if (!page) return [{ key: 'setup.steps.selfKey' }];
  return [{ key: 'setup.steps.openKeys', link: page }, { key: 'setup.steps.createKey' }];
}

function selfHostedSteps(provider) {
  if (provider === 'Ollama')
    return [
      { key: 'setup.steps.ollamaAddress', values: { url: OLLAMA_DOCKER_URL } },
      { key: 'setup.steps.ollamaModel', commands: ['ollama list'] }
    ];
  return [{ key: 'setup.steps.compatAddress' }, { key: 'setup.steps.compatModel' }];
}

// Numbered steps for a provider and method: [{ key, values, commands, paths, link }]. commands are copyable chips;
// paths is { unix, windows } for a file to choose; link is a URL opened in a new tab.
export function signInSteps(provider, method, container) {
  if (!provider || !method) return [];
  const selfHosted = provider === 'Ollama' || provider === 'OpenAiCompatible';
  if (method === 'None') return selfHosted ? selfHostedSteps(provider) : [];
  if (method === 'ApiKey') return selfHosted ? [...selfHostedSteps(provider), { key: 'setup.steps.selfKey' }] : keySteps(provider);
  if (method === 'AccessToken') {
    return [
      { key: 'setup.steps.runAnywhere', values: { app: 'Claude Code' }, commands: ['claude setup-token'] },
      { key: 'setup.steps.finishBrowser' },
      { key: 'setup.steps.copyToken', values: { prefix: 'sk-ant-oat' } }
    ];
  }
  if (method === 'AuthFile') {
    if (provider === 'Codex')
      return [
        { key: 'setup.steps.codexLogin', commands: ['codex login'] },
        { key: 'setup.steps.chooseFile', paths: FILE_PATHS.Codex }
      ];
    if (provider === 'GeminiCli')
      return [
        { key: 'setup.steps.geminiLogin', commands: ['gemini'] },
        { key: 'setup.steps.chooseFile', paths: FILE_PATHS.GeminiCli }
      ];
    if (provider === 'Mux') return [{ key: 'setup.steps.muxChoose', paths: FILE_PATHS.Mux }];
    return [];
  }
  if (method === 'ServerSignIn') {
    const finish = {
      ClaudeCode: { key: 'setup.steps.serverClaudeLogin' },
      Codex: { key: 'setup.steps.serverCodexLogin' },
      GeminiCli: { key: 'setup.steps.serverGeminiLogin' },
      Mux: { key: 'setup.steps.serverMuxConfigure', values: { file: FILE_PATHS.Mux.unix } }
    }[provider];
    const command = serverCommand(provider, container);
    return command && finish ? [{ key: 'setup.steps.serverOpen', commands: [command] }, finish, { key: 'setup.steps.serverReturn' }] : [];
  }
  return [];
}

// Notes under the steps: { info, warning, tip, install } where each is a catalog key, except install (a command).
export function signInNotes(provider, method) {
  const notes = {};
  if (method === 'AccessToken') notes.info = 'setup.steps.tokenNote';
  if (method === 'AuthFile') {
    notes.warning = provider === 'Mux' ? 'setup.steps.muxWarning' : 'setup.steps.fileWarning';
    notes.tip = 'setup.steps.hiddenFolder';
  }
  if (isAgentCli(provider) && (method === 'AccessToken' || method === 'AuthFile') && INSTALL_COMMANDS[provider])
    notes.install = INSTALL_COMMANDS[provider];
  return notes;
}

// What to try when the connection test fails: { key, values, command }.
export function failureHint(provider, method, container) {
  switch (method) {
    case 'AccessToken':
      return { key: 'setup.hint.token', values: { command: 'claude setup-token' } };
    case 'AuthFile':
      if (provider === 'Codex') return { key: 'setup.hint.file', values: { command: 'codex login', file: 'auth.json' } };
      if (provider === 'GeminiCli') return { key: 'setup.hint.file', values: { command: 'gemini', file: 'oauth_creds.json' } };
      return { key: 'setup.hint.muxFile', values: { file: authFileName(provider) || '' } };
    case 'ServerSignIn':
      return { key: 'setup.hint.server', command: serverCommand(provider, container) };
    case 'None':
      return { key: 'setup.hint.selfHosted' };
    default:
      return { key: provider === 'Ollama' || provider === 'OpenAiCompatible' ? 'setup.hint.selfHosted' : 'setup.hint.apiKey' };
  }
}

// The connect fields a 400 can be shown next to. provider and authMethod refusals go to the result area.
const FIELD_NAMES = ['secret', 'model', 'baseUrl'];

// Which connect field a 400 is about: 'secret', 'model', 'baseUrl', or null for the result area. The server names it
// in details.field; older servers did not, so the message is read as a fallback.
export function errorField(error) {
  if (!error) return null;
  const field = error.details && typeof error.details.field === 'string' ? error.details.field : null;
  if (field) return FIELD_NAMES.includes(field) ? field : null;
  if (error.code === 'EgressBlocked') return 'baseUrl';
  const message = String(error.message || '');
  if (/server's address|base url|absolute url/i.test(message)) return 'baseUrl';
  if (/name the model/i.test(message)) return 'model';
  if (/^paste |one line|looks like an|not the contents of|has no (google )?sign-in|should be a json/i.test(message)) return 'secret';
  return null;
}
