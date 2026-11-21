// Regression tests for the dashboard findings of the 2026-11-20 simulated user testing session
// (user-testing/2026-11-20-full-session.md). Each describe names its finding.
import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../App.jsx';
import HeatmapChart from '../charts/HeatmapChart.jsx';
import { AppProvider } from '../context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from '../context/AuthContext.jsx';
import { ThemeProvider } from '../context/ThemeContext.jsx';
import { ADMIN_DETAILS, MEMBER_DETAILS, fakeClient } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { DIMENSION, RUN, TOPIC, state } from '../test/researchFixtures.js';
import { ApiError } from '../utils/api.js';
import { PENDING_DELETION_KEY, deletionCancelledBySignIn, parsePurgeDate, rememberPendingDeletion } from '../utils/accountDeletion.js';
import { isHostedTenant } from './HomeView.jsx';
import { editionScopeQuery } from './EditionsView.jsx';
import { serverRows } from './ServerInfoView.jsx';
import { bulkAssignments, tasksForEndpoint } from './models/AssignmentsTab.jsx';
import { deletionMessage } from './profile/AccountCard.jsx';
import { problemMessageKey } from '../components/ModelKeyWarning.jsx';

const GRAFANA = { name: 'Grafana', url: 'http://127.0.0.1:12110', credentials: 'admin / admin' };
const SERVER_INFO = {
  product: 'My Morning News',
  version: '0.1.0-alpha',
  uptimeSeconds: 5,
  startedUtc: '2026-11-20T00:00:00Z',
  databaseType: 'Postgresql',
  schemaVersion: 12,
  vectorAvailable: true,
  runtimeVersion: '.NET 10.0.0',
  operatingSystem: 'Ubuntu 24.04.5 LTS',
  pendingRestart: [],
  externalServices: [GRAFANA]
};
const PERSONAL_ADMIN = { ...ADMIN_DETAILS, userId: 'usr_sam', email: 'sam@example.test', tenantId: 'ten_sam', isAdmin: false, isTenantAdmin: true };
const QUEUED = { ...RUN, status: 'Queued', startedUtc: null, completedUtc: null, summary: null, newFindingCount: 0 };

// A fake streamRunEvents whose calls stay open until the test emits events or ends them.
function controllableStream() {
  const calls = [];
  const fn = vi.fn(
    (id, { signal, onEvent }) =>
      new Promise((resolve, reject) => {
        calls.push({ id, end: resolve, emit: (event, data) => act(() => onEvent({ event, data, raw: JSON.stringify(data), id: null })) });
        signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' })));
      })
  );
  return { fn, calls };
}

// The whole app signed out at a path, as a visitor arriving from an email link sees it.
function renderSignedOut(path, overrides = {}) {
  window.localStorage.removeItem(STORAGE_KEYS.token);
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

describe('UT-005: Home keeps the operator infrastructure to global administrators', () => {
  it('treats a personal tenant, or any non-default tenant on a sign-up server, as hosted', () => {
    expect(isHostedTenant({ isPersonal: true }, null, 'ten_1')).toBe(true);
    expect(isHostedTenant({ isPersonal: false }, { signupEnabled: true }, 'ten_1')).toBe(true);
    expect(isHostedTenant({ isPersonal: false }, { signupEnabled: true }, 'default')).toBe(false);
    expect(isHostedTenant(null, { signupEnabled: false }, 'ten_1')).toBe(false);
  });

  it('shows external services and their credentials to a global administrator on the default tenant', async () => {
    renderRoute('/dashboard/home', { getServerInfo: vi.fn(async () => SERVER_INFO), getTenant: vi.fn(async () => ({ id: 'default', isPersonal: false })) });
    const services = await screen.findByTestId('external-services');
    expect(within(services).getByText('admin / admin')).toBeInTheDocument();
  });

  it('hides external services from a sign-up user, the tenant administrator of a personal tenant', async () => {
    const client = renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => PERSONAL_ADMIN), getServerInfo: vi.fn(async () => SERVER_INFO) });
    await waitFor(() => expect(client.getServerInfo).toHaveBeenCalled());
    expect(await screen.findByText('Try the API in the explorer')).toBeInTheDocument();
    expect(screen.queryByTestId('external-services')).toBeNull();
    expect(screen.queryByText('admin / admin')).toBeNull();
  });

  it('hides external services from a global administrator signed in to a personal tenant', async () => {
    const client = renderRoute('/dashboard/home', { getServerInfo: vi.fn(async () => SERVER_INFO), getTenant: vi.fn(async () => ({ id: 'default', isPersonal: true })) });
    await waitFor(() => expect(client.getTenant).toHaveBeenCalledWith('default'));
    expect(await screen.findByText('Try the API in the explorer')).toBeInTheDocument();
    expect(screen.queryByTestId('external-services')).toBeNull();
  });
});

describe('Home next steps offer only what the user can do', () => {
  it('offers members neither model setup nor the default password', async () => {
    renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getMe: vi.fn(async () => ({ id: 'usr_member', firstName: 'Riley', isProtected: false })) });
    expect(await screen.findByText('Try the API in the explorer')).toBeInTheDocument();
    expect(screen.queryByText('Set up a model endpoint')).toBeNull();
    expect(screen.queryByText('Change the default password')).toBeNull();
  });

  it('offers the default-password step to the seeded administrator only', async () => {
    renderRoute('/dashboard/home', { getMe: vi.fn(async () => ({ id: 'usr_admin', firstName: 'Admin', isProtected: true })) });
    expect(await screen.findByText('Change the default password')).toBeInTheDocument();
    expect(screen.getByText('Set up a model endpoint')).toBeInTheDocument();
  });

  it('does not offer the default-password step to another administrator', async () => {
    renderRoute('/dashboard/home', { getMe: vi.fn(async () => ({ id: 'usr_admin', firstName: 'Admin', isProtected: false })) });
    expect(await screen.findByText('Set up a model endpoint')).toBeInTheDocument();
    expect(screen.queryByText('Change the default password')).toBeNull();
  });
});

describe('UT-008: the wizard preview run shows progress and the result', () => {
  const review = () => state({ session: { step: 'Review' }, topic: { brief: '# Brief' }, dimensions: [DIMENSION] });

  it('follows the dry run until it ends and shows the findings it produced', async () => {
    const stream = controllableStream();
    const client = renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => review()),
      listTopicRecipients: vi.fn(async () => []),
      runTopic: vi.fn(async () => ({ ...QUEUED, isDryRun: true })),
      getRun: vi.fn(async () => ({ ...QUEUED, isDryRun: true })),
      listRunStages: vi.fn(async () => []),
      streamRunEvents: stream.fn
    });
    await userEvent.click(await screen.findByTestId('preview-run'));
    await waitFor(() => expect(client.runTopic).toHaveBeenCalledWith('top_1', { dryRun: true }));
    const notice = await screen.findByTestId('live-run-notice');
    expect(within(notice).getByText('Dry run')).toBeInTheDocument();
    expect(within(notice).getByText('Queued. It starts when a worker is free.')).toBeInTheDocument();
    await waitFor(() => expect(stream.fn).toHaveBeenCalled());
    const call = stream.calls[0];
    call.emit('stage', { stage: 'Search', status: 'Running' });
    call.emit('run', { ...QUEUED, isDryRun: true, status: 'Running' });
    expect(await within(notice).findByText('Running: Search.')).toBeInTheDocument();
    call.emit('done', { ...QUEUED, isDryRun: true, status: 'Completed', newFindingCount: 11, summary: 'Eleven new findings.' });
    await act(async () => call.end('done'));
    expect(await within(notice).findByText('Finished with 11 new findings.')).toBeInTheDocument();
    expect(within(notice).getByRole('link', { name: 'View findings (11)' })).toHaveAttribute('href', '/dashboard/findings?runId=run_1&includeDryRun=true');
    expect(within(notice).getByRole('link', { name: 'Open the run' })).toHaveAttribute('href', '/dashboard/runs?run=run_1');
  });

  it('shows why the preview could not start', async () => {
    renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => review()),
      listTopicRecipients: vi.fn(async () => []),
      runTopic: vi.fn(async () => {
        throw new ApiError(429, { error: 'CapReached', message: 'Your plan allows 2 runs a day.' });
      })
    });
    await userEvent.click(await screen.findByTestId('preview-run'));
    expect(await screen.findByText(/Your plan allows 2 runs a day/)).toBeInTheDocument();
    expect(screen.queryByTestId('live-run-notice')).toBeNull();
  });

  it('says what the dry run does without claiming it stays out of history', async () => {
    renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', { getRefinementSession: vi.fn(async () => review()), listTopicRecipients: vi.fn(async () => []) });
    expect(await screen.findByText(/It still shows in Runs, marked as a dry run/)).toBeInTheDocument();
  });
});

describe('UT-009: the topic page follows Run now until the run ends', () => {
  it('shows the queued run, its stages, and the result, then refreshes the last run', async () => {
    const stream = controllableStream();
    const detail = { topic: { ...TOPIC, lastRunUtc: null }, dimensions: [DIMENSION], constraints: [], recipients: [] };
    const getTopic = vi.fn(async () => detail);
    const client = renderRoute('/dashboard/topics/top_1', {
      getTopic,
      listSchedules: vi.fn(async () => page([])),
      runTopic: vi.fn(async () => QUEUED),
      getRun: vi.fn(async () => QUEUED),
      listRunStages: vi.fn(async () => []),
      streamRunEvents: stream.fn
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Run now' }));
    const notice = await screen.findByTestId('live-run-notice');
    expect(within(notice).getByText('Queued', { selector: '.badge' })).toBeInTheDocument();
    await waitFor(() => expect(stream.fn).toHaveBeenCalledWith('run_1', expect.anything()));
    const calls = getTopic.mock.calls.length;
    stream.calls[0].emit('done', { ...QUEUED, status: 'Failed', errorMessage: 'No search provider answered.' });
    await act(async () => stream.calls[0].end('done'));
    expect(await within(notice).findByText('The run failed: No search provider answered.')).toBeInTheDocument();
    await waitFor(() => expect(getTopic.mock.calls.length).toBeGreaterThan(calls));
    expect(client.runTopic).toHaveBeenCalledWith('top_1', { dryRun: false });
    await userEvent.click(within(notice).getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByTestId('live-run-notice')).toBeNull();
  });
});

describe('UT-010: Next on the Context step saves the brief', () => {
  it('explains why Next waits, then saves the typed brief and moves on', async () => {
    const context = state({ session: { step: 'Context', round: 1 }, dimensions: [DIMENSION] });
    const client = renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => context),
      listTopicRecipients: vi.fn(async () => []),
      updateTopic: vi.fn(async (id, body) => ({ ...context.topic, ...body })),
      suggestSources: vi.fn(async () => [])
    });
    const next = await screen.findByTestId('wizard-next');
    expect(next).toBeDisabled();
    expect(screen.getByTestId('wizard-next-hint')).toHaveTextContent('Write or draft a brief to continue.');
    await userEvent.type(screen.getByRole('textbox', { name: 'Brief' }), 'Track coding agents');
    await waitFor(() => expect(next).toBeEnabled());
    expect(screen.queryByTestId('wizard-next-hint')).toBeNull();
    await userEvent.click(next);
    await waitFor(() => expect(client.updateTopic).toHaveBeenCalledWith('top_1', expect.objectContaining({ brief: 'Track coding agents' })));
    await waitFor(() => expect(client.suggestSources).toHaveBeenCalled());
    expect(await screen.findByRole('heading', { level: 2, name: 'Sources' })).toBeInTheDocument();
  });

  it('stays on Context and shows the error when saving fails', async () => {
    const context = state({ session: { step: 'Context', round: 1 }, dimensions: [DIMENSION] });
    const client = renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => context),
      listTopicRecipients: vi.fn(async () => []),
      updateTopic: vi.fn(async () => {
        throw new ApiError(400, { error: 'ValidationFailed', message: 'The brief is too long.' });
      }),
      suggestSources: vi.fn(async () => [])
    });
    await userEvent.type(await screen.findByRole('textbox', { name: 'Brief' }), 'x');
    await userEvent.click(screen.getByTestId('wizard-next'));
    expect(await screen.findByText(/The brief is too long/)).toBeInTheDocument();
    expect(client.suggestSources).not.toHaveBeenCalled();
    expect(screen.getByRole('heading', { level: 2, name: 'Context' })).toBeInTheDocument();
  });
});

describe('UT-011: sign-up users hear about a missing platform key in terms they can act on', () => {
  it('points only global administrators at the server environment variable', () => {
    expect(problemMessageKey('platformMissing', true)).toBe('modelKeys.problem.platformMissing');
    expect(problemMessageKey('platformMissing', false)).toBe('modelKeys.problem.platformMissingUser');
    expect(problemMessageKey('missing', false)).toBe('modelKeys.problem.missing');
  });

  it('warns a sign-up user on Home and offers to use their own key', async () => {
    renderRoute('/dashboard/home', {
      getTokenDetails: vi.fn(async () => PERSONAL_ADMIN),
      getClientConfig: vi.fn(async () => ({ signupEnabled: true, platformModelKeyConfigured: false })),
      listModelEndpoints: vi.fn(async () => page([{ id: 'mdl_1', name: 'Anthropic', provider: 'Anthropic', credentialId: 'platform:anthropic', supportsChat: true, active: true }])),
      listVaultCredentials: vi.fn(async () => page([]))
    });
    const warning = await screen.findByTestId('model-key-warning');
    expect(within(warning).getByText(/uses this server's shared model key, and the server operator has not set one up/)).toBeInTheDocument();
    expect(within(warning).queryByText(/MYMORNINGNEWS_ANTHROPIC_API_KEY/)).toBeNull();
    expect(within(warning).getByRole('link', { name: 'Use your own API key for Anthropic' })).toHaveAttribute('href', '/dashboard/models?endpoint=mdl_1');
  });
});

describe('UT-012: account deletion says what happens, when, and that signing in cancels it', () => {
  const t = (key, values) => key + (values ? JSON.stringify(values) : '');
  const f = { date: (d) => new Date(d).toISOString().slice(0, 10) };

  it('gives the deletion date before confirming when the server says how long the grace period is', () => {
    const now = new Date('2026-11-20T12:00:00Z');
    expect(deletionMessage(t, f, { deletionGraceDays: 7 }, now)).toBe('account.delete.messageDated{"date":"2026-11-27"}');
    expect(deletionMessage(t, f, {}, now)).toBe('account.delete.message');
    expect(deletionMessage(t, f, null, now)).toBe('account.delete.message');
  });

  it('remembers the deletion and recognizes the sign-in that cancels it', () => {
    const now = new Date('2026-11-21T00:00:00Z');
    rememberPendingDeletion('Sam@Example.test', '2026-11-27T00:00:00Z');
    expect(deletionCancelledBySignIn('someone@example.test', null, now)).toBe(false);
    expect(window.localStorage.getItem(PENDING_DELETION_KEY)).not.toBeNull();
    expect(deletionCancelledBySignIn(' sam@example.test ', null, now)).toBe(true);
    expect(window.localStorage.getItem(PENDING_DELETION_KEY)).toBeNull();
    expect(deletionCancelledBySignIn('sam@example.test', null, now)).toBe(false);
    rememberPendingDeletion('sam@example.test', '2026-11-20T00:00:00Z');
    expect(deletionCancelledBySignIn('sam@example.test', null, now)).toBe(false);
  });

  it('prefers the server answer when the sign-in response carries one', () => {
    rememberPendingDeletion('sam@example.test', '2027-01-01T00:00:00Z');
    expect(deletionCancelledBySignIn('sam@example.test', { deletionCancelled: false })).toBe(false);
    expect(deletionCancelledBySignIn('other@example.test', { deletionCancelled: true })).toBe(true);
  });

  it('shows a signed-out page with the deletion date and how to cancel', async () => {
    renderSignedOut('/account-deleted?until=2026-11-27T00%3A00%3A00Z');
    const pageEl = await screen.findByTestId('account-deleted');
    expect(within(pageEl).getByRole('heading', { name: 'Your account is scheduled for deletion' })).toBeInTheDocument();
    expect(within(pageEl).getByText(/Your account will be deleted on/)).toBeInTheDocument();
    expect(within(pageEl).getByText(/Sign in before that date and the deletion is cancelled/)).toBeInTheDocument();
    expect(parsePurgeDate('not a date')).toBeNull();
  });

  it('tells the user after signing in that the deletion was cancelled', async () => {
    rememberPendingDeletion('sam@example.test', new Date(Date.now() + 6 * 86400000).toISOString());
    renderSignedOut('/', { getTokenDetails: vi.fn(async () => PERSONAL_ADMIN) });
    await userEvent.type(await screen.findByLabelText('Email'), 'sam@example.test');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'sam-usertest-password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    const notice = await screen.findByTestId('deletion-cancelled');
    expect(within(notice).getByText('Your account deletion is cancelled.')).toBeInTheDocument();
    await userEvent.click(within(notice).getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByTestId('deletion-cancelled')).toBeNull();
  });

  it('does not report an expired session for a request that fails after the session ended', async () => {
    renderSignedOut('/');
    await screen.findByRole('button', { name: 'Sign in' });
    act(() => {
      window.dispatchEvent(new CustomEvent('auth:unauthorized', { detail: { path: '/v1.0/token' } }));
    });
    expect(screen.queryByText(/Your session expired/)).toBeNull();
  });
});

describe('UT-013: editions start with your own, with the owner shown for everyone', () => {
  const EDITION = { id: 'edn_1', userId: 'usr_admin', name: 'Morning News', enabled: true, emailEnabled: true, lookbackMode: 'SinceLastEdition' };

  it('asks for mine only when an administrator chose it', () => {
    expect(editionScopeQuery('mine', true)).toEqual({ mine: true });
    expect(editionScopeQuery('all', true)).toEqual({});
    expect(editionScopeQuery('mine', false)).toEqual({});
  });

  it('lists the administrator editions first, then everyone with an owner column', async () => {
    const client = renderRoute('/dashboard/editions', {
      listEditions: vi.fn(async (q) => page(q.mine ? [EDITION] : [EDITION, { ...EDITION, id: 'edn_2', userId: 'usr_sam' }])),
      listUsers: vi.fn(async () => page([{ id: 'usr_sam', email: 'sam@example.test' }]))
    });
    await screen.findAllByText('Morning News', { selector: 'strong' });
    expect(client.listEditions).toHaveBeenCalledWith(expect.objectContaining({ mine: true }));
    expect(screen.queryByRole('columnheader', { name: 'Owner' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: "Everyone's" }));
    await waitFor(() => expect(screen.getAllByTestId('edition-owner')).toHaveLength(2));
    const owners = screen.getAllByTestId('edition-owner').map((el) => el.textContent);
    expect(owners).toEqual(expect.arrayContaining(['You', 'sam@example.test']));
    expect(client.listEditions).toHaveBeenLastCalledWith(expect.not.objectContaining({ mine: true }));
  });

  it('gives members no scope toggle', async () => {
    renderRoute('/dashboard/editions', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), listEditions: vi.fn(async () => page([EDITION])) });
    await screen.findByText('Morning News', { selector: 'strong' });
    expect(screen.queryByRole('button', { name: "Everyone's" })).toBeNull();
  });

  it('offers only your own editions on Today', async () => {
    const client = renderRoute('/dashboard/today', { listEditions: vi.fn(async () => page([EDITION])) });
    await waitFor(() => expect(client.listEditions).toHaveBeenCalledWith(expect.objectContaining({ mine: true })));
  });
});

describe('UT-017: members see the user directory read-only', () => {
  it('lists users without create, edit, or delete controls', async () => {
    renderRoute('/dashboard/admin/users', {
      getTokenDetails: vi.fn(async () => MEMBER_DETAILS),
      listUsers: vi.fn(async () => page([{ id: 'usr_admin', email: 'admin@mymorningnews', isAdmin: true, active: true }]))
    });
    expect(await screen.findByText('admin@mymorningnews')).toBeInTheDocument();
    expect(screen.getByTestId('users-read-only')).toBeInTheDocument();
    expect(screen.queryByText('Access denied')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Add' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Actions' }));
    expect(screen.getByRole('menuitem', { name: 'View JSON' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Edit' })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: 'Delete' })).toBeNull();
  });

  it('keeps the controls for administrators', async () => {
    renderRoute('/dashboard/admin/users', { listUsers: vi.fn(async () => page([{ id: 'usr_2', email: 'riley@example.test', active: true }])) });
    expect(await screen.findByText('riley@example.test')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add' })).toBeInTheDocument();
    expect(screen.queryByTestId('users-read-only')).toBeNull();
  });
});

describe('UT-022: model assignments change in bulk and topic overrides pick a topic', () => {
  const CHAT = { id: 'mdl_1', name: 'Anthropic', provider: 'Anthropic', supportsChat: true, supportsEmbeddings: false, active: true };
  const LOCAL = { id: 'mdl_2', name: 'Local chat', provider: 'OpenAiCompatible', supportsChat: true, supportsEmbeddings: true, active: true };

  it('builds one assignment per task the endpoint serves, keeping reasoning effort and clearing model overrides', () => {
    expect(tasksForEndpoint(CHAT)).toHaveLength(10);
    expect(tasksForEndpoint(LOCAL)).toHaveLength(11);
    const bodies = bulkAssignments(LOCAL, { Scoring: { reasoningEffort: 'High', modelOverride: 'old' } });
    expect(bodies.find((b) => b.task === 'Scoring')).toEqual({ task: 'Scoring', modelEndpointId: 'mdl_2', modelOverride: null, reasoningEffort: 'High' });
    expect(bodies.find((b) => b.task === 'Embedding')).toEqual({ task: 'Embedding', modelEndpointId: 'mdl_2', modelOverride: null, reasoningEffort: null });
  });

  it('moves every task to one endpoint after confirming', async () => {
    const client = renderRoute('/dashboard/models?tab=assignments', {
      listModelEndpoints: vi.fn(async () => page([CHAT, LOCAL])),
      listModelAssignments: vi.fn(async () => page([{ id: 'mda_1', task: 'Scoring', modelEndpointId: 'mdl_1', reasoningEffort: 'Medium' }])),
      upsertModelAssignment: vi.fn(async (b) => ({ id: 'mda_x', ...b }))
    });
    const bulk = await screen.findByTestId('assign-bulk');
    await userEvent.selectOptions(within(bulk).getByLabelText('Use one endpoint for every task'), 'mdl_2');
    await userEvent.click(within(bulk).getByRole('button', { name: 'Apply to all tasks' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Assign Local chat to all 11 tasks/)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Apply to all tasks' }));
    await waitFor(() => expect(client.upsertModelAssignment).toHaveBeenCalledTimes(11));
    expect(client.upsertModelAssignment).toHaveBeenCalledWith({ task: 'Scoring', modelEndpointId: 'mdl_2', modelOverride: null, reasoningEffort: 'Medium' });
    expect(await screen.findByText('Local chat now serves 11 tasks')).toBeInTheDocument();
  });

  it('saves every changed row with one button', async () => {
    const client = renderRoute('/dashboard/models?tab=assignments', {
      listModelEndpoints: vi.fn(async () => page([CHAT, LOCAL])),
      listModelAssignments: vi.fn(async () => page([])),
      upsertModelAssignment: vi.fn(async (b) => ({ id: 'mda_x', ...b }))
    });
    await userEvent.selectOptions(within(await screen.findByTestId('assignment-Scoring')).getByLabelText('Endpoint for Scoring'), 'mdl_2');
    await userEvent.selectOptions(within(screen.getByTestId('assignment-Synthesis')).getByLabelText('Endpoint for Synthesis'), 'mdl_2');
    await userEvent.click(screen.getByRole('button', { name: 'Save all changes (2)' }));
    await waitFor(() => expect(client.upsertModelAssignment).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('2 assignments saved')).toBeInTheDocument();
  });

  it('adds a topic override from a topic picker, preselected from the topic page link', async () => {
    const client = renderRoute('/dashboard/models?tab=assignments&topic=top_1', {
      listModelEndpoints: vi.fn(async () => page([CHAT, LOCAL])),
      listModelAssignments: vi.fn(async () => page([])),
      listTopics: vi.fn(async () => page([TOPIC])),
      upsertModelAssignment: vi.fn(async (b) => ({ id: 'mda_x', ...b }))
    });
    const topicSelect = await screen.findByRole('combobox', { name: 'Topic' });
    await waitFor(() => expect(within(topicSelect).getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    expect(topicSelect).toHaveValue('top_1');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Endpoint' }), 'mdl_2');
    await userEvent.click(screen.getByRole('button', { name: 'Add topic override' }));
    await waitFor(() => expect(client.upsertModelAssignment).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1', modelEndpointId: 'mdl_2', task: 'Synthesis' })));
  });
});

describe('UT-027: a used verification link points to sign-in', () => {
  it('says the link was used when the server reports LinkUsed', async () => {
    renderSignedOut('/verify?token=vt_1', {
      verifyEmail: vi.fn(async () => {
        throw new ApiError(400, { error: 'LinkUsed', message: 'This link was already used.' });
      })
    });
    expect(await screen.findByText('This link was already used. If your email is confirmed, sign in.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/');
    expect(screen.queryByRole('button', { name: 'Send a new link' })).toBeNull();
  });

  it('keeps the general message for any other failure', async () => {
    renderSignedOut('/verify?token=vt_1', {
      verifyEmail: vi.fn(async () => {
        throw new ApiError(400, { error: 'BadRequest', message: 'This link is not valid.' });
      })
    });
    expect(await screen.findByText(/This link is not valid or has expired/)).toBeInTheDocument();
  });
});

describe('UT-033: server runtime details are for administrators', () => {
  const t = (key) => key;
  const f = { dateTime: (v) => v, seconds: (v) => v + 's', list: (v) => v.join(', ') };

  it('builds runtime rows only for administrators and leaves out values the server did not send', () => {
    const keys = (rows) => rows.map((r) => r.key);
    expect(keys(serverRows(SERVER_INFO, { isAdmin: false, t, f }))).toEqual(['product', 'version', 'started', 'uptime']);
    expect(keys(serverRows(SERVER_INFO, { isAdmin: true, t, f }))).toEqual(['product', 'version', 'started', 'uptime', 'db', 'vector', 'runtime', 'os', 'pending']);
    expect(keys(serverRows({ product: 'x', version: '1' }, { isAdmin: true, t, f }))).toEqual(['product', 'version', 'started', 'uptime', 'pending']);
  });

  it('hides the operating system and schema from members', async () => {
    renderRoute('/dashboard/server-info', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getServerInfo: vi.fn(async () => SERVER_INFO) });
    expect(await screen.findByText('Uptime')).toBeInTheDocument();
    expect(screen.queryByText('Operating system')).toBeNull();
    expect(screen.queryByText(/Ubuntu/)).toBeNull();
    expect(screen.queryByText(/Schema version/)).toBeNull();
  });

  it('shows them to administrators', async () => {
    renderRoute('/dashboard/server-info', { getServerInfo: vi.fn(async () => SERVER_INFO) });
    expect(await screen.findByText('Ubuntu 24.04.5 LTS')).toBeInTheDocument();
    expect(screen.getByText(/Schema version 12/)).toBeInTheDocument();
  });
});

describe('Activity heatmap on a phone', () => {
  it('marks minor hour labels so a narrow grid can drop them', () => {
    render(
      <HeatmapChart
        ariaLabel="Activity"
        rows={[{ key: '1', label: 'Mon', values: [0, 1, 2] }]}
        columns={[
          { key: '0', label: '12 AM', short: '0' },
          { key: '1', label: '1 AM', short: '1', minor: true },
          { key: '2', label: '2 AM', short: '2', minor: true }
        ]}
      />
    );
    const labels = screen.getAllByRole('columnheader').slice(1);
    expect(labels.map((el) => el.className)).toEqual(['heatmap-col-label', 'heatmap-col-label minor', 'heatmap-col-label minor']);
  });
});
