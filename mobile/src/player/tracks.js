import { routes } from '@/api/routes';

/** Chapters of a media detail as player chapters; `sectionId` links a chapter to the briefing section it reads. */
export function mediaChapters(chapters) {
  return (Array.isArray(chapters) ? chapters : [])
    .slice()
    .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0) || Number(a.startSeconds || 0) - Number(b.startSeconds || 0))
    .map((c) => {
      const chapter = { title: String(c.title || ''), startSeconds: Number(c.startSeconds ?? 0) };
      if (c.briefingSectionId) chapter.sectionId = c.briefingSectionId;
      return chapter;
    })
    .filter((c) => Number.isFinite(c.startSeconds));
}

/** Where an episode's audio streams from: the detail's `audioPath`, or the artifact of its `artifactId`. */
export function mediaAudioPath(detail) {
  if (!detail) return null;
  if (detail.audioPath) return detail.audioPath;
  const media = detail.media || {};
  return media.artifactId ? routes.artifactContent(media.artifactId) : null;
}

/**
 * A player track for a briefing's audio (plan section 22) from `GET /v1.0/api/media/{id}`: streamed from
 * `GET /v1.0/api/artifacts/{id}/content` with the bearer token (the route serves HTTP Range requests, so seeking
 * works), or from the downloaded file when one exists. The track ID is per briefing, so the resume position and the
 * synced progress survive a remade episode. Returns null when the episode has no audio file yet.
 *
 * @param {{ media: object, chapters: object[], audioPath?: string }} detail
 * @param {{ briefingId: string, title?: string, album?: string, client: object, token?: string, localUri?: string, artworkUrl?: string }} options
 */
export function mediaTrack(detail, { briefingId, title, album, client, token, localUri, artworkUrl }) {
  const media = (detail && detail.media) || null;
  const path = mediaAudioPath(detail);
  if (!media || (!path && !localUri)) return null;
  const id = briefingId || media.briefingId || media.id;
  return {
    id: 'briefing.' + id,
    briefingId: id,
    mediaId: media.id,
    title: title || media.title || '',
    artist: 'My Morning News',
    album: album || undefined,
    uri: localUri || client.url(path),
    headers: localUri || !token ? undefined : { Authorization: 'Bearer ' + token },
    artworkUrl,
    chapters: mediaChapters(detail.chapters),
    durationSeconds: Number(media.durationSeconds) || 0,
  };
}

const norm = (text) =>
  String(text || '')
    .trim()
    .toLowerCase();

/**
 * The reader heading (a parseBriefing outline or block id) for the chapter playing now, so the reader can mark the
 * section being read aloud. A chapter names its briefing section (`sectionId`); without one, the chapter title is
 * matched against the section headings. Returns null when this briefing is not the one playing.
 *
 * @param {{ track: object, position: number }} state Player state.
 * @param {string} briefingId
 * @param {{ id: string, heading: string }[]} sections Briefing sections.
 * @param {{ type: string, id: string, text: string }[]} blocks Parsed Markdown blocks.
 */
export function playingHeadingId(state, briefingId, sections, blocks) {
  if (!state || !state.track || state.track.briefingId !== briefingId) return null;
  const chapters = (state.track.chapters || []).slice().sort((a, b) => a.startSeconds - b.startSeconds);
  let chapter = null;
  for (const c of chapters) if ((state.position || 0) + 0.001 >= c.startSeconds) chapter = c;
  if (!chapter) return null;
  const section = chapter.sectionId ? (sections || []).find((s) => s.id === chapter.sectionId) : null;
  const wanted = norm(section ? section.heading : chapter.title);
  if (!wanted) return null;
  const heading = (blocks || []).find((b) => b.type === 'heading' && norm(b.text) === wanted);
  return heading ? heading.id : null;
}

/** What a download keeps beside the file so the episode plays offline with its chapters. */
export function downloadMeta(track) {
  return { briefingId: track.briefingId, mediaId: track.mediaId, title: track.title, album: track.album, chapters: track.chapters, durationSeconds: track.durationSeconds };
}

/** A track for a downloaded episode (offline), from its download entry. */
export function downloadedTrack(entry, { artworkUrl } = {}) {
  if (!entry || !entry.uri) return null;
  const meta = entry.meta || {};
  const briefingId = meta.briefingId || entry.id;
  return {
    id: 'briefing.' + briefingId,
    briefingId,
    mediaId: meta.mediaId,
    title: meta.title || entry.title || '',
    artist: 'My Morning News',
    album: meta.album || undefined,
    uri: entry.uri,
    artworkUrl,
    chapters: Array.isArray(meta.chapters) ? meta.chapters : [],
    durationSeconds: Number(meta.durationSeconds) || 0,
  };
}
