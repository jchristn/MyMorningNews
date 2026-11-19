// The only HTTP client in the dashboard. Views call named methods; raw fetch handling stays here and in the API
// Explorer. Base URLs use 127.0.0.1 rather than localhost.
export const DEFAULT_SERVER_URL = 'http://127.0.0.1:12100';

export class ApiError extends Error {
  constructor(status, body, message) {
    super(message || (body && body.message) || 'HTTP ' + status);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
    this.code = body && body.error ? body.error : null;
    this.requestId = body && body.requestId ? body.requestId : null;
    this.details = body && body.details ? body.details : null;
  }
}

// Build a query string, skipping undefined, null, and empty values.
export function buildQuery(query) {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) value.forEach((v) => params.append(key, String(v)));
    else params.append(key, value instanceof Date ? value.toISOString() : String(value));
  }
  const s = params.toString();
  return s ? '?' + s : '';
}

// Paging, sort, search, and filters from a table state object.
export function pagedQuery({ pageNumber, pageSize, sortBy, sortDirection, search, filters } = {}) {
  return {
    pageNumber,
    pageSize,
    sortBy,
    sortDirection: sortDirection === 'asc' ? 'Ascending' : sortDirection === 'desc' ? 'Descending' : undefined,
    search,
    ...(filters || {})
  };
}

const enc = encodeURIComponent;

export class ApiClient {
  constructor(baseUrl, token = null, options = {}) {
    this.baseUrl = String(baseUrl || DEFAULT_SERVER_URL).replace(/\/+$/, '');
    this.token = token;
    this.getLocale = options.getLocale || (() => undefined);
    this.fetchImpl = options.fetch || ((...args) => fetch(...args));
  }

  _headers(extra = {}) {
    const headers = { Accept: 'application/json' };
    if (this.token) headers.Authorization = 'Bearer ' + this.token;
    const locale = this.getLocale();
    if (locale) headers['Accept-Language'] = locale;
    return { ...headers, ...extra };
  }

  async _request(method, path, { query, body, headers, signal } = {}) {
    const url = this.baseUrl + path + buildQuery(query);
    const init = { method, headers: this._headers(headers), signal };
    if (body !== undefined && body !== null) {
      init.headers['Content-Type'] = 'application/json';
      init.body = typeof body === 'string' ? body : JSON.stringify(body);
    }
    let response;
    try {
      response = await this.fetchImpl(url, init);
    } catch (err) {
      if (err && err.name === 'AbortError') throw err;
      throw new ApiError(0, null, err && err.message ? err.message : 'Network error');
    }
    const text = response.status === 204 ? '' : await response.text();
    let data = null;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        data = text;
      }
    }
    if (response.status === 401 && this.token && typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('auth:unauthorized', { detail: { path } }));
    }
    if (!response.ok) throw new ApiError(response.status, data && typeof data === 'object' ? data : null, typeof data === 'string' ? data : undefined);
    return data;
  }

  get(path, query, signal) {
    return this._request('GET', path, { query, signal });
  }

  // Sessions
  login(email, password, tenantId) {
    return this._request('POST', '/v1.0/token', { headers: { 'x-email': email, 'x-password': password, 'x-tenant-guid': tenantId } });
  }
  validateToken() {
    return this._request('GET', '/v1.0/token');
  }
  getTokenDetails() {
    return this._request('GET', '/v1.0/token/details');
  }
  refreshToken() {
    return this._request('POST', '/v1.0/token/refresh');
  }
  logout() {
    return this._request('DELETE', '/v1.0/token');
  }

  // Health, server
  healthCheck(signal) {
    return this._request('GET', '/v1.0/api/health', { signal });
  }
  getServerInfo() {
    return this._request('GET', '/v1.0/api/server-info');
  }
  getOpenApiSpec() {
    return this._request('GET', '/openapi.json');
  }

  // Profile
  getMe() {
    return this._request('GET', '/v1.0/api/me');
  }
  updateMe(body) {
    return this._request('PUT', '/v1.0/api/me', { body });
  }
  changePassword(currentPassword, newPassword) {
    return this._request('PUT', '/v1.0/api/me/password', { body: { currentPassword, newPassword } });
  }

  // Tenants
  listTenants(query) {
    return this._request('GET', '/v1.0/tenants', { query });
  }
  getTenant(id) {
    return this._request('GET', '/v1.0/tenants/' + enc(id));
  }
  createTenant(body) {
    return this._request('POST', '/v1.0/tenants', { body });
  }
  updateTenant(id, body) {
    return this._request('PUT', '/v1.0/tenants/' + enc(id), { body });
  }
  deleteTenant(id) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(id));
  }

  // Users
  listUsers(tenantId, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/users', { query });
  }
  getUser(tenantId, id) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(id));
  }
  createUser(tenantId, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/users', { body });
  }
  updateUser(tenantId, id, body) {
    return this._request('PUT', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(id), { body });
  }
  deleteUser(tenantId, id) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(id));
  }
  getUserPermissions(tenantId, id) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(id) + '/permissions');
  }

  // Credentials
  listCredentials(tenantId, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/credentials', { query });
  }
  getCredential(tenantId, id) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id));
  }
  createCredential(tenantId, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/credentials', { body });
  }
  updateCredential(tenantId, id, body) {
    return this._request('PUT', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id), { body });
  }
  deleteCredential(tenantId, id) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id));
  }
  getCredentialPermissions(tenantId, id) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id) + '/permissions');
  }
  listCredentialScopes(tenantId, id, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id) + '/scopes', { query });
  }
  createCredentialScope(tenantId, id, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id) + '/scopes', { body });
  }
  deleteCredentialScope(tenantId, id, scopeId) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/credentials/' + enc(id) + '/scopes/' + enc(scopeId));
  }

  // Roles, permissions, assignments
  listRoles(tenantId, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/roles', { query });
  }
  getRole(tenantId, id) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/roles/' + enc(id));
  }
  createRole(tenantId, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/roles', { body });
  }
  updateRole(tenantId, id, body) {
    return this._request('PUT', '/v1.0/tenants/' + enc(tenantId) + '/roles/' + enc(id), { body });
  }
  deleteRole(tenantId, id) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/roles/' + enc(id));
  }
  listPermissions(tenantId, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/permissions', { query });
  }
  createPermission(tenantId, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/permissions', { body });
  }
  updatePermission(tenantId, id, body) {
    return this._request('PUT', '/v1.0/tenants/' + enc(tenantId) + '/permissions/' + enc(id), { body });
  }
  deletePermission(tenantId, id) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/permissions/' + enc(id));
  }
  listAssignments(tenantId, userId, query) {
    return this._request('GET', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(userId) + '/assignments', { query });
  }
  createAssignment(tenantId, userId, body) {
    return this._request('POST', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(userId) + '/assignments', { body });
  }
  deleteAssignment(tenantId, userId, assignmentId) {
    return this._request('DELETE', '/v1.0/tenants/' + enc(tenantId) + '/users/' + enc(userId) + '/assignments/' + enc(assignmentId));
  }

  // Audit
  listAudit(query) {
    return this._request('GET', '/v1.0/api/audit', { query });
  }
  getAudit(id) {
    return this._request('GET', '/v1.0/api/audit/' + enc(id));
  }
  deleteAudit(id) {
    return this._request('DELETE', '/v1.0/api/audit/' + enc(id));
  }

  // Request history
  getRequestHistory(filters) {
    return this._request('GET', '/v1.0/api/request-history', { query: filters });
  }
  getRequestHistorySummary(filters) {
    return this._request('GET', '/v1.0/api/request-history/summary', { query: filters });
  }
  getRequestHistoryEntry(id) {
    return this._request('GET', '/v1.0/api/request-history/' + enc(id));
  }
  deleteRequestHistoryEntry(id) {
    return this._request('DELETE', '/v1.0/api/request-history/' + enc(id));
  }
  deleteRequestHistoryBulk(filters) {
    return this._request('DELETE', '/v1.0/api/request-history', { query: filters });
  }

  // Settings
  getSettings() {
    return this._request('GET', '/v1.0/api/settings');
  }
  updateSettings(settings) {
    return this._request('PUT', '/v1.0/api/settings', { body: settings });
  }
  restartServer() {
    return this._request('POST', '/v1.0/api/settings/restart');
  }

  // API Explorer: returns the raw Response so the explorer can show status, headers, timing, and streams.
  async executeExplorer({ method, path, query, headers, body, signal }) {
    const url = this.baseUrl + path + buildQuery(query);
    const init = { method, headers: this._headers(headers || {}), signal };
    if (body !== undefined && body !== null && body !== '' && method !== 'GET' && method !== 'HEAD') {
      init.headers['Content-Type'] = init.headers['Content-Type'] || 'application/json';
      init.body = body;
    }
    return this.fetchImpl(url, init);
  }
}
