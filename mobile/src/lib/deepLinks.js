// Deep and universal links (plan sections 33.3 and 35.5). Accepted forms:
//   mymorningnews://briefings/{id}            custom scheme (host is the first segment)
//   mymorningnews:///briefings/{id}
//   https://app.mymorningnews.ai/briefings/{id}            universal links from email and the web
//   https://app.mymorningnews.ai/dashboard/briefings/{id}  dashboard URLs (all routes live under /dashboard)
//   /briefings/{id}                                        a path from the router
//   mymorningnews://redeem?code=SPRING-2027                promotional codes (also /account/redeem?code=)
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
    case 'today':
    case 'home':
      return '/today';
    case 'sign-in':
    case 'sign-up':
    case 'forgot-password':
    case 'server':
      return '/' + first;
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

/**
 * The route for a tapped push notification. The payload carries `kind` and IDs, never content:
 * `{ kind, briefingId | topicId | predictionId }`.
 */
export function routeForNotification(data) {
  const d = data && typeof data === 'object' ? data : {};
  const str = (v) => (typeof v === 'string' && v.length > 0 ? v : null);
  if (str(d.briefingId)) return '/today/briefings/' + enc(d.briefingId);
  if (str(d.predictionId)) return '/today/predictions/' + enc(d.predictionId);
  if (str(d.topicId)) return '/topics/' + enc(d.topicId);
  const kind = str(d.kind) || '';
  if (kind.startsWith('account') || kind === 'cap.reached' || kind === 'test') return '/account';
  if (kind.startsWith('prediction')) return '/today/predictions';
  if (kind.startsWith('briefing')) return '/today';
  if (str(d.url)) return mapDeepLink(d.url);
  return null;
}
