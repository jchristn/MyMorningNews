// How model endpoints sign in to their providers. Pure functions mirroring AgentCliService on the server, so the
// Setup page, the Models page, and the key warnings agree on what each method needs.

import { AGENT_CLI_PROVIDERS, AUTH_FILE_NAMES, AUTH_METHODS_FOR, SECRET_AUTH_METHODS } from './enums.js';

// The value the server stores for "let the CLI pick its own model".
export const CLI_DEFAULT_MODEL = 'default';

export function isAgentCli(provider) {
  return AGENT_CLI_PROVIDERS.includes(provider);
}

// The sign-in methods a provider accepts, recommended first. Hosted API providers take an API key only.
export function authMethodsFor(provider) {
  return AUTH_METHODS_FOR[provider] || ['ApiKey'];
}

// True when the method needs a stored secret (an API key, a subscription token, or a sign-in file).
export function needsSecret(method) {
  return SECRET_AUTH_METHODS.includes(method);
}

// The file a provider's AuthFile sign-in reads, or null.
export function authFileName(provider) {
  return AUTH_FILE_NAMES[provider] || null;
}

// The method an endpoint uses: its own, else what the server assumes from its provider and stored credential.
export function effectiveAuthMethod(endpoint) {
  if (!endpoint) return 'ApiKey';
  const methods = authMethodsFor(endpoint.provider);
  if (endpoint.authMethod && methods.includes(endpoint.authMethod)) return endpoint.authMethod;
  if (isAgentCli(endpoint.provider)) return endpoint.credentialId ? methods[0] : 'ServerSignIn';
  if (endpoint.provider === 'Ollama' || endpoint.provider === 'OpenAiCompatible') return endpoint.credentialId ? 'ApiKey' : 'None';
  return 'ApiKey';
}

// True when a model value means the CLI's own default (empty, or the literal "default").
export function isCliDefaultModel(model) {
  return !model || String(model).trim().toLowerCase() === CLI_DEFAULT_MODEL;
}
