// The player's state machine, kept pure (no native modules) so every transition is unit tested. PlayerService feeds it
// actions from the UI and status updates from expo-audio.
//
// Statuses: idle (nothing loaded), loading, paused, playing, buffering, ended, error.

/** Playback speeds offered in the UI (plan section 35.3: 0.75x to 2x). */
export const RATES = Object.freeze([0.75, 1, 1.25, 1.5, 1.75, 2]);
export const MIN_RATE = 0.75;
export const MAX_RATE = 2;
/** Skip intervals in seconds (also shown on the lock screen and the Android media notification). */
export const SKIP_BACK_SECONDS = 15;
export const SKIP_FORWARD_SECONDS = 30;
/** Sleep timer presets in minutes. */
export const SLEEP_PRESETS = Object.freeze([5, 15, 30, 45, 60]);

export const initialPlayerState = Object.freeze({
  status: 'idle',
  track: null,
  position: 0,
  duration: 0,
  rate: 1,
  sleep: { mode: 'off', endsAt: null },
  error: null,
});

export function clampRate(rate) {
  const value = Number(rate);
  if (!Number.isFinite(value)) return 1;
  return Math.min(MAX_RATE, Math.max(MIN_RATE, Math.round(value * 100) / 100));
}

/** The next preset speed after `rate`, wrapping from 2x back to 0.75x. */
export function nextRate(rate) {
  const current = clampRate(rate);
  const next = RATES.find((r) => r > current + 0.001);
  return next === undefined ? RATES[0] : next;
}

export function clampPosition(position, duration) {
  const value = Number(position);
  if (!Number.isFinite(value) || value < 0) return 0;
  return duration > 0 ? Math.min(value, duration) : value;
}

/** Chapters sorted by start, each with an end (the next start, or the duration). */
export function normalizeChapters(chapters, duration) {
  const list = (Array.isArray(chapters) ? chapters : [])
    .filter((c) => c && Number.isFinite(Number(c.startSeconds)))
    .map((c) => ({ title: String(c.title || ''), startSeconds: Math.max(0, Number(c.startSeconds)) }))
    .sort((a, b) => a.startSeconds - b.startSeconds);
  return list.map((c, i) => ({ ...c, endSeconds: i + 1 < list.length ? list[i + 1].startSeconds : duration > 0 ? duration : Infinity }));
}

/** Index of the chapter that contains `position`, or -1 when there are no chapters or it precedes the first. */
export function chapterIndexAt(chapters, position) {
  let index = -1;
  for (let i = 0; i < chapters.length; i += 1) {
    if (position + 0.001 >= chapters[i].startSeconds) index = i;
  }
  return index;
}

/** Position to seek to for "previous chapter": the start of this chapter, or the previous one when near its start. */
export function previousChapterStart(chapters, position) {
  const index = chapterIndexAt(chapters, position);
  if (index < 0) return 0;
  if (position - chapters[index].startSeconds > 3 || index === 0) return chapters[index].startSeconds;
  return chapters[index - 1].startSeconds;
}

export function nextChapterStart(chapters, position) {
  const index = chapterIndexAt(chapters, position);
  const next = chapters[index + 1];
  return next ? next.startSeconds : null;
}

/** True when the sleep timer should stop playback now. */
export function sleepDue(state, now) {
  const { sleep } = state;
  if (!sleep || sleep.mode === 'off') return false;
  if (sleep.mode === 'timer') return sleep.endsAt !== null && now >= sleep.endsAt;
  if (sleep.mode === 'endOfChapter') return Number.isFinite(sleep.chapterEnd) && state.position >= sleep.chapterEnd - 0.25;
  return false;
}

/**
 * Reducer. Actions:
 *   load { track, position }    a new track (resume position in seconds)
 *   loaded { duration }
 *   play, pause, stop
 *   status { currentTime, duration, playing, isBuffering, didJustFinish, isLoaded }  from expo-audio
 *   seek { position }, skip { seconds }
 *   rate { rate }
 *   sleep { mode: 'off' | 'timer' | 'endOfChapter' | 'endOfTrack', minutes, now }
 *   sleepFired
 *   error { message }
 */
export function playerReducer(state, action) {
  switch (action.type) {
    case 'load': {
      if (!action.track) return state;
      const duration = Number(action.track.durationSeconds) > 0 ? Number(action.track.durationSeconds) : 0;
      return { ...state, status: 'loading', track: action.track, position: clampPosition(action.position || 0, duration), duration, error: null, sleep: { mode: 'off', endsAt: null } };
    }
    case 'loaded':
      if (state.status !== 'loading') return { ...state, duration: action.duration > 0 ? action.duration : state.duration };
      return { ...state, status: 'paused', duration: action.duration > 0 ? action.duration : state.duration };
    case 'play':
      if (!state.track || state.status === 'error') return state;
      if (state.status === 'ended') return { ...state, status: 'playing', position: 0 };
      return { ...state, status: state.status === 'loading' ? 'loading' : 'playing' };
    case 'pause':
      if (!state.track || state.status === 'idle' || state.status === 'error' || state.status === 'ended') return state;
      return { ...state, status: state.status === 'loading' ? 'loading' : 'paused' };
    case 'stop':
      return { ...initialPlayerState, rate: state.rate };
    case 'status': {
      if (!state.track) return state;
      const duration = action.duration > 0 ? action.duration : state.duration;
      const position = clampPosition(action.currentTime, duration);
      if (action.didJustFinish) {
        return { ...state, status: 'ended', position: duration || position, duration, sleep: state.sleep.mode === 'endOfTrack' ? { mode: 'off', endsAt: null } : state.sleep };
      }
      let status = state.status;
      if (action.isLoaded === false) status = state.status === 'error' ? 'error' : 'loading';
      else if (action.playing) status = action.isBuffering ? 'buffering' : 'playing';
      else if (action.isBuffering && state.status === 'playing') status = 'buffering';
      else if (state.status === 'loading' || state.status === 'playing' || state.status === 'buffering') status = 'paused';
      return { ...state, status, position, duration };
    }
    case 'seek':
      if (!state.track) return state;
      return { ...state, position: clampPosition(action.position, state.duration), status: state.status === 'ended' ? 'paused' : state.status };
    case 'skip':
      if (!state.track) return state;
      return { ...state, position: clampPosition(state.position + Number(action.seconds || 0), state.duration), status: state.status === 'ended' && action.seconds < 0 ? 'paused' : state.status };
    case 'rate':
      return { ...state, rate: clampRate(action.rate) };
    case 'sleep': {
      if (action.mode === 'timer') {
        const minutes = Math.max(1, Math.min(240, Number(action.minutes) || 0));
        return { ...state, sleep: { mode: 'timer', endsAt: (action.now || 0) + minutes * 60000, minutes } };
      }
      if (action.mode === 'endOfChapter') {
        const chapters = normalizeChapters(state.track && state.track.chapters, state.duration);
        const chapterIndex = chapterIndexAt(chapters, state.position);
        if (chapterIndex < 0 || !Number.isFinite(chapters[chapterIndex].endSeconds)) return { ...state, sleep: { mode: 'endOfTrack', endsAt: null } };
        return { ...state, sleep: { mode: 'endOfChapter', endsAt: null, chapterIndex, chapterEnd: chapters[chapterIndex].endSeconds } };
      }
      if (action.mode === 'endOfTrack') return { ...state, sleep: { mode: 'endOfTrack', endsAt: null } };
      return { ...state, sleep: { mode: 'off', endsAt: null } };
    }
    case 'sleepFired':
      return { ...state, status: state.status === 'playing' || state.status === 'buffering' ? 'paused' : state.status, sleep: { mode: 'off', endsAt: null } };
    case 'error':
      return { ...state, status: 'error', error: action.message || 'Playback failed.' };
    default:
      return state;
  }
}

/** True while audio is (or is about to be) coming out of the speaker. */
export function isActive(state) {
  return state.status === 'playing' || state.status === 'buffering';
}
