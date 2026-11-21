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

  /**
   * Public sign-up: `{ email, password, displayName, timeZone, locale, acceptTerms, confirmAge, promoCode,
   * turnstileToken }`. Answers 202 `{ message, verificationRequired }` whether or not the email has an account, 404
   * `SignupClosed` when sign-up is off, and 400 for validation.
   */
  signUp(client, body) {
    return client.request('POST', routes.signup, { body, auth: false });
  },

  /** Confirm an email address with the token from the emailed link: `{ verified, email }`, or 400 for a bad link. */
  verifyEmail(client, token) {
    return client.request('POST', routes.verifyEmail, { body: { token }, auth: false });
  },

  /** Send a new verification link. Always 202, whether or not the address has an unconfirmed account. */
  resendVerification(client, email) {
    return client.request('POST', routes.verifyEmailResend, { body: { email }, auth: false });
  },

  /** Request a password reset email. Always 202, whether or not the address has an account. */
  forgotPassword(client, email) {
    return client.request('POST', routes.passwordForgot, { body: { email }, auth: false });
  },

  /** Set a new password with the token from the reset email: `{ reset, email }`. Signs out every session. */
  resetPassword(client, token, password) {
    return client.request('POST', routes.passwordReset, { body: { token, password }, auth: false });
  },
};
