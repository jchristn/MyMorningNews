import React, { createContext, useContext, useEffect, useMemo, useRef, useSyncExternalStore } from 'react';
import { AppState } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Network from 'expo-network';
import { isNetworkError } from '@/api/errors';
import { feedbackApi } from '@/api/research';
import { routes } from '@/api/routes';
import { FeedbackQueue, queueOwner } from '@/feedback/FeedbackQueue';
import { ConnectivityMonitor } from '@/net/ConnectivityMonitor';
import { useAuth } from './AuthContext';

const OfflineContext = createContext(null);

const EMPTY = Object.freeze([]);

/**
 * Connection state and the offline feedback queue for the signed-in identity (plan section 35.4).
 *
 * - A `ConnectivityMonitor` follows the device's network (expo-network) and every exchange of the API client, and
 *   probes `GET /v1.0/api/client-config` (no auth) until an unreachable server answers again.
 * - A `FeedbackQueue` keeps feedback given without a connection in AsyncStorage and sends it in order when the
 *   status returns to `online`, when the app starts, and when it returns to the foreground.
 * - Signing out clears the queue. A session the server ended (`expired`) keeps it on the device for the same person
 *   signing back in; anyone else signing in discards it.
 *
 * Screens rendered without this provider (unit tests of one screen) get an always-online state and direct sends.
 */
export function OfflineProvider({ children, network = Network, storage = AsyncStorage, monitor: injectedMonitor, queue: injectedQueue }) {
  const { client, state } = useAuth();
  const monitor = useMemo(() => injectedMonitor || new ConnectivityMonitor({ network }), [injectedMonitor, network]);
  const queue = useMemo(() => injectedQueue || new FeedbackQueue({ storage, api: feedbackApi }), [injectedQueue, storage]);

  // First, so the effects below already probe and send with the current client.
  useEffect(() => {
    monitor.setProbe(() => client.send('GET', routes.clientConfig, {}));
    queue.setClient(client);
    return client.onNetwork((reached) => (reached ? monitor.reportSuccess() : monitor.reportFailure()));
  }, [client, monitor, queue]);

  useEffect(() => {
    monitor.start();
    const flushWhenOnline = (status) => {
      if (status === 'online') void queue.flush();
    };
    const unsubscribe = monitor.subscribe(flushWhenOnline);
    const subscription = AppState.addEventListener('change', (next) => {
      if (next === 'active') void monitor.check().then(flushWhenOnline);
    });
    return () => {
      unsubscribe();
      subscription.remove();
      monitor.stop();
    };
  }, [monitor, queue]);

  const owner = queueOwner(state);
  const previous = useRef({ status: state.status });
  useEffect(() => {
    const before = previous.current.status;
    previous.current = { status: state.status };
    if (state.status === 'loading') return;
    if (before === 'signedIn' && state.status === 'signedOut' && state.reason !== 'expired') {
      void queue.clear();
      return;
    }
    void queue.load(owner).then(() => {
      if (monitor.status() === 'online') void queue.flush();
    });
  }, [owner, state.status, state.reason, queue, monitor]);

  const subscribeStatus = useMemo(() => (listener) => monitor.subscribe(listener), [monitor]);
  const status = useSyncExternalStore(subscribeStatus, () => monitor.status());
  const subscribeQueue = useMemo(() => (listener) => queue.subscribe(listener), [queue]);
  const snapshot = useSyncExternalStore(subscribeQueue, () => queue.getSnapshot());

  const value = useMemo(() => ({ monitor, queue, status, items: snapshot.items, dropped: snapshot.dropped }), [monitor, queue, status, snapshot]);
  return <OfflineContext.Provider value={value}>{children}</OfflineContext.Provider>;
}

/**
 * `{ status, waiting, dropped, retry(), dismiss() }`: `status` is `online`, `offline`, or `unreachable`; `waiting`
 * counts queued feedback actions; `dropped` counts actions the server refused since the notice was last dismissed.
 */
export function useConnection() {
  const context = useContext(OfflineContext);
  return useMemo(() => {
    if (!context) return { status: 'online', waiting: 0, dropped: 0, retry: () => undefined, dismiss: () => undefined };
    return {
      status: context.status,
      waiting: context.items.length,
      dropped: context.dropped,
      retry: () => void context.monitor.check(),
      dismiss: () => context.queue.dismissNotice(),
    };
  }, [context]);
}

/** The queued rating or retraction for an item (the newest one), or null. */
export function usePendingFeedback(targetType, targetId) {
  const context = useContext(OfflineContext);
  const items = context ? context.items : EMPTY;
  return useMemo(() => {
    for (let i = items.length - 1; i >= 0; i -= 1) {
      const item = items[i];
      if (item.kind !== 'update' && item.targetType === targetType && item.targetId === targetId) return item;
    }
    return null;
  }, [items, targetType, targetId]);
}

/** `isQueued(feedback)`: true while any queued action concerns the saved feedback or the item it rates. */
export function useQueuedFeedback() {
  const context = useContext(OfflineContext);
  const items = context ? context.items : EMPTY;
  return useMemo(
    () => (feedback) => Boolean(feedback) && items.some((item) => (item.targetType === feedback.targetType && item.targetId === feedback.targetId) || (feedback.id && item.feedbackId === feedback.id)),
    [items],
  );
}

/** Call `listener({ item, outcome, feedback })` when a queued action is `sent` or `dropped`. */
export function useFeedbackSettled(listener) {
  const context = useContext(OfflineContext);
  const queue = context ? context.queue : null;
  const latest = useRef(listener);
  useEffect(() => {
    latest.current = listener;
  });
  useEffect(() => (queue ? queue.onSettled((event) => latest.current(event)) : undefined), [queue]);
}

/**
 * Every feedback write goes through here: `submit({ targetType, targetId, action, reason })`,
 * `update(feedback, reason)`, and `retract({ id, targetType, targetId })`. Each resolves `{ queued, feedback }`.
 *
 * Online, the request is sent at once and `feedback` is the server's answer; a server error is thrown as before.
 * Without a connection, after a network failure, or while earlier actions for the same item are still waiting (so
 * order holds), the action is queued instead and `queued` is true. A full queue throws `FeedbackQueueFullError`.
 */
export function useFeedbackActions() {
  const context = useContext(OfflineContext);
  const { client } = useAuth();
  return useMemo(() => {
    const monitor = context ? context.monitor : null;
    // Read at call time: the queue gains its owner a moment after sign-in.
    const active = () => (context && context.queue.owner ? context.queue : null);
    const queued = async (queue, enqueue, attempted) => {
      await enqueue(queue, attempted);
      if (monitor.status() === 'online') void queue.flush();
      return { queued: true, feedback: null };
    };
    const run = async (target, direct, enqueue) => {
      const queue = active();
      if (queue && (monitor.status() !== 'online' || queue.hasPending(target.targetType, target.targetId, target.id))) return queued(queue, enqueue, false);
      try {
        return { queued: false, feedback: await direct() };
      } catch (error) {
        if (!queue || !isNetworkError(error)) throw error;
        // The request may have arrived before the connection dropped; the queue checks before sending it again.
        return queued(queue, enqueue, true);
      }
    };
    return {
      submit: (fields) => run(fields, () => feedbackApi.submit(client, fields), (queue, attempted) => queue.enqueueSubmit({ ...fields, attempted })),
      update: (feedback, reason) =>
        run(feedback, () => feedbackApi.update(client, feedback.id, reason), (queue) => queue.enqueueUpdate({ feedbackId: feedback.id, targetType: feedback.targetType, targetId: feedback.targetId, reason })),
      retract: async (feedback) => {
        if (!feedback.id) {
          // Only ever queued: taking it back sends nothing.
          const queue = active();
          if (queue) await queue.enqueueRetract({ feedbackId: null, targetType: feedback.targetType, targetId: feedback.targetId });
          return { queued: false, feedback: null };
        }
        return run(feedback, () => feedbackApi.retract(client, feedback.id), (queue) => queue.enqueueRetract({ feedbackId: feedback.id, targetType: feedback.targetType, targetId: feedback.targetId }));
      },
    };
  }, [context, client]);
}
