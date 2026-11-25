// Deep and universal links (plan sections 33.3 and 35.5). Accepted forms:
//   mymorningnews://briefings/{id}            custom scheme (host is the first segment)
//   mymorningnews:///briefings/{id}
//   https://app.mymorningnews.ai/briefings/{id}            universal links from email and the web
//   https://app.mymorningnews.ai/dashboard/briefings/{id}  dashboard URLs (all routes live under /dashboard)
//   /briefings/{id}                                        a path from the router
//   mymorningnews://redeem?code=SPRING-2027                promotional codes (also /account/redeem?code=)
//   https://app.mymorningnews.ai/verify?token=...          the verification email (also /reset-password?token=)
//   https://app.mymorningnews.ai/signup?code=...           the web sign-up page, with an optional promotional code
// They map onto the app's routes. Anything unknown returns null and the caller falls back to Today.

export const APP_HOST = 'app.mymorningnews.ai';
export const SCHEME = 'mymorningnews';

/** Path segments of a link, or null when it is not one of ours. */
export function linkSegments(input) {
  if (!input || typeof input !== 'string') return null;
  let rest = input.trim();
  const scheme = rest.match(/^([a-z][a-z0-9+.-]*):\/\/(.*)$/i);
  if (scheme) {
    const protocol = scheme[1].toLowerCase();
    rest = scheme[2];
    if (protocol === SCHEME) {
      // mymorningnews://briefings/x has host "briefings"; mymorningnews:///briefings/x has an empty host.
    } else if (protocol === 'https' || protocol === 'http') {
      const slash = rest.indexOf('/');
      const host = (slash < 0 ? rest : rest.slice(0, slash)).toLowerCase().split(':')[0];
      if (host !== APP_HOST) return null;
      rest = slash < 0 ? '' : rest.slice(slash + 1);
    } else {
      return null;
    }
  }
  rest = rest.split('#')[0].split('?')[0];
  const segments = rest
    .split('/')
    .filter(Boolean)
    .map((s) => {
      try {
        return decodeURIComponent(s);
      } catch {
        return s;
      }
    });
  if (segments[0] === 'dashboard') segments.shift();
  if (segments[0] === '--') segments.shift();
  return segments;
}

const enc = encodeURIComponent;

/** Query parameters of a link (`?a=1&b=2`, before any `#`), decoded. */
export function linkQuery(input) {
  const query = {};
  if (!input || typeof input !== 'string') return query;
  const q = input.split('#')[0].split('?')[1];
  if (!q) return query;
  for (const pair of q.split('&')) {
    if (!pair) continue;
    const [k, v = ''] = pair.split('=');
    try {
      query[decodeURIComponent(k)] = decodeURIComponent(v.replace(/\+/g, ' '));
    } catch {
      query[k] = v;
    }
  }
  return query;
}

/** The Redeem screen, with the code from the link when it looks like one (letters, digits, and dashes, up to 40). */
function redeemRoute(input) {
  const code = String(linkQuery(input).code || '')
    .trim()
    .toUpperCase();
  return /^[A-Z0-9-]{1,40}$/.test(code) ? '/account/redeem?code=' + enc(code) : '/account/redeem';
}

/** A link token from an email: base64url from the server, so letters, digits, dash, underscore (and . or ~). */
const LINK_TOKEN = /^[A-Za-z0-9_.~-]{1,512}$/;

/** The verify email or reset password screen, with the token from the link when it looks like one. */
function tokenRoute(route, input) {
  const token = String(linkQuery(input).token || '').trim();
  return LINK_TOKEN.test(token) ? route + '?token=' + enc(token) : route;
}

/** The sign-up screen, with a promotional code from the link when it looks like one. */
function signUpRoute(input) {
  const code = String(linkQuery(input).code || '')
    .trim()
    .toUpperCase();
  return /^[A-Z0-9-]{1,40}$/.test(code) ? '/sign-up?code=' + enc(code) : '/sign-up';
}

/** Tabs whose nested paths are app routes already (for example /today/briefings/{id}, /account/profile). */
const APP_ROUTE_ROOTS = ['today', 'listen', 'account', 'insights'];

/** The app route for a link, or null. */
export function mapDeepLink(input) {
  const segments = linkSegments(input);
  if (!segments) return null;
  const [first, second, third] = segments;
  if (!first) return '/today';
  if (first === 'redeem' || first === 'promo' || (first === 'account' && second === 'redeem')) return redeemRoute(input);
  // The app's own routes pass through unchanged.
  if (APP_ROUTE_ROOTS.includes(first) && second && first !== 'insights') return '/' + segments.map(enc).join('/');
  switch (first) {
    case 'catalog':
      return second && third === 'issues' && segments[3] ? '/today/catalog/' + enc(second) + '/' + enc(segments[3]) : '/today/catalog';
    case 'today':
    case 'home':
      return '/today';
    case 'sign-in':
    case 'forgot-password':
    case 'server':
      return '/' + first;
    case 'login':
      return '/sign-in';
    case 'sign-up':
    case 'signup':
      return signUpRoute(input);
    case 'verify':
    case 'verify-email':
      return tokenRoute('/verify', input);
    case 'reset-password':
      return tokenRoute('/reset-password', input);
    case 'briefings':
      return second ? '/today/briefings/' + enc(second) : '/today/briefings';
    case 'findings':
      return second ? '/today/findings/' + enc(second) : '/today/findings';
    case 'predictions':
      return second ? '/today/predictions/' + enc(second) : '/today/predictions';
    case 'suggestions':
      return '/today/suggestions';
    case 'topics':
      if (second === 'new') return '/topics/new';
      if (second && third === 'refine') return '/topics/new?topicId=' + enc(second);
      return second ? '/topics/' + enc(second) : '/topics';
    case 'editions':
      return second ? '/topics/editions/' + enc(second) : '/topics/editions';
    case 'runs':
      return second ? '/topics/runs/' + enc(second) : '/topics';
    case 'one-time':
      if (second === 'new') return '/today/new-request';
      return second ? '/today/requests/' + enc(second) : '/today/requests';
    case 'listen':
    case 'podcast':
      return '/listen';
    case 'insights':
      return third || !second ? '/insights' : ['relevance', 'accuracy', 'feedback', 'preferences', 'usage'].includes(second) ? '/insights/' + second : '/insights';
    case 'account':
    case 'profile':
      return second === 'delete' ? '/account/delete' : '/account';
    case 'usage':
      return '/insights/usage';
    default:
      return null;
  }
}

/** The full player for one episode: it loads and plays that briefing's audio when it is not the current track. */
export function playerRoute(briefingId, mediaAssetId) {
  return '/listen/player?mediaId=' + enc(mediaAssetId) + '&briefingId=' + enc(briefingId);
}

/**
 * The route for a tapped push notification. The payload carries `kind` and IDs, never content:
 * `{ kind, briefingId | mediaAssetId | runId | topicId | predictionId }`. `audio.ready` opens the player on that
 * briefing's audio and `run.results` opens the run; every other kind goes to the item it names. `topic.stale`
 * (`{ reviewId, notice: Prompt | Reminder | Deleted, topicId? }`) opens the topic, where its keep-or-delete review
 * shows, or the topics list once the topic was deleted (no topicId).
 */
export function routeForNotification(data) {
  const d = data && typeof data === 'object' ? data : {};
  const str = (v) => (typeof v === 'string' && v.length > 0 ? v : null);
  const kind = str(d.kind) || '';
  if (str(d.catalogPublicationId) && str(d.briefingId)) return '/today/catalog/' + enc(d.catalogPublicationId) + '/' + enc(d.briefingId);
  if (kind === 'audio.ready' && str(d.briefingId) && str(d.mediaAssetId)) return playerRoute(d.briefingId, d.mediaAssetId);
  if (kind === 'run.results' && str(d.runId)) return '/topics/runs/' + enc(d.runId);
  if (kind === 'topic.stale') return str(d.topicId) && d.notice !== 'Deleted' ? '/topics/' + enc(d.topicId) : '/topics';
  if (str(d.briefingId)) return '/today/briefings/' + enc(d.briefingId);
  if (str(d.predictionId)) return '/today/predictions/' + enc(d.predictionId);
  if (str(d.topicId)) return '/topics/' + enc(d.topicId);
  if (kind.startsWith('account') || kind === 'cap.reached' || kind === 'test') return '/account';
  if (kind.startsWith('prediction')) return '/today/predictions';
  if (kind.startsWith('briefing') || kind.startsWith('audio')) return '/today';
  if (kind.startsWith('run') || kind.startsWith('topic')) return '/topics';
  if (str(d.url)) return mapDeepLink(d.url);
  return null;
}

/** Push payload kinds for the in-app notification kinds (NotificationKindEnum). */
const PUSH_KINDS = { RunResults: 'run.results', BriefingReady: 'briefing.ready', AudioReady: 'audio.ready', RunFailed: 'run.failed', CapReached: 'cap.reached', PredictionDue: 'prediction.due', TopicStale: 'topic.stale' };

/** The route for an in-app notification (`{ kind, topicId?, runId?, briefingId?, mediaAssetId?, predictionId? }`): where its push opens, except that a failed run opens the run itself and a plan limit opens Your usage. */
export function routeForInApp(notification) {
  const n = notification && typeof notification === 'object' ? notification : {};
  const kind = PUSH_KINDS[n.kind];
  if (!kind) return null;
  if (n.kind === 'CapReached') return '/account/usage';
  if (n.kind === 'RunFailed' && typeof n.runId === 'string' && n.runId) return '/topics/runs/' + enc(n.runId);
  return routeForNotification({ kind, catalogPublicationId: n.catalogPublicationId, topicId: n.topicId, runId: n.runId, briefingId: n.briefingId, mediaAssetId: n.mediaAssetId, predictionId: n.predictionId });
}
