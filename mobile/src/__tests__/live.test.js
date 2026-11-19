/**
 * @jest-environment node
 */
// Live checks against a running server (skipped unless MYMORNINGNEWS_LIVE_SERVER is set), for the mobile contracts:
//   MYMORNINGNEWS_LIVE_SERVER=http://127.0.0.1:12100 npx jest src/__tests__/live.test.js
// Optional: MYMORNINGNEWS_LIVE_EMAIL and MYMORNINGNEWS_LIVE_PASSWORD (default admin@mymorningnews / password).
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { ApiClient, listItems } from '@/api/client';
import { accountApi, devicesApi, serverApi } from '@/api/account';
import { authApi } from '@/api/auth';
import { briefingsApi, editionsApi, findingsApi, predictionsApi, refinementApi, requestsApi, runsApi, schedulesApi, sourcesApi, suggestionsApi, topicsApi } from '@/api/research';
import { isItemNotFound, isModelError, isNotAvailable } from '@/api/errors';
import { routes } from '@/api/routes';
import { parseBriefing } from '@/lib/markdown';

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
    // A made-up token is rejected by Expo, and the server prunes the device it belongs to, so removing it afterwards
    // finds nothing (404 for the item) unless the push service kept it.
    const sent = await devicesApi.test(auth.client, device.id);
    expect(sent).toEqual(expect.objectContaining({ accepted: expect.any(Number), failed: expect.any(Number), removed: expect.any(Number) }));
    const removed = await devicesApi.remove(auth.client, device.id).catch((e) => e);
    expect(removed === null || isNotAvailable(removed) || isItemNotFound(removed)).toBe(true);

    await expect(accountApi.usage(auth.client, { groupBy: 'Task' })).resolves.toMatchObject({ totals: expect.any(Object), rows: expect.any(Array) });
    await expect(topicsApi.list(auth.client, { pageNumber: 1, pageSize: 5 })).resolves.toMatchObject({ items: expect.any(Array) });
    const briefings = await briefingsApi.list(auth.client, {}).catch((e) => e);
    expect(Array.isArray(briefings.items) || isNotAvailable(briefings)).toBe(true);
    await auth.signOut();
  });

  // Phases 4 to 7, the routes the research screens call. Everything this creates is deleted again.
  it('research: editions compose, briefings, wizard by hand, schedules, runs, requests, and lists', async () => {
    const auth = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl });
    await auth.signIn({ email: EMAIL, password: PASSWORD });
    const c = auth.client;
    const cleanup = [];
    try {
      for (const call of [findingsApi.list(c, { pageSize: 5 }), predictionsApi.list(c, { pageSize: 5 }), suggestionsApi.list(c, { pageSize: 5 }), sourcesApi.list(c, { pageSize: 5 }), runsApi.list(c, { pageSize: 5 }), requestsApi.list(c, { pageSize: 5 })]) {
        await expect(call).resolves.toMatchObject({ items: expect.any(Array) });
      }

      // Editions: compose now, poll until ready, read it, pin and unpin, download the Markdown.
      const edition = listItems(await editionsApi.list(c, { pageSize: 20 })).find((e) => e.userId === auth.getState().user.id);
      expect(edition).toBeTruthy();
      const described = await editionsApi.get(c, edition.id);
      expect(described).toMatchObject({ edition: { id: edition.id }, topics: expect.any(Array), schedules: expect.any(Array) });
      const queued = await editionsApi.compose(c, edition.id);
      cleanup.push(() => c.delete(routes.briefing(queued.id)));
      let detail = null;
      for (let i = 0; i < 40; i += 1) {
        detail = await briefingsApi.get(c, queued.id);
        if (!['Queued', 'Composing'].includes(detail.briefing.status)) break;
        await new Promise((resolve) => setTimeout(resolve, 500));
      }
      expect(detail.briefing.status).toBe('Ready');
      expect(detail).toMatchObject({ sections: expect.any(Array), items: expect.any(Array), files: expect.any(Array) });
      expect(parseBriefing(detail.briefing.markdown, { skipTitle: detail.briefing.title }).blocks.length).toBeGreaterThan(0);
      await expect(briefingsApi.pin(c, queued.id, true)).resolves.toMatchObject({ pinned: true });
      await expect(briefingsApi.pin(c, queued.id, false)).resolves.toMatchObject({ pinned: false });
      const markdown = await c.get(routes.briefingDownload(queued.id), { format: 'md' });
      expect(String(markdown)).toContain('#');

      // The wizard by hand: start, a model step answers 502 without a key, add a dimension, write the brief, apply.
      const state = await refinementApi.start(c, { seedText: 'Mobile live test topic ' + Date.now() });
      const topicId = state.topic.id;
      cleanup.unshift(() => c.delete(routes.topic(topicId)));
      const proposed = await refinementApi.proposeDimensions(c, state.session.id).catch((e) => e);
      expect(isModelError(proposed) || Array.isArray(proposed.dimensions)).toBe(true);
      await topicsApi.addDimension(c, topicId, { name: 'Live dimension', weight: 'Normal' });
      await topicsApi.update(c, topicId, { brief: '## Brief\n\nLive test.' });
      const done = await refinementApi.complete(c, state.session.id);
      expect(done.topic.status).toBe('Active');

      // Schedules and a run.
      const body = { ownerType: 'Topic', ownerId: topicId, intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timesOfDay: ['07:00'], timeZone: 'UTC', enabled: false };
      await expect(schedulesApi.preview(c, { ...body, previewCount: 3 })).resolves.toMatchObject({ valid: true });
      const schedule = await schedulesApi.create(c, body);
      expect((await schedulesApi.list(c, { ownerType: 'Topic', ownerId: topicId })).items.map((x) => x.id)).toContain(schedule.id);
      const run = await topicsApi.run(c, topicId, { dryRun: true });
      await expect(runsApi.get(c, run.id)).resolves.toMatchObject({ id: run.id, topicId });
      expect(Array.isArray(await runsApi.stages(c, run.id))).toBe(true);

      // A one-time request is saved even when planning fails for want of a model.
      const text = 'Mobile live test request ' + Date.now();
      const created = await requestsApi.create(c, { requestText: text, deliverables: ['Report'], length: 'Short', skipQuestions: true }).catch((e) => e);
      const saved = listItems(await requestsApi.list(c, { pageSize: 5, sortBy: 'createdUtc', sortDirection: 'Descending' })).find((r) => r.requestText === text);
      if (saved) cleanup.unshift(() => requestsApi.remove(c, saved.id));
      expect(isModelError(created) || Boolean(created.request)).toBe(true);
      expect(saved).toBeTruthy();
    } finally {
      for (const step of cleanup) await step().catch(() => undefined);
      await auth.signOut();
    }
  });
});
