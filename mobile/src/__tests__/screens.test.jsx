import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { I18nextProvider } from 'react-i18next';
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { initI18n } from '@/i18n/runtime';
import { SignInScreen, preselectedTenant, signInErrorKey, signInServerMessage, tenantOptions } from '@/screens/auth/SignInScreen';
import { UsageScreen, tokenMixRows, usageChartRows, usageWindow } from '@/screens/insights/UsageScreen';
import { NotificationsSection, pushStatusKey } from '@/screens/account/NotificationsSection';
import { passwordProblem } from '@/screens/account/PasswordSection';
import { BriefingsScreen } from '@/screens/today/TodayScreens';
import { barLayout, topRows } from '@/charts/barLayout';
import { parseBlocks, plainText } from '@/lib/markdown';
import { ApiError, NetworkError } from '@/api/errors';
import { AuthProvider } from '@/state/AuthContext';
import { PrefsProvider, usePrefs } from '@/state/PrefsContext';
import { ServerProvider } from '@/state/ServerContext';
import { DEFAULT_PREFS, normalizeServerUrl, isInsecureServer } from '@/storage/prefs';
import { ThemeProvider, resolvePalette } from '@/theme/ThemeContext';
import { dark, light } from '@/theme/palette';
import { formatClock, formatCost } from '@/lib/format';
import { PlayerProvider, usePlayer } from '@/player/PlayerContext';
import { PlayerService } from '@/player/PlayerService';
import { fakeServer, tokenResponse } from './fakeServer';

const SERVER = 'http://127.0.0.1:12100';

async function renderWith(ui, handlers, prefs = {}) {
  const server = fakeServer({
    'GET /v1.0/api/client-config': () => ({ body: { productName: 'My Morning News', serverVersion: '0.1.0-alpha', signupEnabled: false, pushEnabled: true, links: {}, copyright: '(c) 2026 Lattice AI LLC' } }),
    ...handlers,
  });
  const controller = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl: server.fetchImpl });
  const i18n = initI18n('en');
  const utils = await render(
    <I18nextProvider i18n={i18n}>
      <PrefsProvider initial={{ ...DEFAULT_PREFS, serverUrl: SERVER, ...prefs }}>
        <ThemeProvider preference="light">
          <AuthProvider controller={controller}>
            <ServerProvider>{ui}</ServerProvider>
          </AuthProvider>
        </ThemeProvider>
      </PrefsProvider>
    </I18nextProvider>,
  );
  return { ...utils, server, controller };
}

describe('sign-in screen', () => {
  it('warns about plain HTTP and signs in through the tenant picker (contract B)', async () => {
    const { server, controller } = await renderWith(<SignInScreen />, {
      'POST /v1.0/token': (call) =>
        call.headers['x-tenant-guid'] ? { body: tokenResponse() } : { status: 409, body: { error: 'TenantSelectionRequired', tenants: [{ id: 'ten_a', name: 'Acme' }, { id: 'ten_b', name: 'Beta' }] } },
    });
    expect(screen.getByText('Plain HTTP')).toBeTruthy();
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'admin@mymorningnews');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'password');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(screen.getByTestId('tenant-picker')).toBeTruthy());
    await fireEvent.press(screen.getByLabelText('Beta'));
    await fireEvent.press(screen.getByTestId('tenant-continue'));
    await waitFor(() => expect(controller.getState().status).toBe('signedIn'));
    expect(server.calls.filter((c) => c.key === 'POST /v1.0/token').map((c) => c.headers['x-tenant-guid'])).toEqual([undefined, 'ten_b']);
  });

  it('has no tenant field and signs in without a tenant', async () => {
    const { server, controller } = await renderWith(<SignInScreen />, { 'POST /v1.0/token': () => ({ body: tokenResponse() }) }, { lastTenantId: 'ten_old' });
    expect(screen.queryByTestId('sign-in-tenant')).toBeNull();
    expect(screen.queryByText(/tenant/i)).toBeNull();
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'admin@mymorningnews');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'password');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(controller.getState().status).toBe('signedIn'));
    expect(server.calls.filter((c) => c.key === 'POST /v1.0/token').map((c) => c.headers['x-tenant-guid'])).toEqual([undefined]);
  });

  it('signs in directly when the server offers a single workspace', async () => {
    const { server, controller } = await renderWith(<SignInScreen />, {
      'POST /v1.0/token': (call) => (call.headers['x-tenant-guid'] ? { body: tokenResponse() } : { status: 409, body: { error: 'TenantSelectionRequired', tenants: [{ id: 'ten_only', name: 'Only' }] } }),
    });
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'admin@mymorningnews');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'password');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(controller.getState().status).toBe('signedIn'));
    expect(screen.queryByTestId('tenant-picker')).toBeNull();
    expect(server.calls.filter((c) => c.key === 'POST /v1.0/token').map((c) => c.headers['x-tenant-guid'])).toEqual([undefined, 'ten_only']);
  });

  it('preselects the workspace chosen last time in the picker', async () => {
    const { server, controller } = await renderWith(
      <SignInScreen />,
      {
        'POST /v1.0/token': (call) =>
          call.headers['x-tenant-guid'] ? { body: tokenResponse() } : { status: 409, body: { error: 'TenantSelectionRequired', tenants: [{ id: 'ten_a', name: 'Acme' }, { id: 'ten_b', name: 'Beta' }] } },
      },
      { lastTenantId: 'ten_b' },
    );
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'admin@mymorningnews');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'password');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(screen.getByTestId('tenant-picker')).toBeTruthy());
    expect(screen.getByText('Choose a workspace')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('tenant-continue'));
    await waitFor(() => expect(controller.getState().status).toBe('signedIn'));
    expect(server.calls.filter((c) => c.key === 'POST /v1.0/token').map((c) => c.headers['x-tenant-guid'])).toEqual([undefined, 'ten_b']);
  });

  it('shows the server message when an older server requires a tenant, and explains bad credentials', async () => {
    let attempt = 0;
    await renderWith(<SignInScreen />, {
      'POST /v1.0/token': () => {
        attempt += 1;
        return attempt === 1 ? { status: 400, body: { error: 'BadRequest', message: 'x-tenant-guid is required.' } } : { status: 401, body: { error: 'Unauthorized' } };
      },
    });
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'a@b');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'x');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(screen.getByText('x-tenant-guid is required.')).toBeTruthy());
    expect(screen.queryByTestId('sign-in-tenant')).toBeNull();
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await waitFor(() => expect(screen.getByText('The email or password is incorrect.')).toBeTruthy());
  });

  it('maps sign-in failures to messages', () => {
    expect(signInErrorKey(new NetworkError())).toBe('mobile.signIn.errorUnreachable');
    expect(signInErrorKey(new ApiError(401, null))).toBe('mobile.signIn.errorCredentials');
    expect(signInErrorKey(new ApiError(429, null))).toBe('mobile.signIn.errorRateLimited');
    expect(signInErrorKey(new ApiError(500, null))).toBe('mobile.signIn.errorGeneric');
    expect(signInErrorKey(new ApiError(400, { message: 'x-tenant-guid is required.' }))).toBe('mobile.signIn.errorGeneric');
    expect(signInServerMessage(new ApiError(400, { message: 'x-tenant-guid is required.' }))).toBe('x-tenant-guid is required.');
    expect(signInServerMessage(new ApiError(400, null))).toBeNull();
    expect(signInServerMessage(new ApiError(401, { message: 'No.' }))).toBeNull();
  });

  it('lists workspaces by name and shows the ID only for a shared name', () => {
    expect(tenantOptions([{ id: 'ten_a', name: 'Acme' }, { id: 'ten_b', name: 'Beta' }])).toEqual([{ value: 'ten_a', label: 'Acme', detail: undefined }, { value: 'ten_b', label: 'Beta', detail: undefined }]);
    expect(tenantOptions([{ id: 'ten_a', name: 'Acme' }, { id: 'ten_b', name: 'Acme' }]).map((o) => o.detail)).toEqual(['ten_a', 'ten_b']);
    expect(preselectedTenant([{ id: 'ten_a' }, { id: 'ten_b' }], 'ten_b')).toBe('ten_b');
    expect(preselectedTenant([{ id: 'ten_a' }, { id: 'ten_b' }], 'ten_gone')).toBe('ten_a');
  });
});

describe('usage screen', () => {
  const report = {
    fromUtc: '2026-10-20T00:00:00Z',
    toUtc: '2026-11-19T00:00:00Z',
    groupBy: 'Task',
    totals: { calls: 12, failures: 1, costUsd: 0.42, totalTokens: 52000, inputTokens: 40000, cachedInputTokens: 10000, outputTokens: 12000, toolCalls: 3 },
    rows: [
      { key: 'Rank', label: 'Rank', calls: 8, costUsd: 0.3 },
      { key: 'Compose', label: 'Compose', calls: 4, costUsd: 0.12 },
    ],
  };

  it('shows totals and a cost chart from GET /v1.0/api/usage/me', async () => {
    const { server, controller } = await renderWith(<UsageScreen />, { 'POST /v1.0/token': () => ({ body: tokenResponse() }), 'GET /v1.0/api/usage/me': () => ({ body: report }) });
    await act(async () => controller.signIn({ email: 'a@b', password: 'p' }));
    await waitFor(() => expect(screen.getByTestId('usage-totals')).toBeTruthy());
    expect(screen.getByText('$0.42')).toBeTruthy();
    expect(screen.getByTestId('usage-cost-chart').props.accessibilityLabel).toContain('Rank: $0.30');
    const call = server.calls.find((c) => c.key === 'GET /v1.0/api/usage/me');
    expect(call.url).toMatch(/groupBy=Task/);
  });

  it('turns report rows into chart rows and token types', () => {
    expect(usageChartRows(report, 'calls')).toEqual([{ label: 'Rank', value: 8 }, { label: 'Compose', value: 4 }]);
    const t = (k) => k.split('.').pop();
    expect(tokenMixRows(report.totals, t)).toEqual([{ label: 'input', value: 30000 }, { label: 'cached', value: 10000 }, { label: 'output', value: 12000 }]);
    const w = usageWindow(7, Date.parse('2026-11-19T00:00:00Z'));
    expect(w).toEqual({ from: '2026-11-12T00:00:00.000Z', to: '2026-11-19T00:00:00.000Z' });
  });
});

describe('screens before their server phase', () => {
  it('say "later server update" instead of an error when the route is missing', async () => {
    const { controller } = await renderWith(<BriefingsScreen />, { 'POST /v1.0/token': () => ({ body: tokenResponse() }) });
    await act(async () => controller.signIn({ email: 'a@b', password: 'p' }));
    await waitFor(() => expect(screen.getByTestId('state-not-available')).toBeTruthy());
    expect(screen.getByText('Briefings will be available after a later server update.')).toBeTruthy();
  });

  it('notifications explain that push is unsupported when the devices route is missing', async () => {
    await renderWith(<NotificationsSection />, {});
    await waitFor(() => expect(screen.getAllByText('Push notifications arrive in a later server update.').length).toBeGreaterThan(0));
    expect(pushStatusKey('simulator')).toBe('mobile.notifications.statusSimulator');
    expect(pushStatusKey('nope')).toBeNull();
  });
});

describe('player provider', () => {
  it('restores a saved speed once and persists later changes without looping', async () => {
    const service = new PlayerService({ audio: { createAudioPlayer: jest.fn(), setAudioModeAsync: jest.fn(async () => undefined) } });
    const seen = {};
    function Probe() {
      const { state } = usePlayer();
      const { prefs } = usePrefs();
      seen.rate = state.rate;
      seen.saved = prefs.playbackRate;
      return null;
    }
    await renderWith(
      <PlayerProvider service={service}>
        <Probe />
      </PlayerProvider>,
      {},
      { playbackRate: 1.5 },
    );
    await waitFor(() => expect(seen).toEqual({ rate: 1.5, saved: 1.5 }));
    await act(async () => service.setRate(1.75));
    await waitFor(() => expect(seen).toEqual({ rate: 1.75, saved: 1.75 }));
  });
});

describe('helpers', () => {
  it('lays out bars proportionally and folds the long tail', () => {
    const { bars, height } = barLayout([{ label: 'a', value: 10 }, { label: 'b', value: 5 }, { label: 'c', value: 0 }], { width: 200, rowHeight: 10, gap: 5 });
    expect(bars.map((b) => b.width)).toEqual([200, 100, 0]);
    expect(bars[0].share).toBeCloseTo(2 / 3);
    expect(height).toBe(40);
    expect(barLayout([{ label: 'tiny', value: 0.001 }, { label: 'big', value: 1000 }], { width: 100 }).bars[0].width).toBe(2);
    expect(topRows([{ label: 'a', value: 1 }, { label: 'b', value: 3 }, { label: 'c', value: 2 }], 2, 'Other')).toEqual([{ label: 'b', value: 3 }, { label: 'c', value: 2 }, { label: 'Other', value: 1 }]);
  });

  it('checks passwords before sending them', () => {
    expect(passwordProblem('', 'abcdefgh', 'abcdefgh')).toBe('mobile.password.errorCurrent');
    expect(passwordProblem('old', 'short', 'short')).toBe('mobile.password.errorShort');
    expect(passwordProblem('old', 'abcdefgh', 'abcdefgX')).toBe('mobile.password.errorMismatch');
    expect(passwordProblem('abcdefgh', 'abcdefgh', 'abcdefgh')).toBe('mobile.password.errorSame');
    expect(passwordProblem('old', 'abcdefgh', 'abcdefgh')).toBeNull();
  });

  it('normalizes server URLs and flags plain HTTP', () => {
    expect(normalizeServerUrl(' app.mymorningnews.ai/ ')).toBe('https://app.mymorningnews.ai');
    expect(normalizeServerUrl('http://127.0.0.1:12100//')).toBe('http://127.0.0.1:12100');
    expect(normalizeServerUrl('')).toBe('');
    expect(isInsecureServer('http://192.168.1.20:12100')).toBe(true);
    expect(isInsecureServer('https://app.mymorningnews.ai')).toBe(false);
  });

  it('resolves the theme from the preference and the system', () => {
    expect(resolvePalette('system', 'dark')).toBe(dark);
    expect(resolvePalette('system', 'light')).toBe(light);
    expect(resolvePalette('dark', 'light')).toBe(dark);
    expect(resolvePalette('light', 'dark')).toBe(light);
  });

  it('formats the player clock and costs', () => {
    expect(formatClock(0)).toBe('0:00');
    expect(formatClock(75)).toBe('1:15');
    expect(formatClock(3725)).toBe('1:02:05');
    expect(formatClock(-3)).toBe('0:00');
    expect(formatCost(0.42, 'en')).toBe('$0.42');
    expect(formatCost(0.0012, 'en')).toBe('$0.0012');
  });

  it('splits briefing Markdown into readable blocks', () => {
    const blocks = parseBlocks('# Top of the news\n\nA **big** [story](http://x.example).\n- one\n> quote');
    expect(blocks.map((b) => b.type)).toEqual(['heading', 'paragraph', 'list', 'quote']);
    expect(blocks[0]).toMatchObject({ level: 1, text: 'Top of the news' });
    expect(plainText(blocks[1].children)).toBe('A big story.');
    expect(plainText(blocks[2].items[0].children)).toBe('one');
    expect(plainText(blocks[3].children)).toBe('quote');
  });
});
