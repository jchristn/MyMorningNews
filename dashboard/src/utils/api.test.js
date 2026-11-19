import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError, buildQuery, pagedQuery } from './api.js';

function response(status, body) {
  const text = body === undefined ? '' : typeof body === 'string' ? body : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text };
}

describe('buildQuery', () => {
  it('skips empty values and encodes the rest', () => {
    expect(buildQuery({ a: 1, b: '', c: null, d: undefined, e: 'x y' })).toBe('?a=1&e=x+y');
  });

  it('repeats array values and serializes dates', () => {
    const when = new Date('2026-01-02T03:04:05.000Z');
    expect(buildQuery({ tag: ['a', 'b'], fromUtc: when })).toBe('?tag=a&tag=b&fromUtc=2026-01-02T03%3A04%3A05.000Z');
  });

  it('returns an empty string when nothing is set', () => {
    expect(buildQuery({})).toBe('');
    expect(buildQuery(null)).toBe('');
  });
});

describe('pagedQuery', () => {
  it('maps the table sort direction to the server enum and spreads filters', () => {
    expect(pagedQuery({ pageNumber: 2, pageSize: 25, sortBy: 'name', sortDirection: 'desc', filters: { active: 'true' } })).toEqual({
      pageNumber: 2,
      pageSize: 25,
      sortBy: 'name',
      sortDirection: 'Descending',
      search: undefined,
      active: 'true'
    });
    expect(pagedQuery({ sortDirection: 'asc' }).sortDirection).toBe('Ascending');
    expect(pagedQuery({}).sortDirection).toBeUndefined();
  });
});

describe('ApiError', () => {
  it('exposes the error code, request ID, and details from the body', () => {
    const err = new ApiError(400, { error: 'BadRequest', message: 'Bad', requestId: 'req_1', details: { name: 'Required' } });
    expect(err.status).toBe(400);
    expect(err.code).toBe('BadRequest');
    expect(err.requestId).toBe('req_1');
    expect(err.details).toEqual({ name: 'Required' });
    expect(err.message).toBe('Bad');
  });
});

describe('ApiClient', () => {
  it('sends the bearer token, Accept-Language, and query string', async () => {
    const fetch = vi.fn(async () => response(200, { items: [] }));
    const client = new ApiClient('http://127.0.0.1:12100/', 'tok', { fetch, getLocale: () => 'fr' });
    await client.listTenants({ pageNumber: 1, pageSize: 10 });
    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:12100/v1.0/tenants?pageNumber=1&pageSize=10');
    expect(init.method).toBe('GET');
    expect(init.headers.Authorization).toBe('Bearer tok');
    expect(init.headers['Accept-Language']).toBe('fr');
  });

  it('logs in with credential headers', async () => {
    const fetch = vi.fn(async () => response(200, { token: 't' }));
    const client = new ApiClient('http://127.0.0.1:12100', null, { fetch });
    await client.login('a@b', 'pw', 'default');
    const [url, init] = fetch.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:12100/v1.0/token');
    expect(init.method).toBe('POST');
    expect(init.headers['x-email']).toBe('a@b');
    expect(init.headers['x-password']).toBe('pw');
    expect(init.headers['x-tenant-guid']).toBe('default');
    expect(init.headers.Authorization).toBeUndefined();
  });

  it('serializes JSON bodies', async () => {
    const fetch = vi.fn(async () => response(201, { id: 'ten_1' }));
    const client = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    const created = await client.createTenant({ name: 'Acme' });
    expect(created).toEqual({ id: 'ten_1' });
    const init = fetch.mock.calls[0][1];
    expect(init.headers['Content-Type']).toBe('application/json');
    expect(JSON.parse(init.body)).toEqual({ name: 'Acme' });
  });

  it('throws ApiError for error statuses', async () => {
    const fetch = vi.fn(async () => response(404, { error: 'NotFound', message: 'Missing', requestId: 'req_9' }));
    const client = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await expect(client.getTenant('x')).rejects.toMatchObject({ status: 404, code: 'NotFound', requestId: 'req_9' });
  });

  it('maps network failures to status 0', async () => {
    const fetch = vi.fn(async () => {
      throw new TypeError('Failed to fetch');
    });
    const client = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await expect(client.healthCheck()).rejects.toMatchObject({ status: 0 });
  });

  it('dispatches auth:unauthorized on 401 when a token was sent', async () => {
    const fetch = vi.fn(async () => response(401, { error: 'Unauthorized' }));
    const listener = vi.fn();
    window.addEventListener('auth:unauthorized', listener);
    const client = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await expect(client.getMe()).rejects.toBeInstanceOf(ApiError);
    expect(listener).toHaveBeenCalledTimes(1);
    const anonymous = new ApiClient('http://127.0.0.1:12100', null, { fetch });
    await expect(anonymous.login('a', 'b', 'c')).rejects.toBeInstanceOf(ApiError);
    expect(listener).toHaveBeenCalledTimes(1);
    window.removeEventListener('auth:unauthorized', listener);
  });

  it('returns null for 204 responses', async () => {
    const fetch = vi.fn(async () => response(204));
    const client = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await expect(client.deleteTenant('ten_1')).resolves.toBeNull();
  });
});
