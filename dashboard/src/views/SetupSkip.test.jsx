import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SETUP_PASSED_KEY } from '../context/SetupContext.jsx';
import { ADMIN_DETAILS, MEMBER_DETAILS, READY_SETUP } from '../test/fakeClient.js';
import { renderRoute } from '../test/renderRoute.jsx';
import { ApiError, SETUP_REQUIRED_EVENT } from '../utils/api.js';

// Setup is never a trap: Settings opens on Profile, a ready workspace gets a status page, and an unfinished setup can
// be skipped with a warning that says what is missing.
const MODEL_READY = READY_SETUP.checks[0];
const SEARCH_READY = READY_SETUP.checks[1];
const extra = (kind, more = {}) => ({ key: kind.toLowerCase(), kind, required: false, state: 'Ready', message: null, resourceId: null, resourceName: null, provider: null, model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: null, ...more });
const SPEECH = extra('Speech', { provider: 'OpenAiCompatibleSpeech', resourceName: 'tts:8880', baseUrl: 'http://tts:8880/v1', model: 'kokoro', voice: 'af_heart', verifiedUtc: '2026-11-20T08:00:00Z' });
const EXTRAS = [extra('Embeddings', { state: 'Missing', message: 'No embedding endpoint is set up.' }), extra('Email', { state: 'Missing', message: 'Mailgun is not configured.' }), SPEECH];
const READY = { ...READY_SETUP, checks: [MODEL_READY, SEARCH_READY, ...EXTRAS] };
const NOT_READY = {
  ready: false,
  canConfigure: true,
  checkedUtc: '2026-11-20T08:00:00Z',
  checks: [{ ...MODEL_READY, state: 'Missing', hasApiKey: false, verifiedUtc: null, message: 'Anthropic needs an API key.' }, SEARCH_READY, ...EXTRAS]
};
const NOTHING_WORKS = { ...NOT_READY, checks: [NOT_READY.checks[0], { ...SEARCH_READY, state: 'Failed', message: 'SearXNG did not answer.' }] };
const MEMBER_BLOCKED = { ...NOT_READY, canConfigure: false, checks: NOT_READY.checks.slice(0, 2) };
const passes = () => JSON.parse(window.localStorage.getItem(SETUP_PASSED_KEY) || '{}');

describe('Settings and the Setup status page in a ready workspace', () => {
  it('opens Settings on Profile, with Setup as the last tab', async () => {
    renderRoute('/dashboard/home', { getSetupStatus: vi.fn(async () => READY) });
    const settings = await screen.findByRole('link', { name: 'Settings' });
    expect(settings).toHaveAttribute('href', '/dashboard/profile');
    await userEvent.click(settings);
    const strip = await screen.findByRole('navigation', { name: 'Settings pages' });
    expect(within(strip).getAllByRole('link').map((link) => link.textContent)).toEqual(['Profile', 'Notifications', 'Server settings', 'Server info', 'Setup']);
    expect(within(strip).getByRole('link', { name: 'Profile' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('heading', { name: 'Set up My Morning News' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
  });

  it('shows Setup as a status page with normal page chrome and no takeover', async () => {
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => READY) });
    expect(await screen.findByRole('heading', { name: 'Setup', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('What My Morning News is connected to. Change a connection or test it again here.')).toBeInTheDocument();
    // The sidebar and the Settings tab strip are there, with every page open.
    const strip = screen.getByRole('navigation', { name: 'Settings pages' });
    expect(within(strip).getByRole('link', { name: 'Setup' })).toHaveAttribute('aria-current', 'page');
    expect(within(strip).getByRole('link', { name: 'Profile' })).not.toHaveClass('hub-tab-blocked');
    expect(screen.getByRole('link', { name: 'Topics' })).not.toHaveClass('nav-item-blocked');
    // What is connected: the model, search, and the optional items, open.
    const model = screen.getByTestId('setup-check-model:mep_1');
    expect(within(model).getByText('claude-opus-5-5')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument();
    expect(screen.getByText('Web search works (SearXNG).')).toBeInTheDocument();
    expect(screen.getByTestId('setup-extras')).toHaveAttribute('open');
    // Nothing of the wizard: no checklist, no step numbers, no "You're ready", no first-topic prompt, no skip.
    expect(screen.queryByRole('heading', { name: 'Set up My Morning News' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument();
    expect(screen.queryByText('Step 1')).not.toBeInTheDocument();
    expect(screen.queryByText("You're ready")).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Create your first topic' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-complete')).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-skip')).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
  });

  it('never sends a ready workspace to Setup, from the dashboard root or a product route', async () => {
    renderRoute('/dashboard', { getSetupStatus: vi.fn(async () => READY) });
    const home = await screen.findByRole('link', { name: 'Home' });
    await waitFor(() => expect(home).toHaveAttribute('aria-current', 'page'));
    expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Topics' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
  });
});

describe('skipping setup', () => {
  it('lists what is missing, keeps setting up on request, and opens the app on Skip for now', async () => {
    renderRoute('/dashboard/topics', { getSetupStatus: vi.fn(async () => NOT_READY) });
    // First run: the guide leads to Setup.
    expect(await screen.findByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Step 1')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Skip for now' }));
    const dialog = screen.getByRole('dialog', { name: 'Skip setup for now?' });
    expect(within(dialog).getByText('Model: Anthropic')).toBeInTheDocument();
    expect(within(dialog).getByText('Missing')).toBeInTheDocument();
    expect(within(dialog).getByText('Anthropic needs an API key.')).toBeInTheDocument();
    expect(within(dialog).getByText('The topic wizard, research runs, and briefings will not work. They need a working AI model.')).toBeInTheDocument();
    // Search works here, so it is not listed.
    expect(within(dialog).queryByText(/^Search/)).not.toBeInTheDocument();
    expect(within(dialog).queryByText('Research cannot find new sources. It needs a web search provider.')).not.toBeInTheDocument();
    expect(within(dialog).queryByText(/Embeddings|Email|Speech/)).not.toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep setting up' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
    expect(passes()).toEqual({});

    await userEvent.click(screen.getByRole('button', { name: 'Skip for now' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Skip for now' }));
    // Home opens, the callout stays, nothing is dimmed, and the page says what will not work.
    expect(await screen.findByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument();
    expect(passes()).toEqual({ ['default|' + ADMIN_DETAILS.userId]: 'skipped' });
    expect(screen.getByTestId('setup-callout')).toHaveAttribute('href', '/dashboard/setup');
    expect(screen.getByRole('link', { name: 'Topics' })).not.toHaveClass('nav-item-blocked');
    const warning = screen.getByTestId('setup-warning');
    expect(warning).toHaveTextContent('Setup is not finished. Still needed: Model: Anthropic. The topic wizard, research runs, and briefings will not work until then.');
    expect(within(warning).getByRole('link', { name: 'Finish setup' })).toHaveAttribute('href', '/dashboard/setup');

    // The warning can be dismissed; pages stay open and the callout stays.
    await userEvent.click(within(warning).getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Topics' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
    expect(screen.getByTestId('setup-callout')).toBeInTheDocument();
  });

  it('names both the model and search when neither works', async () => {
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => NOTHING_WORKS) });
    await userEvent.click(await screen.findByRole('button', { name: 'Skip for now' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText('Model: Anthropic')).toBeInTheDocument();
    expect(within(dialog).getByText('Search: SearXNG')).toBeInTheDocument();
    expect(within(dialog).getByText('SearXNG did not answer.')).toBeInTheDocument();
    expect(within(dialog).getByText('Research cannot find new sources. It needs a web search provider.')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Skip for now' }));
    expect(await screen.findByTestId('setup-warning')).toHaveTextContent('Still needed: Model: Anthropic and Search: SearXNG.');
  });

  it('remembers the choice for the next visit and never turns the guide back on by itself', async () => {
    window.localStorage.setItem(SETUP_PASSED_KEY, JSON.stringify({ ['default|' + ADMIN_DETAILS.userId]: 'skipped', 'default|usr_other': 'skipped' }));
    const getSetupStatus = vi.fn(async () => NOT_READY);
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.getByTestId('setup-warning')).toBeInTheDocument();
    expect(screen.getByTestId('setup-callout')).toBeInTheDocument();

    // A request the server refuses re-reads the status and shows the server's message; the page stays.
    const calls = getSetupStatus.mock.calls.length;
    act(() => {
      window.dispatchEvent(new CustomEvent(SETUP_REQUIRED_EVENT, { detail: { path: '/v1.0/api/refinement-sessions', message: 'Anthropic needs an API key. An administrator can finish this on the Setup page.' } }));
    });
    await waitFor(() => expect(getSetupStatus.mock.calls.length).toBe(calls + 1));
    expect(await screen.findByText('Anthropic needs an API key. An administrator can finish this on the Setup page.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-progress')).not.toBeInTheDocument();

    // Pages that do not depend on setup carry no warning once that message is dismissed.
    await userEvent.click(within(screen.getByTestId('setup-warning')).getByRole('button', { name: 'Dismiss' }));
    await userEvent.click(screen.getByRole('link', { name: 'Settings' }));
    expect(await screen.findByRole('navigation', { name: 'Settings pages' })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
  });

  it('turns the guide back on only when the person asks, from the Setup page', async () => {
    window.localStorage.setItem(SETUP_PASSED_KEY, JSON.stringify({ ['default|' + ADMIN_DETAILS.userId]: 'skipped', 'default|usr_other': 'skipped' }));
    renderRoute('/dashboard/home', { getSetupStatus: vi.fn(async () => NOT_READY) });
    await userEvent.click(await screen.findByTestId('setup-callout'));
    expect(await screen.findByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
    // Skipped: the steps are here to finish, without the warning banner or a second Skip button.
    expect(screen.getByTestId('setup-skipped-note')).toHaveTextContent('You skipped setup, so every page is open.');
    expect(screen.queryByTestId('setup-skip')).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-warning')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Topics' })).not.toHaveClass('nav-item-blocked');

    await userEvent.click(screen.getByRole('button', { name: 'Keep me on this page until setup is done' }));
    expect(screen.getByTestId('setup-skip')).toBeInTheDocument();
    expect(screen.queryByTestId('setup-skipped-note')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Topics' })).toHaveClass('nav-item-blocked');
    // Only this person's choice in this workspace is forgotten.
    expect(passes()).toEqual({ 'default|usr_other': 'skipped' });
  });

  it('ends the guide for good once setup has been ready', async () => {
    const getSetupStatus = vi.fn(async () => READY);
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    await waitFor(() => expect(passes()).toEqual({ ['default|' + ADMIN_DETAILS.userId]: 'ready' }));
  });
});

describe('members while setup is not finished', () => {
  it('explains once, then lets the member continue into the app and remembers it', async () => {
    const client = renderRoute('/dashboard/topics', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus: vi.fn(async () => MEMBER_BLOCKED) });
    const blocked = await screen.findByTestId('setup-blocked');
    expect(blocked).toHaveTextContent('This workspace is not ready');
    expect(within(blocked).getByText('You can look around in the meantime. Anything that needs the AI model will not work until setup is finished.')).toBeInTheDocument();
    await userEvent.click(within(blocked).getByRole('button', { name: 'Continue to the app' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(passes()).toEqual({ ['default|' + MEMBER_DETAILS.userId]: 'skipped' });
    // A member cannot open Setup, so there is no callout; the warning says who can finish it.
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
    const warning = screen.getByTestId('setup-warning');
    expect(warning).toHaveTextContent('Still needed: Model: Anthropic.');
    expect(warning).toHaveTextContent('An administrator needs to finish setup before this works.');
    expect(within(warning).queryByRole('link')).not.toBeInTheDocument();
    expect(client.verifySetup).not.toHaveBeenCalled();
  });

  it('goes straight in on the next visit', async () => {
    window.localStorage.setItem(SETUP_PASSED_KEY, JSON.stringify({ ['default|' + MEMBER_DETAILS.userId]: 'skipped' }));
    renderRoute('/dashboard/topics', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus: vi.fn(async () => MEMBER_BLOCKED) });
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-blocked')).not.toBeInTheDocument();
  });
});

describe('speech on the Setup page', () => {
  it('shows the speech server, model, and default voice with links to where each is changed', async () => {
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => READY) });
    const card = await screen.findByTestId('setup-check-speech');
    expect(within(card).getByText('Speech')).toBeInTheDocument();
    expect(within(card).getByText('Ready')).toBeInTheDocument();
    expect(within(card).getByText('Reads briefings aloud and makes podcast episodes.')).toBeInTheDocument();
    expect(within(card).getByText('tts:8880')).toBeInTheDocument();
    expect(within(card).getByText('kokoro')).toBeInTheDocument();
    expect(within(card).getByText('af_heart')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Speech server settings' })).toHaveAttribute('href', '/dashboard/settings');
    expect(within(card).getByRole('link', { name: 'Voices' })).toHaveAttribute('href', '/dashboard/voices');
    expect(within(card).queryByText(/Only a server administrator/)).not.toBeInTheDocument();
    expect(screen.getByText('1 of 3 turned on')).toBeInTheDocument();
  });

  it('tests speech alone and shows the result, or the reason it failed', async () => {
    const failed = { ...NOT_READY, checks: NOT_READY.checks.map((c) => (c.kind === 'Speech' ? { ...c, state: 'Failed', message: 'Could not reach the speech endpoint at http://tts:8880/v1: Connection refused.' } : c)) };
    const verifySetup = vi
      .fn()
      .mockResolvedValueOnce(NOT_READY)
      .mockRejectedValueOnce(new ApiError(500, { error: 'InternalError', message: 'The check could not run.' }));
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => failed), verifySetup });
    // While setup is unfinished the optional items are collapsed, and speech is one of them.
    const extras = await screen.findByTestId('setup-extras');
    expect(extras).not.toHaveAttribute('open');
    const card = screen.getByTestId('setup-check-speech');
    expect(within(card).getByText('Failed')).toBeInTheDocument();
    expect(within(card).getByText('Could not reach the speech endpoint at http://tts:8880/v1: Connection refused.')).toBeInTheDocument();
    await userEvent.click(within(card).getByRole('button', { name: 'Test speech' }));
    await waitFor(() => expect(verifySetup).toHaveBeenCalledWith('Speech'));
    expect(await within(screen.getByTestId('setup-check-speech')).findByText('Speech works.')).toBeInTheDocument();
    expect(within(screen.getByTestId('setup-check-speech')).getByText('Ready')).toBeInTheDocument();
    await userEvent.click(within(screen.getByTestId('setup-check-speech')).getByRole('button', { name: 'Test speech' }));
    expect(await within(screen.getByTestId('setup-check-speech')).findByText('The check could not run.')).toBeInTheDocument();
    expect(verifySetup).toHaveBeenCalledTimes(2);
  });

  it('tells a workspace administrator who can change the speech server', async () => {
    renderRoute('/dashboard/setup', { getTokenDetails: vi.fn(async () => ({ ...ADMIN_DETAILS, isAdmin: false, isTenantAdmin: true })), getSetupStatus: vi.fn(async () => READY) });
    const card = await screen.findByTestId('setup-check-speech');
    expect(within(card).queryByRole('link', { name: 'Speech server settings' })).not.toBeInTheDocument();
    expect(within(card).getByText('Only a server administrator can change the speech server, under Settings, Server settings.')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Voices' })).toHaveAttribute('href', '/dashboard/voices');
  });
});
