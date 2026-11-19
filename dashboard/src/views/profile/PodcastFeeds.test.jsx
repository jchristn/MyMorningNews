import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { EDITION } from '../../test/briefingFixtures.js';
import { FEED_TOKEN } from '../../test/mediaFixtures.js';
import { isLoopbackUrl } from './PodcastFeeds.jsx';

const ME = { id: 'usr_admin', email: 'admin@mymorningnews', firstName: 'Admin', lastName: 'User' };
const REVOKED = { ...FEED_TOKEN, id: 'fdt_2', name: 'Old phone', tokenPrefix: 'mmnf_zz99', editionId: undefined, lastUsedUtc: undefined, revokedUtc: '2026-11-19T08:00:00Z' };

function profileApi(overrides = {}) {
  return {
    getMe: vi.fn(async () => ME),
    listEditions: vi.fn(async () => page([EDITION])),
    listFeedTokens: vi.fn(async () => [FEED_TOKEN, REVOKED]),
    ...overrides
  };
}

describe('isLoopbackUrl', () => {
  it('recognizes loopback feed URLs', () => {
    expect(isLoopbackUrl('http://127.0.0.1:12100/v1.0/public/feeds/x/podcast.xml')).toBe(true);
    expect(isLoopbackUrl('https://news.example.com/v1.0/public/feeds/x/podcast.xml')).toBe(false);
    expect(isLoopbackUrl('not a url')).toBe(false);
  });
});

describe('Profile podcast feeds', () => {
  it('lists feed tokens with prefix, edition, last used, and revoked status', async () => {
    renderRoute('/dashboard/profile', profileApi());
    const table = await screen.findByRole('table', { name: 'Podcast feeds' });
    const active = within(table).getByText('Overcast').closest('tr');
    expect(within(active).getByText('mmnf_ab12...')).toBeInTheDocument();
    expect(await within(active).findByText('Morning News')).toBeInTheDocument();
    expect(within(active).getByText('Active', { selector: '.badge' })).toBeInTheDocument();
    const revoked = within(table).getByText('Old phone').closest('tr');
    expect(within(revoked).getByText('Revoked', { selector: '.badge' })).toBeInTheDocument();
    expect(within(revoked).getByText('All my audio')).toBeInTheDocument();
    expect(within(revoked).getByText('Never')).toBeInTheDocument();
    expect(within(revoked).queryByRole('button', { name: /Revoke/ })).not.toBeInTheDocument();
    expect(screen.getByText(/MYMORNINGNEWS_PUBLIC_BASE_URL to an address the phone can open/)).toBeInTheDocument();
    expect(screen.getByText(/Apple Podcasts/)).toBeInTheDocument();
    expect(screen.getByText(/Overcast: tap the plus button/)).toBeInTheDocument();
  });

  it('creates a feed for one edition and shows the secret and URL once with the private warning', async () => {
    const createFeedToken = vi.fn(async () => ({
      token: { ...FEED_TOKEN, id: 'fdt_3', name: 'Pocket Casts' },
      secret: 'mmnf_secret_value',
      feedUrl: 'http://127.0.0.1:12100/v1.0/public/feeds/mmnf_secret_value/podcast.xml'
    }));
    const api = renderRoute('/dashboard/profile', profileApi({ createFeedToken }));
    await screen.findByRole('table', { name: 'Podcast feeds' });
    await userEvent.type(screen.getByLabelText('Name', { selector: '#feed-name' }), 'Pocket Casts');
    await waitFor(() => expect(screen.getByRole('option', { name: 'Morning News' })).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText('Edition'), 'edn_1');
    await userEvent.click(screen.getByRole('button', { name: 'Create feed' }));
    await waitFor(() => expect(createFeedToken).toHaveBeenCalledWith({ name: 'Pocket Casts', editionId: 'edn_1' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByTestId('feed-secret')).toHaveTextContent('mmnf_secret_value');
    expect(within(dialog).getByTestId('feed-url')).toHaveTextContent('/v1.0/public/feeds/mmnf_secret_value/podcast.xml');
    expect(within(dialog).getByText(/This URL is private and is shown only once/)).toBeInTheDocument();
    expect(within(dialog).getByText(/points at 127.0.0.1/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Copy feed URL' })).toBeInTheDocument();
    expect(api.listFeedTokens.mock.calls.length).toBeGreaterThan(1);
    await userEvent.click(within(dialog).getByRole('button', { name: 'I have saved it' }));
    await waitFor(() => expect(screen.queryByText('mmnf_secret_value')).not.toBeInTheDocument());
  });

  it('revokes a feed after a confirm', async () => {
    const revokeFeedToken = vi.fn(async () => null);
    renderRoute('/dashboard/profile', profileApi({ revokeFeedToken }));
    await screen.findByRole('table', { name: 'Podcast feeds' });
    await userEvent.click(screen.getByRole('button', { name: 'Revoke Overcast' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/stop receiving episodes/)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(revokeFeedToken).toHaveBeenCalledWith('fdt_1'));
    expect(await screen.findByText('Feed revoked')).toBeInTheDocument();
  });

  it('shows the empty state', async () => {
    renderRoute('/dashboard/profile', profileApi({ listFeedTokens: vi.fn(async () => []) }));
    expect(await screen.findByText('No podcast feeds')).toBeInTheDocument();
  });
});
