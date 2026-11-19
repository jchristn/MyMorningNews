import { AuthController, EARLY_REFRESH_MS } from '@/auth/AuthController';
import { lockTransition } from '@/auth/lockPolicy';
import { memoryTokenStorage, secureTokenStorage } from '@/auth/tokens';
import { TenantSelectionRequiredError } from '@/api/errors';
import { accountApi } from '@/api/account';
import { fakeServer, tokenResponse } from './fakeServer';

const SERVER = 'http://127.0.0.1:12100';

function controller(handlers, { now = () => Date.now(), storage = memoryTokenStorage() } = {}) {
  const server = fakeServer(handlers);
  const auth = new AuthController({ serverUrl: SERVER, storage, fetchImpl: server.fetchImpl, now });
  return { auth, server, storage };
}

describe('AuthController', () => {
  it('signs in with x-client mobile and stores the session and refresh token per server', async () => {
    const { auth, server, storage } = controller({ 'POST /v1.0/token': () => ({ body: tokenResponse() }) });
    await auth.signIn({ email: ' admin@mymorningnews ', password: 'password' });
    expect(server.calls[0].headers['x-email']).toBe('admin@mymorningnews');
    expect(server.calls[0].headers['x-client']).toBe('mobile');
    expect(auth.getState()).toMatchObject({ status: 'signedIn', tenantId: 'default', serverUrl: SERVER });
    const saved = await storage.load(SERVER);
    expect(saved).toMatchObject({ token: 'tok-1', refreshToken: 'ref-1', sessionId: 'ses_1' });
    expect(await storage.load('https://other.example')).toBeNull();
  });

  it('surfaces tenant selection (contract B) and signs in after the retry with the chosen tenant', async () => {
    const { auth, server } = controller({
      'POST /v1.0/token': (call) =>
        call.headers['x-tenant-guid'] ? { body: tokenResponse({ token: 'tok-b' }) } : { status: 409, body: { error: 'TenantSelectionRequired', tenants: [{ id: 'ten_a', name: 'A' }, { id: 'ten_b', name: 'B' }] } },
    });
    const error = await auth.signIn({ email: 'a@b', password: 'p' }).catch((e) => e);
    expect(error).toBeInstanceOf(TenantSelectionRequiredError);
    expect(auth.getState().status).toBe('loading');
    await auth.signIn({ email: 'a@b', password: 'p', tenantId: 'ten_b' });
    expect(server.calls[1].headers['x-tenant-guid']).toBe('ten_b');
    expect(auth.getState().status).toBe('signedIn');
  });

  it('refreshes before expiry with the rotating refresh token, one refresh at a time', async () => {
    let now = Date.now();
    let issued = 0;
    const { auth, server, storage } = controller(
      {
        'POST /v1.0/token': () => ({ body: tokenResponse({ now, minutes: 60 }) }),
        'POST /v1.0/token/refresh-token': async (call) => {
          issued += 1;
          await new Promise((r) => setTimeout(r, 10));
          return { body: tokenResponse({ token: 'tok-' + (issued + 1), refreshToken: 'ref-' + (issued + 1), now, minutes: 60 }) };
        },
        'GET /v1.0/api/me': (call) => ({ body: { id: 'usr_1', auth: call.headers.Authorization } }),
      },
      { now: () => now },
    );
    await auth.signIn({ email: 'a@b', password: 'p' });
    now += 60 * 60000 - EARLY_REFRESH_MS + 1000;
    const results = await Promise.all([accountApi.getMe(auth.client), accountApi.getMe(auth.client), accountApi.getMe(auth.client)]);
    expect(server.count('POST /v1.0/token/refresh-token')).toBe(1);
    expect(server.calls.find((c) => c.key === 'POST /v1.0/token/refresh-token').body).toEqual({ refreshToken: 'ref-1' });
    expect(results.every((r) => r.auth === 'Bearer tok-2')).toBe(true);
    expect((await storage.load(SERVER)).refreshToken).toBe('ref-2');
  });

  it('refreshes after a 401 and retries, then ends the session when the refresh is rejected', async () => {
    let meCalls = 0;
    const { auth, server } = controller({
      'POST /v1.0/token': () => ({ body: tokenResponse() }),
      'POST /v1.0/token/refresh-token': (call) => (call.body.refreshToken === 'ref-1' ? { body: tokenResponse({ token: 'tok-2', refreshToken: 'ref-2' }) } : { status: 401, body: { error: 'Unauthorized' } }),
      'GET /v1.0/api/me': (call) => {
        meCalls += 1;
        if (meCalls === 1) return { status: 401, body: { error: 'Unauthorized' } };
        if (meCalls === 2) return { body: { id: 'usr_1', auth: call.headers.Authorization } };
        return { status: 401, body: { error: 'Unauthorized' } };
      },
    });
    await auth.signIn({ email: 'a@b', password: 'p' });
    await expect(accountApi.getMe(auth.client)).resolves.toMatchObject({ auth: 'Bearer tok-2' });
    // The server revokes the sign-in; the next refresh (with ref-2) is rejected.
    server.calls.length = 0;
    await expect(accountApi.getMe(auth.client)).rejects.toMatchObject({ name: 'SessionExpiredError' });
    await new Promise((r) => setTimeout(r, 0));
    expect(auth.getState()).toMatchObject({ status: 'signedOut', reason: 'expired' });
  });

  it('uses the authenticated legacy refresh when the server issued no refresh token', async () => {
    let now = Date.now();
    const { auth, server } = controller(
      {
        'POST /v1.0/token': () => ({ body: tokenResponse({ refreshToken: null, now, minutes: 10 }) }),
        'POST /v1.0/token/refresh': (call) => ({ body: { ...tokenResponse({ token: 'tok-legacy', refreshToken: null, now, minutes: 10 }), user: undefined, seen: call.headers.Authorization } }),
        'GET /v1.0/api/me': (call) => ({ body: { auth: call.headers.Authorization } }),
      },
      { now: () => now },
    );
    await auth.signIn({ email: 'a@b', password: 'p' });
    expect(auth.hasRefreshToken()).toBe(false);
    now += 10 * 60000;
    await expect(accountApi.getMe(auth.client)).resolves.toEqual({ auth: 'Bearer tok-legacy' });
    expect(server.calls.find((c) => c.key === 'POST /v1.0/token/refresh').headers.Authorization).toBe('Bearer tok-1');
  });

  it('keeps the session when the refresh fails for lack of network', async () => {
    let now = Date.now();
    let offline = false;
    const server = fakeServer({ 'POST /v1.0/token': () => ({ body: tokenResponse({ now }) }) });
    const fetchImpl = (url, init) => (offline ? Promise.reject(new TypeError('Network request failed')) : server.fetchImpl(url, init));
    const auth = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl, now: () => now });
    await auth.signIn({ email: 'a@b', password: 'p' });
    offline = true;
    now += 60 * 60000;
    await expect(auth.refresh()).resolves.toBe(false);
    expect(auth.getState().status).toBe('signedIn');
  });

  it('signs out with DELETE /v1.0/token and forgets the tokens', async () => {
    const { auth, server, storage } = controller({ 'POST /v1.0/token': () => ({ body: tokenResponse() }), 'DELETE /v1.0/token': () => ({ status: 204 }) });
    await auth.signIn({ email: 'a@b', password: 'p' });
    await auth.signOut();
    expect(server.count('DELETE /v1.0/token')).toBe(1);
    expect(server.calls[1].headers.Authorization).toBe('Bearer tok-1');
    expect(await storage.load(SERVER)).toBeNull();
    expect(auth.getState()).toMatchObject({ status: 'signedOut', reason: null });
  });

  it('restores a saved session, keeps it offline, and drops it when the server rejects it', async () => {
    const storage = memoryTokenStorage();
    await storage.save(SERVER, { token: 'saved', expiresUtc: new Date(Date.now() + 3600000).toISOString(), user: { email: 'cached@x' } });
    const ok = controller({ 'GET /v1.0/api/me': () => ({ body: { id: 'usr_1', email: 'fresh@x' } }) }, { storage });
    await ok.auth.restore();
    expect(ok.auth.getState()).toMatchObject({ status: 'signedIn', user: { email: 'fresh@x' } });

    const offline = new AuthController({ serverUrl: SERVER, storage, fetchImpl: () => Promise.reject(new TypeError('offline')) });
    await offline.restore();
    expect(offline.getState()).toMatchObject({ status: 'signedIn', user: { email: 'fresh@x' } });

    const rejected = controller({ 'GET /v1.0/api/me': () => ({ status: 401, body: { error: 'Unauthorized' } }), 'POST /v1.0/token/refresh': () => ({ status: 401, body: {} }) }, { storage });
    await rejected.auth.restore();
    expect(rejected.auth.getState()).toMatchObject({ status: 'signedOut', reason: 'expired' });
    expect(await storage.load(SERVER)).toBeNull();
  });

  it('starts signed out without saved tokens', async () => {
    const { auth } = controller({});
    await auth.restore();
    expect(auth.getState()).toMatchObject({ status: 'signedOut', reason: null });
  });

  it('persists tokens in the secure store keyed by server', async () => {
    await secureTokenStorage.save('https://app.mymorningnews.ai', { token: 'a' });
    expect(await secureTokenStorage.load('https://app.mymorningnews.ai')).toEqual({ token: 'a' });
    expect(await secureTokenStorage.load('http://127.0.0.1:12100')).toBeNull();
    await secureTokenStorage.save('https://app.mymorningnews.ai', null);
    expect(await secureTokenStorage.load('https://app.mymorningnews.ai')).toBeNull();
  });
});

describe('lock policy', () => {
  it('ignores the inactive state the Face ID sheet causes', () => {
    expect(lockTransition('inactive', { locked: false, backgroundedAt: null }, 1000, 0)).toEqual({ locked: false, backgroundedAt: null, prompt: false });
  });

  it('locks on return after the timeout, and not before it', () => {
    const away = lockTransition('background', { locked: false, backgroundedAt: null }, 1000, 60000);
    expect(away).toEqual({ locked: false, backgroundedAt: 1000, prompt: false });
    expect(lockTransition('active', away, 30000, 60000)).toEqual({ locked: false, backgroundedAt: null, prompt: false });
    expect(lockTransition('active', away, 61000, 60000)).toEqual({ locked: true, backgroundedAt: null, prompt: true });
  });

  it('locks on every return with a zero timeout and keeps the first background time', () => {
    const first = lockTransition('background', { locked: false, backgroundedAt: null }, 5, 0);
    const again = lockTransition('background', first, 10, 0);
    expect(again.backgroundedAt).toBe(5);
    expect(lockTransition('active', again, 6, 0)).toMatchObject({ locked: true, prompt: true });
  });
});
