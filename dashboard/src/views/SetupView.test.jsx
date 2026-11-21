import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS, READY_SETUP } from '../test/fakeClient.js';
import { renderRoute } from '../test/renderRoute.jsx';
import { ApiError, SETUP_REQUIRED_EVENT } from '../utils/api.js';

const MODEL_READY = READY_SETUP.checks[0];
const SEARCH_READY = READY_SETUP.checks[1];

function status({ ready = false, canConfigure = true, model = {}, search = {}, extra = [] } = {}) {
  return {
    ready,
    canConfigure,
    checkedUtc: '2026-11-20T08:00:00Z',
    checks: [{ ...MODEL_READY, ...model }, { ...SEARCH_READY, ...search }, ...extra]
  };
}

const MISSING_KEY = status({ model: { state: 'Missing', hasApiKey: false, verifiedUtc: null, message: 'Endpoint Anthropic (Anthropic) needs an API key.' } });
const EMAIL_OPTIONAL = { key: 'email', kind: 'Email', required: false, state: 'Missing', message: 'Mailgun is not configured.', resourceId: null, resourceName: null, provider: null, model: null, tasks: [], needsApiKey: false, hasApiKey: false, usesPlatformKey: false, verifiedUtc: null };

describe('setup gate for administrators', () => {
  it('saves and verifies a missing model key, then opens the product without a reload', async () => {
    const setSetupModelKey = vi.fn(async () => READY_SETUP);
    const client = renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => ({ ...MISSING_KEY, checks: [...MISSING_KEY.checks, EMAIL_OPTIONAL] })), setSetupModelKey });
    const card = await screen.findByTestId('setup-check-model:mep_1');
    expect(within(card).getByText('Missing')).toBeInTheDocument();
    expect(within(card).getByText('Endpoint Anthropic (Anthropic) needs an API key.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Required' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Optional' })).toBeInTheDocument();
    expect(screen.getByText('Mailgun is not configured.')).toBeInTheDocument();
    expect(screen.getByText(/MYMORNINGNEWS_ANTHROPIC_API_KEY/)).toBeInTheDocument();
    expect(screen.getByTestId('setup-callout')).toBeInTheDocument();
    expect(client.verifySetup).not.toHaveBeenCalled();

    const input = within(card).getByLabelText('API key for Anthropic');
    expect(input).toHaveAttribute('type', 'password');
    await userEvent.type(input, ' sk-ant-test ');
    await userEvent.click(within(card).getByRole('button', { name: 'Save and verify' }));
    await waitFor(() => expect(setSetupModelKey).toHaveBeenCalledWith('mep_1', 'sk-ant-test'));

    expect(await screen.findByTestId('setup-ready')).toHaveTextContent("You're ready");
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Go to topics' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
  });

  it('shows the provider message when the key fails verification and keeps the form for another try', async () => {
    const failed = status({ model: { state: 'Failed', hasApiKey: true, message: 'invalid x-api-key' } });
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => MISSING_KEY), setSetupModelKey: vi.fn(async () => failed) });
    const card = await screen.findByTestId('setup-check-model:mep_1');
    await userEvent.type(within(card).getByLabelText('API key for Anthropic'), 'sk-bad');
    await userEvent.click(within(card).getByRole('button', { name: 'Save and verify' }));
    expect(await within(card).findByText('invalid x-api-key')).toBeInTheDocument();
    expect(within(card).getByText('Failed')).toBeInTheDocument();
    expect(within(card).getByLabelText('API key for Anthropic')).toHaveValue('');
    expect(within(card).getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-ready')).not.toBeInTheDocument();
  });

  it('redirects product routes to the Setup screen and keeps configuration pages open', async () => {
    renderRoute('/dashboard/topics', { getSetupStatus: vi.fn(async () => MISSING_KEY) });
    expect(await screen.findByTestId('setup-intro')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Setup', level: 1 })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('link', { name: 'Search providers' }));
    expect(await screen.findByRole('heading', { name: 'Search providers', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-intro')).not.toBeInTheDocument();
  });

  it('verifies once on open when a key is stored but unverified', async () => {
    const verifySetup = vi.fn(async () => READY_SETUP);
    renderRoute('/dashboard/setup', { getSetupStatus: vi.fn(async () => status({ model: { state: 'Unverified', verifiedUtc: null } })), verifySetup });
    expect(await screen.findByTestId('setup-ready')).toBeInTheDocument();
    expect(verifySetup).toHaveBeenCalledTimes(1);
  });

  it('links a failed search check to the search providers page without a key field', async () => {
    renderRoute('/dashboard/setup', {
      getSetupStatus: vi.fn(async () => status({ search: { state: 'Failed', message: 'SearXNG did not answer.', needsApiKey: true, hasApiKey: false } }))
    });
    const card = await screen.findByTestId('setup-check-search');
    expect(within(card).getByText('SearXNG did not answer.')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Open search providers' })).toHaveAttribute('href', '/dashboard/search-providers');
    expect(within(card).queryByLabelText(/API key/)).not.toBeInTheDocument();
  });

  it('sends a task with no endpoint to the Models page', async () => {
    renderRoute('/dashboard/setup', {
      getSetupStatus: vi.fn(async () => status({ model: { key: 'model:none', state: 'Missing', resourceId: null, resourceName: null, provider: null, model: null, message: 'No model endpoint serves Synthesis.' } }))
    });
    const card = await screen.findByTestId('setup-check-model:none');
    expect(within(card).getByText('No model endpoint serves Synthesis.')).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: 'Open the Models page' })).toHaveAttribute('href', '/dashboard/models');
    expect(within(card).queryByLabelText(/API key/)).not.toBeInTheDocument();
  });
});

describe('setup gate for everyone else', () => {
  it('shows members a full-screen message with Check again and Sign out', async () => {
    const blocked = status({ canConfigure: false, model: { state: 'Missing', hasApiKey: false } });
    const getSetupStatus = vi.fn(async () => blocked);
    const client = renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus });
    expect(await screen.findByTestId('setup-blocked')).toHaveTextContent('This workspace is not ready');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/API key/)).not.toBeInTheDocument();
    const calls = getSetupStatus.mock.calls.length;
    getSetupStatus.mockResolvedValueOnce(READY_SETUP);
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    await waitFor(() => expect(getSetupStatus.mock.calls.length).toBe(calls + 1));
    expect(await screen.findByRole('navigation')).toBeInTheDocument();
    expect(screen.queryByTestId('setup-blocked')).not.toBeInTheDocument();
    expect(client.verifySetup).not.toHaveBeenCalled();
  });

  it('signs a member out from the blocked screen', async () => {
    const client = renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getSetupStatus: vi.fn(async () => status({ canConfigure: false, model: { state: 'Failed' } })) });
    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
    await waitFor(() => expect(client.logout).toHaveBeenCalled());
    expect(await screen.findByRole('button', { name: /Sign in/ })).toBeInTheDocument();
  });

  it('keeps the product closed when the setup status cannot be loaded', async () => {
    const getSetupStatus = vi.fn(async () => {
      throw new ApiError(0, null, 'Network error');
    });
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByTestId('setup-load-error')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    getSetupStatus.mockImplementation(async () => READY_SETUP);
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
  });

  it('shows the normal app when setup is ready', async () => {
    renderRoute('/dashboard/topics');
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
    expect(screen.queryByTestId('setup-intro')).not.toBeInTheDocument();
  });
});

describe('SetupRequired refusals', () => {
  const refusal = () => new ApiError(409, { error: 'SetupRequired', message: 'Add and verify a model API key before starting research.', details: { check: 'model:mep_1' } });

  it('shows the server message and a link to Setup on a one-time request', async () => {
    renderRoute('/dashboard/one-time/new', {
      createResearchRequest: vi.fn(async () => {
        throw refusal();
      })
    });
    await userEvent.type(await screen.findByLabelText('What do you want?'), 'Heat pump rebates');
    await userEvent.click(screen.getByRole('button', { name: 'Plan it' }));
    expect(await screen.findByText('Add and verify a model API key before starting research.')).toBeInTheDocument();
    expect(screen.getByText('Setup is not finished.')).toBeInTheDocument();
    expect(screen.getByTestId('setup-required-link')).toHaveAttribute('href', '/dashboard/setup');
  });

  it('re-reads the setup status and re-engages the gate', async () => {
    const getSetupStatus = vi.fn(async () => READY_SETUP);
    renderRoute('/dashboard/topics', { getSetupStatus });
    expect(await screen.findByRole('heading', { name: 'Topics', level: 1 })).toBeInTheDocument();
    getSetupStatus.mockImplementation(async () => status({ model: { state: 'Failed', message: 'credit balance is too low' } }));
    act(() => {
      window.dispatchEvent(new CustomEvent(SETUP_REQUIRED_EVENT, { detail: { path: '/v1.0/api/refinement-sessions', message: 'Add and verify a model API key before starting research.' } }));
    });
    expect(await screen.findByTestId('setup-intro')).toBeInTheDocument();
    expect(screen.getByText('Add and verify a model API key before starting research.')).toBeInTheDocument();
    expect(screen.getByText('credit balance is too low')).toBeInTheDocument();
  });
});
