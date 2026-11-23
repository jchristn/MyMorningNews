import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { Share } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import React from 'react';
import { shareDayOptions, shareStatus } from '@/lib/sharing';
import { BriefingDetail } from '@/screens/today/BriefingDetail';
import { page, renderSignedIn } from './renderApp';

const DAY = 86400000;
const NOW = Date.now();
const iso = (ms) => new Date(ms).toISOString();
const CONFIG = { productName: 'My Morning News', serverVersion: '0.1.0-alpha', audioEnabled: false, sharingEnabled: true, sharingMaxExpiryDays: 14, sharingDefaultExpiryDays: 7, links: {}, copyright: '(c) 2026 Lattice AI LLC' };
const BRIEFING = { id: 'brf_1', title: 'Morning News', kind: 'Edition', status: 'Ready', markdown: '# Morning News\n\n## Top of the news\n\nAgents ship.\n', composedUtc: '2026-11-19T06:30:00Z', readMinutes: 1 };
const detail = (briefing = {}) => ({ briefing: { ...BRIEFING, ...briefing }, sections: [], items: [], files: [], filesExpired: false, editionName: 'Morning News', itemTitles: {} });
const share = (extra = {}) => ({ id: 'shr_1', briefingId: 'brf_1', tokenHash: 'redacted', tokenPrefix: 'mmns_abcde', showOwnerName: false, expiresUtc: iso(NOW + 7 * DAY), revokedUtc: null, viewCount: 3, lastViewedUtc: iso(NOW - 3600000), ...extra });
const CREATED = {
  share: share({ id: 'shr_new', viewCount: 0, lastViewedUtc: null, expiresUtc: iso(NOW + 3 * DAY) }),
  secret: 'mmns_secret',
  url: 'https://app.mymorningnews.ai/v1.0/public/shares/mmns_secret',
  readUrl: 'https://app.mymorningnews.ai/v1.0/public/shares/mmns_secret',
  listenUrl: 'https://app.mymorningnews.ai/v1.0/public/shares/mmns_secret',
  audioReady: false,
};
const base = (extra = {}) => ({
  'GET /v1.0/api/client-config': () => ({ body: CONFIG }),
  'GET /v1.0/api/briefings/brf_1': () => ({ body: detail() }),
  'GET /v1.0/api/feedback': () => page([]),
  ...extra,
});

afterEach(() => jest.restoreAllMocks());

describe('share helpers', () => {
  it('tell live, expired, and revoked shares apart', () => {
    expect(shareStatus(share(), NOW)).toBe('Live');
    expect(shareStatus(share({ expiresUtc: iso(NOW - 1000) }), NOW)).toBe('Expired');
    expect(shareStatus(share({ revokedUtc: iso(NOW - 1000) }), NOW)).toBe('Revoked');
    expect(shareStatus(null, NOW)).toBe('Expired');
  });

  it('offer 1 to 14 days with 7 chosen, and follow the server limits', () => {
    expect(shareDayOptions(null)).toEqual({ days: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14], max: 14, initial: 7 });
    expect(shareDayOptions({ sharingMaxExpiryDays: 3, sharingDefaultExpiryDays: 7 })).toEqual({ days: [1, 2, 3], max: 3, initial: 3 });
  });
});

describe('Briefing reader: public share links', () => {
  it('creates a share for the chosen days and hands each link to the share sheet, with copy and the expiry date', async () => {
    const sheet = jest.spyOn(Share, 'share').mockResolvedValue({ action: 'sharedAction' });
    const clipboard = jest.spyOn(Clipboard, 'setStringAsync').mockResolvedValue(true);
    let shares = [];
    const { server } = await renderSignedIn(
      <BriefingDetail id="brf_1" />,
      base({
        'GET /v1.0/api/briefings/brf_1/shares': () => page(shares),
        'POST /v1.0/api/briefings/brf_1/shares': () => {
          shares = [CREATED.share];
          return { status: 201, body: CREATED };
        },
      }),
    );
    await waitFor(() => expect(screen.getByTestId('briefing-share-open')).toBeTruthy());
    expect(server.count('GET /v1.0/api/briefings/brf_1/shares')).toBe(0);
    await fireEvent.press(screen.getByTestId('briefing-share-open'));
    await waitFor(() => expect(screen.getByText('This briefing has not been shared.')).toBeTruthy());
    expect(screen.getByText('From 1 to 14 days. Every share expires.')).toBeTruthy();
    expect(screen.getByTestId('share-days-1')).toBeTruthy();
    expect(screen.getByTestId('share-days-14')).toBeTruthy();
    expect(screen.queryByTestId('share-days-15')).toBeNull();

    await fireEvent.press(screen.getByTestId('share-days-3'));
    await fireEvent.press(screen.getByTestId('share-create'));
    await waitFor(() => expect(screen.getByTestId('share-created')).toBeTruthy());
    const post = server.calls.find((c) => c.key === 'POST /v1.0/api/briefings/brf_1/shares');
    expect(post.body).toEqual({ expiresInDays: 3, showOwnerName: false });
    expect(screen.getByTestId('share-url').props.children).toBe(CREATED.url);
    expect(screen.getByTestId('share-expires').props.children).toMatch(/^Expires .*\d/);
    expect(screen.getByText(/no audio yet/)).toBeTruthy();
    expect(screen.getByText(/shown only once/)).toBeTruthy();

    await fireEvent.press(screen.getByTestId('share-send'));
    await waitFor(() => expect(sheet).toHaveBeenCalledWith(expect.objectContaining({ url: CREATED.url, message: expect.stringContaining(CREATED.url) })));
    await fireEvent.press(screen.getByTestId('share-copy'));
    await waitFor(() => expect(clipboard).toHaveBeenCalledWith(CREATED.url));
    await waitFor(() => expect(screen.getByTestId('share-shr_new')).toBeTruthy());
  });

  it('lists shares with their expiry and openings, shows expired ones as expired, and revokes after a second tap', async () => {
    let shares = [share(), share({ id: 'shr_old', expiresUtc: iso(NOW - DAY), viewCount: 0, lastViewedUtc: null })];
    const { server } = await renderSignedIn(
      <BriefingDetail id="brf_1" />,
      base({
        'GET /v1.0/api/briefings/brf_1/shares': () => page(shares),
        'DELETE /v1.0/api/briefings/brf_1/shares/shr_1': () => {
          shares = [{ ...shares[0], revokedUtc: iso(NOW) }, shares[1]];
          return { status: 204 };
        },
        'DELETE /v1.0/api/briefings/brf_1/shares/shr_old': () => {
          shares = shares.slice(0, 1);
          return { status: 204 };
        },
      }),
    );
    await waitFor(() => expect(screen.getByTestId('briefing-share-open')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('briefing-share-open'));
    await waitFor(() => expect(screen.getByTestId('share-shr_1')).toBeTruthy());
    expect(screen.getByText('Live')).toBeTruthy();
    expect(screen.getByText('Expired')).toBeTruthy();
    expect(screen.getByText(/^Opened: 3/)).toBeTruthy();
    expect(screen.getByText(/^Expired .*\d/)).toBeTruthy();

    await fireEvent.press(screen.getByTestId('share-revoke-shr_1'));
    expect(server.count('DELETE /v1.0/api/briefings/brf_1/shares/shr_1')).toBe(0);
    expect(screen.getByText('Tap again to confirm')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('share-revoke-shr_1'));
    await waitFor(() => expect(server.count('DELETE /v1.0/api/briefings/brf_1/shares/shr_1')).toBe(1));
    await waitFor(() => expect(screen.getByText('Revoked')).toBeTruthy());

    await fireEvent.press(screen.getByTestId('share-revoke-shr_old'));
    await fireEvent.press(screen.getByTestId('share-revoke-shr_old'));
    await waitFor(() => expect(server.calls.find((c) => c.key === 'DELETE /v1.0/api/briefings/brf_1/shares/shr_old').url).toContain('purge=true'));
    await waitFor(() => expect(screen.queryByTestId('share-shr_old')).toBeNull());
  });

  it('shows the server message when a share is refused', async () => {
    await renderSignedIn(
      <BriefingDetail id="brf_1" />,
      base({
        'GET /v1.0/api/briefings/brf_1/shares': () => page([]),
        'POST /v1.0/api/briefings/brf_1/shares': () => ({ status: 400, body: { error: 'ValidationFailed', message: 'expiresInDays must be between 1 and 14.' } }),
      }),
    );
    await waitFor(() => expect(screen.getByTestId('briefing-share-open')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('briefing-share-open'));
    await waitFor(() => expect(screen.getByTestId('share-create')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('share-create'));
    await waitFor(() => expect(screen.getByText('expiresInDays must be between 1 and 14.')).toBeTruthy());
    expect(screen.queryByTestId('share-created')).toBeNull();
  });

  it('is hidden when the server has sharing turned off, and for a briefing that is not ready', async () => {
    const off = await renderSignedIn(<BriefingDetail id="brf_1" />, base({ 'GET /v1.0/api/client-config': () => ({ body: { ...CONFIG, sharingEnabled: false } }) }));
    await waitFor(() => expect(screen.getByTestId('briefing-pin')).toBeTruthy());
    await waitFor(() => expect(screen.queryByTestId('briefing-share-open')).toBeNull());
    off.unmount();
    await renderSignedIn(<BriefingDetail id="brf_1" />, base({ 'GET /v1.0/api/briefings/brf_1': () => ({ body: detail({ status: 'Failed', errorMessage: 'No findings.' }) }) }));
    await waitFor(() => expect(screen.getByTestId('briefing-pin')).toBeTruthy());
    expect(screen.queryByTestId('briefing-share-open')).toBeNull();
  });
});
