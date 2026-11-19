// Typed errors for every API call. Screens branch on the class (or the helpers below), never on message text.

/** Any non-2xx response, or a request that never got one (status 0). */
export class ApiError extends Error {
  constructor(status, body, message) {
    super(message || (body && typeof body === 'object' && body.message) || (status ? 'HTTP ' + status : 'Network error'));
    this.name = 'ApiError';
    this.status = status;
    this.body = body && typeof body === 'object' ? body : null;
    this.code = this.body && this.body.error ? this.body.error : null;
    this.requestId = this.body && this.body.requestId ? this.body.requestId : null;
    this.details = this.body && this.body.details ? this.body.details : null;
  }
}

/** The server could not be reached (offline, DNS, TLS, timeout). */
export class NetworkError extends ApiError {
  constructor(message, cause) {
    super(0, null, message || 'Network error');
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

/** The request took longer than the client's timeout. */
export class TimeoutError extends NetworkError {
  constructor(timeoutMs) {
    super('The server did not answer within ' + Math.round(timeoutMs / 1000) + ' seconds.');
    this.name = 'TimeoutError';
  }
}

/** The session is gone (401 after a refresh attempt, or a refresh token that was rejected). */
export class SessionExpiredError extends ApiError {
  constructor(body) {
    super(401, body, 'Your session has expired. Sign in again.');
    this.name = 'SessionExpiredError';
  }
}

/** Contract B: the email and password match several tenants; the client retries with one of `tenants`. */
export class TenantSelectionRequiredError extends ApiError {
  constructor(body) {
    super(409, body, 'Choose a tenant to sign in to.');
    this.name = 'TenantSelectionRequiredError';
    this.tenants = Array.isArray(body && body.tenants) ? body.tenants.filter((t) => t && typeof t.id === 'string') : [];
  }
}

/**
 * True when the route does not exist on this server yet (an older server, or a contract that arrives in a later
 * phase). Screens show "This arrives in a later server update" instead of an error.
 */
export function isNotAvailable(error) {
  return error instanceof ApiError && (error.status === 404 || error.status === 405 || error.status === 501);
}

export function isNetworkError(error) {
  return error instanceof NetworkError || (error instanceof ApiError && error.status === 0);
}

export function isSessionExpired(error) {
  return error instanceof SessionExpiredError;
}

/** Field errors from a 400 ValidationFailed response: `{ fieldName: message }`. */
export function fieldErrors(error) {
  return error instanceof ApiError && error.details && typeof error.details === 'object' ? error.details : {};
}
