import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';

const SEARX = { id: 'spr_1', name: 'SearXNG', kind: 'Searxng', endpoint: 'http://searxng:8080', isDefault: true, maxResults: 10, timeoutMs: 15000, active: true, lastTestedUtc: '2026-11-20T08:00:00Z', lastTestOk: true };
const HN = { id: 'spr_2', name: 'Hacker News', kind: 'HackerNews', isDefault: false, maxResults: 10, timeoutMs: 15000, active: true };

describe('Search providers', () => {
  it('lists providers and runs a test query', async () => {
    const testSearchProvider = vi.fn(async () => ({ providerId: 'spr_1', kind: 'Searxng', success: true, durationMs: 420, hits: [{ url: 'https://example.com/a', title: 'Grid batteries', rank: 1 }] }));
    renderRoute('/dashboard/search-providers', { listSearchProviders: vi.fn(async () => page([SEARX, HN])), testSearchProvider });
    const row = (await screen.findByText('Hacker News', { selector: 'strong' })).closest('tr');
    expect(within(row).getByText('Never')).toBeInTheDocument();
    const searx = screen.getByText('http://searxng:8080').closest('tr');
    await userEvent.click(within(searx).getByRole('button', { name: 'Actions' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Test' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /Run test/ }));
    expect(await within(dialog).findByRole('link', { name: 'Grid batteries' })).toBeInTheDocument();
    expect(testSearchProvider).toHaveBeenCalledWith('spr_1', { query: 'artificial intelligence', maxResults: 5 });
  });
});

describe('Topic search providers', () => {
  it('saves a topic choice and can go back to the defaults', async () => {
    const setTopicSearchProviders = vi.fn(async (id, body) => (body.length ? [HN] : [SEARX]));
    renderRoute('/dashboard/topics/top_1?tab=sources', {
      getTopic: vi.fn(async () => ({ topic: { id: 'top_1', name: 'Grid', status: 'Active', userId: 'usr_admin', mcpToolIds: [] }, dimensions: [], constraints: [], recipients: [] })),
      listSearchProviders: vi.fn(async () => page([SEARX, HN])),
      listTopicSearchProviders: vi.fn(async () => enumerationResult([SEARX])),
      setTopicSearchProviders
    });
    const hn = await screen.findByLabelText(/Hacker News/);
    expect(screen.getByLabelText(/SearXNG/)).toBeChecked();
    await userEvent.click(screen.getByLabelText(/SearXNG/));
    await userEvent.click(hn);
    const panel = hn.closest('section');
    await userEvent.click(within(panel).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(setTopicSearchProviders).toHaveBeenCalledWith('top_1', [{ searchProviderId: 'spr_2', enabled: true }]));
    await userEvent.click(within(panel).getByRole('button', { name: 'Use the defaults' }));
    await waitFor(() => expect(setTopicSearchProviders).toHaveBeenLastCalledWith('top_1', []));
  });
});
