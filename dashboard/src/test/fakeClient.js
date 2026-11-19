import { vi } from 'vitest';

export const ADMIN_DETAILS = {
  valid: true,
  sessionId: 'ses_1',
  principalType: 'User',
  scheme: 'Bearer',
  tenantId: 'default',
  tenantName: 'Default',
  userId: 'usr_admin',
  email: 'admin@mymorningnews',
  displayName: 'Admin',
  isAdmin: true,
  isTenantAdmin: true
};

export const MEMBER_DETAILS = { ...ADMIN_DETAILS, userId: 'usr_member', email: 'member@mymorningnews', isAdmin: false, isTenantAdmin: false };

export const EMPTY_PAGE = { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0, durationMs: 1 };

// A client whose named methods are spies. Methods not given in overrides resolve to an empty page so any view renders.
export function fakeClient(overrides = {}) {
  const base = {
    login: vi.fn(async () => ({ token: 'tok_1', tenantId: 'default' })),
    getTokenDetails: vi.fn(async () => ADMIN_DETAILS),
    logout: vi.fn(async () => null),
    healthCheck: vi.fn(async () => ({ status: 'Healthy', version: '0.1.0-alpha', uptimeSeconds: 5, components: { database: 'Healthy' } })),
    getRequestHistorySummary: vi.fn(async () => ({ totalCount: 0, totalSuccess: 0, totalFailure: 0, averageDurationMs: 0, buckets: [] })),
    getServerInfo: vi.fn(async () => ({ product: 'My Morning News', version: '0.1.0-alpha', uptimeSeconds: 5, externalServices: [] })),
    ...overrides
  };
  return new Proxy(base, {
    get(target, prop) {
      if (prop in target) return target[prop];
      if (prop === 'then' || typeof prop === 'symbol') return undefined;
      target[prop] = vi.fn(async () => EMPTY_PAGE);
      return target[prop];
    }
  });
}
