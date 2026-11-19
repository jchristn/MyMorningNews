// Defensive readers for list and detail items whose full shapes arrive with later server phases (plan section 24).
// Screens show a title, a one-line detail, and a date, reading the fields the plan names and falling back to the ID.

export function itemTitle(item) {
  if (!item || typeof item !== 'object') return '';
  return String(item.title || item.name || item.headline || item.statement || item.text || item.id || '');
}

export function itemDetail(item) {
  if (!item || typeof item !== 'object') return '';
  const text = item.summary || item.description || item.rationale || item.status || '';
  return String(text);
}

export function itemDate(item) {
  if (!item || typeof item !== 'object') return null;
  return item.publishedUtc || item.createdUtc || item.dueUtc || item.lastUpdateUtc || null;
}

/** Audio for a briefing: `mediaId` (or `audioMediaId`), plus optional chapters. */
export function briefingAudio(briefing) {
  if (!briefing || typeof briefing !== 'object') return null;
  const mediaId = briefing.mediaId || briefing.audioMediaId || (briefing.audio && briefing.audio.mediaId) || null;
  if (!mediaId) return null;
  const chapters = (briefing.audio && briefing.audio.chapters) || briefing.chapters || [];
  const durationSeconds = (briefing.audio && briefing.audio.durationSeconds) || briefing.audioDurationSeconds || 0;
  return { mediaId, chapters, durationSeconds };
}
