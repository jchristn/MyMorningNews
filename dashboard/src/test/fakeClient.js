import { vi } from 'vitest';

export const ADMIN_DETAILS = {
  valid: true,
  sessionId: 'ses_1',
  principalType: 'User',
  scheme: 'Bearer',
  tenantId: 'default',
  tenantName: 'Default',
  userId: 'usr_admin',
  email: 'admin@mymorningnews',
  displayName: 'Admin',
  isAdmin: true,
  isTenantAdmin: true
};

export const MEMBER_DETAILS = { ...ADMIN_DETAILS, userId: 'usr_member', email: 'member@mymorningnews', isAdmin: false, isTenantAdmin: false };

// A setup status where every required check passed, so views render the product. Tests of the gate override it.
export const READY_SETUP = {
  ready: true,
  canConfigure: true,
  checkedUtc: '2026-11-20T08:00:00Z',
  checks: [
    { key: 'model:mep_1', kind: 'Model', required: true, state: 'Ready', message: null, resourceId: 'mep_1', resourceName: 'Anthropic', provider: 'Anthropic', model: 'claude-opus-5-5', tasks: ['Refinement', 'Synthesis'], authMethod: 'ApiKey', needsApiKey: true, hasApiKey: true, usesPlatformKey: false, verifiedUtc: '2026-11-20T08:00:00Z' },
    { key: 'search', kind: 'Search', required: true, state: 'Ready', message: null, resourceId: 'srp_1', resourceName: 'SearXNG', provider: 'Searxng', model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: '2026-11-20T08:00:00Z' }
  ]
};

// The providers and sign-in methods GET /v1.0/api/setup/options returns on a server with every CLI installed. Only
// Anthropic is recommended.
const text = (method, recommended = false) => ({ method, input: 'Text', fileName: null, recommended });
const file = (fileName) => ({ method: 'AuthFile', input: 'File', fileName, recommended: true });
const none = (method, recommended = false) => ({ method, input: 'None', fileName: null, recommended });
const provider = (name, group, extra = {}) => ({ provider: name, group, available: true, installed: true, turnedOff: false, unavailableReason: null, recommended: false, defaultModel: null, needsBaseUrl: false, defaultBaseUrl: null, methods: [text('ApiKey', true)], ...extra });
export const SETUP_OPTIONS = {
  serverContainer: 'mymorningnews-server',
  providers: [
    provider('Anthropic', 'Api', { recommended: true, defaultModel: 'claude-opus-5-5' }),
    provider('OpenAi', 'Api'),
    provider('Gemini', 'Api'),
    provider('XAi', 'Api'),
    provider('OllamaCloud', 'Api'),
    provider('ClaudeCode', 'AgentCli', { defaultModel: 'claude-opus-5-5', methods: [text('AccessToken', true), text('ApiKey'), none('ServerSignIn')] }),
    provider('Codex', 'AgentCli', { defaultModel: 'default', methods: [file('auth.json'), text('ApiKey'), none('ServerSignIn')] }),
    provider('GeminiCli', 'AgentCli', { defaultModel: 'default', methods: [file('oauth_creds.json'), text('ApiKey'), none('ServerSignIn')] }),
    provider('Mux', 'AgentCli', { defaultModel: 'default', methods: [file('endpoints.json'), none('ServerSignIn')] }),
    provider('Ollama', 'SelfHosted', { needsBaseUrl: true, defaultBaseUrl: 'http://host.docker.internal:11434', methods: [none('None', true), text('ApiKey')] }),
    provider('OpenAiCompatible', 'SelfHosted', { needsBaseUrl: true, methods: [text('ApiKey', true), none('None')] })
  ]
};

export const EMPTY_PAGE = { objects: [], pageNumber: 1, pageSize: 10, totalRecords: 0, totalPages: 0, totalMs: 1 };

// A client whose named methods are spies. Methods not given in overrides resolve to an empty page so any view renders.
export function fakeClient(overrides = {}) {
  const base = {
    login: vi.fn(async () => ({ token: 'tok_1', tenantId: 'default' })),
    getTokenDetails: vi.fn(async () => ADMIN_DETAILS),
    logout: vi.fn(async () => null),
    healthCheck: vi.fn(async () => ({ status: 'Healthy', version: '0.1.0-alpha', uptimeSeconds: 5, components: { database: 'Healthy' } })),
    getRequestHistorySummary: vi.fn(async () => ({ totalCount: 0, totalSuccess: 0, totalFailure: 0, averageDurationMs: 0, buckets: [] })),
    getServerInfo: vi.fn(async () => ({ product: 'My Morning News', version: '0.1.0-alpha', uptimeSeconds: 5, externalServices: [] })),
    getSetupStatus: vi.fn(async () => READY_SETUP),
    getSetupOptions: vi.fn(async () => SETUP_OPTIONS),
    ...overrides
  };
  return new Proxy(base, {
    get(target, prop) {
      if (prop in target) return target[prop];
      if (prop === 'then' || typeof prop === 'symbol') return undefined;
      target[prop] = vi.fn(async () => EMPTY_PAGE);
      return target[prop];
    }
  });
}
