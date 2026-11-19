import { readSecret, secureKey, writeSecret } from '@/storage/secure';

/**
 * A saved session for one server:
 * `{ token, expiresUtc, refreshToken?, refreshExpiresUtc?, sessionId, tenantId, userId, user }`.
 * `refreshToken` is present only when the server supports mobile refresh tokens (contract A).
 */
export function isTokenSet(value) {
  return Boolean(value && typeof value === 'object' && typeof value.token === 'string' && value.token.length > 0);
}

/** Tokens persisted in the Keychain or Keystore, one entry per server URL. */
export const secureTokenStorage = {
  async load(serverUrl) {
    const text = await readSecret(secureKey('mymorningnews.session', serverUrl));
    if (!text) return null;
    try {
      const parsed = JSON.parse(text);
      return isTokenSet(parsed) ? parsed : null;
    } catch {
      return null;
    }
  },
  async save(serverUrl, tokens) {
    await writeSecret(secureKey('mymorningnews.session', serverUrl), tokens ? JSON.stringify(tokens) : null);
  },
};

/** In-memory storage for tests. */
export function memoryTokenStorage() {
  const map = new Map();
  return {
    async load(serverUrl) {
      return map.has(serverUrl) ? map.get(serverUrl) : null;
    },
    async save(serverUrl, tokens) {
      if (tokens) map.set(serverUrl, tokens);
      else map.delete(serverUrl);
    },
  };
}
