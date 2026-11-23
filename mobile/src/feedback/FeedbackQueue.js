// The offline feedback queue (plan section 35.4): feedback given without a connection is kept on the device and sent
// in order when the connection returns. Host agnostic: storage, the feedback API, and the client are injected, so
// the tests drive it directly.
import { ApiError, isNetworkError, isSessionExpired } from '@/api/errors';
import { actionOf } from '@/lib/feedback';

/** Where the queue is kept (AsyncStorage): `{ owner, items }`. */
export const FEEDBACK_QUEUE_KEY = 'mymorningnews.feedbackQueue';

/** The most actions the queue holds. A new action is refused beyond it, and the control says so. */
export const DEFAULT_MAX_ITEMS = 100;

/** How often an action the server keeps failing on (5xx, 408, 429) is tried before it is dropped. */
export const DEFAULT_MAX_ATTEMPTS = 5;

/** The queue is full; the action was not kept. */
export class FeedbackQueueFullError extends Error {
  constructor() {
    super('The feedback queue is full.');
    this.name = 'FeedbackQueueFullError';
  }
}

/** The identity a queue belongs to, or null when nobody is signed in. Another identity never sends it. */
export function queueOwner(state) {
  if (!state || state.status !== 'signedIn' || !state.user || !state.user.id) return null;
  return [state.serverUrl || '', state.tenantId || '', state.user.id].join('|');
}

function sameTarget(item, targetType, targetId) {
  return item.targetType === targetType && item.targetId === targetId;
}

/** True when the server refused the action for good: a 4xx other than "try later" (408, 429). */
function isRejection(error) {
  return error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429;
}

/**
 * A durable, ordered, bounded queue of feedback actions.
 *
 * Items are `{ key, kind, targetType, targetId, action, reason, feedbackId, createdUtc, attempts }` with kind
 * `submit` (`POST /feedback`), `update` (`PUT /feedback/{id}`, the reason only), or `retract`
 * (`DELETE /feedback/{id}`).
 *
 * De-duplication happens twice. On the way in, a new action for an item replaces the queued one it supersedes (the
 * last rating wins, and retracting a rating that was never sent just removes it). On the way out, an action whose
 * earlier attempt may have reached the server (the connection dropped before the answer) is checked first: a submit
 * is skipped when the server's current rating already matches, and a retract that answers 404 is already done.
 *
 * `flush()` sends from the front and stops at the first network failure, so order holds. A 4xx rejection drops the
 * action and raises the quiet notice; a server that keeps failing drops it after `maxAttempts`.
 */
export class FeedbackQueue {
  constructor({ storage, api, getClient, now, maxItems, maxAttempts } = {}) {
    this.storage = storage;
    this.api = api;
    this.getClient = getClient || (() => null);
    this.now = now || (() => Date.now());
    this.maxItems = Math.max(1, maxItems || DEFAULT_MAX_ITEMS);
    this.maxAttempts = Math.max(1, maxAttempts || DEFAULT_MAX_ATTEMPTS);
    this.owner = null;
    this.items = [];
    this.dropped = 0;
    this.snapshot = { items: this.items, dropped: 0 };
    this.listeners = new Set();
    this.settledListeners = new Set();
    this.flushing = null;
    this.sequence = 0;
    this.epoch = 0;
  }

  /** Use this API client from now on (it changes when the server does). */
  setClient(client) {
    this.getClient = () => client;
  }

  // State ------------------------------------------------------------------------------------------------------

  /** `{ items, dropped }`; a new object whenever either changes (for useSyncExternalStore). */
  getSnapshot() {
    return this.snapshot;
  }

  subscribe(listener) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Call `listener({ item, outcome, feedback })` when an action is `sent` or `dropped`. */
  onSettled(listener) {
    this.settledListeners.add(listener);
    return () => this.settledListeners.delete(listener);
  }

  count() {
    return this.items.length;
  }

  /** The newest queued submit or retract for an item, or null. */
  pendingFor(targetType, targetId) {
    for (let i = this.items.length - 1; i >= 0; i -= 1) {
      const item = this.items[i];
      if (item.kind !== 'update' && sameTarget(item, targetType, targetId)) return item;
    }
    return null;
  }

  /** True when any queued action concerns the item (or the saved feedback with this ID). */
  hasPending(targetType, targetId, feedbackId) {
    return this.items.some((item) => sameTarget(item, targetType, targetId) || (feedbackId && item.feedbackId === feedbackId));
  }

  dismissNotice() {
    if (this.dropped === 0) return;
    this.dropped = 0;
    this.emit();
  }

  // Ownership --------------------------------------------------------------------------------------------------

  /**
   * Load the stored queue for `owner` (see queueOwner). A queue stored by another identity is discarded, and a
   * missing or corrupt one is empty. A null owner (signed out) keeps the stored queue untouched and sends nothing.
   */
  async load(owner) {
    const epoch = ++this.epoch;
    this.owner = owner || null;
    if (!this.owner) {
      this.setItems([]);
      return;
    }
    let stored = null;
    try {
      const text = await this.storage.getItem(FEEDBACK_QUEUE_KEY);
      stored = text ? JSON.parse(text) : null;
    } catch {
      stored = null;
    }
    if (epoch !== this.epoch) return;
    const mine = stored && stored.owner === this.owner && Array.isArray(stored.items);
    const items = mine ? stored.items.filter((item) => item && typeof item === 'object' && item.kind && item.targetId).slice(0, this.maxItems) : [];
    this.setItems(items);
    if (stored && !mine) await this.persist();
  }

  /** Forget everything, on the device too (sign-out). */
  async clear() {
    this.epoch += 1;
    this.owner = null;
    this.dropped = 0;
    this.setItems([]);
    try {
      await this.storage.removeItem(FEEDBACK_QUEUE_KEY);
    } catch {
      // Nothing more to do: the in-memory queue is already empty.
    }
  }

  // Adding -----------------------------------------------------------------------------------------------------

  /**
   * Queue a rating (with or without a reason). It replaces a queued rating or reason edit for the same item.
   * `attempted` says a direct send already failed without an answer, so it may have arrived: the first send from the
   * queue then checks the server's current rating first.
   */
  async enqueueSubmit({ targetType, targetId, action, reason, attempted }) {
    const kept = this.items.filter((item) => !(sameTarget(item, targetType, targetId) && item.kind !== 'retract'));
    return this.add(kept, { kind: 'submit', targetType, targetId, action: action || null, reason: reason || null, feedbackId: null }, attempted ? 1 : 0);
  }

  /** Queue a new reason for a saved feedback. It replaces a queued edit of the same feedback. */
  async enqueueUpdate({ feedbackId, targetType, targetId, reason }) {
    const kept = this.items.filter((item) => !(item.kind === 'update' && item.feedbackId === feedbackId));
    return this.add(kept, { kind: 'update', targetType, targetId, action: null, reason: reason || '', feedbackId });
  }

  /**
   * Queue retracting a saved feedback, and drop queued ratings and edits for the item. Without a `feedbackId` (the
   * rating was only ever queued) nothing is sent: the queued rating is just removed. Resolves the queued item or null.
   */
  async enqueueRetract({ feedbackId, targetType, targetId }) {
    const kept = this.items.filter((item) => !(sameTarget(item, targetType, targetId) && item.kind !== 'retract') && !(item.kind === 'update' && feedbackId && item.feedbackId === feedbackId));
    if (!feedbackId || kept.some((item) => item.kind === 'retract' && item.feedbackId === feedbackId)) {
      this.setItems(kept);
      await this.persist();
      return null;
    }
    return this.add(kept, { kind: 'retract', targetType, targetId, action: null, reason: null, feedbackId });
  }

  async add(kept, fields, attempts = 0) {
    if (!this.owner) throw new Error('Nobody is signed in.');
    if (kept.length >= this.maxItems) throw new FeedbackQueueFullError();
    this.sequence += 1;
    const item = { key: this.now() + '-' + this.sequence, ...fields, createdUtc: new Date(this.now()).toISOString(), attempts };
    this.setItems([...kept, item]);
    await this.persist();
    return item;
  }

  // Sending ----------------------------------------------------------------------------------------------------

  /** Send queued actions in order. One flush runs at a time; a second call joins the one in flight. */
  flush() {
    if (!this.flushing) {
      this.flushing = this.run().finally(() => {
        this.flushing = null;
      });
    }
    return this.flushing;
  }

  async run() {
    const epoch = this.epoch;
    while (this.owner && epoch === this.epoch && this.items.length > 0) {
      const client = this.getClient();
      if (!client) return;
      const item = this.items[0];
      const retried = item.attempts > 0;
      // Count the attempt before sending: if the app dies mid-request, the next start knows it may have arrived.
      this.replace(item, { ...item, attempts: item.attempts + 1 });
      await this.persist();
      let feedback = null;
      try {
        feedback = await this.send(client, item, retried);
      } catch (error) {
        if (epoch !== this.epoch) return;
        if (isNetworkError(error) || isSessionExpired(error)) return;
        if (isRejection(error) || item.attempts + 1 >= this.maxAttempts) {
          await this.settle(item.key, 'dropped', null);
          continue;
        }
        return;
      }
      if (epoch !== this.epoch) return;
      await this.settle(item.key, 'sent', feedback);
    }
  }

  async send(client, item, retried) {
    if (item.kind === 'update') return this.api.update(client, item.feedbackId, item.reason);
    if (item.kind === 'retract') {
      try {
        await this.api.retract(client, item.feedbackId);
      } catch (error) {
        // Already gone: an earlier attempt arrived, or it was retracted elsewhere.
        if (!(error instanceof ApiError && error.status === 404)) throw error;
      }
      return null;
    }
    if (retried) {
      const current = await this.currentOrNull(client, item);
      if (current && (!item.action || actionOf(current) === item.action) && (current.reason || '') === (item.reason || '')) return current;
    }
    const saved = await this.api.submit(client, { targetType: item.targetType, targetId: item.targetId, action: item.action || undefined, reason: item.reason || undefined });
    return saved && typeof saved === 'object' ? saved : null;
  }

  /** The server's current rating for a retried submit. Only a network failure stops the flush; anything else sends. */
  async currentOrNull(client, item) {
    try {
      return await this.api.current(client, item.targetType, item.targetId);
    } catch (error) {
      if (isNetworkError(error) || isSessionExpired(error)) throw error;
      return null;
    }
  }

  async settle(key, outcome, feedback) {
    const item = this.items.find((i) => i.key === key);
    if (!item) return;
    if (outcome === 'dropped') this.dropped += 1;
    this.setItems(this.items.filter((i) => i.key !== key));
    await this.persist();
    for (const listener of [...this.settledListeners]) {
      try {
        listener({ item, outcome, feedback });
      } catch {
        // A listener's failure never stops the queue.
      }
    }
  }

  // Internals --------------------------------------------------------------------------------------------------

  replace(item, next) {
    this.setItems(this.items.map((i) => (i.key === item.key ? next : i)));
  }

  setItems(items) {
    this.items = items;
    this.emit();
  }

  emit() {
    this.snapshot = { items: this.items, dropped: this.dropped };
    for (const listener of [...this.listeners]) listener();
  }

  async persist() {
    if (!this.owner) return;
    try {
      await this.storage.setItem(FEEDBACK_QUEUE_KEY, JSON.stringify({ owner: this.owner, items: this.items }));
    } catch {
      // The queue still works for this session; it only loses durability.
    }
  }
}
