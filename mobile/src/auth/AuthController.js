import { ApiClient } from '@/api/client';
import { ApiError, NetworkError, isNetworkError } from '@/api/errors';
import { authApi } from '@/api/auth';
import { accountApi } from '@/api/account';

/** Refresh this long before the session token's stated expiry, so requests rarely meet a 401. */
export const EARLY_REFRESH_MS = 60000;

/**
 * Session handling for the mobile app (plan section 35, contracts A and B).
 *
 * - Signs in with `POST /v1.0/token` and `x-client: mobile`, keeping the session token and (when the server supports
 *   contract A) the rotating refresh token in secure storage, per server.
 * - Refreshes before expiry and after a 401, with exactly one refresh in flight: refresh tokens rotate and reusing an
 *   old one revokes the session, so concurrent callers share the same promise.
 * - Without a refresh token (servers before contract A) it extends the session with the authenticated
 *   `POST /v1.0/token/refresh` while the token is still valid.
 * - A rejected refresh ends the session with reason `expired`; a network failure keeps it (the next call retries).
 *
 * Host agnostic apart from the injected storage and fetch, so it is unit tested in Node.
 *
 * State: `{ status: 'loading' | 'signedOut' | 'signedIn', serverUrl, user, tenantId, reason }`, where `reason` is
 * `expired` after the server ended the session and null otherwise.
 */
export class AuthController {
  constructor({ serverUrl, storage, getLocale, fetchImpl, now }) {
    this.serverUrl = serverUrl;
    this.storage = storage;
    this.now = now || (() => Date.now());
    this.tokens = null;
    this.listeners = new Set();
    this.refreshing = null;
    this.state = { status: 'loading', serverUrl, user: null, tenantId: null, reason: null };
    this.client = new ApiClient({
      baseUrl: serverUrl,
      getAccessToken: () => this.getAccessToken(),
      refreshTokens: () => this.refresh(),
      onUnauthorized: () => {
        void this.clearSession('expired');
      },
      getLocale,
      fetchImpl,
    });
  }

  getState() {
    return this.state;
  }

  subscribe(listener) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /** True when the server issued a refresh token (contract A). */
  hasRefreshToken() {
    return Boolean(this.tokens && this.tokens.refreshToken);
  }

  /** Load saved tokens and confirm the session with `GET /v1.0/api/me`. */
  async restore() {
    this.tokens = await this.storage.load(this.serverUrl);
    if (!this.tokens) return this.setState({ status: 'signedOut', user: null, tenantId: null, reason: null });
    try {
      const user = await accountApi.getMe(this.client);
      if (this.tokens) await this.persist({ ...this.tokens, user });
      return this.setState({ status: 'signedIn', user, tenantId: this.tokens ? this.tokens.tenantId : null, reason: null });
    } catch (error) {
      if (isNetworkError(error) && this.tokens) {
        // Offline: keep the saved session; screens show the network error and retry.
        return this.setState({ status: 'signedIn', user: this.tokens.user || null, tenantId: this.tokens.tenantId || null, reason: null });
      }
      await this.clearSession(this.tokens ? 'expired' : null);
      return this.state;
    }
  }

  /**
   * Sign in. Throws TenantSelectionRequiredError (contract B) when the account belongs to several tenants; the caller
   * retries with one of `error.tenants`. Throws ApiError for wrong credentials and NetworkError when unreachable.
   */
  async signIn({ email, password, tenantId }) {
    const response = await authApi.signIn(this.client, { email: String(email || '').trim(), password, tenantId: tenantId ? String(tenantId).trim() : '' });
    await this.adopt(response);
    this.setState({ status: 'signedIn', user: response.user || null, tenantId: response.tenantId || null, reason: null });
    return response.user || null;
  }

  /** Revoke the session on the server (best effort) and forget the tokens. */
  async signOut() {
    if (this.tokens) {
      try {
        await authApi.signOut(this.client);
      } catch {
        // Signing out locally always succeeds; the server session expires on its own.
      }
    }
    await this.clearSession(null);
  }

  /** Replace the cached user after a profile change. */
  setUser(user) {
    if (this.state.status !== 'signedIn') return;
    if (this.tokens) void this.persist({ ...this.tokens, user });
    this.setState({ ...this.state, user });
  }

  /** The session token, refreshed first when it is about to expire. */
  async getAccessToken() {
    if (!this.tokens) return null;
    const expires = this.tokens.expiresUtc ? Date.parse(this.tokens.expiresUtc) : NaN;
    if (!Number.isNaN(expires) && expires - EARLY_REFRESH_MS <= this.now()) {
      await this.refresh();
    }
    return this.tokens ? this.tokens.token : null;
  }

  /** Rotate the tokens. Concurrent callers share one request. Resolves false when no retry makes sense. */
  refresh() {
    if (!this.refreshing) {
      this.refreshing = this.doRefresh().finally(() => {
        this.refreshing = null;
      });
    }
    return this.refreshing;
  }

  async doRefresh() {
    const current = this.tokens;
    if (!current) return false;
    const refreshExpires = current.refreshExpiresUtc ? Date.parse(current.refreshExpiresUtc) : NaN;
    if (current.refreshToken && !Number.isNaN(refreshExpires) && refreshExpires <= this.now()) {
      await this.clearSession('expired');
      return false;
    }
    try {
      const response = current.refreshToken
        ? await authApi.refreshWithToken(this.client, current.refreshToken)
        : await authApi.refreshSession(this.client, current.token);
      // The legacy refresh returns no refresh token; never keep a refresh token the server did not just issue.
      await this.adopt(response, current.user);
      return true;
    } catch (error) {
      if (error instanceof NetworkError) return false;
      await this.clearSession('expired');
      return false;
    }
  }

  async adopt(response, previousUser) {
    if (!response || typeof response.token !== 'string' || !response.token) {
      throw new ApiError(500, null, 'The server did not return a session token. Is it a My Morning News server?');
    }
    const tokens = {
      token: response.token,
      expiresUtc: response.expiresUtc || null,
      refreshToken: response.refreshToken || null,
      refreshExpiresUtc: response.refreshExpiresUtc || null,
      sessionId: response.sessionId || null,
      tenantId: response.tenantId || null,
      userId: response.userId || null,
      user: response.user || previousUser || null,
    };
    await this.persist(tokens);
  }

  async persist(tokens) {
    this.tokens = tokens;
    await this.storage.save(this.serverUrl, tokens);
  }

  async clearSession(reason) {
    const hadSession = Boolean(this.tokens);
    // A second clear (for example the client's onUnauthorized after a rejected refresh) keeps the first reason.
    if (!hadSession && this.state.status === 'signedOut') return;
    this.tokens = null;
    await this.storage.save(this.serverUrl, null);
    this.setState({ status: 'signedOut', user: null, tenantId: null, reason: hadSession ? reason : null });
  }

  setState(next) {
    this.state = { ...next, serverUrl: this.serverUrl };
    for (const listener of this.listeners) listener(this.state);
    return this.state;
  }
}
