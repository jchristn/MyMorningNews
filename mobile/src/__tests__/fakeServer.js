// A scripted fake of the server for unit tests: `fetchImpl` records every request and answers from handlers keyed by
// "METHOD /path" (query ignored). A handler returns `{ status, body }` or throws to simulate a network failure.
export function fakeServer(handlers) {
  const calls = [];
  async function fetchImpl(url, init = {}) {
    const path = url.replace(/^https?:\/\/[^/]+/, '').split('?')[0];
    const key = (init.method || 'GET') + ' ' + path;
    const call = { key, url, headers: init.headers || {}, body: init.body ? JSON.parse(init.body) : undefined };
    calls.push(call);
    const handler = handlers[key];
    if (!handler) return response(404, { error: 'NotFound', message: 'The requested resource was not found.' });
    const result = await handler(call, calls);
    return response(result.status || 200, result.body);
  }
  return { fetchImpl, calls, count: (key) => calls.filter((c) => c.key === key).length };
}

function response(status, body) {
  const text = body === undefined || body === null ? '' : typeof body === 'string' ? body : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text };
}

/** A TokenResponse like the server's, expiring `minutes` from `now`. */
export function tokenResponse({ token = 'tok-1', refreshToken = 'ref-1', now = Date.now(), minutes = 60, user } = {}) {
  return {
    token,
    expiresUtc: new Date(now + minutes * 60000).toISOString(),
    tenantId: 'default',
    sessionId: 'ses_1',
    userId: 'usr_1',
    principalType: 'User',
    user: user || { id: 'usr_1', email: 'admin@mymorningnews', firstName: 'Default', lastName: 'Administrator', tenantId: 'default' },
    ...(refreshToken ? { refreshToken, refreshExpiresUtc: new Date(now + 30 * 86400000).toISOString() } : null),
  };
}
