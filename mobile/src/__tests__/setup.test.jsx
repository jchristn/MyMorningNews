import React, { useState } from 'react';
import { Linking, Pressable, Text } from 'react-native';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { I18nextProvider } from 'react-i18next';
import { setupApi } from '@/api/account';
import { ApiClient } from '@/api/client';
import { ApiError, isSetupRequired } from '@/api/errors';
import { requestsApi } from '@/api/research';
import { routes } from '@/api/routes';
import { SetupGate, dashboardUrl, normalizeSetupStatus, pendingChecks } from '@/components/SetupGate';
import { ActionError } from '@/components/states';
import { initI18n } from '@/i18n/runtime';
import { setupSkipKey } from '@/storage/prefs';
import { useAuth } from '@/state/AuthContext';
import { ThemeProvider } from '@/theme/ThemeContext';
import { fakeServer } from './fakeServer';
import { renderSignedIn } from './renderApp';

const MODEL = { key: 'model:mep_1', kind: 'Model', required: true, state: 'Ready', message: null, resourceId: 'mep_1', resourceName: 'Anthropic', provider: 'Anthropic', model: 'claude-opus-5-5', tasks: ['Refinement'], needsApiKey: true, hasApiKey: true, usesPlatformKey: false, verifiedUtc: '2026-11-20T08:00:00Z' };
const SEARCH = { key: 'search', kind: 'Search', required: true, state: 'Ready', message: null, resourceId: 'srp_1', resourceName: 'SearXNG', provider: 'Searxng', model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: null };
const READY = { ready: true, canConfigure: true, checkedUtc: '2026-11-20T08:00:00Z', checks: [MODEL, SEARCH] };
const missing = (canConfigure) => ({
  ready: false,
  canConfigure,
  checkedUtc: '2026-11-20T08:00:00Z',
  checks: [{ ...MODEL, state: 'Missing', hasApiKey: false, message: 'Endpoint Anthropic (Anthropic) needs an API key.' }, SEARCH],
});
const REFUSAL = { status: 409, body: { error: 'SetupRequired', message: 'Add and verify a model API key first.', details: { check: 'model:mep_1' }, requestId: 'rqi_1' } };
const CONFIG = { productName: 'My Morning News', serverVersion: '0.1.0-alpha', links: {}, copyright: '(c) 2026 Lattice AI LLC', appUrl: 'https://app.example.com' };

const Product = () => <Text testID="product">tabs</Text>;

/** A product screen that starts a one-time request and keeps the error it gets. */
function Starter() {
  const { client } = useAuth();
  const [error, setError] = useState(null);
  return (
    <>
      <Pressable testID="start" onPress={() => requestsApi.create(client, { requestText: 'x' }).catch(setError)}>
        <Text>start</Text>
      </Pressable>
      <ActionError error={error} />
    </>
  );
}

/** Answers GET /setup from a list: each call takes the next answer, and the last one repeats. */
function sequence(...answers) {
  let i = 0;
  return () => answers[Math.min(i++, answers.length - 1)];
}

describe('setup API', () => {
  it('reads the setup status and recognizes SetupRequired', async () => {
    const server = fakeServer({ 'GET /v1.0/api/setup': () => ({ body: READY }), 'POST /v1.0/api/requests': () => REFUSAL });
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', fetchImpl: server.fetchImpl });
    const listener = jest.fn();
    const remove = client.onSetupRequired(listener);
    expect(routes.setup).toBe('/v1.0/api/setup');
    await expect(setupApi.status(client)).resolves.toEqual(READY);
    const error = await requestsApi.create(client, { requestText: 'x' }).catch((e) => e);
    expect(isSetupRequired(error)).toBe(true);
    expect(error.details).toEqual({ check: 'model:mep_1' });
    expect(listener).toHaveBeenCalledWith(expect.objectContaining({ error: 'SetupRequired' }));
    remove();
    await requestsApi.create(client, { requestText: 'x' }).catch(() => undefined);
    expect(listener).toHaveBeenCalledTimes(1);
    expect(isSetupRequired(new ApiError(409, { error: 'Conflict' }))).toBe(false);
  });

  it('normalizes the status and picks out what is pending', () => {
    expect(() => normalizeSetupStatus({ items: [] })).toThrow();
    const status = normalizeSetupStatus(missing(false));
    expect(pendingChecks(status).map((c) => c.key)).toEqual(['model:mep_1']);
    expect(dashboardUrl({ appUrl: ' https://app.example.com ' })).toBe('https://app.example.com');
    expect(dashboardUrl({ appUrl: 'javascript:alert(1)' })).toBeNull();
    expect(dashboardUrl(null)).toBeNull();
  });
});

describe('setup gate', () => {
  it('opens the product when setup is ready', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: READY }) },
    );
    expect(await screen.findByTestId('product')).toBeTruthy();
  });

  it('blocks a member until an administrator finishes setup, then opens on Check again', async () => {
    const { server } = await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': sequence({ body: missing(false) }, { body: READY }) },
    );
    expect(await screen.findByText('This workspace is not ready')).toBeTruthy();
    expect(screen.getByText('An administrator needs to add and verify the model API key before anyone can use My Morning News.')).toBeTruthy();
    expect(screen.getByText('Endpoint Anthropic (Anthropic) needs an API key.')).toBeTruthy();
    expect(screen.getByText('Missing')).toBeTruthy();
    expect(screen.queryByTestId('setup-dashboard')).toBeNull();
    expect(screen.queryByTestId('product')).toBeNull();
    fireEvent.press(screen.getByTestId('setup-check-again'));
    expect(await screen.findByTestId('product')).toBeTruthy();
    expect(server.count('GET /v1.0/api/setup')).toBe(2);
  });

  it('tells an administrator to finish setup in the web dashboard and opens it', async () => {
    const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(true) }), 'GET /v1.0/api/client-config': () => ({ body: CONFIG }) },
    );
    expect(await screen.findByText('Finish setup in the web dashboard: add and verify the model API key, then check again here.')).toBeTruthy();
    expect(await screen.findByText('Web dashboard: https://app.example.com')).toBeTruthy();
    fireEvent.press(screen.getByTestId('setup-open-dashboard'));
    expect(open).toHaveBeenCalledWith('https://app.example.com');
    open.mockRestore();
  });

  it('keeps the product closed when the status cannot be read', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': sequence({ status: 500, body: { error: 'InternalError', message: 'Boom', requestId: 'rqi_2' } }, { body: READY }) },
    );
    expect(await screen.findByText('Setup status could not be loaded')).toBeTruthy();
    expect(screen.getByText('Boom')).toBeTruthy();
    expect(screen.queryByTestId('product')).toBeNull();
    fireEvent.press(screen.getByTestId('setup-check-again'));
    expect(await screen.findByTestId('product')).toBeTruthy();
  });

  it('keeps the app closed on a server without the setup route', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
    );
    expect(await screen.findByTestId('setup-error')).toBeTruthy();
    expect(screen.queryByTestId('product')).toBeNull();
  });

  it('signs out from the blocked screen', async () => {
    const { controller } = await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(false) }), 'DELETE /v1.0/token': () => ({ status: 204 }) },
    );
    fireEvent.press(await screen.findByTestId('setup-sign-out'));
    await waitFor(() => expect(controller.getState().status).toBe('signedOut'));
  });

  it('re-checks on a SetupRequired refusal and closes the product again', async () => {
    const { server } = await renderSignedIn(
      <SetupGate>
        <Starter />
      </SetupGate>,
      { 'GET /v1.0/api/setup': sequence({ body: READY }, { body: missing(false) }), 'POST /v1.0/api/requests': () => REFUSAL },
    );
    fireEvent.press(await screen.findByTestId('start'));
    expect(await screen.findByText('This workspace is not ready')).toBeTruthy();
    expect(server.count('GET /v1.0/api/setup')).toBe(2);
  });
});

/** The remembered-skip key of the account `renderSignedIn` signs in: server, workspace, and user. */
const SKIP_KEY = 'http://127.0.0.1:12100|default|usr_1';

describe('setup gate: going on without finished setup', () => {
  it('lets a member continue to the app, keeps a callout, and remembers the choice', async () => {
    const AsyncStorage = require('@react-native-async-storage/async-storage');
    await AsyncStorage.clear();
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(false) }) },
    );
    expect(await screen.findByText('This workspace is not ready')).toBeTruthy();
    expect(screen.queryByTestId('setup-skip')).toBeNull();
    expect(screen.getByText('You can look around in the meantime. Research, briefings, and audio will not work until setup is finished.')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('setup-continue'));
    expect(await screen.findByTestId('product')).toBeTruthy();
    expect(screen.getByTestId('setup-skipped-banner')).toBeTruthy();
    expect(screen.getByText('Research will not work until an administrator finishes setup.')).toBeTruthy();
    await waitFor(async () => expect(JSON.parse(await AsyncStorage.getItem('mymorningnews.prefs')).setupSkipped).toEqual({ [SKIP_KEY]: true }));
    expect(setupSkipKey({ serverUrl: 'http://127.0.0.1:12100', tenantId: 'default', user: { id: 'usr_1' } })).toBe(SKIP_KEY);
    expect(setupSkipKey({ serverUrl: 'x', tenantId: null, user: { id: 'usr_1' } })).toBeNull();
  });

  it('asks an administrator to confirm Skip for now, listing what is missing', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(true) }) },
    );
    await fireEvent.press(await screen.findByTestId('setup-skip'));
    expect(screen.queryByTestId('setup-continue')).toBeNull();
    expect(screen.getByText('Skip setup for now?')).toBeTruthy();
    expect(screen.getByTestId('setup-skip-missing-model:mep_1').props.children).toBe('Model: Anthropic: Missing');
    expect(screen.queryByTestId('setup-skip-missing-search')).toBeNull();
    expect(screen.queryByTestId('product')).toBeNull();
    // Cancel keeps the app closed.
    await fireEvent.press(screen.getByTestId('setup-skip-cancel'));
    expect(screen.queryByTestId('setup-skip-confirm')).toBeNull();
    expect(screen.queryByTestId('product')).toBeNull();
    await fireEvent.press(screen.getByTestId('setup-skip'));
    await fireEvent.press(screen.getByTestId('setup-skip-confirm-button'));
    expect(await screen.findByTestId('product')).toBeTruthy();
    expect(screen.getByText('Research will not work until you finish setup in the web dashboard.')).toBeTruthy();
  });

  it('opens straight away for someone who already skipped, and shows the details again on request', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(false) }) },
      { prefs: { setupSkipped: { [SKIP_KEY]: true } } },
    );
    expect(await screen.findByTestId('product')).toBeTruthy();
    expect(screen.getByTestId('setup-skipped-banner')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('setup-skipped-review'));
    expect(await screen.findByText('This workspace is not ready')).toBeTruthy();
    expect(screen.getByText('Endpoint Anthropic (Anthropic) needs an API key.')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('setup-continue'));
    expect(await screen.findByTestId('product')).toBeTruthy();
  });

  it('remembers the choice per user and workspace, not for everyone on the device', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: missing(false) }) },
      { prefs: { setupSkipped: { 'http://127.0.0.1:12100|default|usr_2': true, 'http://127.0.0.1:12100|ten_other|usr_1': true } } },
    );
    expect(await screen.findByText('This workspace is not ready')).toBeTruthy();
    expect(screen.queryByTestId('product')).toBeNull();
  });

  it('shows no gate and no callout in a ready workspace, even after an earlier skip', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ body: READY }) },
      { prefs: { setupSkipped: { [SKIP_KEY]: true } } },
    );
    expect(await screen.findByTestId('product')).toBeTruthy();
    expect(screen.queryByTestId('setup-skipped-banner')).toBeNull();
    expect(screen.queryByTestId('setup-blocked')).toBeNull();
  });

  it('offers no way in when the status could not be read', async () => {
    await renderSignedIn(
      <SetupGate>
        <Product />
      </SetupGate>,
      { 'GET /v1.0/api/setup': () => ({ status: 500, body: { error: 'InternalError', message: 'Boom', requestId: 'rqi_2' } }) },
      { prefs: { setupSkipped: { [SKIP_KEY]: true } } },
    );
    expect(await screen.findByText('Setup status could not be loaded')).toBeTruthy();
    expect(screen.queryByTestId('setup-continue')).toBeNull();
    expect(screen.queryByTestId('setup-skip')).toBeNull();
    expect(screen.queryByTestId('product')).toBeNull();
  });
});

describe('SetupRequired action error', () => {
  it('shows the server message and where setup is finished', async () => {
    await render(
      <I18nextProvider i18n={initI18n('en')}>
        <ThemeProvider preference="light">
          <ActionError error={new ApiError(409, REFUSAL.body)} />
        </ThemeProvider>
      </I18nextProvider>,
    );
    expect(screen.getByText('Setup is not finished')).toBeTruthy();
    expect(screen.getByText('Add and verify a model API key first.')).toBeTruthy();
    expect(screen.getByText('An administrator needs to finish setup in the web dashboard before this works.')).toBeTruthy();
  });
});
