import AsyncStorage from '@react-native-async-storage/async-storage';
import { Directory, File, Paths } from 'expo-file-system';

const INDEX_KEY = 'mymorningnews.downloads';
const FOLDER = 'audio';

function safeName(id) {
  return String(id).replace(/[^A-Za-z0-9._-]/g, '_') + '.mp3';
}

/**
 * Offline audio (plan section 35.3): episodes downloaded into the app's documents folder, with an index of what is
 * stored so Account and Listen can show sizes and free space. File system access is injected for tests.
 */
export function createDownloads({ fs = { Directory, File, Paths }, storage = AsyncStorage, now = () => Date.now() } = {}) {
  const folder = () => {
    const dir = new fs.Directory(fs.Paths.document, FOLDER);
    if (!dir.exists) dir.create({ intermediates: true, idempotent: true });
    return dir;
  };

  async function readIndex() {
    try {
      const text = await storage.getItem(INDEX_KEY);
      return text ? JSON.parse(text) || {} : {};
    } catch {
      return {};
    }
  }

  async function writeIndex(index) {
    await storage.setItem(INDEX_KEY, JSON.stringify(index));
  }

  return {
    /** Every downloaded episode, newest first: `{ id, title, uri, bytes, downloadedUtc, meta }`. Missing files are dropped. */
    async list() {
      const index = await readIndex();
      const items = [];
      let changed = false;
      for (const [id, entry] of Object.entries(index)) {
        const file = new fs.File(entry.uri);
        if (!file.exists) {
          delete index[id];
          changed = true;
          continue;
        }
        items.push({ ...entry, id });
      }
      if (changed) await writeIndex(index);
      return items.sort((a, b) => String(b.downloadedUtc).localeCompare(String(a.downloadedUtc)));
    },

    /**
     * Local file URI for a downloaded episode, or null. With `mediaId`, a download of an older episode of the same
     * briefing (audio was made again since) does not count.
     */
    async localUri(id, mediaId) {
      const index = await readIndex();
      const entry = index[id];
      if (!entry) return null;
      if (mediaId && entry.meta && entry.meta.mediaId && entry.meta.mediaId !== mediaId) return null;
      return new fs.File(entry.uri).exists ? entry.uri : null;
    },

    /** The stored entry for a download (`{ id, title, uri, bytes, downloadedUtc, meta }`), or null. */
    async get(id) {
      const index = await readIndex();
      const entry = index[id];
      if (!entry || !new fs.File(entry.uri).exists) return null;
      return { ...entry, id };
    },

    /**
     * Download `url` (with auth headers) and record it. `meta` is kept with the entry (chapters, duration, the episode
     * ID) so the episode plays offline. Resolves the stored entry.
     */
    async download({ id, title, url, headers, meta }) {
      const target = new fs.File(folder(), safeName(id));
      if (target.exists) target.delete();
      const file = await fs.File.downloadFileAsync(url, target, { headers: headers || {}, idempotent: true });
      const entry = { title: title || '', uri: file.uri, bytes: file.size || 0, downloadedUtc: new Date(now()).toISOString() };
      if (meta) entry.meta = meta;
      const index = await readIndex();
      index[id] = entry;
      await writeIndex(index);
      return { ...entry, id };
    },

    async remove(id) {
      const index = await readIndex();
      const entry = index[id];
      if (entry) {
        const file = new fs.File(entry.uri);
        if (file.exists) file.delete();
        delete index[id];
        await writeIndex(index);
      }
    },

    async removeAll() {
      const index = await readIndex();
      for (const entry of Object.values(index)) {
        const file = new fs.File(entry.uri);
        if (file.exists) file.delete();
      }
      await writeIndex({});
    },

    /** Total bytes used by downloads and the device's free space. */
    async usage() {
      const items = await this.list();
      let free = null;
      try {
        free = fs.Paths.availableDiskSpace;
      } catch {
        free = null;
      }
      return { count: items.length, bytes: items.reduce((sum, item) => sum + (item.bytes || 0), 0), freeBytes: free };
    },
  };
}
