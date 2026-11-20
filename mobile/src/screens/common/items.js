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

/** Episode statuses while audio is being made (MediaStatusEnum). */
export const PENDING_AUDIO_STATUSES = Object.freeze(['Queued', 'Scripting', 'Synthesizing']);

/**
 * Audio for a briefing (Phase 8). A briefing names its ready episode in `mediaAssetId`; the briefing detail also
 * carries `audio`, the newest episode in any status (so a remake in progress or a failure shows). Returns
 * `{ mediaId, status, durationSeconds, errorMessage }`, or null when there is no audio at all.
 */
export function briefingAudio(briefing, episode) {
  const media = episode && typeof episode === 'object' && episode.id ? episode : null;
  if (media) return { mediaId: media.id, status: media.status || 'Ready', durationSeconds: Number(media.durationSeconds) || 0, errorMessage: media.errorMessage || null, readyMediaId: (briefing && briefing.mediaAssetId) || (media.status === 'Ready' ? media.id : null) };
  if (!briefing || typeof briefing !== 'object' || !briefing.mediaAssetId) return null;
  return { mediaId: briefing.mediaAssetId, status: 'Ready', durationSeconds: 0, errorMessage: null, readyMediaId: briefing.mediaAssetId };
}
