import { ApiClient, buildQuery, listItems } from '@/api/client';
import { ApiError, NetworkError, SessionExpiredError, TenantSelectionRequiredError, TimeoutError, isItemNotFound, isNotAvailable } from '@/api/errors';
import { authApi } from '@/api/auth';
import { accountApi, devicesApi } from '@/api/account';
import { routes } from '@/api/routes';
import { fakeServer } from './fakeServer';

describe('ApiClient', () => {
  it('builds query strings, skipping empty values and repeating arrays', () => {
    expect(buildQuery({ a: 1, b: '', c: null, d: undefined, e: ['x', 'y'], f: 'a b' })).toBe('?a=1&e=x&e=y&f=a%20b');
    expect(buildQuery({})).toBe('');
  });

  it('reads paged and bare list responses', () => {
    expect(listItems({ items: [1, 2], totalCount: 2 })).toEqual([1, 2]);
    expect(listItems([3])).toEqual([3]);
    expect(listItems(null)).toEqual([]);
  });

  it('sends the bearer token, Accept-Language, and JSON bodies', async () => {
    const server = fakeServer({ 'PUT /v1.0/api/me': (call) => ({ status: 200, body: { id: 'usr_1', ...call.body } }) });
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100/', getAccessToken: async () => 'abc', getLocale: () => 'es', fetchImpl: server.fetchImpl });
    const user = await accountApi.updateMe(client, { firstName: 'Ana' });
    expect(user.firstName).toBe('Ana');
    const call = server.calls[0];
    expect(call.url).toBe('http://127.0.0.1:12100/v1.0/api/me');
    expect(call.headers.Authorization).toBe('Bearer abc');
    expect(call.headers['Accept-Language']).toBe('es');
    expect(call.headers['Content-Type']).toBe('application/json');
  });

  it('turns error bodies into typed ApiErrors with code, request id, and field details', async () => {
    const server = fakeServer({
      'PUT /v1.0/api/me/password': () => ({ status: 400, body: { error: 'ValidationFailed', message: 'The current password is incorrect.', requestId: 'rqi_1', details: { currentPassword: 'Incorrect.' } } }),
    });
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', fetchImpl: server.fetchImpl });
    await expect(accountApi.changePassword(client, 'a', 'bbbbbbbb')).rejects.toMatchObject({ name: 'ApiError', status: 400, code: 'ValidationFailed', requestId: 'rqi_1', details: { currentPassword: 'Incorrect.' } });
  });

  it('reports a missing route as not available (404, 405, 501)', async () => {
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', fetchImpl: fakeServer({}).fetchImpl });
    const error = await devicesApi.list(client).catch((e) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect(isNotAvailable(error)).toBe(true);
    expect(isNotAvailable(new ApiError(500, null))).toBe(false);
  });

  it('tells a missing item (404 with a request ID) from a missing route', () => {
    const item = new ApiError(404, { error: 'NotFound', message: 'Topic was not found.', requestId: 'rqi_1' });
    const route = new ApiError(404, { error: 'NotFound', message: 'The requested resource was not found.' });
    expect([isNotAvailable(item), isItemNotFound(item)]).toEqual([false, true]);
    expect([isNotAvailable(route), isItemNotFound(route)]).toEqual([true, false]);
    expect(isNotAvailable(new ApiError(404, { error: 'RouteNotFound', requestId: 'rqi_2' }))).toBe(true);
    expect(isNotAvailable(new ApiError(501, null))).toBe(true);
  });

  it('raises TenantSelectionRequiredError with the tenant list on 409 (contract B)', async () => {
    const server = fakeServer({ 'POST /v1.0/token': () => ({ status: 409, body: { error: 'TenantSelectionRequired', message: 'Choose', tenants: [{ id: 'ten_a', name: 'A' }, { id: 'ten_b', name: 'B' }] } }) });
    const client = new ApiClient({ baseUrl: 'http://x', fetchImpl: server.fetchImpl });
    const error = await authApi.signIn(client, { email: 'a@b', password: 'p' }).catch((e) => e);
    expect(error).toBeInstanceOf(TenantSelectionRequiredError);
    expect(error.tenants.map((t) => t.id)).toEqual(['ten_a', 'ten_b']);
    expect(server.calls[0].headers['x-client']).toBe('mobile');
    expect(server.calls[0].headers['x-tenant-guid']).toBeUndefined();
  });

  it('sends the tenant header only when a tenant is given', async () => {
    const server = fakeServer({ 'POST /v1.0/token': () => ({ status: 200, body: { token: 't' } }) });
    const client = new ApiClient({ baseUrl: 'http://x', fetchImpl: server.fetchImpl });
    await authApi.signIn(client, { email: 'a@b', password: 'p', tenantId: 'default' });
    expect(server.calls[0].headers['x-tenant-guid']).toBe('default');
    expect(server.calls[0].headers.Authorization).toBeUndefined();
  });

  it('refreshes once after a 401 and retries the request', async () => {
    let token = 'old';
    const server = fakeServer({ 'GET /v1.0/api/me': (call) => (call.headers.Authorization === 'Bearer new' ? { status: 200, body: { id: 'usr_1' } } : { status: 401, body: { error: 'Unauthorized' } }) });
    const refreshTokens = jest.fn(async () => {
      token = 'new';
      return true;
    });
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => token, refreshTokens, fetchImpl: server.fetchImpl });
    await expect(accountApi.getMe(client)).resolves.toEqual({ id: 'usr_1' });
    expect(refreshTokens).toHaveBeenCalledTimes(1);
    expect(server.count('GET /v1.0/api/me')).toBe(2);
  });

  it('calls onUnauthorized and throws SessionExpiredError when the refresh fails', async () => {
    const server = fakeServer({ 'GET /v1.0/api/me': () => ({ status: 401, body: { error: 'Unauthorized' } }) });
    const onUnauthorized = jest.fn();
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', refreshTokens: async () => false, onUnauthorized, fetchImpl: server.fetchImpl });
    await expect(accountApi.getMe(client)).rejects.toBeInstanceOf(SessionExpiredError);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('maps fetch failures to NetworkError and slow responses to TimeoutError', async () => {
    const offline = new ApiClient({ baseUrl: 'http://x', fetchImpl: async () => Promise.reject(new TypeError('Network request failed')) });
    await expect(offline.get(routes.health)).rejects.toBeInstanceOf(NetworkError);
    const slow = new ApiClient({ baseUrl: 'http://x', timeoutMs: 20, fetchImpl: (_url, init) => new Promise((_resolve, reject) => init.signal.addEventListener('abort', () => reject(new Error('aborted')))) });
    await expect(slow.get(routes.health)).rejects.toBeInstanceOf(TimeoutError);
  });

  it('builds media URLs on the server origin', () => {
    const client = new ApiClient({ baseUrl: 'https://app.mymorningnews.ai/' });
    expect(client.url(routes.media('med_1'))).toBe('https://app.mymorningnews.ai/v1.0/api/media/med_1');
    expect(routes.briefingProgress('a/b')).toBe('/v1.0/api/briefings/a%2Fb/progress');
  });
});
