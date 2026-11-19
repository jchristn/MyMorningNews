import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../App.jsx';
import { AppProvider } from '../context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from '../context/AuthContext.jsx';
import { ThemeProvider } from '../context/ThemeContext.jsx';
import { ApiError } from '../utils/api.js';
import { fakeClient } from './fakeClient.js';

// Render the whole app at a dashboard path with a signed-in session and a fake API client.
export function renderRoute(path, overrides = {}) {
  window.localStorage.setItem(STORAGE_KEYS.token, 'tok_1');
  const client = fakeClient(overrides);
  render(
    <ThemeProvider>
      <AuthProvider clientFactory={() => client}>
        <AppProvider>
          <MemoryRouter initialEntries={[path]}>
            <AppRoutes />
          </MemoryRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
  return client;
}

export const forbidden = () => Promise.reject(new ApiError(403, { error: 'Forbidden', message: 'Forbidden' }));

export function page(items) {
  return { items, pageNumber: 1, pageSize: 25, totalCount: items.length, totalPages: 1, durationMs: 1 };
}

export function usageRow(key, overrides = {}) {
  return {
    key,
    label: null,
    calls: 10,
    failures: 1,
    inputTokens: 1000,
    cachedInputTokens: 200,
    cacheWriteTokens: 100,
    outputTokens: 500,
    reasoningTokens: 50,
    totalTokens: 1500,
    toolCalls: 4,
    costUsd: 1.25,
    averageDurationMs: 900,
    averageTimeToFirstTokenMs: 300,
    p95TimeToFirstTokenMs: 700,
    costShare: 0.5,
    ...overrides
  };
}

export function usageReport(groupBy, rows) {
  const totals = rows.reduce(
    (acc, r) => ({ ...acc, calls: acc.calls + r.calls, failures: acc.failures + r.failures, totalTokens: acc.totalTokens + r.totalTokens, costUsd: acc.costUsd + r.costUsd }),
    usageRow('total', { calls: 0, failures: 0, totalTokens: 0, costUsd: 0, costShare: 0 })
  );
  return { fromUtc: '2026-10-20T00:00:00Z', toUtc: '2026-11-19T00:00:00Z', groupBy, totals, rows };
}
