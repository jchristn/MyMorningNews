import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { BRIEFING, EDITION, TEMPLATE, TOPICS, detail, editionDetail } from '../test/briefingFixtures.js';
import { STANDALONE, catalogEntry, comparison, historyPreview, slot, topicSlots } from '../test/comparisonFixtures.js';
import { FINDING_DETAIL, TOPIC } from '../test/researchFixtures.js';

function editionApi(overrides = {}) {
  return {
    listEditions: vi.fn(async () => page([EDITION])),
    getEdition: vi.fn(async () => editionDetail()),
    listBriefingTemplates: vi.fn(async () => page([TEMPLATE])),
    listTopics: vi.fn(async () => page(TOPICS)),
    listCatalog: vi.fn(async () => page([catalogEntry('cat_1', 'Daily science'), catalogEntry('cat_2', 'Space weekly'), catalogEntry('cat_3', 'Not mine', { subscribed: false })])),
    ...overrides
  };
}

describe('Briefing reader comparison', () => {
  it('shows a collapsed callout with baseline and evidence links', async () => {
    const getBriefingComparison = vi.fn(async () => comparison());
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail()), getBriefingComparison });
    const callout = await screen.findByTestId('comparison-callout');
    expect(getBriefingComparison).toHaveBeenCalledWith('brf_1', expect.anything());
    await userEvent.click(within(callout).getByText('Compared with past briefings'));
    expect(within(callout).getByRole('link', { name: 'Rebates' })).toHaveAttribute('href', '/dashboard/briefings/brf_0');
    expect(within(callout).getByRole('link', { name: 'Deadline moved' })).toHaveAttribute('href', '/dashboard/findings/fnd_8');
  });

  it('notes a standalone briefing and waits for a briefing that is still composing', async () => {
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail()), getBriefingComparison: vi.fn(async () => STANDALONE) });
    expect(await screen.findByTestId('comparison-standalone')).toBeInTheDocument();
  });

  it('does not ask for a comparison before the briefing is ready', async () => {
    const getBriefingComparison = vi.fn(async () => comparison());
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, status: 'Composing', markdown: '' } })), getBriefingComparison });
    expect(await screen.findByTestId('briefing-progress')).toBeInTheDocument();
    expect(getBriefingComparison).not.toHaveBeenCalled();
  });
});

describe('Catalog issue reader comparison', () => {
  it('opens an issue from the composed edition link and links baselines to other issues', async () => {
    const issue = {
      id: 'brf_c2',
      publicationId: 'cat_1',
      title: 'Daily science, November 25',
      markdown: '# Daily science\n\nToday.',
      createdUtc: '2026-11-25T06:30:00Z',
      comparison: comparison({ briefingId: 'brf_c2', baselines: [{ id: 'brf_c1', title: 'Daily science, November 24', createdUtc: '2026-11-24T06:30:00Z', available: true }], deltas: comparison().deltas.map((d) => ({ ...d, findingId: null })) })
    };
    const client = renderRoute('/dashboard/catalog/cat_1/issues/brf_c2', { getCatalogIssue: vi.fn(async () => issue), getCatalogProgress: vi.fn(async () => ({ positionSeconds: 0, completed: false })) });
    expect(await screen.findByRole('heading', { level: 1, name: 'Daily science, November 25' })).toBeInTheDocument();
    expect(client.getCatalogIssue).toHaveBeenCalledWith('cat_1', 'brf_c2');
    const callout = screen.getByTestId('comparison-callout');
    await userEvent.click(within(callout).getByText('Compared with past briefings'));
    expect(within(callout).getByRole('link', { name: 'Daily science, November 24' })).toHaveAttribute('href', '/dashboard/catalog/cat_1/issues/brf_c1');
    expect(within(callout).queryByRole('link', { name: 'Rebate cap corrected' })).toBeNull();
    expect(within(callout).getByText('Rebate cap corrected')).toBeInTheDocument();
  });
});

describe('Topic comparison settings', () => {
  it('previews the baseline with the unsaved settings and saves the budget', async () => {
    const previewTopicHistory = vi.fn(async () => historyPreview());
    const updateTopic = vi.fn(async (id, body) => ({ ...TOPIC, ...body }));
    renderRoute('/dashboard/topics/top_1?tab=context', {
      getTopic: vi.fn(async () => ({ topic: { ...TOPIC, historyMode: 'Off', historyCount: 5, historyTokenBudget: 8000 }, dimensions: [], constraints: [], recipients: [] })),
      listSchedules: vi.fn(async () => page([])),
      previewTopicHistory,
      updateTopic
    });
    await userEvent.selectOptions(await screen.findByLabelText('Compare with past briefings'), 'RecentIssues');
    await userEvent.clear(screen.getByLabelText('Maximum issues (1 to 20)'));
    await userEvent.type(screen.getByLabelText('Maximum issues (1 to 20)'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Show baseline' }));
    await waitFor(() => expect(previewTopicHistory).toHaveBeenCalled());
    expect(previewTopicHistory.mock.calls.at(-1)[0]).toBe('top_1');
    expect(previewTopicHistory.mock.calls.at(-1)[1]).toMatchObject({ historyMode: 'RecentIssues', historyCount: 3, historyTokenBudget: 8000 });
    const preview = screen.getByTestId('topic-history-preview');
    expect(await within(preview).findByRole('link', { name: 'Heat pumps weekly' })).toHaveAttribute('href', '/dashboard/briefings/brf_2');
    await userEvent.clear(screen.getByLabelText('Comparison budget (bytes)'));
    await userEvent.type(screen.getByLabelText('Comparison budget (bytes)'), '40000');
    await userEvent.click(screen.getByRole('button', { name: /Save/ }));
    await waitFor(() => expect(updateTopic).toHaveBeenCalled());
    expect(updateTopic.mock.calls[0][1]).toMatchObject({ historyMode: 'RecentIssues', historyCount: 3, historyTokenBudget: 32000 });
  });
});

describe('Edition comparison and system briefings', () => {
  it('saves comparison settings through the edition update and previews the edition baseline', async () => {
    const updateEdition = vi.fn(async (id, body) => editionDetail({ edition: { ...EDITION, ...body } }));
    const previewEditionHistory = vi.fn(async () => historyPreview({ mode: 'PreviousIssue', baselines: [{ id: 'brf_e1', title: 'Morning News, November 25', createdUtc: '2026-11-25T06:30:00Z', available: true }] }));
    const listBriefings = vi.fn(async () => page([{ ...BRIEFING, id: 'brf_e1', title: 'Morning News, November 25', createdUtc: '2026-11-25T06:30:00Z' }]));
    renderRoute('/dashboard/editions/edn_1', editionApi({ updateEdition, previewEditionHistory, listBriefings }));
    const mode = await screen.findByLabelText('Compare with past briefings');
    expect(within(mode).getByRole('option', { name: "Off: keep each topic's own comparison" })).toBeInTheDocument();
    expect(screen.getByText(/Off keeps the topics' own research comparisons/)).toBeInTheDocument();
    await userEvent.selectOptions(mode, 'SpecificIssue');
    await waitFor(() => expect(listBriefings).toHaveBeenCalledWith(expect.objectContaining({ editionId: 'edn_1', status: 'Ready' })));
    await userEvent.selectOptions(await screen.findByLabelText('Baseline issue'), 'brf_e1');
    const preview = screen.getByTestId('edition-history-preview');
    await userEvent.click(within(preview).getByRole('button', { name: 'Show baseline' }));
    await waitFor(() => expect(previewEditionHistory).toHaveBeenCalled());
    expect(previewEditionHistory.mock.calls.at(-1).slice(0, 2)).toEqual(['edn_1', expect.objectContaining({ historyMode: 'SpecificIssue', historyBriefingId: 'brf_e1' })]);
    expect(await within(preview).findByRole('link', { name: 'Morning News, November 25' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /Save edition/ }));
    await waitFor(() => expect(updateEdition).toHaveBeenCalled());
    expect(updateEdition.mock.calls[0][1]).toMatchObject({ historyMode: 'SpecificIssue', historyBriefingId: 'brf_e1', historyCount: 5, historyTokenBudget: 8000, historySinceUtc: null });
  });

  it('chooses system briefings in order and removes an unavailable one', async () => {
    const setEditionPublications = vi.fn(async (id, ids) => editionDetail({ publications: ids.map((p, i) => ({ publicationId: p, title: p, sortOrder: i, available: true })) }));
    renderRoute(
      '/dashboard/editions/edn_1',
      editionApi({
        getEdition: vi.fn(async () => editionDetail({ publications: [{ publicationId: 'cat_gone', title: 'Retired digest', sortOrder: 0, available: false }] })),
        setEditionPublications
      })
    );
    const list = await screen.findByRole('list', { name: 'System briefings included' });
    expect(within(list).getByText('Retired digest')).toBeInTheDocument();
    expect(within(list).getByText('Unavailable')).toBeInTheDocument();
    const picker = await screen.findByLabelText('System briefing to add');
    expect(within(picker).queryByRole('option', { name: 'Not mine' })).toBeNull();
    await userEvent.selectOptions(picker, 'cat_1');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.selectOptions(screen.getByLabelText('System briefing to add'), 'cat_2');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove Retired digest' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move Space weekly up' }));
    await userEvent.click(screen.getByRole('button', { name: /Save system briefings/ }));
    await waitFor(() => expect(setEditionPublications).toHaveBeenCalledWith('edn_1', ['cat_2', 'cat_1']));
  });

  it('explains a plan without custom research when saving is refused with 402', async () => {
    const setEditionPublications = vi.fn(async () => {
      throw new ApiError(402, { error: 'CapReached', message: 'Your plan does not include custom research.' });
    });
    renderRoute('/dashboard/editions/edn_1', editionApi({ setEditionPublications }));
    await userEvent.selectOptions(await screen.findByLabelText('System briefing to add'), 'cat_1');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));
    await userEvent.click(screen.getByRole('button', { name: /Save system briefings/ }));
    expect(await screen.findByText('Plan limit reached.')).toBeInTheDocument();
    expect(screen.getByText(/needs a plan with custom research/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Plans and billing' })).toHaveAttribute('href', '/dashboard/account/billing');
  });
});

describe('Topics that keep running', () => {
  it('reorders with buttons, follows the limit, saves, and clears', async () => {
    const saveTopicSlots = vi.fn(async (ids) =>
      ids.length === 0
        ? topicSlots()
        : topicSlots({ customized: true, topics: ids.map((id, i) => slot(id, { top_1: 'Solar', top_2: 'Wind', top_3: 'Hydro' }[id], i + 1, i < 2, { chosen: true })) })
    );
    renderRoute('/dashboard/account/billing', { getMyBilling: vi.fn(async () => ({ enabled: false, plan: { plan: { name: 'Paid 5' } }, subscriptions: [], offers: [] })), getTopicSlots: vi.fn(async () => topicSlots()), saveTopicSlots });
    const panel = await screen.findByTestId('topic-slots');
    expect(within(panel).getByRole('heading', { name: 'Topics that keep running' })).toBeInTheDocument();
    expect(within(panel).getByText('1 topic is past your plan\'s limit.')).toBeInTheDocument();
    expect(within(panel).getByText(/Past the limit: kept with history, not running\. Creating, duplicating, or restoring a topic stays blocked/)).toBeInTheDocument();
    const rows = () => within(within(panel).getByRole('list', { name: 'Custom topics in keep order' })).getAllByRole('listitem');
    expect(within(rows()[2]).getByText('Past the limit')).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /Clear my order/ })).toBeDisabled();
    await userEvent.click(within(panel).getByRole('button', { name: 'Move Hydro up' }));
    await userEvent.click(within(panel).getByRole('button', { name: 'Move Hydro up' }));
    expect(within(rows()[0]).getByText('Hydro')).toBeInTheDocument();
    expect(within(rows()[0]).getByText('Keeps running')).toBeInTheDocument();
    expect(within(rows()[2]).getByText('Wind')).toBeInTheDocument();
    expect(within(rows()[2]).getByText('Past the limit')).toBeInTheDocument();
    await userEvent.click(within(panel).getByRole('button', { name: /Save order/ }));
    await waitFor(() => expect(saveTopicSlots).toHaveBeenCalledWith(['top_3', 'top_1', 'top_2']));
    expect(await within(panel).findByText('You chose this order.')).toBeInTheDocument();
    await userEvent.click(within(panel).getByRole('button', { name: /Clear my order/ }));
    await waitFor(() => expect(saveTopicSlots).toHaveBeenLastCalledWith([]));
  });

  it('stays hidden for unlimited plans and shows a save refusal inline', async () => {
    renderRoute('/dashboard/account/billing', { getMyBilling: vi.fn(async () => ({ enabled: false, plan: { plan: { name: 'BYOK' } }, subscriptions: [], offers: [] })), getTopicSlots: vi.fn(async () => topicSlots({ limit: null, overLimit: 0 })) });
    expect(await screen.findByText('New paid purchases are unavailable on this server.')).toBeInTheDocument();
    expect(screen.queryByTestId('topic-slots')).toBeNull();
  });

  it('reports a validation refusal next to the list', async () => {
    const saveTopicSlots = vi.fn(async () => {
      throw new ApiError(400, { error: 'ValidationFailed', message: 'Topic top_3 is archived.' });
    });
    renderRoute('/dashboard/account/billing', { getMyBilling: vi.fn(async () => ({ enabled: false, plan: { plan: { name: 'Paid 5' } }, subscriptions: [], offers: [] })), getTopicSlots: vi.fn(async () => topicSlots({ overLimit: 0, limit: 5 })), saveTopicSlots });
    const panel = await screen.findByTestId('topic-slots');
    expect(within(panel).getByText(/Your plan runs up to 5 custom topics/)).toBeInTheDocument();
    await userEvent.click(within(panel).getByRole('button', { name: 'Move Wind down' }));
    await userEvent.click(within(panel).getByRole('button', { name: /Save order/ }));
    expect(await within(panel).findByText('Topic top_3 is archived.')).toBeInTheDocument();
  });

  it('links the Topics list to the panel when topics are past the limit', async () => {
    renderRoute('/dashboard/topics', { listTopics: vi.fn(async () => page([TOPIC])), getTopicSlots: vi.fn(async () => topicSlots({ overLimit: 2 })) });
    const banner = await screen.findByTestId('topic-slots-banner');
    expect(banner).toHaveTextContent('2 topics are past your plan\'s limit.');
    expect(within(banner).getByRole('link', { name: 'Choose which topics keep running' })).toHaveAttribute('href', '/dashboard/account/billing#topic-slots');
  });

  it('shows no banner within the limit', async () => {
    renderRoute('/dashboard/topics', { listTopics: vi.fn(async () => page([TOPIC])), getTopicSlots: vi.fn(async () => topicSlots({ overLimit: 0 })) });
    await screen.findByText('AI developer tools', { selector: 'strong' });
    expect(screen.queryByTestId('topic-slots-banner')).toBeNull();
  });
});

describe('Finding corrections', () => {
  it('labels a finding that corrects the one it updates', async () => {
    renderRoute('/dashboard/findings/fnd_1', { getFinding: vi.fn(async () => ({ ...FINDING_DETAIL, finding: { ...FINDING_DETAIL.finding, isUpdateOfFindingId: 'fnd_0', isCorrection: true } })) });
    expect(await screen.findByText('Correction')).toBeInTheDocument();
    expect(screen.getByText('Update')).toBeInTheDocument();
  });
});
