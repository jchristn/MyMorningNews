import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { detail as briefingDetail } from '../test/briefingFixtures.js';
import { DIMENSION, SOURCE, TOPIC } from '../test/researchFixtures.js';
import { partOfDay } from '../utils/greeting.js';

const NO_KEY = { id: 'mdl_1', name: 'Anthropic', provider: 'Anthropic', supportsChat: true, active: true, createdUtc: '2026-11-01T00:00:00Z' };
const GRAFANA = { name: 'Grafana', url: 'http://127.0.0.1:12110', description: 'Dashboards for metrics, traces, and logs.', credentials: 'admin / admin' };
const SERVER_INFO = { product: 'My Morning News', version: '0.1.0-alpha', uptimeSeconds: 5, externalServices: [GRAFANA] };

describe('Home', () => {
  it('greets the member by first name for the time of day in their profile zone', async () => {
    renderRoute('/dashboard/home', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS), getMe: vi.fn(async () => ({ id: 'usr_member', firstName: 'Demo', email: 'member@mymorningnews', timeZone: 'UTC' })) });
    const expected = { morning: 'Good morning, Demo', afternoon: 'Good afternoon, Demo', evening: 'Good evening, Demo' }[partOfDay(new Date(), 'UTC')];
    expect(await screen.findByRole('heading', { level: 1, name: expected })).toBeInTheDocument();
    expect(screen.queryByText(/member,|Good morning, member/)).toBeNull();
    expect(screen.queryByText(/Phase 3/)).toBeNull();
  });

  it('warns that the model endpoint has no key and hides external services from members', async () => {
    renderRoute('/dashboard/home', {
      getTokenDetails: vi.fn(async () => MEMBER_DETAILS),
      getServerInfo: vi.fn(async () => SERVER_INFO),
      listModelEndpoints: vi.fn(async () => page([NO_KEY])),
      listVaultCredentials: vi.fn(async () => page([]))
    });
    const warning = await screen.findByTestId('model-key-warning');
    expect(within(warning).getByText(/Anthropic has no API key/)).toBeInTheDocument();
    expect(within(warning).getByText(/Ask yours to fix it/)).toBeInTheDocument();
    // External services are the operator's: members do not see them at all (UT-005).
    await waitFor(() => expect(screen.getByText('Try the API in the explorer')).toBeInTheDocument());
    expect(screen.queryByTestId('external-services')).toBeNull();
    expect(screen.queryByText('Dashboards for metrics, traces, and logs.')).toBeNull();
    expect(screen.queryByText('admin / admin')).toBeNull();
  });

  it('shows service credentials to administrators', async () => {
    renderRoute('/dashboard/home', { getServerInfo: vi.fn(async () => SERVER_INFO) });
    expect(await screen.findByText('admin / admin')).toBeInTheDocument();
  });
});

describe('Topic wizard', () => {
  it('warns at the first step when the refinement model has no key', async () => {
    renderRoute('/dashboard/topics/new', { listModelEndpoints: vi.fn(async () => page([NO_KEY])), listVaultCredentials: vi.fn(async () => page([])) });
    const warning = await screen.findByTestId('model-key-warning');
    expect(within(warning).getByText(/fails at the first step/)).toBeInTheDocument();
    expect(within(warning).getByRole('link', { name: 'Add a key to Anthropic' })).toHaveAttribute('href', '/dashboard/models?endpoint=mdl_1');
  });

  it('does not warn when the key is stored in the vault', async () => {
    renderRoute('/dashboard/topics/new', {
      listModelEndpoints: vi.fn(async () => page([{ ...NO_KEY, credentialId: 'vlt_1' }])),
      listVaultCredentials: vi.fn(async () => page([{ id: 'vlt_1', name: 'Anthropic API key', active: true }]))
    });
    expect(await screen.findByRole('heading', { level: 2 })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('model-key-warning')).toBeNull());
  });
});

describe('Briefing items', () => {
  it('lists items by title with links instead of raw IDs', async () => {
    renderRoute('/dashboard/briefings/brf_1', {
      getBriefing: vi.fn(async () => briefingDetail({ itemTitles: { fnd_1: 'Agent evals run on every change', prd_1: 'Evals become a release gate by June' } }))
    });
    expect(await screen.findByRole('link', { name: 'Agent evals run on every change' })).toHaveAttribute('href', '/dashboard/findings/fnd_1');
    expect(screen.getByRole('link', { name: 'Evals become a release gate by June' })).toHaveAttribute('href', '/dashboard/predictions?id=prd_1');
    expect(screen.queryByText('fnd_1')).toBeNull();
    expect(screen.queryByText('prd_1')).toBeNull();
  });
});

describe('Topic schedule', () => {
  const topicDetail = { topic: TOPIC, dimensions: [DIMENSION], constraints: [], recipients: [] };

  it('says paused for a topic whose schedule is disabled', async () => {
    renderRoute('/dashboard/topics/top_1', {
      getTopic: vi.fn(async () => topicDetail),
      listSchedules: vi.fn(async () =>
        page([{ id: 'sch_1', ownerType: 'Topic', ownerId: 'top_1', intervalType: 'DaysOfWeek', intervalValue: 1, daysOfWeekMask: 18, timesOfDay: ['05:00'], timeZone: 'UTC', enabled: false, active: true }])
      )
    });
    expect(await screen.findByTestId('topic-schedule')).toHaveTextContent('(paused)');
    expect(screen.getByTestId('topic-schedule')).not.toHaveTextContent('No schedule');
    expect(screen.getByTestId('topic-next-run')).toHaveTextContent('Paused');
  });

  it('names a paused edition that researches the topic', async () => {
    renderRoute('/dashboard/topics/top_1', {
      getTopic: vi.fn(async () => topicDetail),
      listSchedules: vi.fn(async () => page([])),
      listEditions: vi.fn(async () => page([{ id: 'edn_1', name: 'Morning News' }])),
      getEdition: vi.fn(async () => ({ edition: { id: 'edn_1', name: 'Morning News', enabled: true }, schedules: [{ id: 'sch_9', enabled: false }], coveredTopicIds: ['top_1'], topics: [] }))
    });
    await waitFor(() => expect(screen.getByTestId('topic-schedule')).toHaveTextContent('Researched for Morning News (paused)'));
    expect(screen.getByTestId('topic-next-run')).toHaveTextContent('Paused');
  });
});

describe('Sources', () => {
  it('names the first run by its time and topic instead of its ID', async () => {
    renderRoute('/dashboard/sources', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listSources: vi.fn(async () => page([{ ...SOURCE, contentText: undefined, firstSeenRunId: 'run_7' }])),
      getRun: vi.fn(async () => ({ id: 'run_7', topicId: 'top_1', status: 'Completed', startedUtc: '2026-11-19T15:00:00Z' }))
    });
    const link = await screen.findByRole('link', { name: /^Run of / });
    expect(link).toHaveAttribute('href', '/dashboard/runs?run=run_7');
    const row = link.closest('tr');
    expect(within(row).getByText('AI developer tools')).toBeInTheDocument();
    expect(within(row).queryByText('run_7')).toBeNull();
  });
});
