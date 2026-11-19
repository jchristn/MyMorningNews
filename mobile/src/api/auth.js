import { routes } from './routes';

/**
 * Session routes. Sign-in sends credentials in headers, like the dashboard. `x-client: mobile` asks for a rotating
 * refresh token (contract A); a server without contract A ignores the header and returns a plain TokenResponse.
 * Leaving out `x-tenant-guid` asks for tenant-less sign-in (contract B): a 409 TenantSelectionRequired lists the
 * tenants, and the client retries with one.
 */
export const authApi = {
  signIn(client, { email, password, tenantId }) {
    const headers = { 'x-email': email, 'x-password': password, 'x-client': 'mobile' };
    if (tenantId) headers['x-tenant-guid'] = tenantId;
    return client.request('POST', routes.token, { headers, auth: false });
  },

  /** Contract A: exchange a refresh token for a new TokenResponse with a rotated refresh token. */
  refreshWithToken(client, refreshToken) {
    return client.request('POST', routes.tokenRefreshToken, { body: { refreshToken }, auth: false });
  },

  /** Existing authenticated refresh: extends the session and returns a new token (the old one stops working). */
  refreshSession(client, token) {
    return client.request('POST', routes.tokenRefresh, { headers: { Authorization: 'Bearer ' + token }, auth: false });
  },

  /** Validate the current token. */
  validate(client) {
    return client.get(routes.token);
  },

  signOut(client) {
    return client.request('DELETE', routes.token, { retryOn401: false });
  },

  /** Planned (Phase 14): public sign-up. 404 until then. */
  signUp(client, body) {
    return client.request('POST', routes.signup, { body, auth: false });
  },

  /** Planned (Phase 14): request a password reset email. 404 until then. */
  forgotPassword(client, email) {
    return client.request('POST', routes.passwordForgot, { body: { email }, auth: false });
  },
};
