import { routes } from '@/api/routes';
import { briefingAudio, itemTitle } from '@/screens/common/items';

/**
 * A player track for a briefing's audio (plan section 22): streamed from `GET /v1.0/api/media/{id}` with the bearer
 * token (the route serves HTTP Range requests for seeking), or from the downloaded file when one exists. Returns null
 * when the briefing has no audio.
 */
export function briefingTrack(briefing, { client, token, localUri, artworkUrl }) {
  const audio = briefingAudio(briefing);
  if (!audio) return null;
  const chapters = (Array.isArray(audio.chapters) ? audio.chapters : [])
    .map((c) => ({ title: String(c.title || c.heading || ''), startSeconds: Number(c.startSeconds ?? c.start ?? 0) }))
    .filter((c) => Number.isFinite(c.startSeconds));
  return {
    id: 'briefing.' + briefing.id,
    briefingId: briefing.id,
    title: itemTitle(briefing),
    artist: 'My Morning News',
    album: briefing.editionName || undefined,
    uri: localUri || client.url(routes.media(audio.mediaId)),
    headers: localUri || !token ? undefined : { Authorization: 'Bearer ' + token },
    artworkUrl,
    chapters,
    durationSeconds: audio.durationSeconds || 0,
    mediaId: audio.mediaId,
  };
}
