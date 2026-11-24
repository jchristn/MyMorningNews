import AsyncStorage from '@react-native-async-storage/async-storage';
import { catalogApi } from '@/api/catalog';
import { briefingsApi } from '@/api/research';
import { isNotAvailable } from '@/api/errors';

const KEY = 'mymorningnews.progress';
/** Positions closer than this to the end count as finished, and resume from the start next time. */
export const COMPLETE_MARGIN_SECONDS = 15;
/** Keep at most this many resume positions locally (oldest dropped first). */
export const MAX_ENTRIES = 200;

/** Resume position persisted locally per track, and synced to the server for briefings. */
export function createProgressStore({ storage = AsyncStorage, now = () => Date.now() } = {}) {
  let cache = null;
  let remoteSupported = true;

  async function read() {
    if (cache) return cache;
    try {
      const text = await storage.getItem(KEY);
      cache = text ? JSON.parse(text) || {} : {};
    } catch {
      cache = {};
    }
    return cache;
  }

  async function write() {
    const entries = Object.entries(cache || {}).sort((a, b) => (b[1].updated || 0) - (a[1].updated || 0));
    cache = Object.fromEntries(entries.slice(0, MAX_ENTRIES));
    try {
      await storage.setItem(KEY, JSON.stringify(cache));
    } catch {
      // Losing a resume position is acceptable.
    }
  }

  return {
    /** Resume position in seconds (0 when finished or unknown). */
    async get(trackId) {
      const all = await read();
      const entry = all[trackId];
      return entry && !entry.completed ? entry.position : 0;
    },

    async save(trackId, position, duration) {
      if (!trackId) return;
      const all = await read();
      const completed = duration > 0 && position >= duration - COMPLETE_MARGIN_SECONDS;
      all[trackId] = { position: Math.max(0, Math.floor(position)), completed, updated: now() };
      await write();
    },

    /**
     * Sync a briefing's position to `PUT /v1.0/api/briefings/{id}/progress` (plan section 35.5). A 404 means the
     * server does not have the route yet: stop trying for this session. Failures never reach the player.
     */
    async sync(client, briefingId, position, duration, catalogPublicationId) {
      if (!remoteSupported || !client || !briefingId) return false;
      const completed = duration > 0 && position >= duration - COMPLETE_MARGIN_SECONDS;
      try {
        const body = { positionSeconds: Math.floor(position), completed };
        if (catalogPublicationId) await catalogApi.saveProgress(client, catalogPublicationId, briefingId, body);
        else await briefingsApi.saveProgress(client, briefingId, body);
        return true;
      } catch (error) {
        if (!catalogPublicationId && isNotAvailable(error)) remoteSupported = false;
        return false;
      }
    },

    remoteSupported: () => remoteSupported,
  };
}
