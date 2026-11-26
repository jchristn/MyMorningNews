import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { router, useLocalSearchParams } from 'expo-router';
import React from 'react';
import { topicSlotsApi } from '@/api/account';
import { ApiClient } from '@/api/client';
import { briefingsApi, editionsApi, topicsApi } from '@/api/research';
import { comparisonMode, comparisonState, deltaCounts, historyBody, historyForm, moveItem, offersTopicSlots, runsAt, sameHistory, sortedDeltas } from '@/lib/comparison';
import { mapDeepLink } from '@/lib/deepLinks';
import { PlanSection } from '@/screens/account/PlanSections';
import { TopicSlotsSection } from '@/screens/account/TopicSlotsSection';
import { CatalogIssueScreen } from '@/screens/catalog/CatalogScreens';
import { EditionDetail } from '@/screens/editions/EditionScreens';
import { BriefingDetail } from '@/screens/today/BriefingDetail';
import { TopicHistory } from '@/screens/topics/TopicHistory';
import { TopicsScreen } from '@/screens/topics/TopicsScreens';
import { page, renderSignedIn } from './renderApp';

const NOW = '2026-11-25T12:00:00Z';
const COUNTS = { NewDevelopment: 1, ChangedFact: 1, Resolved: 0, Correction: 1, Unchanged: 1, Unavailable: 0 };
const delta = (id, kind, sortOrder, extra) => ({ id, tenantId: 'default', briefingId: 'brf_1', kind, title: kind + ' item', summary: null, findingId: null, predictionId: null, baselineBriefingId: 'brf_0', baselineItemId: null, sortOrder, active: true, createdUtc: NOW, lastUpdateUtc: NOW, ...extra });
const COMPARISON = {
  briefingId: 'brf_1',
  compared: true,
  mode: 'PreviousIssue',
  note: null,
  truncated: false,
  cutoffUtc: NOW,
  baselines: [
    { id: 'brf_0', title: 'Rebates of November 24', createdUtc: '2026-11-24T12:00:00Z', available: true },
    { id: 'brf_gone', title: '', createdUtc: null, available: false },
  ],
  counts: COUNTS,
  noMaterialChanges: false,
  summary: 'Compared with Rebates of November 24: 1 new, 1 changed, 1 correction, 1 unchanged.',
  deltas: [
    delta('bdl_3', 'NewDevelopment', 2, { findingId: 'fnd_new', summary: 'A new state program.' }),
    delta('bdl_1', 'Correction', 0, { findingId: 'fnd_fix' }),
    delta('bdl_2', 'ChangedFact', 1, { findingId: 'fnd_chg' }),
    delta('bdl_4', 'Unchanged', 3),
  ],
};
const STANDALONE = { briefingId: 'brf_1', compared: false, mode: 'Off', note: 'Comparison is off for this topic, so this is a standalone briefing.', truncated: false, cutoffUtc: null, baselines: [], counts: { ...COUNTS, NewDevelopment: 0, ChangedFact: 0, Correction: 0, Unchanged: 0 }, noMaterialChanges: false, summary: null, deltas: [] };
const NO_CHANGES = { ...COMPARISON, noMaterialChanges: true, summary: 'Compared with Rebates of November 24: no material changes.', counts: { ...COUNTS, NewDevelopment: 0, ChangedFact: 0, Correction: 0, Unchanged: 2 }, deltas: [delta('bdl_5', 'Unchanged', 0)] };
const detail = {
  briefing: { id: 'brf_1', kind: 'Run', topicId: 'top_1', title: 'Rebates of November 25', status: 'Ready', markdown: '# Rebates of November 25\n\nOne new program.', pinned: false, createdUtc: NOW },
  sections: [],
  items: [],
  files: [],
  filesExpired: false,
};
const topic = { id: 'top_1', name: 'Heat pump rebates', status: 'Active', historyMode: 'PreviousIssue', historyCount: 5, historyTokenBudget: 8000, lastUpdateUtc: NOW };

describe('comparison helpers', () => {
  it('builds history bodies from the form, keeping server validation for out-of-range values', () => {
    expect(historyForm({})).toEqual({ mode: 'Off', count: '5', date: '', issue: '', budget: '8000' });
    expect(historyBody(historyForm(topic))).toEqual({ historyMode: 'PreviousIssue', historyCount: 5, historyTokenBudget: 8000 });
    expect(historyBody({ mode: 'SinceDate', count: '3', date: '2026-11-01', issue: '', budget: '99' })).toEqual({ historyMode: 'SinceDate', historyCount: 3, historyTokenBudget: 99, historySinceUtc: '2026-11-01T00:00:00.000Z' });
    expect(historyBody({ mode: 'SinceDate', count: '', date: 'soon', issue: '', budget: '' })).toEqual({ historyMode: 'SinceDate', historySinceUtc: null });
    expect(historyBody({ mode: 'SpecificIssue', count: '5', date: '', issue: '', budget: '8000' }).historyBriefingId).toBe('');
    expect(sameHistory(historyForm(topic), { ...historyForm(topic), issue: 'brf_x' })).toBe(true);
    expect(sameHistory(historyForm(topic), { ...historyForm(topic), budget: '4000' })).toBe(false);
  });

  it('classifies comparisons and orders their changes', () => {
    expect(comparisonState(null)).toBe('standalone');
    expect(comparisonState(STANDALONE)).toBe('standalone');
    expect(comparisonState(NO_CHANGES)).toBe('noChanges');
    expect(comparisonState(COMPARISON)).toBe('changes');
    expect(sortedDeltas(COMPARISON).map((d) => d.kind)).toEqual(['Correction', 'ChangedFact', 'NewDevelopment', 'Unchanged']);
    expect(sortedDeltas({ deltas: [delta('a', 'Unchanged', 0), delta('b', 'Correction', 0), delta('c', 'Resolved', 0, { active: false })] }).map((d) => d.id)).toEqual(['b', 'a']);
    expect(deltaCounts(COMPARISON)).toEqual([{ kind: 'Correction', count: 1 }, { kind: 'ChangedFact', count: 1 }, { kind: 'NewDevelopment', count: 1 }, { kind: 'Unchanged', count: 1 }]);
    expect(comparisonMode({ mode: 'Edition:RecentIssues' })).toEqual({ mode: 'RecentIssues', edition: true });
    expect(comparisonMode({ mode: 'PreviousIssue' })).toEqual({ mode: 'PreviousIssue', edition: false });
  });

  it('moves keep-order entries and decides which run', () => {
    expect(moveItem(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
    expect(moveItem(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
    expect(moveItem(['a', 'b', 'c'], 0, 5)).toEqual(['b', 'c', 'a']);
    expect(runsAt(4, 5)).toBe(true);
    expect(runsAt(5, 5)).toBe(false);
    expect(runsAt(50, null)).toBe(true);
    expect(offersTopicSlots({ limit: 5, counted: 2, topics: [] })).toBe(true);
    expect(offersTopicSlots({ limit: null, counted: 2, topics: [] })).toBe(false);
    expect(offersTopicSlots({ limit: 5, counted: 0, topics: [] })).toBe(false);
    expect(offersTopicSlots(null)).toBe(false);
  });

  it('maps the system briefing links of an edition to the catalog issue reader', () => {
    expect(mapDeepLink('https://app.mymorningnews.ai/dashboard/catalog/cat_1/issues/brf_9')).toBe('/today/catalog/cat_1/brf_9');
  });
});

describe('comparison API', () => {
  function recorder() {
    const calls = [];
    const fetchImpl = async (url, init = {}) => {
      calls.push({ method: init.method || 'GET', path: url.replace(/^https?:\/\/[^/]+/, ''), body: init.body ? JSON.parse(init.body) : undefined });
      return { status: 200, text: async () => JSON.stringify({ ok: true }) };
    };
    return { client: new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl }), calls };
  }

  it.each([
    ['briefings.comparison', (c) => briefingsApi.comparison(c, 'brf_1'), 'GET', '/v1.0/api/briefings/brf_1/comparison', undefined],
    ['topics.previewHistory', (c) => topicsApi.previewHistory(c, 'top_1', { historyMode: 'RecentIssues', historyCount: 3 }), 'POST', '/v1.0/api/topics/top_1/history/preview', { historyMode: 'RecentIssues', historyCount: 3 }],
    ['topics.previewHistory saved', (c) => topicsApi.previewHistory(c, 'top_1'), 'POST', '/v1.0/api/topics/top_1/history/preview', {}],
    ['editions.previewHistory', (c) => editionsApi.previewHistory(c, 'edn_1', { historyMode: 'PreviousIssue' }), 'POST', '/v1.0/api/editions/edn_1/history/preview', { historyMode: 'PreviousIssue' }],
    ['editions.publications', (c) => editionsApi.publications(c, 'edn_1'), 'GET', '/v1.0/api/editions/edn_1/publications', undefined],
    ['editions.publications paged', (c) => editionsApi.publications(c, 'edn_1', { pageNumber: 1, pageSize: 20 }), 'GET', '/v1.0/api/editions/edn_1/publications?pageNumber=1&pageSize=20', undefined],
    ['editions.setPublications', (c) => editionsApi.setPublications(c, 'edn_1', ['cat_2', 'cat_1']), 'PUT', '/v1.0/api/editions/edn_1/publications', { publicationIds: ['cat_2', 'cat_1'] }],
    ['topicSlots.get', (c) => topicSlotsApi.get(c), 'GET', '/v1.0/api/account/topic-slots', undefined],
    ['topicSlots.save', (c) => topicSlotsApi.save(c, ['top_2', 'top_1']), 'PUT', '/v1.0/api/account/topic-slots', { keepTopicIds: ['top_2', 'top_1'] }],
    ['topicSlots.clear', (c) => topicSlotsApi.save(c, []), 'PUT', '/v1.0/api/account/topic-slots', { keepTopicIds: [] }],
  ])('%s', async (_name, call, method, path, body) => {
    const { client, calls } = recorder();
    await call(client);
    expect(calls).toEqual([{ method, path, body }]);
  });
});

describe('briefing reader: Compared with', () => {
  it('shows the summary collapsed, then the baselines and labeled changes that open what they name', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail }),
      'GET /v1.0/api/briefings/brf_1/comparison': () => ({ body: COMPARISON }),
    });
    await waitFor(() => expect(screen.getByTestId('comparison')).toBeTruthy());
    expect(screen.getByText(COMPARISON.summary)).toBeTruthy();
    expect(screen.queryByTestId('comparison-details')).toBeNull();
    const toggle = screen.getByTestId('comparison-toggle');
    expect(toggle.props.accessibilityState.expanded).toBe(false);
    await fireEvent.press(toggle);
    expect(screen.getByTestId('comparison-toggle').props.accessibilityState.expanded).toBe(true);
    expect(screen.getByText('Comparison: Previous issue')).toBeTruthy();
    expect(screen.getByText('Rebates of November 24')).toBeTruthy();
    expect(screen.getByText('No longer available')).toBeTruthy();
    for (const label of ['New', 'Changed', 'Correction', 'Unchanged']) expect(screen.getByText(label)).toBeTruthy();
    expect(screen.getByText('A new state program.')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('comparison-baseline-brf_0'));
    expect(router.push).toHaveBeenCalledWith('/today/briefings/brf_0');
    await fireEvent.press(screen.getByTestId('comparison-delta-bdl_1'));
    expect(router.push).toHaveBeenCalledWith('/today/findings/fnd_fix');
    // A change without current evidence is not a button.
    expect(screen.getByTestId('comparison-delta-bdl_4').props.accessibilityRole).toBeUndefined();
    expect(screen.getByTestId('comparison-delta-bdl_3').props.accessibilityLabel).toBe('New, NewDevelopment item, A new state program.');
  });

  it('says No material changes found', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail }),
      'GET /v1.0/api/briefings/brf_1/comparison': () => ({ body: NO_CHANGES }),
    });
    await waitFor(() => expect(screen.getByTestId('comparison-no-changes')).toBeTruthy());
    expect(screen.getByText('No material changes found')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('comparison-toggle'));
    expect(screen.getByText('Nothing new, changed, corrected, or resolved since the earlier issue.')).toBeTruthy();
  });

  it('shows a plain note for a standalone briefing, and nothing on a server without the route', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail }),
      'GET /v1.0/api/briefings/brf_1/comparison': () => ({ body: STANDALONE }),
    });
    await waitFor(() => expect(screen.getByTestId('comparison-standalone')).toBeTruthy());
    expect(screen.getByText(STANDALONE.note)).toBeTruthy();
    expect(screen.queryByTestId('comparison-toggle')).toBeNull();
    screen.unmount();
    await renderSignedIn(<BriefingDetail id="brf_1" />, { 'GET /v1.0/api/briefings/brf_1': () => ({ body: detail }) });
    await waitFor(() => expect(screen.getByTestId('briefing-body')).toBeTruthy());
    expect(screen.queryByTestId('comparison')).toBeNull();
    expect(screen.queryByTestId('comparison-standalone')).toBeNull();
  });
});

describe('system briefing issue: Compared with', () => {
  afterEach(() => useLocalSearchParams.mockReturnValue({}));

  it('reads the comparison field and opens the other issue of the publication', async () => {
    useLocalSearchParams.mockReturnValue({ publicationId: 'cat_1', issueId: 'brf_1' });
    const comparison = { ...COMPARISON, deltas: COMPARISON.deltas.map((d) => ({ ...d, findingId: null, predictionId: null, baselineItemId: null })) };
    await renderSignedIn(<CatalogIssueScreen />, {
      'GET /v1.0/api/catalog/cat_1/issues/brf_1': () => ({ body: { id: 'brf_1', publicationId: 'cat_1', title: 'Heat pumps weekly', markdown: '# Heat pumps weekly\n\nText.', createdUtc: NOW, comparison } }),
    });
    await waitFor(() => expect(screen.getByTestId('catalog-comparison')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('catalog-comparison-toggle'));
    await fireEvent.press(screen.getByTestId('comparison-baseline-brf_0'));
    expect(router.push).toHaveBeenCalledWith('/today/catalog/cat_1/brf_0');
    // Shared issues carry no finding IDs, so the changes are not buttons.
    expect(screen.getByTestId('comparison-delta-bdl_1').props.accessibilityRole).toBeUndefined();
  });

  it('shows nothing extra for an issue without a comparison', async () => {
    useLocalSearchParams.mockReturnValue({ publicationId: 'cat_1', issueId: 'brf_1' });
    await renderSignedIn(<CatalogIssueScreen />, {
      'GET /v1.0/api/catalog/cat_1/issues/brf_1': () => ({ body: { id: 'brf_1', publicationId: 'cat_1', title: 'Heat pumps weekly', markdown: 'Text.', createdUtc: NOW, comparison: null } }),
    });
    await waitFor(() => expect(screen.getByText('Heat pumps weekly')).toBeTruthy());
    expect(screen.queryByTestId('catalog-comparison')).toBeNull();
  });
});

describe('topic comparison settings: baseline preview', () => {
  const PREVIEW = { mode: 'RecentIssues', baselines: [{ id: 'brf_0', title: 'Rebates of November 24', createdUtc: '2026-11-24T12:00:00Z', available: true }], contextBytes: 9100, budgetBytes: 4000, truncated: true, note: null, cutoffUtc: NOW };

  it('previews unsaved values, shows size against the budget and truncation, and saves the budget', async () => {
    const onSaved = jest.fn();
    const { server } = await renderSignedIn(<TopicHistory topic={topic} onSaved={onSaved} />, {
      'POST /v1.0/api/topics/top_1/history/preview': () => ({ body: PREVIEW }),
      'PUT /v1.0/api/topics/top_1': (call) => ({ body: { ...topic, ...call.body } }),
    });
    expect(screen.getByTestId('save-topic-history').props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(screen.getByTestId('topic-history-mode-RecentIssues'));
    await fireEvent.changeText(screen.getByTestId('topic-history-count'), '3');
    await fireEvent.changeText(screen.getByTestId('topic-history-budget'), '4000');
    await fireEvent.press(screen.getByTestId('topic-history-preview'));
    await waitFor(() => expect(screen.getByTestId('history-preview-result')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/topics/top_1/history/preview').body).toEqual({ historyMode: 'RecentIssues', historyCount: 3, historyTokenBudget: 4000 });
    expect(server.count('PUT /v1.0/api/topics/top_1')).toBe(0);
    expect(screen.getByText('Earlier issues that would be compared: 1')).toBeTruthy();
    expect(screen.getByText('Comparison context: 9,100 of 4,000 bytes')).toBeTruthy();
    expect(screen.getByTestId('history-preview-truncated')).toBeTruthy();
    // Editing a value clears the preview, which no longer matches.
    await fireEvent.changeText(screen.getByTestId('topic-history-count'), '4');
    expect(screen.queryByTestId('history-preview-result')).toBeNull();
    await fireEvent.press(screen.getByTestId('save-topic-history'));
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/topics/top_1').body).toEqual({ historyMode: 'RecentIssues', historyCount: 4, historyTokenBudget: 4000 });
  });

  it('shows the note of a standalone preview', async () => {
    await renderSignedIn(<TopicHistory topic={topic} onSaved={jest.fn()} />, {
      'POST /v1.0/api/topics/top_1/history/preview': () => ({ body: { ...PREVIEW, baselines: [], contextBytes: 0, budgetBytes: 8000, truncated: false, note: 'No earlier issue is available yet, so the next briefing will be standalone.' } }),
    });
    await fireEvent.press(screen.getByTestId('topic-history-preview'));
    await waitFor(() => expect(screen.getByTestId('history-preview-note')).toBeTruthy());
    expect(screen.getByText('No earlier issue is available yet, so the next briefing will be standalone.')).toBeTruthy();
    expect(screen.queryByTestId('history-preview-truncated')).toBeNull();
  });

  it.each([
    [400, { error: 'ValidationFailed', message: 'The history settings are not valid.', requestId: 'rqi_1', details: { historyTokenBudget: 'Must be between 256 and 32000.' } }, 'Check these settings', 'Must be between 256 and 32000.'],
    [409, { error: 'Conflict', message: 'The pinned baseline issue is no longer available.', requestId: 'rqi_2' }, 'The pinned issue is not available', 'The pinned baseline issue is no longer available.'],
  ])('explains a %s inline', async (status, body, title, text) => {
    await renderSignedIn(<TopicHistory topic={topic} onSaved={jest.fn()} />, {
      'POST /v1.0/api/topics/top_1/history/preview': () => ({ status, body }),
    });
    await fireEvent.press(screen.getByTestId('topic-history-preview'));
    await waitFor(() => expect(screen.getByTestId('history-preview-refused')).toBeTruthy());
    expect(screen.getByText(title)).toBeTruthy();
    expect(screen.getByText(text)).toBeTruthy();
  });
});

describe('editions: comparison and system briefings', () => {
  const edition = { id: 'edn_1', name: 'Morning News', enabled: true, lookbackMode: 'SinceLastEdition', lookbackHours: 24, historyMode: 'Off', historyCount: 5, historyTokenBudget: 8000, createdUtc: NOW, lastUpdateUtc: NOW };
  const publications = [
    { publicationId: 'cat_1', title: 'Heat pumps weekly', sortOrder: 0, available: true },
    { publicationId: 'cat_old', title: 'Retired briefing', sortOrder: 1, available: false },
  ];
  const catalog = page([
    { id: 'cat_1', title: 'Heat pumps weekly', subscribed: true, suspended: false, eligible: true },
    { id: 'cat_2', title: 'Grid news', subscribed: true, suspended: false, eligible: true },
    { id: 'cat_3', title: 'Not subscribed', subscribed: false, eligible: true },
  ]);

  it('previews the edition baseline and saves the ordered system briefings', async () => {
    const editionDetail = { edition, topics: [], schedules: [], coveredTopicIds: [], publications };
    const { server } = await renderSignedIn(<EditionDetail id="edn_1" />, {
      'GET /v1.0/api/editions/edn_1': () => ({ body: editionDetail }),
      'GET /v1.0/api/topics': () => page([]),
      'GET /v1.0/api/catalog': () => catalog,
      'POST /v1.0/api/editions/edn_1/history/preview': () => ({ body: { mode: 'PreviousIssue', baselines: [{ id: 'brf_e0', title: 'Morning News for November 24', createdUtc: NOW, available: true }], contextBytes: 1200, budgetBytes: 8000, truncated: false, note: null, cutoffUtc: NOW } }),
      'PUT /v1.0/api/editions/edn_1/publications': (call) => ({ body: { ...editionDetail, publications: call.body.publicationIds.map((id, i) => ({ publicationId: id, title: id, sortOrder: i, available: true })) } }),
    });
    await waitFor(() => expect(screen.getByTestId('edition-history')).toBeTruthy());
    expect(screen.getByText("Off keeps each topic's own comparison. Any other choice compares each edition with its own earlier issues.")).toBeTruthy();
    await fireEvent.press(screen.getByTestId('edition-history-mode-PreviousIssue'));
    await fireEvent.press(screen.getByTestId('edition-history-preview'));
    await waitFor(() => expect(screen.getByText('Morning News for November 24 (Nov 25, 2026)')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/editions/edn_1/history/preview').body).toEqual({ historyMode: 'PreviousIssue', historyCount: 5, historyTokenBudget: 8000 });

    await waitFor(() => expect(screen.getByTestId('edition-publication-cat_2')).toBeTruthy());
    expect(screen.queryByTestId('edition-publication-cat_3')).toBeNull();
    expect(screen.getByText('Chosen: 2 of 20, in this order')).toBeTruthy();
    expect(screen.getByTestId('edition-publication-cat_old').props.accessibilityLabel).toBe('Retired briefing, selected, Not available now, skipped when the edition is composed');
    expect(screen.getByTestId('edition-publications-save').props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(screen.getByTestId('edition-publication-cat_old'));
    await fireEvent.press(screen.getByTestId('edition-publication-cat_2'));
    await fireEvent.press(screen.getByTestId('edition-publications-save'));
    await waitFor(() => expect(server.count('PUT /v1.0/api/editions/edn_1/publications')).toBe(1));
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/editions/edn_1/publications').body).toEqual({ publicationIds: ['cat_1', 'cat_2'] });
  });

  it('reads the paged publications route when the edition detail does not carry them', async () => {
    const { server } = await renderSignedIn(<EditionDetail id="edn_1" />, {
      'GET /v1.0/api/editions/edn_1': () => ({ body: { edition, topics: [], schedules: [], coveredTopicIds: [] } }),
      'GET /v1.0/api/topics': () => page([]),
      'GET /v1.0/api/catalog': () => catalog,
      'GET /v1.0/api/editions/edn_1/publications': () => ({ body: { success: true, ...page([publications[0]]).body, totalMs: 1 } }),
    });
    await waitFor(() => expect(screen.getByText('Chosen: 1 of 20, in this order')).toBeTruthy());
    expect(screen.getByTestId('edition-publication-cat_1').props.accessibilityLabel).toBe('Heat pumps weekly, selected');
    expect(server.calls.find((c) => c.key === 'GET /v1.0/api/editions/edn_1/publications').url).toContain('pageSize=20');
  });

  it('explains 402 CapReached and hides the picker on a server without the route', async () => {
    const { server } = await renderSignedIn(<EditionDetail id="edn_1" />, {
      'GET /v1.0/api/editions/edn_1': () => ({ body: { edition, topics: [], schedules: [], coveredTopicIds: [], publications: [] } }),
      'GET /v1.0/api/topics': () => page([]),
      'GET /v1.0/api/catalog': () => catalog,
      'PUT /v1.0/api/editions/edn_1/publications': () => ({ status: 402, body: { error: 'CapReached', message: 'Custom research is not in your plan.', requestId: 'rqi_3' } }),
    });
    await waitFor(() => expect(screen.getByTestId('edition-publication-cat_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('edition-publication-cat_1'));
    await fireEvent.press(screen.getByTestId('edition-publications-save'));
    await waitFor(() => expect(screen.getByTestId('edition-publications-capped')).toBeTruthy());
    expect(server.count('PUT /v1.0/api/editions/edn_1/publications')).toBe(1);
    screen.unmount();
    await renderSignedIn(<EditionDetail id="edn_1" />, {
      'GET /v1.0/api/editions/edn_1': () => ({ body: { edition, topics: [], schedules: [], coveredTopicIds: [] } }),
      'GET /v1.0/api/topics': () => page([]),
    });
    await waitFor(() => expect(screen.getByTestId('edition-settings')).toBeTruthy());
    await waitFor(() => expect(screen.queryByTestId('edition-publications')).toBeNull());
  });
});

describe('Topics that keep running', () => {
  const SLOTS = {
    limit: 2,
    counted: 3,
    overLimit: 1,
    customized: false,
    topics: [
      { topicId: 'top_1', name: 'Heat pump rebates', status: 'Active', position: 1, chosen: false, withinPlan: true, lastRunUtc: NOW },
      { topicId: 'top_2', name: 'Grid storage', status: 'Paused', position: 2, chosen: false, withinPlan: true, lastRunUtc: null },
      { topicId: 'top_3', name: 'Solar tariffs', status: 'Active', position: 3, chosen: false, withinPlan: false, lastRunUtc: null },
    ],
  };

  it('reorders with accessible buttons, marks which topics run, saves, and clears', async () => {
    const { server } = await renderSignedIn(<TopicSlotsSection />, {
      'GET /v1.0/api/account/topic-slots': () => ({ body: SLOTS }),
      'PUT /v1.0/api/account/topic-slots': (call) => {
        const ids = call.body.keepTopicIds.length ? call.body.keepTopicIds : ['top_1', 'top_2', 'top_3'];
        const byId = Object.fromEntries(SLOTS.topics.map((x) => [x.topicId, x]));
        return { body: { ...SLOTS, customized: call.body.keepTopicIds.length > 0, topics: ids.map((id, i) => ({ ...byId[id], position: i + 1, chosen: call.body.keepTopicIds.length > 0, withinPlan: i < 2 })) } };
      },
    });
    await waitFor(() => expect(screen.getByTestId('topic-slot-top_3')).toBeTruthy());
    expect(screen.getByTestId('topic-slots-over')).toBeTruthy();
    expect(screen.getByText('Past the limit: kept with history, not running; creating, duplicating, or restoring stays blocked until you archive enough or upgrade.')).toBeTruthy();
    expect(screen.getAllByText('Keeps running')).toHaveLength(2);
    expect(screen.getAllByText('Past the limit, not running')).toHaveLength(1);
    expect(screen.getByTestId('topic-slot-up-top_1').props.accessibilityState.disabled).toBe(true);
    expect(screen.getByTestId('topic-slot-down-top_3').props.accessibilityState.disabled).toBe(true);
    expect(screen.getByTestId('topic-slot-up-top_3').props.accessibilityLabel).toBe('Move Solar tariffs up');
    expect(screen.getByLabelText('3 of 3: Solar tariffs, Active, Past the limit, not running')).toBeTruthy();
    expect(screen.getByTestId('topic-slots-save').props.accessibilityState.disabled).toBe(true);
    expect(screen.getByTestId('topic-slots-clear').props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(screen.getByTestId('topic-slot-up-top_3'));
    expect(screen.getByLabelText('2 of 3: Solar tariffs, Active, Keeps running')).toBeTruthy();
    expect(screen.getByLabelText('3 of 3: Grid storage, Paused, Past the limit, not running')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('topic-slots-save'));
    await waitFor(() => expect(screen.getByTestId('topic-slots-saved')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/account/topic-slots').body).toEqual({ keepTopicIds: ['top_1', 'top_3', 'top_2'] });
    expect(screen.getAllByText('Your order')).toHaveLength(3);
    await fireEvent.press(screen.getByTestId('topic-slots-clear'));
    await waitFor(() => expect(screen.getByText('Cleared. Topics keep running in the order they were created.')).toBeTruthy());
    expect(server.calls.filter((c) => c.key === 'PUT /v1.0/api/account/topic-slots')[1].body).toEqual({ keepTopicIds: [] });
  });

  it('shows the server refusal inline', async () => {
    await renderSignedIn(<TopicSlotsSection />, {
      'GET /v1.0/api/account/topic-slots': () => ({ body: SLOTS }),
      'PUT /v1.0/api/account/topic-slots': () => ({ status: 400, body: { error: 'ValidationFailed', message: 'Topic top_3 is archived.', requestId: 'rqi_4' } }),
    });
    await waitFor(() => expect(screen.getByTestId('topic-slot-down-top_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-slot-down-top_1'));
    await fireEvent.press(screen.getByTestId('topic-slots-save'));
    await waitFor(() => expect(screen.getByTestId('topic-slots-error')).toBeTruthy());
    expect(screen.getByText('Topic top_3 is archived.')).toBeTruthy();
  });

  it('is offered from Plan when the limit is finite, and as a banner on Topics while over the limit', async () => {
    await renderSignedIn(<PlanSection />, {
      'GET /v1.0/api/account/plan': () => ({ body: { plan: { name: 'Paid 5' }, features: {}, upcoming: [] } }),
      'GET /v1.0/api/account/topic-slots': () => ({ body: SLOTS }),
    });
    await waitFor(() => expect(screen.getByTestId('plan-topic-slots')).toBeTruthy());
    expect(screen.getByText('Custom topics: 3 of 2')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('plan-topic-slots-open'));
    expect(router.push).toHaveBeenCalledWith('/account/slots');
    screen.unmount();

    await renderSignedIn(<PlanSection />, {
      'GET /v1.0/api/account/plan': () => ({ body: { plan: { name: 'Unlimited' }, features: {}, upcoming: [] } }),
      'GET /v1.0/api/account/topic-slots': () => ({ body: { ...SLOTS, limit: null, overLimit: 0 } }),
    });
    await waitFor(() => expect(screen.getByTestId('plan-current')).toBeTruthy());
    expect(screen.queryByTestId('plan-topic-slots')).toBeNull();
    screen.unmount();

    await renderSignedIn(<TopicsScreen />, {
      'GET /v1.0/api/topics': () => page([{ id: 'top_1', name: 'Heat pump rebates', status: 'Active' }]),
      'GET /v1.0/api/account/topic-slots': () => ({ body: SLOTS }),
    });
    await waitFor(() => expect(screen.getByTestId('topics-over-limit')).toBeTruthy());
    expect(screen.getByText('Topics past your plan limit: 1')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('topics-over-limit-open'));
    expect(router.push).toHaveBeenCalledWith('/account/slots');
  });
});
