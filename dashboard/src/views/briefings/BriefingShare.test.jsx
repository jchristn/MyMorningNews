import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../../utils/api.js';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { BRIEFING, detail } from '../../test/briefingFixtures.js';
import { shareDayOptions, shareStatus } from './BriefingShare.jsx';

afterEach(() => vi.restoreAllMocks());

const DAY = 86400000;
const NOW = Date.now();
const iso = (ms) => new Date(ms).toISOString();
const share = (overrides = {}) => ({
  id: 'shr_1',
  tenantId: 'default',
  userId: 'usr_admin',
  briefingId: 'brf_1',
  tokenHash: 'redacted',
  tokenPrefix: 'mmns_abcde',
  showOwnerName: false,
  expiresUtc: iso(NOW + 7 * DAY),
  revokedUtc: null,
  viewCount: 3,
  lastViewedUtc: iso(NOW - 3600000),
  createdUtc: iso(NOW - DAY),
  ...overrides
});
const CREATED = {
  share: share({ id: 'shr_new', viewCount: 0, lastViewedUtc: null, expiresUtc: iso(NOW + 3 * DAY) }),
  secret: 'mmns_secret',
  url: 'https://app.example.test/v1.0/public/shares/mmns_secret',
  readUrl: 'https://app.example.test/v1.0/public/shares/mmns_secret',
  listenUrl: 'https://app.example.test/v1.0/public/shares/mmns_secret',
  audioReady: false
};

function readerApi(overrides = {}) {
  return {
    getBriefing: vi.fn(async () => detail()),
    getClientConfig: vi.fn(async () => ({ sharingEnabled: true, sharingMaxExpiryDays: 14, sharingDefaultExpiryDays: 7 })),
    listBriefingShares: vi.fn(async () => page([])),
    createBriefingShare: vi.fn(async () => CREATED),
    revokeBriefingShare: vi.fn(async () => null),
    ...overrides
  };
}

describe('share helpers', () => {
  it('tell live, expired, and revoked shares apart', () => {
    expect(shareStatus(share(), NOW)).toBe('Live');
    expect(shareStatus(share({ expiresUtc: iso(NOW - 1000) }), NOW)).toBe('Expired');
    expect(shareStatus(share({ revokedUtc: iso(NOW - 1000) }), NOW)).toBe('Revoked');
    expect(shareStatus(null, NOW)).toBe('Expired');
  });

  it('offer 1 to 14 days with 7 chosen, and follow the server limits', () => {
    const standard = shareDayOptions(null);
    expect(standard.days).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]);
    expect(standard.initial).toBe(7);
    const lower = shareDayOptions({ sharingMaxExpiryDays: 3, sharingDefaultExpiryDays: 7 });
    expect(lower.days).toEqual([1, 2, 3]);
    expect(lower.initial).toBe(3);
  });
});

describe('BriefingReader share', () => {
  it('creates a share with a 1 to 14 day expiry and shows the link once with a copy button and the expiry date', async () => {
    const api = renderRoute('/dashboard/briefings/brf_1', readerApi());
    await userEvent.click(await screen.findByRole('button', { name: 'Share' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/without signing in/)).toBeInTheDocument();
    await waitFor(() => expect(api.listBriefingShares).toHaveBeenCalledWith('brf_1', expect.anything()));
    expect(await within(dialog).findByText('This briefing has not been shared.')).toBeInTheDocument();

    const days = within(dialog).getByLabelText('Days until the link expires');
    expect(days).toHaveValue('7');
    expect(within(days).getAllByRole('option').map((o) => o.value)).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13', '14']);
    expect(within(dialog).getByText('From 1 to 14 days. Every share expires.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Show my name on the shared page')).not.toBeChecked();

    await userEvent.selectOptions(days, '3');
    api.listBriefingShares.mockResolvedValue(page([CREATED.share]));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create link' }));
    await waitFor(() => expect(api.createBriefingShare).toHaveBeenCalledWith('brf_1', { expiresInDays: 3, showOwnerName: false }));

    const created = await within(dialog).findByTestId('share-created');
    expect(within(created).getByLabelText('Link')).toHaveValue(CREATED.url);
    expect(within(created).getAllByRole('button', { name: 'Copy link' })).toHaveLength(1);
    expect(within(created).getByTestId('share-expires').textContent).toMatch(/^Expires .*\d/);
    expect(within(created).getByText(/no audio yet/)).toBeInTheDocument();
    expect(within(created).getByText(/shown only once/)).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Create another link' })).toBeInTheDocument();
    expect(await within(dialog).findByTestId('share-shr_new')).toHaveTextContent('Live');
  });

  it('lists shares with expiry and view count, shows expired ones as expired, and revokes and removes them', async () => {
    const live = share();
    const expired = share({ id: 'shr_old', expiresUtc: iso(NOW - DAY), viewCount: 0, lastViewedUtc: null });
    const api = renderRoute('/dashboard/briefings/brf_1', readerApi({ listBriefingShares: vi.fn(async () => page([live, expired])) }));
    await userEvent.click(await screen.findByRole('button', { name: 'Share' }));
    const dialog = await screen.findByRole('dialog');

    const liveRow = await within(dialog).findByTestId('share-shr_1');
    expect(liveRow).toHaveTextContent('Live');
    expect(liveRow).toHaveTextContent(/Expires /);
    expect(liveRow).toHaveTextContent('Opened: 3');
    expect(liveRow).toHaveTextContent(/last opened/);
    const oldRow = within(dialog).getByTestId('share-shr_old');
    expect(oldRow).toHaveTextContent('Expired');
    expect(oldRow).toHaveTextContent('Opened: 0');
    expect(within(oldRow).queryByRole('button', { name: 'Revoke' })).toBeNull();
    expect(within(dialog).queryByLabelText('Reading link')).toBeNull();

    await userEvent.click(within(liveRow).getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(api.revokeBriefingShare).toHaveBeenCalledWith('brf_1', 'shr_1', { purge: false }));
    expect(await screen.findByText('The link no longer works.')).toBeInTheDocument();

    await userEvent.click(within(within(dialog).getByTestId('share-shr_old')).getByRole('button', { name: 'Remove from the list' }));
    await waitFor(() => expect(api.revokeBriefingShare).toHaveBeenCalledWith('brf_1', 'shr_old', { purge: true }));
  });

  it('shows the server message when a share is refused', async () => {
    const createBriefingShare = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'expiresInDays must be between 1 and 14.' });
    });
    renderRoute('/dashboard/briefings/brf_1', readerApi({ createBriefingShare }));
    await userEvent.click(await screen.findByRole('button', { name: 'Share' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create link' }));
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument();
    expect(within(dialog).queryByTestId('share-created')).toBeNull();
  });

  it('hides Share when the server has sharing turned off', async () => {
    renderRoute('/dashboard/briefings/brf_1', readerApi({ getClientConfig: vi.fn(async () => ({ sharingEnabled: false })) }));
    expect(await screen.findByRole('button', { name: 'Download .md' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Share' })).toBeNull());
  });

  it('disables Share for a briefing that is still composing', async () => {
    renderRoute('/dashboard/briefings/brf_1', readerApi({ getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, status: 'Composing', markdown: null } })) }));
    expect(await screen.findByRole('button', { name: 'Share' })).toBeDisabled();
  });
});
