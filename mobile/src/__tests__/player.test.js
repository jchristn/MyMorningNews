import { RATES, chapterIndexAt, clampRate, initialPlayerState, nextChapterStart, nextRate, normalizeChapters, playerReducer, previousChapterStart, sleepDue } from '@/player/machine';
import { LOCK_SCREEN_OPTIONS, PROGRESS_INTERVAL_MS, PlayerService } from '@/player/PlayerService';
import { COMPLETE_MARGIN_SECONDS, createProgressStore } from '@/player/progress';
import { createDownloads } from '@/player/downloads';
import { briefingTrack } from '@/player/tracks';
import { ApiClient } from '@/api/client';
import { fakeServer } from './fakeServer';

const TRACK = {
  id: 'briefing.b1',
  briefingId: 'b1',
  title: 'Morning News',
  uri: 'http://127.0.0.1:12100/v1.0/api/media/m1',
  chapters: [
    { title: 'Intro', startSeconds: 0 },
    { title: 'Topic A', startSeconds: 60 },
    { title: 'Topic B', startSeconds: 180 },
  ],
  durationSeconds: 300,
};

function run(actions, state = initialPlayerState) {
  return actions.reduce((s, a) => playerReducer(s, a), state);
}

describe('player state machine', () => {
  it('goes idle, loading, paused, playing, paused, ended', () => {
    let s = run([{ type: 'load', track: TRACK, position: 42 }]);
    expect(s).toMatchObject({ status: 'loading', position: 42, duration: 300 });
    s = run([{ type: 'loaded', duration: 301 }], s);
    expect(s).toMatchObject({ status: 'paused', duration: 301 });
    s = run([{ type: 'play' }], s);
    expect(s.status).toBe('playing');
    s = run([{ type: 'status', currentTime: 50, duration: 301, playing: true, isBuffering: true, isLoaded: true }], s);
    expect(s).toMatchObject({ status: 'buffering', position: 50 });
    s = run([{ type: 'status', currentTime: 51, duration: 301, playing: false, isBuffering: false, isLoaded: true }], s);
    expect(s.status).toBe('paused');
    s = run([{ type: 'status', currentTime: 301, duration: 301, playing: false, didJustFinish: true, isLoaded: true }], s);
    expect(s.status).toBe('ended');
    s = run([{ type: 'play' }], s);
    expect(s).toMatchObject({ status: 'playing', position: 0 });
  });

  it('ignores transport actions with nothing loaded, and stop keeps the speed', () => {
    expect(run([{ type: 'play' }, { type: 'pause' }, { type: 'skip', seconds: 30 }])).toBe(initialPlayerState);
    const s = run([{ type: 'rate', rate: 1.5 }, { type: 'load', track: TRACK }, { type: 'stop' }]);
    expect(s).toMatchObject({ status: 'idle', track: null, rate: 1.5 });
  });

  it('clamps skips and seeks to the track', () => {
    let s = run([{ type: 'load', track: TRACK, position: 10 }, { type: 'loaded', duration: 300 }]);
    expect(run([{ type: 'skip', seconds: -15 }], s).position).toBe(0);
    expect(run([{ type: 'skip', seconds: 30 }], s).position).toBe(40);
    expect(run([{ type: 'seek', position: 999 }], s).position).toBe(300);
    expect(run([{ type: 'seek', position: -1 }], s).position).toBe(0);
    s = run([{ type: 'error', message: 'boom' }], s);
    expect(s).toMatchObject({ status: 'error', error: 'boom' });
  });

  it('limits speed to 0.75x to 2x and cycles through the presets', () => {
    expect(clampRate(0.5)).toBe(0.75);
    expect(clampRate(3)).toBe(2);
    expect(clampRate('x')).toBe(1);
    expect(RATES[0]).toBe(0.75);
    expect(nextRate(1)).toBe(1.25);
    expect(nextRate(2)).toBe(0.75);
  });

  it('finds chapters and moves between them', () => {
    const chapters = normalizeChapters(TRACK.chapters, 300);
    expect(chapters.map((c) => c.endSeconds)).toEqual([60, 180, 300]);
    expect(chapterIndexAt(chapters, 59)).toBe(0);
    expect(chapterIndexAt(chapters, 60)).toBe(1);
    expect(previousChapterStart(chapters, 70)).toBe(60);
    expect(previousChapterStart(chapters, 61)).toBe(0);
    expect(nextChapterStart(chapters, 70)).toBe(180);
    expect(nextChapterStart(chapters, 200)).toBeNull();
    expect(normalizeChapters([{ title: 'b', startSeconds: 5 }, { title: 'a', startSeconds: 1 }, { startSeconds: 'x' }], 0).map((c) => c.title)).toEqual(['a', 'b']);
  });

  it('runs the sleep timer: minutes, end of chapter, end of episode, and off', () => {
    const base = run([{ type: 'load', track: TRACK, position: 70 }, { type: 'loaded', duration: 300 }, { type: 'play' }]);
    const timer = run([{ type: 'sleep', mode: 'timer', minutes: 15, now: 1000 }], base);
    expect(sleepDue(timer, 1000 + 14 * 60000)).toBe(false);
    expect(sleepDue(timer, 1000 + 15 * 60000)).toBe(true);
    expect(run([{ type: 'sleepFired' }], timer)).toMatchObject({ status: 'paused', sleep: { mode: 'off' } });

    const chapter = run([{ type: 'sleep', mode: 'endOfChapter' }], base);
    expect(chapter.sleep).toMatchObject({ mode: 'endOfChapter', chapterEnd: 180 });
    expect(sleepDue({ ...chapter, position: 179 }, 0)).toBe(false);
    expect(sleepDue({ ...chapter, position: 180 }, 0)).toBe(true);

    const episode = run([{ type: 'sleep', mode: 'endOfTrack' }], base);
    expect(run([{ type: 'status', currentTime: 300, duration: 300, didJustFinish: true, isLoaded: true }], episode)).toMatchObject({ status: 'ended', sleep: { mode: 'off' } });
    expect(run([{ type: 'sleep', mode: 'off' }], timer).sleep.mode).toBe('off');
  });
});

function fakeAudio() {
  const players = [];
  const audio = {
    setAudioModeAsync: jest.fn(async () => undefined),
    createAudioPlayer: jest.fn((source) => {
      const listeners = [];
      const player = {
        source,
        play: jest.fn(),
        pause: jest.fn(),
        replace: jest.fn(),
        seekTo: jest.fn(async () => undefined),
        setPlaybackRate: jest.fn(),
        setActiveForLockScreen: jest.fn(),
        clearLockScreenControls: jest.fn(),
        remove: jest.fn(),
        addListener: jest.fn((event, fn) => {
          listeners.push(fn);
          return { remove: jest.fn() };
        }),
        emit: (status) => listeners.forEach((fn) => fn(status)),
      };
      players.push(player);
      return player;
    }),
  };
  return { audio, players };
}

function fakeTimers() {
  const intervals = new Map();
  let next = 1;
  return {
    setInterval: (fn) => {
      intervals.set(next, fn);
      return next++;
    },
    clearInterval: (id) => intervals.delete(id),
    tick: () => [...intervals.values()].forEach((fn) => fn()),
    count: () => intervals.size,
  };
}

describe('PlayerService', () => {
  it('plays in the background with lock-screen controls for back 15 and forward 30 seconds', async () => {
    const { audio, players } = fakeAudio();
    const service = new PlayerService({ audio, timers: fakeTimers() });
    await service.load(TRACK);
    expect(audio.setAudioModeAsync).toHaveBeenCalledWith(expect.objectContaining({ shouldPlayInBackground: true, playsInSilentMode: true, interruptionMode: 'doNotMix' }));
    const player = players[0];
    expect(player.source).toEqual({ uri: TRACK.uri, headers: undefined });
    expect(player.setActiveForLockScreen).toHaveBeenCalledWith(true, expect.objectContaining({ title: 'Morning News', artist: 'My Morning News' }), LOCK_SCREEN_OPTIONS);
    expect(LOCK_SCREEN_OPTIONS).toMatchObject({ seekBackwardIntervalSeconds: 15, seekForwardIntervalSeconds: 30, showSeekBackward: true, showSeekForward: true });
    expect(player.play).toHaveBeenCalled();
  });

  it('resumes from the saved position once the source is loaded, and reuses the native player', async () => {
    const { audio, players } = fakeAudio();
    const progress = { get: jest.fn(async () => 95), save: jest.fn(async () => undefined), sync: jest.fn(async () => true) };
    const service = new PlayerService({ audio, progress, timers: fakeTimers() });
    await service.load(TRACK, { autoplay: false });
    expect(service.getState()).toMatchObject({ status: 'loading', position: 95 });
    players[0].emit({ isLoaded: true, duration: 300, currentTime: 0, playing: false });
    expect(players[0].seekTo).toHaveBeenCalledWith(95);
    expect(service.getState()).toMatchObject({ status: 'paused', position: 95 });
    await service.load({ ...TRACK, id: 'other', uri: 'file:///x.mp3' }, { startAt: 0 });
    expect(audio.createAudioPlayer).toHaveBeenCalledTimes(1);
    expect(players[0].replace).toHaveBeenCalledWith({ uri: 'file:///x.mp3', headers: undefined });
  });

  it('skips, seeks, changes speed, and saves progress when paused', async () => {
    const { audio, players } = fakeAudio();
    const progress = { get: async () => 0, save: jest.fn(async () => undefined), sync: jest.fn(async () => true) };
    const client = {};
    const service = new PlayerService({ audio, progress, getClient: () => client, timers: fakeTimers() });
    await service.load(TRACK);
    players[0].emit({ isLoaded: true, duration: 300, currentTime: 100, playing: true });
    service.skipForward();
    expect(players[0].seekTo).toHaveBeenLastCalledWith(130);
    service.skipBack();
    expect(players[0].seekTo).toHaveBeenLastCalledWith(115);
    service.nextChapter();
    expect(players[0].seekTo).toHaveBeenLastCalledWith(180);
    service.setRate(5);
    expect(players[0].setPlaybackRate).toHaveBeenLastCalledWith(2, 'high');
    service.pause();
    await new Promise((r) => setTimeout(r, 0));
    expect(progress.save).toHaveBeenCalledWith('briefing.b1', 180, 300);
    expect(progress.sync).toHaveBeenCalledWith(client, 'b1', 180, 300);
  });

  it('saves progress periodically while playing', async () => {
    let now = 0;
    const { audio, players } = fakeAudio();
    const progress = { get: async () => 0, save: jest.fn(async () => undefined), sync: jest.fn(async () => true) };
    const service = new PlayerService({ audio, progress, now: () => now, timers: fakeTimers() });
    await service.load(TRACK);
    now = PROGRESS_INTERVAL_MS;
    players[0].emit({ isLoaded: true, duration: 300, currentTime: 10, playing: true });
    expect(progress.save).toHaveBeenCalledTimes(1);
    now += 1000;
    players[0].emit({ isLoaded: true, duration: 300, currentTime: 11, playing: true });
    expect(progress.save).toHaveBeenCalledTimes(1);
  });

  it('stops playback when the sleep timer runs out, even without status updates', async () => {
    let now = 0;
    const timers = fakeTimers();
    const { audio, players } = fakeAudio();
    const service = new PlayerService({ audio, now: () => now, timers });
    await service.load(TRACK);
    players[0].emit({ isLoaded: true, duration: 300, currentTime: 10, playing: true });
    service.setSleep('timer', 5);
    now = 5 * 60000;
    timers.tick();
    expect(players[0].pause).toHaveBeenCalled();
    expect(service.getState()).toMatchObject({ status: 'paused', sleep: { mode: 'off' } });
    expect(timers.count()).toBe(0);
  });

  it('releases the player and the lock-screen controls on stop', async () => {
    const { audio, players } = fakeAudio();
    const service = new PlayerService({ audio, timers: fakeTimers() });
    await service.load(TRACK);
    await service.stop();
    expect(players[0].clearLockScreenControls).toHaveBeenCalled();
    expect(players[0].remove).toHaveBeenCalled();
    expect(service.getState().status).toBe('idle');
  });

  it('refuses a track without a source', async () => {
    const service = new PlayerService({ audio: fakeAudio().audio });
    await expect(service.load({ id: 'x' })).rejects.toThrow('uri');
  });
});

describe('progress store', () => {
  function memoryStorage() {
    const map = new Map();
    return { getItem: async (k) => (map.has(k) ? map.get(k) : null), setItem: async (k, v) => map.set(k, v) };
  }

  it('saves positions locally and resumes from the start when finished', async () => {
    const store = createProgressStore({ storage: memoryStorage() });
    await store.save('t1', 120.7, 600);
    expect(await store.get('t1')).toBe(120);
    await store.save('t1', 600 - COMPLETE_MARGIN_SECONDS + 1, 600);
    expect(await store.get('t1')).toBe(0);
    expect(await store.get('unknown')).toBe(0);
  });

  it('syncs to PUT /briefings/{id}/progress and stops trying after a 404', async () => {
    const server = fakeServer({});
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', fetchImpl: server.fetchImpl });
    const store = createProgressStore({ storage: memoryStorage() });
    expect(await store.sync(client, 'b1', 30, 300)).toBe(false);
    expect(store.remoteSupported()).toBe(false);
    expect(await store.sync(client, 'b1', 40, 300)).toBe(false);
    expect(server.calls).toHaveLength(1);
    expect(server.calls[0]).toMatchObject({ key: 'PUT /v1.0/api/briefings/b1/progress', body: { positionSeconds: 30, completed: false } });

    const ok = fakeServer({ 'PUT /v1.0/api/briefings/b1/progress': () => ({ status: 204 }) });
    const store2 = createProgressStore({ storage: memoryStorage() });
    expect(await store2.sync(new ApiClient({ baseUrl: 'http://x', fetchImpl: ok.fetchImpl }), 'b1', 299, 300)).toBe(true);
    expect(ok.calls[0].body).toEqual({ positionSeconds: 299, completed: true });
  });
});

describe('downloads', () => {
  function fakeFs() {
    const files = new Map();
    class File {
      constructor(...parts) {
        this.uri = parts.map((p) => (typeof p === 'string' ? p : p.uri)).join('/');
      }
      get exists() {
        return files.has(this.uri);
      }
      get size() {
        return files.get(this.uri) || 0;
      }
      delete() {
        files.delete(this.uri);
      }
      static async downloadFileAsync(url, target) {
        files.set(target.uri, 1000);
        return target;
      }
    }
    class Directory {
      constructor(...parts) {
        this.uri = parts.map((p) => (typeof p === 'string' ? p : p.uri)).join('/');
      }
      get exists() {
        return true;
      }
      create() {}
    }
    return { fs: { File, Directory, Paths: { document: { uri: 'file://docs' }, availableDiskSpace: 5000 } }, files };
  }

  it('downloads, lists, measures, and removes episodes', async () => {
    const { fs, files } = fakeFs();
    const map = new Map();
    const storage = { getItem: async (k) => map.get(k) || null, setItem: async (k, v) => map.set(k, v) };
    const downloads = createDownloads({ fs, storage, now: () => 0 });
    await downloads.download({ id: 'b/1', title: 'One', url: 'http://x/media/1', headers: { Authorization: 'Bearer t' } });
    await downloads.download({ id: 'b2', title: 'Two', url: 'http://x/media/2' });
    expect((await downloads.list()).map((d) => d.title).sort()).toEqual(['One', 'Two']);
    expect(await downloads.localUri('b/1')).toBe('file://docs/audio/b_1.mp3');
    expect(await downloads.usage()).toEqual({ count: 2, bytes: 2000, freeBytes: 5000 });
    files.delete('file://docs/audio/b2.mp3');
    expect((await downloads.list()).map((d) => d.id)).toEqual(['b/1']);
    await downloads.removeAll();
    expect(await downloads.list()).toEqual([]);
  });
});

describe('briefing tracks', () => {
  it('streams media with the bearer token, or plays the downloaded file', () => {
    const client = new ApiClient({ baseUrl: 'https://app.mymorningnews.ai' });
    const briefing = { id: 'b1', title: 'Morning', mediaId: 'm1', chapters: [{ title: 'Top of the news', startSeconds: 0 }] };
    expect(briefingTrack({ id: 'b2', title: 'No audio' }, { client })).toBeNull();
    expect(briefingTrack(briefing, { client, token: 't' })).toMatchObject({ id: 'briefing.b1', briefingId: 'b1', uri: 'https://app.mymorningnews.ai/v1.0/api/media/m1', headers: { Authorization: 'Bearer t' }, chapters: [{ title: 'Top of the news', startSeconds: 0 }] });
    expect(briefingTrack(briefing, { client, token: 't', localUri: 'file:///b1.mp3' })).toMatchObject({ uri: 'file:///b1.mp3', headers: undefined });
  });
});
