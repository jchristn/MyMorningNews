/**
 * @jest-environment node
 */
// Live checks against a running server (skipped unless MYMORNINGNEWS_LIVE_SERVER is set), for the mobile contracts:
//   MYMORNINGNEWS_LIVE_SERVER=http://127.0.0.1:12100 npx jest src/__tests__/live.test.js
// Optional: MYMORNINGNEWS_LIVE_EMAIL and MYMORNINGNEWS_LIVE_PASSWORD (default admin@mymorningnews / password).
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { ApiClient } from '@/api/client';
import { accountApi, devicesApi, serverApi } from '@/api/account';
import { authApi } from '@/api/auth';
import { briefingsApi, topicsApi } from '@/api/research';
import { isNotAvailable } from '@/api/errors';

const SERVER = process.env.MYMORNINGNEWS_LIVE_SERVER;
const EMAIL = process.env.MYMORNINGNEWS_LIVE_EMAIL || 'admin@mymorningnews';
const PASSWORD = process.env.MYMORNINGNEWS_LIVE_PASSWORD || 'password';
const live = SERVER ? describe : describe.skip;
// The Jest preset replaces fetch with a stub, so live calls use a minimal fetch over node:http (status and text are
// all the ApiClient reads).
const http = require('node:http');
const https = require('node:https');
function fetchImpl(url, init = {}) {
  return new Promise((resolve, reject) => {
    const target = new URL(url);
    const request = (target.protocol === 'https:' ? https : http).request(target, { method: init.method || 'GET', headers: init.headers || {} }, (response) => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', (chunk) => (body += chunk));
      response.on('end', () => resolve({ status: response.statusCode, ok: response.statusCode < 300, text: async () => body }));
    });
    request.on('error', reject);
    if (init.signal) init.signal.addEventListener('abort', () => request.destroy(new Error('aborted')));
    if (init.body) request.write(init.body);
    request.end();
  });
}

live('live server contracts', () => {
  it('D: serves client config without authentication', async () => {
    const config = await serverApi.clientConfig(new ApiClient({ baseUrl: SERVER, fetchImpl }));
    expect(config).toMatchObject({ productName: 'My Morning News', links: expect.objectContaining({ privacy: expect.any(String) }) });
    expect(typeof config.serverVersion).toBe('string');
  });

  it('A and B: tenant-less mobile sign-in, refresh-token rotation, reuse revokes, sign-out', async () => {
    const storage = memoryTokenStorage();
    const auth = new AuthController({ serverUrl: SERVER, storage, fetchImpl });
    await auth.signIn({ email: EMAIL, password: PASSWORD });
    const first = await storage.load(SERVER);
    expect(first.refreshToken).toBeTruthy();
    expect(Date.parse(first.refreshExpiresUtc)).toBeGreaterThan(Date.now() + 20 * 86400000);

    await expect(auth.refresh()).resolves.toBe(true);
    const second = await storage.load(SERVER);
    expect(second.refreshToken).not.toBe(first.refreshToken);
    expect(second.token).not.toBe(first.token);
    await expect(accountApi.getMe(auth.client)).resolves.toMatchObject({ email: EMAIL });

    // Reusing the rotated-out refresh token is rejected and revokes the sign-in.
    const reuse = await authApi.refreshWithToken(new ApiClient({ baseUrl: SERVER, fetchImpl }), first.refreshToken).catch((e) => e);
    expect(reuse.status).toBe(401);
    const after = await new ApiClient({ baseUrl: SERVER, fetchImpl, getAccessToken: async () => second.token }).get('/v1.0/api/me').catch((e) => e);
    expect(after.status).toBe(401);

    const again = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl });
    await again.signIn({ email: EMAIL, password: PASSWORD });
    await again.signOut();
    expect(again.getState().status).toBe('signedOut');
  });

  it('C: registers, lists, tests, and removes a device; profile and usage work', async () => {
    const auth = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl });
    await auth.signIn({ email: EMAIL, password: PASSWORD });
    const token = 'ExponentPushToken[mobile-live-test-' + Date.now() + ']';
    const device = await devicesApi.register(auth.client, { expoPushToken: token, platform: 'ios', name: 'Live test', locale: 'en' });
    expect(device).toMatchObject({ platform: 'ios', name: 'Live test' });
    const again = await devicesApi.register(auth.client, { expoPushToken: token, platform: 'ios', name: 'Live test', locale: 'en' });
    expect(again.id).toBe(device.id);
    const list = await devicesApi.list(auth.client);
    expect((list.items || list).some((d) => d.id === device.id)).toBe(true);
    // A made-up token is rejected by Expo, and the server prunes the device it belongs to.
    const sent = await devicesApi.test(auth.client, device.id);
    expect(sent).toEqual(expect.objectContaining({ accepted: expect.any(Number), failed: expect.any(Number), removed: expect.any(Number) }));
    const removed = await devicesApi.remove(auth.client, device.id).catch((e) => e);
    expect(removed === null || isNotAvailable(removed)).toBe(true);

    await expect(accountApi.usage(auth.client, { groupBy: 'Task' })).resolves.toMatchObject({ totals: expect.any(Object), rows: expect.any(Array) });
    await expect(topicsApi.list(auth.client, { pageNumber: 1, pageSize: 5 })).resolves.toMatchObject({ items: expect.any(Array) });
    const briefings = await briefingsApi.list(auth.client, {}).catch((e) => e);
    expect(Array.isArray(briefings.items) || isNotAvailable(briefings)).toBe(true);
    await auth.signOut();
  });
});
