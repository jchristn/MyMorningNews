import { SKIP_BACK_SECONDS, SKIP_FORWARD_SECONDS, clampRate, initialPlayerState, isActive, normalizeChapters, nextChapterStart, playerReducer, previousChapterStart, sleepDue } from './machine';

/** Lock screen and Android media notification controls: play and pause, back 15 seconds, forward 30 seconds. */
export const LOCK_SCREEN_OPTIONS = Object.freeze({
  showSeekBackward: true,
  showSeekForward: true,
  showNextTrack: false,
  showPreviousTrack: false,
  seekBackwardIntervalSeconds: SKIP_BACK_SECONDS,
  seekForwardIntervalSeconds: SKIP_FORWARD_SECONDS,
});

/** How long a seek may take before status updates are trusted again (milliseconds). */
export const SEEK_SETTLE_MS = 3000;

/** Save the resume position this often while playing (milliseconds). */
export const PROGRESS_INTERVAL_MS = 10000;

/**
 * The single audio player behind the Listen tab, the mini player, and the lock screen (plan sections 22 and 35.3).
 *
 * Built on expo-audio (see README "Audio player"): one AudioPlayer, background playback (audio session
 * `doNotMix`, `shouldPlayInBackground`), lock-screen and notification metadata and controls through
 * `setActiveForLockScreen`, speed with pitch correction, chapters, a sleep timer, and the resume position saved
 * locally and synced for briefings. Native access is injected (`audio`), so the service runs in Jest with a fake.
 *
 * A track: `{ id, title, artist, album, uri | asset, headers, artworkUrl, chapters: [{ title, startSeconds }],
 * durationSeconds, briefingId }`.
 */
export class PlayerService {
  constructor({ audio, progress, getClient, now, timers } = {}) {
    this.audio = audio;
    this.progress = progress || null;
    this.getClient = getClient || (() => null);
    this.now = now || (() => Date.now());
    this.timers = timers || { setInterval: (fn, ms) => setInterval(fn, ms), clearInterval: (id) => clearInterval(id) };
    this.state = initialPlayerState;
    this.listeners = new Set();
    this.player = null;
    this.subscription = null;
    this.ticker = null;
    this.lastSavedAt = 0;
    this.modeReady = false;
    this.pendingSeek = null;
    this.seekGuard = null;
  }

  getState() {
    return this.state;
  }

  /** The API client used to sync listening progress (it changes with the server). */
  setClientProvider(getClient) {
    this.getClient = getClient || (() => null);
  }

  subscribe(listener) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  dispatch(action) {
    const next = playerReducer(this.state, action);
    if (next === this.state) return;
    this.state = next;
    for (const listener of this.listeners) listener(next);
  }

  async ensureAudioMode() {
    if (this.modeReady) return;
    await this.audio.setAudioModeAsync({ playsInSilentMode: true, shouldPlayInBackground: true, interruptionMode: 'doNotMix', allowsRecording: false });
    this.modeReady = true;
  }

  /** Load a track and (by default) start playing from its resume position. */
  async load(track, { autoplay = true, startAt } = {}) {
    if (!track || (!track.uri && track.asset === undefined)) throw new Error('A track needs a uri or a bundled asset.');
    await this.ensureAudioMode();
    const resume = Number.isFinite(startAt) ? startAt : this.progress ? await this.progress.get(track.id) : 0;
    this.dispatch({ type: 'load', track, position: resume });
    const source = track.asset !== undefined ? track.asset : { uri: track.uri, headers: track.headers || undefined };
    if (!this.player) {
      this.player = this.audio.createAudioPlayer(source, { updateInterval: 500 });
      this.subscription = this.player.addListener('playbackStatusUpdate', (status) => this.onStatus(status));
    } else {
      this.player.replace(source);
    }
    this.player.setPlaybackRate(this.state.rate, 'high');
    this.pendingSeek = resume > 0 ? resume : null;
    this.player.setActiveForLockScreen(true, this.metadata(track), LOCK_SCREEN_OPTIONS);
    if (autoplay) this.play();
  }

  metadata(track) {
    return { title: track.title || '', artist: track.artist || 'My Morning News', albumTitle: track.album || undefined, artworkUrl: track.artworkUrl || undefined };
  }

  onStatus(status) {
    if (!status || !this.state.track) return;
    if (status.isLoaded && this.state.status === 'loading') {
      this.dispatch({ type: 'loaded', duration: status.duration });
      if (this.pendingSeek !== null && this.player) {
        const target = this.pendingSeek;
        this.pendingSeek = null;
        this.seekTo(target);
      }
    }
    this.dispatch({ type: 'status', currentTime: this.reportedTime(status.currentTime), duration: status.duration, playing: status.playing, isBuffering: status.isBuffering, didJustFinish: status.didJustFinish, isLoaded: status.isLoaded });
    if (status.didJustFinish) {
      this.stopTicker();
      void this.saveProgress(true);
      return;
    }
    if (isActive(this.state) && this.now() - this.lastSavedAt >= PROGRESS_INTERVAL_MS) void this.saveProgress(false);
    this.checkSleep();
  }

  /** Seek the native player and hold the reported position at the target until the seek lands (up to 3 seconds). */
  seekTo(target) {
    this.seekGuard = { target, until: this.now() + SEEK_SETTLE_MS };
    if (this.player) void this.player.seekTo(target);
  }

  /** Status updates sent before a seek completes still carry the old time; report the target instead. */
  reportedTime(currentTime) {
    if (this.pendingSeek !== null) return this.state.position;
    const guard = this.seekGuard;
    if (guard && this.now() < guard.until && Math.abs((currentTime || 0) - guard.target) > 1.5) return guard.target;
    this.seekGuard = null;
    return currentTime;
  }

  play() {
    if (!this.player || !this.state.track) return;
    if (this.state.status === 'ended') this.seekTo(0);
    this.dispatch({ type: 'play' });
    this.player.play();
    this.startTicker();
  }

  pause() {
    if (!this.player) return;
    this.player.pause();
    this.dispatch({ type: 'pause' });
    this.stopTicker();
    void this.saveProgress(true);
  }

  toggle() {
    if (isActive(this.state)) this.pause();
    else this.play();
  }

  seek(position) {
    if (!this.player || !this.state.track) return;
    this.dispatch({ type: 'seek', position });
    this.seekTo(this.state.position);
  }

  skip(seconds) {
    if (!this.player || !this.state.track) return;
    this.dispatch({ type: 'skip', seconds });
    this.seekTo(this.state.position);
  }

  skipBack() {
    this.skip(-SKIP_BACK_SECONDS);
  }

  skipForward() {
    this.skip(SKIP_FORWARD_SECONDS);
  }

  chapters() {
    return normalizeChapters(this.state.track && this.state.track.chapters, this.state.duration);
  }

  previousChapter() {
    this.seek(previousChapterStart(this.chapters(), this.state.position));
  }

  nextChapter() {
    const start = nextChapterStart(this.chapters(), this.state.position);
    if (start !== null) this.seek(start);
  }

  setRate(rate) {
    this.dispatch({ type: 'rate', rate: clampRate(rate) });
    if (this.player) this.player.setPlaybackRate(this.state.rate, 'high');
  }

  /** Sleep timer: `off`, `timer` (with minutes), `endOfChapter`, or `endOfTrack`. */
  setSleep(mode, minutes) {
    this.dispatch({ type: 'sleep', mode, minutes, now: this.now() });
    if (isActive(this.state)) this.startTicker();
  }

  checkSleep() {
    if (sleepDue(this.state, this.now())) {
      if (this.player) this.player.pause();
      this.dispatch({ type: 'sleepFired' });
      this.stopTicker();
      void this.saveProgress(true);
    }
  }

  /** A one-second ticker so the sleep timer fires even when status updates pause. */
  startTicker() {
    if (this.ticker) return;
    this.ticker = this.timers.setInterval(() => this.checkSleep(), 1000);
  }

  stopTicker() {
    if (!this.ticker) return;
    this.timers.clearInterval(this.ticker);
    this.ticker = null;
  }

  async saveProgress(sync) {
    const { track, position, duration } = this.state;
    if (!track || !this.progress) return;
    this.lastSavedAt = this.now();
    await this.progress.save(track.id, position, duration);
    if (sync && track.briefingId) await this.progress.sync(this.getClient(), track.briefingId, position, duration);
  }

  /** Stop playback, remove the lock-screen controls, and release the native player. */
  async stop() {
    this.stopTicker();
    await this.saveProgress(true);
    if (this.player) {
      try {
        this.player.pause();
        this.player.clearLockScreenControls();
      } catch {
        // Releasing a player that is already gone is harmless.
      }
      if (this.subscription) this.subscription.remove();
      this.player.remove();
    }
    this.player = null;
    this.subscription = null;
    this.dispatch({ type: 'stop' });
  }
}
