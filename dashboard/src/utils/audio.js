// Playback math for the audio player: chapters in order, the chapter playing at a time, tick positions on the
// progress bar, skips, and clock labels. Pure functions; AudioPlayer renders the result.

export const PLAYBACK_SPEEDS = [0.75, 1, 1.25, 1.5, 1.75, 2];
export const SKIP_BACK_SECONDS = 15;
export const SKIP_FORWARD_SECONDS = 30;

function finite(value, fallback = 0) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

// Chapters sorted by start (then sortOrder), each with a numeric start and an end. A missing or inverted end runs to
// the next chapter's start, or to the duration for the last chapter.
export function normalizeChapters(chapters, durationSeconds = 0) {
  const list = (Array.isArray(chapters) ? chapters : [])
    .filter((c) => c && Number.isFinite(Number(c.startSeconds)))
    .map((c) => ({ ...c, startSeconds: Math.max(0, finite(c.startSeconds)), endSeconds: finite(c.endSeconds, NaN) }))
    .sort((a, b) => a.startSeconds - b.startSeconds || finite(a.sortOrder) - finite(b.sortOrder));
  const duration = finite(durationSeconds);
  return list.map((c, i) => {
    const next = list[i + 1];
    const limit = next ? next.startSeconds : Math.max(duration, c.startSeconds);
    const end = Number.isFinite(c.endSeconds) && c.endSeconds > c.startSeconds ? c.endSeconds : limit;
    return { ...c, endSeconds: end };
  });
}

// The index of the chapter playing at time (seconds), or -1 before the first chapter or with none. Chapters are the
// output of normalizeChapters. A time in a gap between chapters belongs to the earlier chapter; the end of the last
// chapter (and anything after it) belongs to the last chapter.
export function chapterIndexAt(chapters, time) {
  if (!Array.isArray(chapters) || chapters.length === 0) return -1;
  const t = finite(time);
  let found = -1;
  for (let i = 0; i < chapters.length; i += 1) {
    if (t >= chapters[i].startSeconds) found = i;
    else break;
  }
  return found;
}

// The chapter at time, or null.
export function chapterAt(chapters, time) {
  const index = chapterIndexAt(chapters, time);
  return index >= 0 ? chapters[index] : null;
}

// Where each chapter starts as a fraction (0 to 1) of the duration, for the tick marks on the progress bar. The tick
// at the very start is left out (it would sit on the edge of the bar), as are starts at or past the end.
export function chapterTicks(chapters, durationSeconds) {
  const duration = finite(durationSeconds);
  if (!Array.isArray(chapters) || duration <= 0) return [];
  return chapters
    .filter((c) => c.startSeconds > 0 && c.startSeconds < duration)
    .map((c) => ({ key: c.id || String(c.startSeconds), fraction: c.startSeconds / duration, title: c.title || '' }));
}

// The fraction (0 to 1) of the duration that time represents.
export function progressFraction(time, durationSeconds) {
  const duration = finite(durationSeconds);
  if (duration <= 0) return 0;
  return Math.min(1, Math.max(0, finite(time) / duration));
}

// time clamped to [0, duration]. With an unknown duration, only the lower bound applies.
export function clampTime(time, durationSeconds) {
  const t = Math.max(0, finite(time));
  const duration = finite(durationSeconds);
  return duration > 0 ? Math.min(duration, t) : t;
}

// The time a click at offsetX on a bar of width pixels points at.
export function timeAtOffset(offsetX, width, durationSeconds) {
  const w = finite(width);
  if (w <= 0) return 0;
  return clampTime((finite(offsetX) / w) * finite(durationSeconds), durationSeconds);
}

// A clock label: m:ss under an hour, h:mm:ss from an hour. Negative and missing values read 0:00.
export function formatClock(seconds) {
  const total = Math.max(0, Math.floor(finite(seconds)));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const ss = String(s).padStart(2, '0');
  return h > 0 ? h + ':' + String(m).padStart(2, '0') + ':' + ss : m + ':' + ss;
}
