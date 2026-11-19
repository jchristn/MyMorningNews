import { ApiError, NetworkError, SessionExpiredError, TenantSelectionRequiredError, TimeoutError } from './errors';

/** Requests give up after this long unless a call passes its own timeout. */
export const DEFAULT_TIMEOUT_MS = 30000;

/** Build a query string, skipping undefined, null, and empty values (same rules as the dashboard client). */
export function buildQuery(query) {
  if (!query) return '';
  const parts = [];
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    const values = Array.isArray(value) ? value : [value];
    for (const v of values) {
      const text = v instanceof Date ? v.toISOString() : String(v);
      parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(text));
    }
  }
  return parts.length ? '?' + parts.join('&') : '';
}

/** Items of a list response: the server's paged `{ items, ... }` shape, or a bare array. */
export function listItems(result) {
  if (Array.isArray(result)) return result;
  if (result && Array.isArray(result.items)) return result.items;
  return [];
}

function parseBody(text) {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

/**
 * The app's HTTP client. Host agnostic: the base URL, the token provider, the refresh function, and fetch are
 * injected, so it runs unchanged in Jest.
 *
 * - `getAccessToken()` returns the bearer token (the auth controller refreshes it first when it is about to expire).
 * - `refreshTokens()` rotates the tokens and resolves true when a retry makes sense. It is called after a 401, and the
 *   auth controller guarantees one refresh in flight at a time.
 * - `onUnauthorized()` runs when a 401 survives the refresh: the session is gone.
 */
export class ApiClient {
  constructor({ baseUrl, getAccessToken, refreshTokens, onUnauthorized, getLocale, fetchImpl, timeoutMs } = {}) {
    if (!baseUrl) throw new Error('ApiClient needs a baseUrl.');
    this.baseUrl = String(baseUrl).replace(/\/+$/, '');
    this.getAccessToken = getAccessToken || (async () => null);
    this.refreshTokens = refreshTokens || null;
    this.onUnauthorized = onUnauthorized || (() => undefined);
    this.getLocale = getLocale || (() => null);
    this.fetchImpl = fetchImpl || ((...args) => fetch(...args));
    this.timeoutMs = timeoutMs || DEFAULT_TIMEOUT_MS;
  }

  /** Absolute URL for a path (used for media sources that the player fetches itself). */
  url(path, query) {
    return this.baseUrl + path + buildQuery(query);
  }

  /**
   * Send a request and return the parsed body (null for 204).
   * @param {string} method
   * @param {string} path Starts with /v1.0/...
   * @param {object} [options] `query`, `body`, `headers`, `auth` (default true), `timeoutMs`, `retryOn401` (default true)
   */
  async request(method, path, options = {}) {
    const { query, body, headers, auth = true, timeoutMs, retryOn401 = true } = options;
    const token = auth ? await this.getAccessToken() : null;
    const response = await this.send(method, path, { query, body, headers, token, timeoutMs });
    if (response.status === 401 && auth && token) {
      if (retryOn401 && this.refreshTokens && (await this.refreshTokens())) {
        return this.request(method, path, { ...options, retryOn401: false });
      }
      this.onUnauthorized();
      throw new SessionExpiredError(response.data);
    }
    return this.unwrap(response);
  }

  get(path, query, options) {
    return this.request('GET', path, { ...options, query });
  }

  post(path, body, options) {
    return this.request('POST', path, { ...options, body });
  }

  put(path, body, options) {
    return this.request('PUT', path, { ...options, body });
  }

  delete(path, options) {
    return this.request('DELETE', path, options);
  }

  /** One HTTP exchange with a timeout. Resolves `{ status, data }`; throws NetworkError when there is no response. */
  async send(method, path, { query, body, headers, token, timeoutMs }) {
    const init = { method, headers: { Accept: 'application/json', ...headers } };
    if (token) init.headers.Authorization = 'Bearer ' + token;
    const locale = this.getLocale();
    if (locale) init.headers['Accept-Language'] = locale;
    if (body !== undefined && body !== null) {
      init.headers['Content-Type'] = 'application/json';
      init.body = typeof body === 'string' ? body : JSON.stringify(body);
    }
    const limit = timeoutMs || this.timeoutMs;
    const controller = typeof AbortController === 'function' ? new AbortController() : null;
    if (controller) init.signal = controller.signal;
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      if (controller) controller.abort();
    }, limit);
    let response;
    try {
      response = await this.fetchImpl(this.url(path, query), init);
    } catch (error) {
      if (timedOut) throw new TimeoutError(limit);
      throw new NetworkError(error && error.message ? error.message : 'Network error', error);
    } finally {
      clearTimeout(timer);
    }
    const text = response.status === 204 ? '' : await response.text();
    return { status: response.status, data: parseBody(text) };
  }

  unwrap({ status, data }) {
    if (status >= 200 && status < 300) return data;
    if (status === 409 && data && typeof data === 'object' && data.error === 'TenantSelectionRequired') {
      throw new TenantSelectionRequiredError(data);
    }
    throw new ApiError(status, data && typeof data === 'object' ? data : null, typeof data === 'string' && data ? data : undefined);
  }
}
