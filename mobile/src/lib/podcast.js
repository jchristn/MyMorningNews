// Private podcast feed links (plan section 22). Pure, so the tests cover it directly.

/**
 * The link that hands a feed to a podcast app: Apple Podcasts on iOS takes `podcast://` in place of `https://`;
 * Android podcast apps (Pocket Casts, AntennaPod, Podcast Addict, and others) register `pcast://`. Returns null for
 * anything that is not an http or https URL.
 */
export function podcastAppUrl(feedUrl, platform) {
  const match = /^https?:\/\/(.+)$/i.exec(String(feedUrl || '').trim());
  if (!match) return null;
  return (platform === 'android' ? 'pcast://' : 'podcast://') + match[1];
}

/**
 * Why podcast apps may fail to reach a feed: `local` for a loopback, private, single-label, or `.local` host, `http`
 * for plain HTTP, `invalid` for something that is not a URL, or null when it looks reachable.
 */
export function feedReachabilityWarning(feedUrl) {
  const match = /^(https?):\/\/([^/:]+)/i.exec(String(feedUrl || '').trim());
  if (!match) return 'invalid';
  const host = match[2].toLowerCase();
  if (/^127\./.test(host) || host === '[::1]' || !host.includes('.') || /^10\./.test(host) || /^192\.168\./.test(host) || /^172\.(1[6-9]|2\d|3[01])\./.test(host) || host.endsWith('.local')) return 'local';
  if (match[1].toLowerCase() === 'http') return 'http';
  return null;
}

/** Active tokens first (newest first), then revoked ones. */
export function sortFeedTokens(tokens) {
  const list = Array.isArray(tokens) ? tokens.slice() : [];
  return list.sort((a, b) => Number(Boolean(a.revokedUtc)) - Number(Boolean(b.revokedUtc)) || String(b.createdUtc || '').localeCompare(String(a.createdUtc || '')));
}
