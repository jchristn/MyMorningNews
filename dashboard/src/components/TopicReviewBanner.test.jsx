import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiClient } from '../utils/api.js';
import { notificationTarget } from '../utils/notifications.js';
import { page, renderRoute } from '../test/renderRoute.jsx';

const REVIEW = {
  id: 'trv_1',
  topicId: 'tpc_1',
  topicName: 'Quiet topic',
  status: 'Pending',
  lastNewFindingUtc: null,
  staleDays: 7,
  promptedUtc: '2026-11-20T08:00:00Z',
  deleteAfterUtc: '2026-12-04T08:00:00Z',
  resolvedUtc: null
};

describe('topic review API client', () => {
  it('calls the topic review and catalog promotion routes', async () => {
    const fetch = vi.fn(async () => new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetch);
    const api = new ApiClient('http://127.0.0.1:12100');
    await api.listTopicReviews({ pageSize: 50 });
    await api.keepTopicReview('trv_1');
    await api.deleteTopicReview('trv_1');
    await api.promoteTopicToCatalog('tpc_1', { title: 'Shared', status: 'Draft' });
    await api.getAdminDailyReport('2026-11-23');
    const calls = fetch.mock.calls.map(([url, init]) => init.method + ' ' + url.replace('http://127.0.0.1:12100', ''));
    expect(calls).toEqual([
      'GET /v1.0/api/topic-reviews?pageSize=50',
      'POST /v1.0/api/topic-reviews/trv_1/keep',
      'POST /v1.0/api/topic-reviews/trv_1/delete',
      'POST /v1.0/api/admin/catalog/from-topic/tpc_1',
      'GET /v1.0/api/admin/reports/daily?day=2026-11-23'
    ]);
    vi.unstubAllGlobals();
  });

  it('opens the topic from a stale topic notification, or the list once it is gone', () => {
    expect(notificationTarget({ kind: 'TopicStale', topicId: 'tpc_1' })).toBe('/dashboard/topics/tpc_1');
    expect(notificationTarget({ kind: 'TopicStale', topicId: null })).toBe('/dashboard/topics');
  });
});

describe('TopicReviewBanner', () => {
  it('shows the prompt on the topics list and keeps the topic', async () => {
    let pending = [REVIEW];
    const client = renderRoute('/dashboard/topics', {
      listTopicReviews: vi.fn(async () => page(pending)),
      keepTopicReview: vi.fn(async () => {
        pending = [];
        return { ...REVIEW, status: 'Kept' };
      })
    });
    const banner = await screen.findByTestId('topic-review');
    expect(within(banner).getByText('Keep "Quiet topic"?')).toBeInTheDocument();
    expect(within(banner).getByText(/Nothing new in the last 7 days\./)).toBeInTheDocument();
    await userEvent.click(within(banner).getByRole('button', { name: 'Keep' }));
    await waitFor(() => expect(client.keepTopicReview).toHaveBeenCalledWith('trv_1'));
    await waitFor(() => expect(screen.queryByTestId('topic-review')).not.toBeInTheDocument());
  });

  it('asks before deleting the topic', async () => {
    let pending = [REVIEW];
    const client = renderRoute('/dashboard/topics', {
      listTopicReviews: vi.fn(async () => page(pending)),
      deleteTopicReview: vi.fn(async () => {
        pending = [];
        return null;
      })
    });
    const banner = await screen.findByTestId('topic-review');
    await userEvent.click(within(banner).getByRole('button', { name: 'Delete' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/cannot be undone/)).toBeInTheDocument();
    expect(client.deleteTopicReview).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteTopicReview).toHaveBeenCalledWith('trv_1'));
  });

  it('shows nothing when the server has no reviews or cannot list them', async () => {
    renderRoute('/dashboard/topics', { listTopicReviews: vi.fn(async () => Promise.reject(new Error('down'))) });
    await screen.findByRole('heading', { name: 'Topics' });
    expect(screen.queryByTestId('topic-review')).not.toBeInTheDocument();
  });
});
