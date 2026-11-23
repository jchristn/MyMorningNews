import React from 'react';
import { AppState, Linking, Text } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { ApiClient } from '@/api/client';
import { feedbackApi } from '@/api/research';
import { ConnectionBanner } from '@/components/ConnectionBanner';
import { FeedbackBar } from '@/components/FeedbackBar';
import { UpdateGate } from '@/components/UpdateGate';
import { FEEDBACK_QUEUE_KEY, FeedbackQueue, FeedbackQueueFullError, queueOwner } from '@/feedback/FeedbackQueue';
import { compareVersions, isUpdateRequired, parseVersion, storeUrl } from '@/lib/version';
import { ConnectivityMonitor } from '@/net/ConnectivityMonitor';
import { FeedbackHistory } from '@/screens/insights/FeedbackHistory';
import { OfflineProvider } from '@/state/OfflineContext';
import { fakeServer } from './fakeServer';
import { SERVER, page, renderSignedIn } from './renderApp';

const CONFIG = { productName: 'My Morning News', serverVersion: '0.1.0-alpha', links: {}, copyright: '(c) 2026 Lattice AI LLC' };
const OWNER = SERVER + '|default|usr_1';
const down = () => {
  throw new Error('Network request failed');
};

function memoryStorage() {
  const map = new Map();
  return {
    map,
    getItem: async (key) => (map.has(key) ? map.get(key) : null),
    setItem: async (key, value) => void map.set(key, value),
    removeItem: async (key) => void map.delete(key),
  };
}

/** A queue over the real feedback API and API client, against a scripted server. */
function queueOver(handlers, options = {}) {
  const server = fakeServer(handlers);
  const client = new ApiClient({ baseUrl: SERVER, fetchImpl: server.fetchImpl });
  const storage = options.storage || memoryStorage();
  const queue = new FeedbackQueue({ storage, api: feedbackApi, getClient: () => client, ...options });
  return { server, client, storage, queue };
}

const saved = (call, extra) => ({ body: { id: 'fbk_' + call.body.targetId, targetType: call.body.targetType, targetId: call.body.targetId, relevance: 'Relevant', reason: call.body.reason || null, isCurrent: true, ...extra } });

describe('version comparison', () => {
  it('parses versions, with a prerelease and build metadata', () => {
    expect(parseVersion('0.1.0-alpha')).toEqual({ numbers: [0, 1, 0], prerelease: ['alpha'] });
    expect(parseVersion(' v1.2 ')).toEqual({ numbers: [1, 2], prerelease: [] });
    expect(parseVersion('1.0.0-rc.1+build.5')).toEqual({ numbers: [1, 0, 0], prerelease: ['rc', '1'] });
    for (const bad of ['', 'latest', '1.x', '1..2', '1.0.0-', '-1.0.0', null, undefined, 10, {}]) expect(parseVersion(bad)).toBeNull();
  });

  it.each([
    ['0.1.0', '0.1.0', 0],
    ['0.1.0', '0.2.0', -1],
    ['0.10.0', '0.9.9', 1],
    ['1.0.0', '0.99.99', 1],
    ['1.2', '1.2.0', 0],
    ['1.2.1', '1.2', 1],
    ['0.1.0-alpha', '0.1.0', -1],
    ['0.1.0', '0.1.0-alpha', 1],
    ['0.1.0-alpha', '0.1.0-alpha', 0],
    ['0.1.0-alpha', '0.1.0-alpha.1', -1],
    ['0.1.0-alpha.2', '0.1.0-alpha.10', -1],
    ['0.1.0-alpha.1', '0.1.0-beta', -1],
    ['0.1.0-beta', '0.1.0-rc.1', -1],
    ['0.1.0-1', '0.1.0-alpha', -1],
    ['0.2.0-alpha', '0.1.0', 1],
    ['1.0.0+build.1', '1.0.0+build.2', 0],
  ])('compares %s with %s as %i', (a, b, expected) => {
    expect(compareVersions(a, b)).toBe(expected);
    expect(compareVersions(b, a)).toBe(expected === 0 ? 0 : -expected);
  });

  it('asks for an update only when both versions are readable and the installed one is older', () => {
    expect(isUpdateRequired('0.1.0', '0.2.0')).toBe(true);
    expect(isUpdateRequired('0.1.0-alpha', '0.1.0')).toBe(true);
    expect(isUpdateRequired('0.1.0', '0.1.0')).toBe(false);
    expect(isUpdateRequired('0.1.0', '0.1.0-alpha')).toBe(false);
    expect(isUpdateRequired('0.3.0', '0.2.0')).toBe(false);
    for (const minimum of [undefined, null, '', 'soon', '2.x', 3, {}]) expect(isUpdateRequired('0.1.0', minimum)).toBe(false);
    for (const installed of [undefined, null, '', 'dev']) expect(isUpdateRequired(installed, '9.0.0')).toBe(false);
    expect(compareVersions('dev', '1.0.0')).toBeNull();
  });

  it('finds the store listing, or none', () => {
    expect(storeUrl({ platform: 'ios', links: { appStore: 'https://apps.example.com/app/id1' } })).toBe('https://apps.example.com/app/id1');
    expect(storeUrl({ platform: 'ios', links: { playStore: 'https://play.example.com' }, androidPackage: 'ai.latticetech.mymorningnews' })).toBeNull();
    expect(storeUrl({ platform: 'ios', links: { appStore: 'javascript:alert(1)' } })).toBeNull();
    expect(storeUrl({ platform: 'android', links: {}, androidPackage: 'ai.latticetech.mymorningnews' })).toBe('https://play.google.com/store/apps/details?id=ai.latticetech.mymorningnews');
    expect(storeUrl({ platform: 'android', links: { playStore: 'market://details?id=x.y' }, androidPackage: 'a.b' })).toBe('market://details?id=x.y');
    expect(storeUrl({ platform: 'android', links: null })).toBeNull();
    expect(storeUrl({ platform: 'web', links: { appStore: 'https://a.example.com' } })).toBeNull();
  });
});

describe('update required gate', () => {
  const App = () => <Text testID="app">app</Text>;
  const gate = (props) => (
    <UpdateGate installedVersion="0.1.0" platform="ios" {...props}>
      <App />
    </UpdateGate>
  );
  const configWith = (extra) => ({ 'GET /v1.0/api/client-config': () => ({ body: { ...CONFIG, ...extra } }) });

  it('replaces the app when the installed version is older, with a store button and no way out', async () => {
    const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
    await renderSignedIn(gate(), configWith({ minimumAppVersion: '0.2.0', links: { appStore: 'https://apps.example.com/app/id1' } }));
    await waitFor(() => expect(screen.getByTestId('update-required')).toBeTruthy());
    expect(screen.queryByTestId('app')).toBeNull();
    expect(screen.getByText('Update required')).toBeTruthy();
    expect(screen.getByTestId('update-versions').props.children).toBe('Installed version: 0.1.0. Required version: 0.2.0 or later.');
    expect(screen.queryByText('Cancel')).toBeNull();
    expect(screen.queryByText('Close')).toBeNull();
    // The test server is not the hosted service, so the way back to it is offered.
    expect(screen.getByTestId('update-hosted-service')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('update-open-store'));
    expect(open).toHaveBeenCalledWith('https://apps.example.com/app/id1');
    expect(screen.getByTestId('update-required')).toBeTruthy();
    open.mockRestore();
  });

  it('shows the message without a button when no store listing is known', async () => {
    await renderSignedIn(gate(), configWith({ minimumAppVersion: '1.0.0' }));
    await waitFor(() => expect(screen.getByTestId('update-required')).toBeTruthy());
    expect(screen.queryByTestId('update-open-store')).toBeNull();
    expect(screen.getByTestId('update-no-store')).toBeTruthy();
  });

  it('treats a prerelease as older than its release', async () => {
    await renderSignedIn(gate({ installedVersion: '0.1.0-alpha' }), configWith({ minimumAppVersion: '0.1.0' }));
    await waitFor(() => expect(screen.getByTestId('update-required')).toBeTruthy());
  });

  it.each([
    ['the same version', { minimumAppVersion: '0.1.0' }],
    ['an older minimum', { minimumAppVersion: '0.0.9' }],
    ['a prerelease of the installed version', { minimumAppVersion: '0.1.0-alpha' }],
    ['no minimum', {}],
    ['an empty minimum', { minimumAppVersion: '' }],
    ['an unreadable minimum', { minimumAppVersion: 'newest' }],
    ['a minimum that is not text', { minimumAppVersion: 2 }],
  ])('lets the app through with %s', async (_, extra) => {
    const { server } = await renderSignedIn(gate(), configWith(extra));
    await waitFor(() => expect(server.count('GET /v1.0/api/client-config')).toBeGreaterThan(0));
    await waitFor(() => expect(screen.getByTestId('app')).toBeTruthy());
    expect(screen.queryByTestId('update-required')).toBeNull();
  });

  it('never blocks when client config is missing or the installed version is unknown', async () => {
    const first = await renderSignedIn(gate(), { 'GET /v1.0/api/client-config': () => ({ status: 404, body: { error: 'NotFound' } }) });
    await waitFor(() => expect(first.server.count('GET /v1.0/api/client-config')).toBeGreaterThan(0));
    expect(screen.getByTestId('app')).toBeTruthy();
    first.unmount();
    const second = await renderSignedIn(gate({ installedVersion: '' }), configWith({ minimumAppVersion: '9.0.0' }));
    await waitFor(() => expect(second.server.count('GET /v1.0/api/client-config')).toBeGreaterThan(0));
    expect(screen.getByTestId('app')).toBeTruthy();
  });

  it('checks again on returning to the foreground, and keeps the block when the server cannot be reached', async () => {
    const listeners = [];
    // Replaced by hand: restoring a spy over the React Native mock would leave it returning nothing.
    const original = AppState.addEventListener;
    AppState.addEventListener = (event, fn) => {
      listeners.push(fn);
      return { remove: () => listeners.splice(listeners.indexOf(fn), 1) };
    };
    let answer = () => ({ body: { ...CONFIG, minimumAppVersion: '0.1.0' } });
    const { server } = await renderSignedIn(gate(), { 'GET /v1.0/api/client-config': () => answer() });
    await waitFor(() => expect(screen.getByTestId('app')).toBeTruthy());
    const before = server.count('GET /v1.0/api/client-config');

    answer = () => ({ body: { ...CONFIG, minimumAppVersion: '0.5.0' } });
    await act(async () => listeners.forEach((fn) => fn('background')));
    expect(server.count('GET /v1.0/api/client-config')).toBe(before);
    await act(async () => listeners.forEach((fn) => fn('active')));
    await waitFor(() => expect(screen.getByTestId('update-required')).toBeTruthy());

    answer = down;
    await act(async () => listeners.forEach((fn) => fn('active')));
    await waitFor(() => expect(server.count('GET /v1.0/api/client-config')).toBe(before + 2));
    expect(screen.getByTestId('update-required')).toBeTruthy();

    answer = () => ({ body: { ...CONFIG, minimumAppVersion: '0.1.0' } });
    await act(async () => listeners.forEach((fn) => fn('active')));
    await waitFor(() => expect(screen.getByTestId('app')).toBeTruthy());
    AppState.addEventListener = original;
  });
});

describe('connectivity monitor', () => {
  function monitorWith(probe) {
    const timers = [];
    let listener = null;
    const network = {
      getNetworkStateAsync: async () => ({ isConnected: true }),
      addNetworkStateListener: (fn) => {
        listener = fn;
        return { remove: jest.fn() };
      },
    };
    const monitor = new ConnectivityMonitor({ network, probe, setTimer: (fn, ms) => timers.push({ fn, ms }) - 1, clearTimer: (id) => (timers[id] = null), retryDelaysMs: [10, 20] });
    const seen = [];
    monitor.subscribe((status) => seen.push(status));
    monitor.start();
    return { monitor, timers, seen, setNetwork: (state) => listener(state) };
  }

  it('reports offline, probes on reconnect, and clears itself', async () => {
    const probe = jest.fn(async () => undefined);
    const { monitor, seen, setNetwork } = monitorWith(probe);
    expect(monitor.status()).toBe('online');
    setNetwork({ isConnected: false, isInternetReachable: false });
    expect(monitor.status()).toBe('offline');
    // A local server works without internet access: only a missing connection counts as offline.
    setNetwork({ isConnected: true, isInternetReachable: false });
    await Promise.resolve();
    await Promise.resolve();
    expect(probe).toHaveBeenCalledTimes(1);
    expect(monitor.status()).toBe('online');
    expect(seen).toEqual(['offline', 'online']);
  });

  it('marks the server unreachable after a failed request and retries with a growing delay until it answers', async () => {
    let up = false;
    const probe = jest.fn(async () => {
      if (!up) throw new Error('down');
    });
    const { monitor, timers, seen } = monitorWith(probe);
    monitor.reportFailure();
    expect(monitor.status()).toBe('unreachable');
    expect(timers.map((t) => t && t.ms)).toEqual([10]);
    await timers[0].fn();
    expect(timers[1].ms).toBe(20);
    await timers[1].fn();
    expect(timers[2].ms).toBe(20);
    up = true;
    await timers[2].fn();
    expect(monitor.status()).toBe('online');
    expect(timers).toHaveLength(3);
    expect(seen).toEqual(['unreachable', 'online']);
    monitor.reportFailure();
    monitor.reportSuccess();
    expect(timers[3]).toBeNull();
    monitor.stop();
  });
});

describe('offline feedback queue', () => {
  it('names its owner only for a signed-in identity', () => {
    expect(queueOwner({ status: 'signedIn', serverUrl: SERVER, tenantId: 'default', user: { id: 'usr_1' } })).toBe(OWNER);
    expect(queueOwner({ status: 'signedOut', serverUrl: SERVER, tenantId: null, user: null })).toBeNull();
    expect(queueOwner(null)).toBeNull();
  });

  it('keeps actions on the device and sends them in order when the server answers again', async () => {
    let online = false;
    const { server, storage, queue } = queueOver({ 'POST /v1.0/api/feedback': (call) => (online ? saved(call) : down()) });
    await queue.load(OWNER);
    for (const id of ['fnd_1', 'fnd_2', 'fnd_3']) await queue.enqueueSubmit({ targetType: 'Finding', targetId: id, action: 'relevant' });
    expect(queue.count()).toBe(3);
    expect(JSON.parse(storage.map.get(FEEDBACK_QUEUE_KEY)).items.map((i) => i.targetId)).toEqual(['fnd_1', 'fnd_2', 'fnd_3']);

    await queue.flush();
    // The first failure stops the flush: nothing behind it jumps the line, and nothing is lost.
    expect(server.count('POST /v1.0/api/feedback')).toBe(1);
    expect(queue.count()).toBe(3);

    // A restart: a new queue over the same storage has the same actions.
    const restarted = new FeedbackQueue({ storage, api: feedbackApi, getClient: () => new ApiClient({ baseUrl: SERVER, fetchImpl: server.fetchImpl }) });
    await restarted.load(OWNER);
    expect(restarted.count()).toBe(3);

    online = true;
    const settled = [];
    restarted.onSettled((event) => settled.push(event.outcome + ' ' + event.item.targetId));
    await restarted.flush();
    const posts = server.calls.filter((c) => c.key === 'POST /v1.0/api/feedback').slice(1);
    // The first action was tried before, so it is checked against the server (404 here) and then sent.
    expect(posts.map((c) => c.body.targetId)).toEqual(['fnd_1', 'fnd_2', 'fnd_3']);
    expect(posts[0].body).toEqual({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant', channel: 'Mobile' });
    expect(settled).toEqual(['sent fnd_1', 'sent fnd_2', 'sent fnd_3']);
    expect(restarted.count()).toBe(0);
    expect(JSON.parse(storage.map.get(FEEDBACK_QUEUE_KEY)).items).toEqual([]);
  });

  it('does not apply an action twice when an earlier attempt reached the server', async () => {
    let attempts = 0;
    const { server, queue } = queueOver({
      // The first request arrives, but the connection drops before the answer.
      'POST /v1.0/api/feedback': () => {
        attempts += 1;
        return down();
      },
      'GET /v1.0/api/feedback': () => page([{ id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'NotRelevant', reason: 'Old news', isCurrent: true, createdUtc: '2026-11-20T08:00:00Z' }]),
    });
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Old news' });
    await queue.flush();
    expect(attempts).toBe(1);
    expect(queue.count()).toBe(1);
    const settled = [];
    queue.onSettled((event) => settled.push(event));
    await queue.flush();
    // The server already has this rating, so it is not posted again.
    expect(attempts).toBe(1);
    expect(server.count('GET /v1.0/api/feedback')).toBe(1);
    expect(queue.count()).toBe(0);
    expect(settled).toHaveLength(1);
    expect(settled[0].outcome).toBe('sent');
    expect(settled[0].feedback.id).toBe('fbk_1');
  });

  it('sends a retried action when the server does not have it, and checks first for a direct send that got no answer', async () => {
    const { server, queue } = queueOver({
      'POST /v1.0/api/feedback': (call) => saved(call),
      'GET /v1.0/api/feedback': () => page([{ id: 'fbk_0', targetType: 'Finding', targetId: 'fnd_1', relevance: 'Relevant', isCurrent: true }]),
    });
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', attempted: true });
    await queue.flush();
    expect(server.calls.map((c) => c.key)).toEqual(['GET /v1.0/api/feedback', 'POST /v1.0/api/feedback']);
    expect(queue.count()).toBe(0);
  });

  it('keeps only the latest queued rating for an item, and a retraction of an unsent rating sends nothing', async () => {
    const { server, queue } = queueOver({ 'POST /v1.0/api/feedback': (call) => saved(call), 'DELETE /v1.0/api/feedback/fbk_9': () => ({ status: 404, body: { error: 'NotFound', requestId: 'rqi_1' } }) });
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant' });
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_2', action: 'relevant' });
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Off topic' });
    expect(queue.getSnapshot().items.map((i) => i.targetId + ' ' + i.action)).toEqual(['fnd_2 relevant', 'fnd_1 not-relevant']);
    expect(queue.pendingFor('Finding', 'fnd_1').reason).toBe('Off topic');

    expect(await queue.enqueueRetract({ feedbackId: null, targetType: 'Finding', targetId: 'fnd_1' })).toBeNull();
    expect(queue.pendingFor('Finding', 'fnd_1')).toBeNull();

    // Retracting a saved rating replaces the queued rating for the item, and is queued once.
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_3', action: 'somewhat' });
    await queue.enqueueRetract({ feedbackId: 'fbk_9', targetType: 'Finding', targetId: 'fnd_3' });
    await queue.enqueueRetract({ feedbackId: 'fbk_9', targetType: 'Finding', targetId: 'fnd_3' });
    await queue.enqueueUpdate({ feedbackId: 'fbk_4', targetType: 'Finding', targetId: 'fnd_4', reason: 'First' });
    await queue.enqueueUpdate({ feedbackId: 'fbk_4', targetType: 'Finding', targetId: 'fnd_4', reason: 'Second' });
    expect(queue.getSnapshot().items.map((i) => i.kind + ' ' + i.targetId + ' ' + (i.reason || ''))).toEqual(['submit fnd_2 ', 'retract fnd_3 ', 'update fnd_4 Second']);

    const settled = [];
    queue.onSettled((event) => settled.push(event.outcome + ' ' + event.item.kind));
    await queue.flush();
    // A retraction that answers 404 is already done, not an error. The edit has no route here: it is dropped.
    expect(settled).toEqual(['sent submit', 'sent retract', 'dropped update']);
    expect(server.calls.map((c) => c.key)).toEqual(['POST /v1.0/api/feedback', 'DELETE /v1.0/api/feedback/fbk_9', 'PUT /v1.0/api/feedback/fbk_4']);
  });

  it('drops an action the server refuses, counts it for the notice, and goes on', async () => {
    const { server, queue } = queueOver({
      'POST /v1.0/api/feedback': (call) => (call.body.targetId === 'fnd_gone' ? { status: 400, body: { error: 'ValidationFailed', message: 'The target does not exist.', requestId: 'rqi_1' } } : saved(call)),
    });
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_gone', action: 'relevant' });
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_2', action: 'relevant' });
    const settled = [];
    queue.onSettled((event) => settled.push(event.outcome + ' ' + event.item.targetId));
    await queue.flush();
    expect(settled).toEqual(['dropped fnd_gone', 'sent fnd_2']);
    expect(queue.count()).toBe(0);
    expect(queue.getSnapshot().dropped).toBe(1);
    await queue.flush();
    expect(server.count('POST /v1.0/api/feedback')).toBe(2);
    queue.dismissNotice();
    expect(queue.getSnapshot().dropped).toBe(0);
  });

  it('retries a failing server a bounded number of times, then drops the action', async () => {
    const { server, queue } = queueOver({ 'POST /v1.0/api/feedback': () => ({ status: 500, body: { error: 'InternalError', requestId: 'rqi_1' } }), 'GET /v1.0/api/feedback': () => page([]) }, { maxAttempts: 3 });
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant' });
    await queue.flush();
    await queue.flush();
    expect(queue.count()).toBe(1);
    await queue.flush();
    expect(queue.count()).toBe(0);
    expect(queue.getSnapshot().dropped).toBe(1);
    expect(server.count('POST /v1.0/api/feedback')).toBe(3);
  });

  it('is bounded, belongs to one identity, and is emptied by clear()', async () => {
    const { storage, queue } = queueOver({}, { maxItems: 2 });
    await expect(queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_0', action: 'relevant' })).rejects.toThrow('Nobody is signed in.');
    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant' });
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_2', action: 'relevant' });
    await expect(queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_3', action: 'relevant' })).rejects.toBeInstanceOf(FeedbackQueueFullError);
    // Changing a queued rating is not a new entry.
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_2', action: 'somewhat' });
    expect(queue.count()).toBe(2);

    // Signed out (an expired session): nothing in memory, the stored queue stays for the same person.
    await queue.load(null);
    expect(queue.count()).toBe(0);
    expect(JSON.parse(storage.map.get(FEEDBACK_QUEUE_KEY)).items).toHaveLength(2);
    await queue.load(OWNER);
    expect(queue.count()).toBe(2);

    // Somebody else signs in: the stored queue is discarded.
    await queue.load(SERVER + '|default|usr_2');
    expect(queue.count()).toBe(0);
    expect(JSON.parse(storage.map.get(FEEDBACK_QUEUE_KEY))).toEqual({ owner: SERVER + '|default|usr_2', items: [] });

    await queue.load(OWNER);
    await queue.enqueueSubmit({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant' });
    await queue.clear();
    expect(queue.count()).toBe(0);
    expect(storage.map.has(FEEDBACK_QUEUE_KEY)).toBe(false);

    storage.map.set(FEEDBACK_QUEUE_KEY, '{not json');
    await queue.load(OWNER);
    expect(queue.count()).toBe(0);
  });
});

describe('offline feedback in the app', () => {
  /** A network module the test switches, as expo-network reports it. */
  function fakeNetwork(isConnected) {
    const network = {
      state: { isConnected },
      listener: null,
      getNetworkStateAsync: async () => network.state,
      addNetworkStateListener: (fn) => {
        network.listener = fn;
        return { remove: jest.fn() };
      },
      set: (connected) => {
        network.state = { isConnected: connected };
        if (network.listener) network.listener(network.state);
      },
    };
    return network;
  }

  const stored = async () => JSON.parse((await AsyncStorage.getItem(FEEDBACK_QUEUE_KEY)) || 'null');
  const shell = (network, children) => (
    <OfflineProvider network={network}>
      {children}
      <ConnectionBanner />
    </OfflineProvider>
  );
  const bar = <FeedbackBar targetType="Finding" targetId="fnd_1" title="A finding" />;

  beforeEach(async () => {
    await AsyncStorage.clear();
  });

  it('shows nothing while online with an empty queue', async () => {
    await renderSignedIn(shell(fakeNetwork(true), bar), { 'GET /v1.0/api/feedback': () => page([]) });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    expect(screen.queryByTestId('connection-banner')).toBeNull();
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
  });

  it('queues a rating given offline, shows it as waiting, and sends it when the connection returns', async () => {
    const network = fakeNetwork(false);
    const { server } = await renderSignedIn(shell(network, bar), { 'GET /v1.0/api/feedback': () => page([]), 'POST /v1.0/api/feedback': (call) => saved(call) });
    await waitFor(() => expect(screen.getByTestId('connection-banner')).toBeTruthy());
    expect(screen.getByTestId('connection-line-0').props.children).toBe('You are offline');
    expect(screen.queryByTestId('connection-retry')).toBeNull();
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());

    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-queued')).toBeTruthy());
    expect(screen.getByText('Will send when online')).toBeTruthy();
    expect(screen.getByTestId('feedback-bar-relevant').props.accessibilityState.selected).toBe(true);
    expect(server.count('POST /v1.0/api/feedback')).toBe(0);
    expect(screen.getByTestId('connection-line-1').props.children).toBe('Ratings waiting to send: 1');
    expect(screen.getByTestId('connection-text').props.accessibilityLabel).toBe('You are offline. Ratings waiting to send: 1');
    expect(screen.getByTestId('connection-text').props.accessibilityLiveRegion).toBe('polite');
    expect((await stored()).items).toHaveLength(1);

    // Changing the rating while offline replaces the queued one.
    await fireEvent.press(screen.getByTestId('feedback-bar-not-relevant'));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-not-relevant').props.accessibilityState.selected).toBe(true));
    expect(screen.getByTestId('connection-line-1').props.children).toBe('Ratings waiting to send: 1');

    await act(async () => network.set(true));
    await waitFor(() => expect(server.count('POST /v1.0/api/feedback')).toBe(1));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/feedback').body).toEqual({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', channel: 'Mobile' });
    await waitFor(() => expect(screen.queryByTestId('connection-banner')).toBeNull());
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
    expect((await stored()).items).toEqual([]);
  });

  it('queues a rating when the server cannot be reached, and Try again clears the banner on recovery', async () => {
    let up = false;
    const { server } = await renderSignedIn(shell(fakeNetwork(true), bar), {
      'GET /v1.0/api/feedback': () => page([]),
      'POST /v1.0/api/feedback': (call) => (up ? saved(call) : down()),
      'GET /v1.0/api/client-config': () => (up || server.count('POST /v1.0/api/feedback') === 0 ? { body: CONFIG } : down()),
    });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-queued')).toBeTruthy());
    expect(screen.getByTestId('connection-line-0').props.children).toBe('Cannot reach the server');
    expect(screen.getByTestId('connection-line-1').props.children).toBe('Ratings waiting to send: 1');
    expect(server.count('POST /v1.0/api/feedback')).toBe(1);
    // The direct send got no answer, so the queued copy is marked as possibly delivered.
    expect((await stored()).items[0].attempts).toBe(1);

    await fireEvent.press(screen.getByTestId('connection-retry'));
    expect(screen.getByTestId('connection-banner')).toBeTruthy();

    up = true;
    await fireEvent.press(screen.getByTestId('connection-retry'));
    await waitFor(() => expect(screen.queryByTestId('connection-banner')).toBeNull());
    expect(server.count('POST /v1.0/api/feedback')).toBe(2);
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
    expect(screen.getByTestId('feedback-bar-relevant').props.accessibilityState.selected).toBe(true);
  });

  it('shows a server error as before while online, without queueing', async () => {
    await renderSignedIn(shell(fakeNetwork(true), bar), { 'GET /v1.0/api/feedback': () => page([]), 'POST /v1.0/api/feedback': () => ({ status: 400, body: { error: 'ValidationFailed', message: 'That did not work.', requestId: 'rqi_1' } }) });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByText('That did not work.')).toBeTruthy());
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
    expect(screen.queryByTestId('connection-banner')).toBeNull();
  });

  it('drops a queued rating the server refuses with a quiet notice that can be dismissed', async () => {
    const network = fakeNetwork(false);
    const { server } = await renderSignedIn(shell(network, bar), { 'GET /v1.0/api/feedback': () => page([]), 'POST /v1.0/api/feedback': () => ({ status: 409, body: { error: 'Conflict', message: 'No.', requestId: 'rqi_1' } }) });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-queued')).toBeTruthy());
    await act(async () => network.set(true));
    await waitFor(() => expect(screen.getByTestId('connection-dismiss')).toBeTruthy());
    expect(screen.getByTestId('connection-line-0').props.children).toBe('Some ratings could not be saved because the server did not accept them.');
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
    expect(screen.getByTestId('feedback-bar-relevant').props.accessibilityState.selected).toBe(false);
    expect(server.count('POST /v1.0/api/feedback')).toBe(1);
    await fireEvent.press(screen.getByTestId('connection-dismiss'));
    expect(screen.queryByTestId('connection-banner')).toBeNull();
    expect(server.count('POST /v1.0/api/feedback')).toBe(1);
  });

  it('retracts and edits from the feedback history through the queue', async () => {
    const network = fakeNetwork(false);
    const item = { id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'Relevant', reason: 'Good', channel: 'Mobile', isCurrent: true, createdUtc: '2026-11-20T08:00:00Z' };
    const { server } = await renderSignedIn(shell(network, <FeedbackHistory />), {
      'GET /v1.0/api/feedback': () => page([item]),
      'PUT /v1.0/api/feedback/fbk_1': (call) => ({ body: { ...item, reason: call.body.reason } }),
    });
    await waitFor(() => expect(screen.getByTestId('history-fbk_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('history-edit-fbk_1'));
    await fireEvent.changeText(screen.getByTestId('why-reason'), 'Better reason');
    await fireEvent.press(screen.getByTestId('why-save'));
    await waitFor(() => expect(screen.getByTestId('history-queued-fbk_1')).toBeTruthy());
    expect(server.count('PUT /v1.0/api/feedback/fbk_1')).toBe(0);
    expect((await stored()).items.map((i) => i.kind + ' ' + i.reason)).toEqual(['update Better reason']);
    await act(async () => network.set(true));
    await waitFor(() => expect(server.count('PUT /v1.0/api/feedback/fbk_1')).toBe(1));
    await waitFor(() => expect(screen.queryByTestId('history-queued-fbk_1')).toBeNull());
  });

  it('clears the queue on sign-out, on the device too', async () => {
    const { controller } = await renderSignedIn(shell(fakeNetwork(false), bar), { 'GET /v1.0/api/feedback': () => page([]), 'DELETE /v1.0/token': () => ({ status: 204 }) });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-queued')).toBeTruthy());
    expect((await stored()).owner).toBe(OWNER);
    await act(async () => {
      await controller.signOut();
    });
    await waitFor(async () => expect(await AsyncStorage.getItem(FEEDBACK_QUEUE_KEY)).toBeNull());
    expect(screen.queryByTestId('feedback-bar-queued')).toBeNull();
    expect(screen.getByTestId('connection-line-0').props.children).toBe('You are offline');
    expect(screen.queryByTestId('connection-line-1')).toBeNull();
  });
});
