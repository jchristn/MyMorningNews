import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import { briefingsApi } from '@/api/research';
import { routes } from '@/api/routes';

export const FILE_TYPES = Object.freeze({
  md: { mimeType: 'text/markdown', uti: 'net.daringfireball.markdown', extension: '.md', kind: 'BriefingMarkdown' },
  html: { mimeType: 'text/html', uti: 'public.html', extension: '.html', kind: 'BriefingHtml' },
});

/** A safe local file name for a briefing download: the server's file name, or one built from the title. */
export function briefingFileName(detail, format) {
  const type = FILE_TYPES[format] || FILE_TYPES.md;
  const stored = ((detail && detail.files) || []).find((f) => f && f.kind === type.kind);
  if (stored && stored.fileName) return String(stored.fileName).replace(/[^A-Za-z0-9._-]/g, '_');
  const title = (detail && detail.briefing && detail.briefing.title) || 'briefing';
  const slug = String(title)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);
  return (slug || 'briefing') + type.extension;
}

/**
 * Download a briefing's `.md` or `.html` file (`GET /v1.0/api/briefings/{id}/download?format=`) into the cache with
 * the bearer token, then open the share sheet so the user can save it to Files, AirDrop it, or open it in another app.
 * The HTML file is never rendered inside the app. Resolves `{ uri, shared }`.
 *
 * A failed download is probed through the API client so the caller gets a typed error (410 ArtifactExpired when the
 * file passed retention, 409 while the briefing is still composing). Dependencies are injectable for tests.
 */
export async function shareBriefingFile({ client, token, detail, format, fs = { File, Paths }, sharing = Sharing }) {
  const type = FILE_TYPES[format] || FILE_TYPES.md;
  const id = detail.briefing.id;
  const target = new fs.File(fs.Paths.cache, briefingFileName(detail, format));
  if (target.exists) target.delete();
  let file;
  try {
    file = await fs.File.downloadFileAsync(briefingsApi.downloadUrl(client, id, format), target, { headers: token ? { Authorization: 'Bearer ' + token } : {}, idempotent: true });
  } catch (error) {
    // The download API reports HTTP failures without the body; ask the API for the typed error.
    await client.get(routes.briefingDownload(id), { format });
    throw error;
  }
  if (!(await sharing.isAvailableAsync())) return { uri: file.uri, shared: false };
  await sharing.shareAsync(file.uri, { mimeType: type.mimeType, UTI: type.uti, dialogTitle: detail.briefing.title });
  return { uri: file.uri, shared: true };
}
