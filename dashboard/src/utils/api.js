// The only HTTP client in the dashboard. Views call named methods; raw fetch handling stays here and in the API
// Explorer. Base URLs use 127.0.0.1 rather than localhost.
import { createSseParser } from './sse.js';

export const DEFAULT_SERVER_URL = 'http://127.0.0.1:12100';

// The server to suggest: the page's own origin when the dashboard is served over HTTPS (the hosted service puts the
// API and the dashboard on one origin), otherwise the local default.
export function defaultServerUrl() {
  if (typeof window !== 'undefined' && window.location && window.location.protocol === 'https:') return window.location.origin;
  return DEFAULT_SERVER_URL;
}

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

// The file name in a Content-Disposition header (filename* first, then filename), or null.
export function filenameFromDisposition(header) {
  if (!header) return null;
  const star = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) {
    try {
      return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
    } catch {
      return star[1].trim().replace(/^"|"$/g, '');
    }
  }
  const plain = /filename\s*=\s*("([^"]*)"|[^;]+)/i.exec(header);
  if (!plain) return null;
  return (plain[2] !== undefined ? plain[2] : plain[1]).trim() || null;
}

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

  async _request(method, path, { query, body, headers, signal, cache } = {}) {
    const url = this.baseUrl + path + buildQuery(query);
    const init = { method, headers: this._headers(headers), signal };
    if (cache) init.cache = cache;
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

  // A file download: resolves to { blob, filename, contentType, status }. Error bodies are parsed as JSON when they
  // can be, so a 410 ArtifactExpired carries its stable code.
  async _requestBlob(method, path, { query, headers, body, signal } = {}) {
    const url = this.baseUrl + path + buildQuery(query);
    const init = { method, headers: this._headers({ Accept: '*/*', ...(headers || {}) }), signal };
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
    if (!response.ok) {
      const text = await response.text();
      let data = null;
      try {
        data = text ? JSON.parse(text) : null;
      } catch {
        data = null;
      }
      if (response.status === 401 && this.token && typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('auth:unauthorized', { detail: { path } }));
      }
      throw new ApiError(response.status, data && typeof data === 'object' ? data : null, data ? undefined : text || undefined);
    }
    const header = (name) => (response.headers && typeof response.headers.get === 'function' ? response.headers.get(name) : null);
    return { blob: await response.blob(), filename: filenameFromDisposition(header('Content-Disposition')), contentType: header('Content-Type'), status: response.status };
  }

  get(path, query, signal) {
    return this._request('GET', path, { query, signal });
  }

  // Sessions
  // Without a tenant, a single matching tenant signs in; several return 409 with the tenant list.
  login(email, password, tenantId) {
    const headers = { 'x-email': email, 'x-password': password };
    if (tenantId) headers['x-tenant-guid'] = tenantId;
    return this._request('POST', '/v1.0/token', { headers });
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

  // Credential vault. Reads never return secrets; reveal is audited and its response must not be kept.
  listVaultCredentials(query) {
    return this._request('GET', '/v1.0/api/vault/credentials', { query });
  }
  getVaultCredential(id) {
    return this._request('GET', '/v1.0/api/vault/credentials/' + enc(id));
  }
  createVaultCredential(body) {
    return this._request('POST', '/v1.0/api/vault/credentials', { body });
  }
  updateVaultCredential(id, body) {
    return this._request('PUT', '/v1.0/api/vault/credentials/' + enc(id), { body });
  }
  deleteVaultCredential(id) {
    return this._request('DELETE', '/v1.0/api/vault/credentials/' + enc(id));
  }
  revealVaultCredential(id) {
    return this._request('POST', '/v1.0/api/vault/credentials/' + enc(id) + '/reveal', { cache: 'no-store' });
  }
  getVaultCredentialUsage(id) {
    return this._request('GET', '/v1.0/api/vault/credentials/' + enc(id) + '/usage');
  }

  // Model endpoints, task role assignments, and the model call log
  listModelEndpoints(query) {
    return this._request('GET', '/v1.0/api/model-endpoints', { query });
  }
  getModelEndpoint(id) {
    return this._request('GET', '/v1.0/api/model-endpoints/' + enc(id));
  }
  createModelEndpoint(body) {
    return this._request('POST', '/v1.0/api/model-endpoints', { body });
  }
  updateModelEndpoint(id, body) {
    return this._request('PUT', '/v1.0/api/model-endpoints/' + enc(id), { body });
  }
  deleteModelEndpoint(id) {
    return this._request('DELETE', '/v1.0/api/model-endpoints/' + enc(id));
  }
  validateModelEndpoint(id) {
    return this._request('POST', '/v1.0/api/model-endpoints/' + enc(id) + '/validate');
  }
  listEndpointModels(id) {
    return this._request('GET', '/v1.0/api/model-endpoints/' + enc(id) + '/models');
  }
  testModelEndpoint(id, body) {
    return this._request('POST', '/v1.0/api/model-endpoints/' + enc(id) + '/test', { body: body || {} });
  }
  listModelAssignments(query) {
    return this._request('GET', '/v1.0/api/model-assignments', { query });
  }
  upsertModelAssignment(body) {
    return this._request('PUT', '/v1.0/api/model-assignments', { body });
  }
  deleteModelAssignment(id) {
    return this._request('DELETE', '/v1.0/api/model-assignments/' + enc(id));
  }
  listLlmCalls(query) {
    return this._request('GET', '/v1.0/api/llm-calls', { query });
  }
  getLlmCall(id) {
    return this._request('GET', '/v1.0/api/llm-calls/' + enc(id));
  }

  // Prompt templates
  listPrompts(query) {
    return this._request('GET', '/v1.0/api/prompts', { query });
  }
  getPrompt(id) {
    return this._request('GET', '/v1.0/api/prompts/' + enc(id));
  }
  savePrompt(id, content, changeNote) {
    return this._request('PUT', '/v1.0/api/prompts/' + enc(id), { body: { content, changeNote: changeNote || null } });
  }
  deletePrompt(id) {
    return this._request('DELETE', '/v1.0/api/prompts/' + enc(id));
  }
  listPromptVersions(id) {
    return this._request('GET', '/v1.0/api/prompts/' + enc(id) + '/versions');
  }
  rollbackPrompt(id, version) {
    return this._request('POST', '/v1.0/api/prompts/' + enc(id) + '/rollback', { query: { version } });
  }
  resetPrompt(id) {
    return this._request('POST', '/v1.0/api/prompts/' + enc(id) + '/reset');
  }
  createPromptOverride(body) {
    return this._request('POST', '/v1.0/api/prompts/overrides', { body });
  }
  runPromptLab(body) {
    return this._request('POST', '/v1.0/api/prompts/lab', { body });
  }

  // Usage and cost analytics
  getUsageReport(query) {
    return this._request('GET', '/v1.0/api/usage/report', { query });
  }
  getUsageTimeseries(query) {
    return this._request('GET', '/v1.0/api/usage/timeseries', { query });
  }
  getMyUsage(query) {
    return this._request('GET', '/v1.0/api/usage/me', { query });
  }

  // Topics with their dimensions, constraints, and email recipients
  listTopics(query) {
    return this._request('GET', '/v1.0/api/topics', { query });
  }
  getTopic(id) {
    return this._request('GET', '/v1.0/api/topics/' + enc(id));
  }
  createTopic(body) {
    return this._request('POST', '/v1.0/api/topics', { body });
  }
  updateTopic(id, body) {
    return this._request('PUT', '/v1.0/api/topics/' + enc(id), { body });
  }
  deleteTopic(id) {
    return this._request('DELETE', '/v1.0/api/topics/' + enc(id));
  }
  pauseTopic(id) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/pause');
  }
  resumeTopic(id) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/resume');
  }
  archiveTopic(id) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/archive');
  }
  duplicateTopic(id) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/duplicate');
  }
  refineTopic(id) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/refine');
  }
  listTopicDimensions(topicId) {
    return this._request('GET', '/v1.0/api/topics/' + enc(topicId) + '/dimensions');
  }
  createTopicDimension(topicId, body) {
    return this._request('POST', '/v1.0/api/topics/' + enc(topicId) + '/dimensions', { body });
  }
  updateTopicDimension(topicId, dimensionId, body) {
    return this._request('PUT', '/v1.0/api/topics/' + enc(topicId) + '/dimensions/' + enc(dimensionId), { body });
  }
  deleteTopicDimension(topicId, dimensionId) {
    return this._request('DELETE', '/v1.0/api/topics/' + enc(topicId) + '/dimensions/' + enc(dimensionId));
  }
  listTopicConstraints(topicId, kind) {
    return this._request('GET', '/v1.0/api/topics/' + enc(topicId) + '/constraints', { query: { kind } });
  }
  createTopicConstraint(topicId, body) {
    return this._request('POST', '/v1.0/api/topics/' + enc(topicId) + '/constraints', { body });
  }
  updateTopicConstraint(topicId, constraintId, body) {
    return this._request('PUT', '/v1.0/api/topics/' + enc(topicId) + '/constraints/' + enc(constraintId), { body });
  }
  deleteTopicConstraint(topicId, constraintId) {
    return this._request('DELETE', '/v1.0/api/topics/' + enc(topicId) + '/constraints/' + enc(constraintId));
  }
  listTopicRecipients(topicId) {
    return this._request('GET', '/v1.0/api/topics/' + enc(topicId) + '/recipients');
  }
  addTopicRecipient(topicId, body) {
    return this._request('POST', '/v1.0/api/topics/' + enc(topicId) + '/recipients', { body });
  }
  deleteTopicRecipient(topicId, recipientId) {
    return this._request('DELETE', '/v1.0/api/topics/' + enc(topicId) + '/recipients/' + enc(recipientId));
  }

  // Refinement wizard. Generation steps call a model and can take tens of seconds; pass a signal to stop waiting.
  startRefinement(body, signal) {
    return this._request('POST', '/v1.0/api/refinement-sessions', { body, signal });
  }
  listRefinementSessions(query) {
    return this._request('GET', '/v1.0/api/refinement-sessions', { query });
  }
  getRefinementSession(id) {
    return this._request('GET', '/v1.0/api/refinement-sessions/' + enc(id));
  }
  proposeDimensions(id, guidance, signal) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/dimensions', { body: guidance ? { guidance } : {}, signal });
  }
  askQuestions(id, guidance, signal) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/questions', { body: guidance ? { guidance } : {}, signal });
  }
  answerQuestion(id, questionId, body) {
    return this._request('PUT', '/v1.0/api/refinement-sessions/' + enc(id) + '/questions/' + enc(questionId), { body });
  }
  draftContext(id, signal) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/context', { signal });
  }
  suggestSources(id, signal) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/sources', { signal });
  }
  acceptSources(id, body) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/sources/accept', { body });
  }
  completeRefinement(id) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/complete');
  }
  abandonRefinement(id) {
    return this._request('POST', '/v1.0/api/refinement-sessions/' + enc(id) + '/abandon');
  }

  // One-time requests
  createResearchRequest(body, signal) {
    return this._request('POST', '/v1.0/api/requests', { body, signal });
  }
  listResearchRequests(query) {
    return this._request('GET', '/v1.0/api/requests', { query });
  }
  getResearchRequest(id) {
    return this._request('GET', '/v1.0/api/requests/' + enc(id));
  }
  answerResearchQuestion(id, questionId, body) {
    return this._request('PUT', '/v1.0/api/requests/' + enc(id) + '/questions/' + enc(questionId), { body });
  }
  replanResearchRequest(id, signal) {
    return this._request('POST', '/v1.0/api/requests/' + enc(id) + '/replan', { signal });
  }
  confirmResearchRequest(id, runAtUtc) {
    return this._request('POST', '/v1.0/api/requests/' + enc(id) + '/confirm', { body: runAtUtc ? { runAtUtc } : {} });
  }
  cancelResearchRequest(id) {
    return this._request('POST', '/v1.0/api/requests/' + enc(id) + '/cancel');
  }
  convertResearchRequest(id) {
    return this._request('POST', '/v1.0/api/requests/' + enc(id) + '/convert');
  }
  deleteResearchRequest(id) {
    return this._request('DELETE', '/v1.0/api/requests/' + enc(id));
  }

  // Schedules and runs
  previewSchedule(body, signal) {
    return this._request('POST', '/v1.0/api/schedules/preview', { body, signal });
  }
  listSchedules(query) {
    return this._request('GET', '/v1.0/api/schedules', { query });
  }
  getSchedule(id) {
    return this._request('GET', '/v1.0/api/schedules/' + enc(id));
  }
  createSchedule(body) {
    return this._request('POST', '/v1.0/api/schedules', { body });
  }
  updateSchedule(id, body) {
    return this._request('PUT', '/v1.0/api/schedules/' + enc(id), { body });
  }
  deleteSchedule(id) {
    return this._request('DELETE', '/v1.0/api/schedules/' + enc(id));
  }
  listRuns(query) {
    return this._request('GET', '/v1.0/api/runs', { query });
  }
  getRun(id) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id));
  }
  cancelRun(id) {
    return this._request('POST', '/v1.0/api/runs/' + enc(id) + '/cancel');
  }
  // Retry a finished run (202 with a Run; 409 while it is not finished). fromStage Synthesize or later re-queues the
  // same run from that stage; no stage or an earlier one queues a new run of the topic.
  retryRun(id, fromStage) {
    return this._request('POST', '/v1.0/api/runs/' + enc(id) + '/retry', { query: { fromStage: fromStage || undefined } });
  }

  // Follow GET /runs/{id}/events (server-sent events) over fetch streaming, so the bearer token is sent. onEvent
  // receives { event, data, raw, id } for run, stage, ping, done, and timeout. Resolves with the last event name when
  // the stream ends ('done', 'timeout', or another name or null when it closed early). Rejects with ApiError when the
  // response is not 2xx or the browser cannot stream; abort with signal to stop.
  async streamRunEvents(id, { signal, onEvent } = {}) {
    const path = '/v1.0/api/runs/' + enc(id) + '/events';
    let response;
    try {
      response = await this.fetchImpl(this.baseUrl + path, { method: 'GET', headers: this._headers({ Accept: 'text/event-stream' }), signal, cache: 'no-store' });
    } catch (err) {
      if (err && err.name === 'AbortError') throw err;
      throw new ApiError(0, null, err && err.message ? err.message : 'Network error');
    }
    if (!response.ok) {
      const text = typeof response.text === 'function' ? await response.text() : '';
      let data = null;
      try {
        data = text ? JSON.parse(text) : null;
      } catch {
        data = null;
      }
      if (response.status === 401 && this.token && typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('auth:unauthorized', { detail: { path } }));
      }
      throw new ApiError(response.status, data && typeof data === 'object' ? data : null, data ? undefined : text || undefined);
    }
    if (!response.body || typeof response.body.getReader !== 'function') throw new ApiError(0, null, 'Streaming responses are not supported here');
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let last = null;
    const parser = createSseParser((evt) => {
      last = evt.event;
      if (onEvent) onEvent(evt);
    });
    let finished = false;
    while (!finished) {
      const { value, done } = await reader.read();
      if (done) break;
      parser.push(typeof value === 'string' ? value : decoder.decode(value, { stream: true }));
      finished = last === 'done' || last === 'timeout';
    }
    if (finished) {
      try {
        await reader.cancel();
      } catch {
        // The server closes the stream after done and timeout anyway.
      }
    } else {
      parser.push(decoder.decode());
      parser.flush();
    }
    return last;
  }
  runTopic(id, { dryRun = false } = {}) {
    return this._request('POST', '/v1.0/api/topics/' + enc(id) + '/run', { query: dryRun ? { dryRun: true } : undefined });
  }

  // Run drill-down: stages in order, the queries the run sent, and the results with their dispositions.
  listRunStages(id) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id) + '/stages');
  }
  listRunQueries(id) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id) + '/queries');
  }
  listRunResults(id, disposition) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id) + '/results', { query: { disposition } });
  }
  // A run's model calls, paged and oldest first unless sortDirection says otherwise.
  listRunLlmCalls(id, query) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id) + '/llm-calls', { query });
  }

  // Findings with evidence, sources, predictions with reviews, and suggestions (Phase 6)
  listFindings(query) {
    return this._request('GET', '/v1.0/api/findings', { query });
  }
  getFinding(id) {
    return this._request('GET', '/v1.0/api/findings/' + enc(id));
  }
  // body: { pinned?, hidden? }. Hidden findings leave lists (unless includeHidden=true), briefings, and emails.
  updateFinding(id, body) {
    return this._request('PUT', '/v1.0/api/findings/' + enc(id), { body });
  }
  setFindingPinned(id, pinned) {
    return this.updateFinding(id, { pinned: Boolean(pinned) });
  }
  setFindingHidden(id, hidden) {
    return this.updateFinding(id, { hidden: Boolean(hidden) });
  }
  // Removes the finding and its evidence links (204). Shared evidence, sources, and rating history stay.
  deleteFinding(id) {
    return this._request('DELETE', '/v1.0/api/findings/' + enc(id));
  }
  // One piece of evidence with its source: { evidence, sourceUrl, sourceTitle, sourceDomain, sourcePublishedUtc, role }.
  getEvidence(id) {
    return this._request('GET', '/v1.0/api/evidence/' + enc(id));
  }
  getSource(id) {
    return this._request('GET', '/v1.0/api/sources/' + enc(id));
  }
  // Fetched sources for my topics, newest first, without content text. query: topicId, runId, search, paging.
  listSources(query) {
    return this._request('GET', '/v1.0/api/sources', { query });
  }
  listPredictions(query) {
    return this._request('GET', '/v1.0/api/predictions', { query });
  }
  getPrediction(id) {
    return this._request('GET', '/v1.0/api/predictions/' + enc(id));
  }
  resolvePrediction(id, status, note) {
    return this._request('POST', '/v1.0/api/predictions/' + enc(id) + '/resolve', { body: { status, note: note || null } });
  }
  acceptPredictionReview(id) {
    return this._request('POST', '/v1.0/api/prediction-reviews/' + enc(id) + '/accept');
  }
  rejectPredictionReview(id) {
    return this._request('POST', '/v1.0/api/prediction-reviews/' + enc(id) + '/reject');
  }
  listSuggestions(query) {
    return this._request('GET', '/v1.0/api/suggestions', { query });
  }
  getSuggestion(id) {
    return this._request('GET', '/v1.0/api/suggestions/' + enc(id));
  }
  setSuggestionStatus(id, status, snoozedUntilUtc) {
    return this._request('PUT', '/v1.0/api/suggestions/' + enc(id) + '/status', { body: { status, snoozedUntilUtc: status === 'Snoozed' ? snoozedUntilUtc || null : null } });
  }

  // Briefings and their stored files (Phase 7). List items omit markdown; the detail carries sections, items, and files.
  listBriefings(query) {
    return this._request('GET', '/v1.0/api/briefings', { query });
  }
  getBriefing(id, signal) {
    return this._request('GET', '/v1.0/api/briefings/' + enc(id), { signal });
  }
  downloadBriefing(id, format = 'md') {
    return this._requestBlob('GET', '/v1.0/api/briefings/' + enc(id) + '/download', { query: { format } });
  }
  pinBriefing(id, pinned) {
    return this._request('PUT', '/v1.0/api/briefings/' + enc(id) + '/pin', { body: { pinned: Boolean(pinned) } });
  }
  regenerateBriefing(id) {
    return this._request('POST', '/v1.0/api/briefings/' + enc(id) + '/regenerate');
  }
  deleteBriefing(id) {
    return this._request('DELETE', '/v1.0/api/briefings/' + enc(id));
  }
  listArtifacts(query) {
    return this._request('GET', '/v1.0/api/artifacts', { query });
  }
  getArtifact(id) {
    return this._request('GET', '/v1.0/api/artifacts/' + enc(id));
  }
  // range is an optional { start, end } byte range (end inclusive, optional) sent as a Range header.
  getArtifactContent(id, { range, download = false, signal } = {}) {
    const headers = range ? { Range: 'bytes=' + range.start + '-' + (range.end === undefined || range.end === null ? '' : range.end) } : undefined;
    return this._requestBlob('GET', '/v1.0/api/artifacts/' + enc(id) + '/content', { query: download ? { download: true } : undefined, headers, signal });
  }
  getArtifactDownloadUrl(id) {
    return this._request('GET', '/v1.0/api/artifacts/' + enc(id) + '/download-url');
  }

  // Audio and podcasts (Phase 8). The media detail returns { media, chapters, audioPath, chaptersPath }; the audio
  // itself streams from the artifact content route (getArtifactContent), which honors Range.
  listMedia(query) {
    return this._request('GET', '/v1.0/api/media', { query });
  }
  getMedia(id, signal) {
    return this._request('GET', '/v1.0/api/media/' + enc(id), { signal });
  }
  deleteMedia(id) {
    return this._request('DELETE', '/v1.0/api/media/' + enc(id));
  }
  // Resolves to the queued episode (202). 409 when audio is already being made or audio is off on the server.
  generateBriefingAudio(briefingId, voiceProfileId = null) {
    return this._request('POST', '/v1.0/api/briefings/' + enc(briefingId) + '/audio', { body: voiceProfileId ? { voiceProfileId } : {} });
  }

  // Voice profiles. Members can read and test; only tenant administrators can write.
  listVoices(query) {
    return this._request('GET', '/v1.0/api/voices', { query });
  }
  getVoice(id) {
    return this._request('GET', '/v1.0/api/voices/' + enc(id));
  }
  createVoice(body) {
    return this._request('POST', '/v1.0/api/voices', { body });
  }
  updateVoice(id, body) {
    return this._request('PUT', '/v1.0/api/voices/' + enc(id), { body });
  }
  deleteVoice(id) {
    return this._request('DELETE', '/v1.0/api/voices/' + enc(id));
  }
  // Speaks a sample: resolves to { blob, contentType } with audio/wav. 502 TtsError when the speech endpoint fails.
  testVoice(id, text, signal) {
    return this._requestBlob('POST', '/v1.0/api/voices/' + enc(id) + '/test', { body: { text: text || '' }, signal });
  }

  // Private podcast feed tokens. Create returns { token, secret, feedUrl } once; only a hash is stored.
  listFeedTokens() {
    return this._request('GET', '/v1.0/api/feed-tokens');
  }
  createFeedToken({ name, editionId } = {}) {
    return this._request('POST', '/v1.0/api/feed-tokens', { body: { name: name || '', editionId: editionId || null } });
  }
  revokeFeedToken(id) {
    return this._request('DELETE', '/v1.0/api/feed-tokens/' + enc(id));
  }

  // Editions. The detail returns { edition, topics, schedules, coveredTopicIds }. An empty topic list covers every
  // active recurring topic. Schedules use the schedules API with ownerType=Edition.
  listEditions(query) {
    return this._request('GET', '/v1.0/api/editions', { query });
  }
  getEdition(id) {
    return this._request('GET', '/v1.0/api/editions/' + enc(id));
  }
  createEdition(body) {
    return this._request('POST', '/v1.0/api/editions', { body });
  }
  updateEdition(id, body) {
    return this._request('PUT', '/v1.0/api/editions/' + enc(id), { body });
  }
  deleteEdition(id) {
    return this._request('DELETE', '/v1.0/api/editions/' + enc(id));
  }
  setEditionTopics(id, topicIds) {
    return this._request('PUT', '/v1.0/api/editions/' + enc(id) + '/topics', { body: { topicIds: topicIds || [] } });
  }
  composeEdition(id) {
    return this._request('POST', '/v1.0/api/editions/' + enc(id) + '/compose');
  }
  previewEdition(id, signal) {
    return this._request('POST', '/v1.0/api/editions/' + enc(id) + '/preview', { signal });
  }

  // Briefing templates: Markdown with front matter and mmn:section directives (plan section 21.1).
  listBriefingTemplates(query) {
    return this._request('GET', '/v1.0/api/briefing-templates', { query });
  }
  getBriefingTemplate(id) {
    return this._request('GET', '/v1.0/api/briefing-templates/' + enc(id));
  }
  createBriefingTemplate({ content, name, shared = false }) {
    return this._request('POST', '/v1.0/api/briefing-templates', { body: { content: content || '', name: name || null, shared: Boolean(shared) } });
  }
  validateBriefingTemplate(content, signal) {
    return this._request('POST', '/v1.0/api/briefing-templates/validate', { body: { content: content || '' }, signal });
  }
  saveBriefingTemplate(id, content, changeNote) {
    return this._request('PUT', '/v1.0/api/briefing-templates/' + enc(id), { body: { content: content || '', changeNote: changeNote || null } });
  }
  deleteBriefingTemplate(id) {
    return this._request('DELETE', '/v1.0/api/briefing-templates/' + enc(id));
  }
  listBriefingTemplateVersions(id) {
    return this._request('GET', '/v1.0/api/briefing-templates/' + enc(id) + '/versions');
  }
  rollbackBriefingTemplate(id, version) {
    return this._request('POST', '/v1.0/api/briefing-templates/' + enc(id) + '/rollback', { body: { version: Number(version) } });
  }
  resetBriefingTemplate(id) {
    return this._request('POST', '/v1.0/api/briefing-templates/' + enc(id) + '/reset');
  }
  exportBriefingTemplate(id) {
    return this._requestBlob('GET', '/v1.0/api/briefing-templates/' + enc(id) + '/export');
  }

  // Promotional codes and plans (plan section 33.2). Global administrators only; everyone else gets 403. List items
  // and reads are PromoCodeStats: { code, planName, remaining, activeAssignments, revoked, lastRedeemedUtc }.
  listPromoCodes(query) {
    return this._request('GET', '/v1.0/api/admin/promo-codes', { query });
  }
  getPromoCode(id) {
    return this._request('GET', '/v1.0/api/admin/promo-codes/' + enc(id));
  }
  createPromoCode(body) {
    return this._request('POST', '/v1.0/api/admin/promo-codes', { body });
  }
  // Resolves to the created PromoCode rows (not stats), all sharing one batchId.
  generatePromoCodes(body) {
    return this._request('POST', '/v1.0/api/admin/promo-codes/generate', { body });
  }
  updatePromoCode(id, body) {
    return this._request('PUT', '/v1.0/api/admin/promo-codes/' + enc(id), { body });
  }
  // 409 Conflict when the code has been used: disable it instead.
  deletePromoCode(id) {
    return this._request('DELETE', '/v1.0/api/admin/promo-codes/' + enc(id));
  }
  listPromoRedemptions(id) {
    return this._request('GET', '/v1.0/api/admin/promo-codes/' + enc(id) + '/redemptions');
  }
  revokePromoRedemption(id, redemptionId) {
    return this._request('POST', '/v1.0/api/admin/promo-codes/' + enc(id) + '/redemptions/' + enc(redemptionId) + '/revoke');
  }
  applyPromoCode(id, tenantId, userId) {
    return this._request('POST', '/v1.0/api/admin/promo-codes/' + enc(id) + '/apply', { body: { tenantId, userId } });
  }
  // Plans resolve to [{ plan, features }] in sort order.
  listPlans() {
    return this._request('GET', '/v1.0/api/admin/plans');
  }
  createPlan(body) {
    return this._request('POST', '/v1.0/api/admin/plans', { body });
  }
  // features: { key: value }; an empty value removes that key from the plan.
  updatePlan(id, body) {
    return this._request('PUT', '/v1.0/api/admin/plans/' + enc(id), { body });
  }
  listPlanAssignments(tenantId, userId) {
    return this._request('GET', '/v1.0/api/admin/plan-assignments', { query: { tenantId, userId } });
  }
  grantPlan(body) {
    return this._request('POST', '/v1.0/api/admin/plan-assignments', { body });
  }
  revokePlanAssignment(id, tenantId) {
    return this._request('DELETE', '/v1.0/api/admin/plan-assignments/' + enc(id), { query: { tenantId } });
  }
  // My effective plan: { plan, features, assignment, endsUtc, upcoming }.
  getMyPlan() {
    return this._request('GET', '/v1.0/api/account/plan');
  }
  // Resolves to my new effective plan. 409 PromoCodeInvalid carries details.reason.
  redeemPromoCode(code) {
    return this._request('POST', '/v1.0/api/promo-codes/redeem', { body: { code } });
  }
  checkPromoCode(code) {
    return this._request('GET', '/v1.0/public/promo-codes/' + enc(code));
  }

  // Hosted accounts (Phase 14). The public routes answer the same way whether or not an account exists.
  getClientConfig() {
    return this._request('GET', '/v1.0/api/client-config');
  }
  signUp(body) {
    return this._request('POST', '/v1.0/public/signup', { body });
  }
  verifyEmail(token) {
    return this._request('POST', '/v1.0/public/verify-email', { body: { token } });
  }
  resendVerification(email) {
    return this._request('POST', '/v1.0/public/verify-email/resend', { body: { email } });
  }
  forgotPassword(email) {
    return this._request('POST', '/v1.0/public/password/forgot', { body: { email } });
  }
  resetPassword(token, password) {
    return this._request('POST', '/v1.0/public/password/reset', { body: { token, password } });
  }
  // The usage meter: plan, month-to-date cost and cap, reset date, counts, and limits.
  getAccountUsage() {
    return this._request('GET', '/v1.0/api/account/usage');
  }
  // Resolves to { blob, filename, contentType, status } for the ZIP.
  exportAccount() {
    return this._requestBlob('GET', '/v1.0/api/account/export');
  }
  // Resolves to { purgeAfterUtc, message }; every session ends.
  deleteAccount() {
    return this._request('POST', '/v1.0/api/account/delete');
  }

  // MCP tool servers for agentic research.
  listMcpServers(query) {
    return this._request('GET', '/v1.0/api/mcp-servers', { query });
  }
  // Resolves to { server, tools }.
  getMcpServer(id) {
    return this._request('GET', '/v1.0/api/mcp-servers/' + enc(id));
  }
  createMcpServer(body) {
    return this._request('POST', '/v1.0/api/mcp-servers', { body });
  }
  updateMcpServer(id, body) {
    return this._request('PUT', '/v1.0/api/mcp-servers/' + enc(id), { body });
  }
  deleteMcpServer(id) {
    return this._request('DELETE', '/v1.0/api/mcp-servers/' + enc(id));
  }
  // Resolves to { success, toolCount, tools } or { success: false, error }; nothing is saved.
  testMcpServer(id) {
    return this._request('POST', '/v1.0/api/mcp-servers/' + enc(id) + '/test');
  }
  discoverMcpServer(id) {
    return this._request('POST', '/v1.0/api/mcp-servers/' + enc(id) + '/discover');
  }
  setMcpToolEnabled(id, toolId, enabled) {
    return this._request('PUT', '/v1.0/api/mcp-servers/' + enc(id) + '/tools/' + enc(toolId), { body: { enabled } });
  }
  listRunToolCalls(id) {
    return this._request('GET', '/v1.0/api/runs/' + enc(id) + '/tool-calls');
  }

  // Email (Phase 9). Administrators see the whole outbox; members see email sent to them. List items omit bodies.
  listEmailMessages(query) {
    return this._request('GET', '/v1.0/api/email-messages', { query });
  }
  getEmailMessage(id) {
    return this._request('GET', '/v1.0/api/email-messages/' + enc(id));
  }
  resendEmailMessage(id) {
    return this._request('POST', '/v1.0/api/email-messages/' + enc(id) + '/resend');
  }
  deleteEmailMessage(id) {
    return this._request('DELETE', '/v1.0/api/email-messages/' + enc(id));
  }
  sendTestEmail() {
    return this._request('POST', '/v1.0/api/email/test');
  }
  getRunEmailPreview(runId) {
    return this._request('GET', '/v1.0/api/runs/' + enc(runId) + '/email-preview');
  }
  getEmailPreferences() {
    return this._request('GET', '/v1.0/api/email-preferences');
  }
  updateEmailPreferences(body) {
    return this._request('PUT', '/v1.0/api/email-preferences', { body });
  }

  // Feedback and learning (Phase 10). action is one of relevant, somewhat, not-relevant, accurate, partially,
  // inaccurate, too-early, useful, or not-useful.
  giveFeedback({ targetType, targetId, action, reason }) {
    return this._request('POST', '/v1.0/api/feedback', { body: { targetType, targetId, action, reason: reason || null, channel: 'Dashboard' } });
  }
  listFeedback(query) {
    return this._request('GET', '/v1.0/api/feedback', { query });
  }
  editFeedback(id, reason) {
    return this._request('PUT', '/v1.0/api/feedback/' + enc(id), { body: { reason: reason || null } });
  }
  retractFeedback(id) {
    return this._request('DELETE', '/v1.0/api/feedback/' + enc(id));
  }
  listPreferences(query) {
    return this._request('GET', '/v1.0/api/learned-preferences', { query });
  }
  getPreference(id) {
    return this._request('GET', '/v1.0/api/learned-preferences/' + enc(id));
  }
  createPreference(body) {
    return this._request('POST', '/v1.0/api/learned-preferences', { body });
  }
  updatePreference(id, body) {
    return this._request('PUT', '/v1.0/api/learned-preferences/' + enc(id), { body });
  }
  deletePreference(id) {
    return this._request('DELETE', '/v1.0/api/learned-preferences/' + enc(id));
  }
  // action: accept, reject, disable, or enable.
  setPreferenceStatus(id, action) {
    return this._request('POST', '/v1.0/api/learned-preferences/' + enc(id) + '/' + enc(action));
  }
  learnTopic(topicId) {
    return this._request('POST', '/v1.0/api/topics/' + enc(topicId) + '/learn');
  }
  getRankingRecommendation(topicId) {
    return this._request('GET', '/v1.0/api/topics/' + enc(topicId) + '/ranking-recommendation');
  }
  // report: overview, relevance, accuracy, feedback, suggestions, reasons, quality, or email. query: { topicId, from, to, userId, all }.
  getAnalytics(report, query) {
    return this._request('GET', '/v1.0/api/analytics/' + enc(report), { query });
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
