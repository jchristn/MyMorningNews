import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { File } from 'expo-file-system';
import React from 'react';
import { EditionDetail, EditionsScreen } from '@/screens/editions/EditionScreens';
import { RequestDetail, NewRequestScreen, RequestsScreen } from '@/screens/requests/RequestScreens';
import { FindingDetail, FindingsScreen } from '@/screens/research/FindingScreens';
import { PredictionDetail, PredictionsScreen } from '@/screens/research/PredictionScreens';
import { SuggestionsScreen } from '@/screens/research/SuggestionsScreen';
import { BriefingDetail } from '@/screens/today/BriefingDetail';
import { BriefingsScreen, TodayScreen } from '@/screens/today/TodayScreens';
import { RunDetail } from '@/screens/topics/RunDetail';
import { TopicWorkspace, TopicsScreen } from '@/screens/topics/TopicsScreens';
import { WizardScreen } from '@/screens/topics/WizardScreen';
import { MODEL_ERROR, page, renderSignedIn } from './renderApp';

const NOW = '2026-11-19T22:41:36Z';

const edition = { id: 'edn_1', name: 'Morning News', description: 'Your daily briefing.', enabled: true, audioEnabled: true, emailEnabled: true, freshResearchBeforeCompose: true, lookbackMode: 'SinceLastEdition', lookbackHours: 24, createdUtc: NOW, lastUpdateUtc: NOW };
const readyBriefing = { id: 'brf_1', kind: 'Edition', editionId: 'edn_1', title: 'Morning News for Thursday, November 19, 2026', status: 'Ready', readMinutes: 3, pinned: false, composedUtc: NOW, createdUtc: NOW };
const MARKDOWN = [
  '# Morning News for Thursday, November 19, 2026',
  '',
  'A quiet day with one release [1].',
  '',
  '## Top of the news',
  '',
  '- **Model release** [1]: something shipped.',
  '',
  '## Sources',
  '',
  '1. [Release notes](https://example.com/notes), example.com, Nov 18, 2026',
].join('\n');
const briefingDetail = {
  briefing: { ...readyBriefing, markdown: MARKDOWN, markdownArtifactId: 'art_md', htmlArtifactId: 'art_html' },
  sections: [
    { id: 'bsc_1', heading: 'Top of the news', source: 'findings', sortOrder: 1 },
    { id: 'bsc_2', heading: 'Sources', source: 'citations', sortOrder: 2 },
  ],
  items: [{ id: 'bit_1', sectionId: 'bsc_1', itemType: 'Finding', itemId: 'fnd_1', citationNumber: 1, sortOrder: 0 }],
  files: [{ id: 'art_md', kind: 'BriefingMarkdown', fileName: '2026-11-19-morning-news.md' }],
  filesExpired: false,
  editionName: 'Morning News',
};
const finding = { id: 'fnd_1', runId: 'run_1', topicId: 'top_1', title: 'Model release', summary: 'Something shipped.', whyItMatters: 'You use it daily.', category: 'Release', tier: 'MustRead', relevanceScore: 86, llmScore: 90, feedbackScore: 50, noveltyScore: 70, sourceScore: 60, recencyScore: 95, confidence: 0.8, createdUtc: NOW };
const topic = { id: 'top_1', name: 'AI developer tools', status: 'Active', brief: '## Brief\n\nCoding agents and IDEs.', audienceLevel: 'Practitioner', horizon: 'Weeks', lastRunUtc: NOW, lastUpdateUtc: NOW };
const run = { id: 'run_1', topicId: 'top_1', trigger: 'Manual', status: 'Completed', isDryRun: false, queuedUtc: NOW, startedUtc: NOW, completedUtc: NOW, durationMs: 4200, newFindingCount: 3, duplicateCount: 1, sourceCount: 12, inputTokens: 12000, outputTokens: 3000, estimatedCostUsd: 0.12, summary: 'Three new findings.' };

describe('Today', () => {
  it('shows the latest edition briefing and recent run briefings, and composes now with status polling', async () => {
    let reads = 0;
    const { server } = await renderSignedIn(<TodayScreen />, {
      'GET /v1.0/api/editions': () => page([edition]),
      'GET /v1.0/api/briefings': (call) => (call.url.includes('kind=Run') ? page([{ ...readyBriefing, id: 'brf_run', kind: 'Run', title: 'AI developer tools run' }]) : page([readyBriefing])),
      'POST /v1.0/api/editions/edn_1/compose': () => ({ status: 202, body: { id: 'brf_new', kind: 'Edition', editionId: 'edn_1', title: 'Morning News', status: 'Queued' } }),
      'GET /v1.0/api/briefings/brf_new': () => {
        reads += 1;
        return { body: { briefing: { id: 'brf_new', title: 'Morning News', status: reads > 1 ? 'Ready' : 'Composing' }, sections: [], items: [], files: [] } };
      },
    });
    await waitFor(() => expect(screen.getByTestId('today-latest')).toBeTruthy());
    expect(screen.getByText('Morning News for Thursday, November 19, 2026')).toBeTruthy();
    expect(screen.getByText('AI developer tools run')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('compose-now'));
    await waitFor(() => expect(screen.getByText('Composing your briefing')).toBeTruthy());
    await waitFor(() => expect(screen.getByText('Your briefing is ready')).toBeTruthy(), { timeout: 8000 });
    await fireEvent.press(screen.getByTestId('compose-open'));
    expect(router.push).toHaveBeenCalledWith('/today/briefings/brf_new');
    expect(server.count('GET /v1.0/api/briefings/brf_new')).toBeGreaterThanOrEqual(2);
    const latest = server.calls.find((c) => c.key === 'GET /v1.0/api/briefings' && c.url.includes('kind=Edition'));
    expect(latest.url).toMatch(/status=Ready/);
  });
});

describe('briefings', () => {
  it('lists briefings and filters by kind', async () => {
    const { server } = await renderSignedIn(<BriefingsScreen />, { 'GET /v1.0/api/briefings': () => page([readyBriefing]) });
    await waitFor(() => expect(screen.getByTestId('briefing-row-brf_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('briefings-filter-Run'));
    await waitFor(() => expect(server.calls.some((c) => c.url.includes('kind=Run'))).toBe(true));
    await fireEvent.press(screen.getByTestId('briefings-filter-pinned'));
    await waitFor(() => expect(server.calls.some((c) => c.url.includes('pinned=true'))).toBe(true));
  });

  it('reads a briefing: sections, citations that jump to Sources, linked items, pin, and share', async () => {
    File.downloadFileAsync = jest.fn(async () => ({ uri: 'file:///cache/2026-11-19-morning-news.md' }));
    const { server } = await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: briefingDetail }),
      'GET /v1.0/api/findings/fnd_1': () => ({ body: { finding, evidence: [] } }),
      'PUT /v1.0/api/briefings/brf_1/pin': (call) => ({ body: { ...readyBriefing, pinned: call.body.pinned } }),
    });
    await waitFor(() => expect(screen.getByTestId('briefing-body')).toBeTruthy());
    expect(screen.getAllByText('Morning News for Thursday, November 19, 2026')).toHaveLength(1);
    expect(screen.getByTestId('section-nav')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('nav-h3'));
    await fireEvent.press(screen.getAllByTestId('cite-1')[0]);
    await waitFor(() => expect(screen.getByTestId('cited-source')).toBeTruthy());
    expect(screen.getByTestId('highlighted-item')).toBeTruthy();
    expect(screen.getByTestId('back-to-text')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('back-to-text'));
    await waitFor(() => expect(screen.getByTestId('item-fnd_1')).toBeTruthy());
    expect(screen.getAllByText('Model release').length).toBeGreaterThan(0);
    await fireEvent.press(screen.getByTestId('item-fnd_1'));
    expect(router.push).toHaveBeenCalledWith('/today/findings/fnd_1');
    await fireEvent.press(screen.getByTestId('briefing-pin'));
    await waitFor(() => expect(screen.getByText('Unpin')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/briefings/brf_1/pin').body).toEqual({ pinned: true });
    await fireEvent.press(screen.getByTestId('briefing-share-md'));
    await waitFor(() => expect(screen.getByText('Saved to file:///cache/2026-11-19-morning-news.md')).toBeTruthy());
    expect(File.downloadFileAsync.mock.calls[0][0]).toBe('http://127.0.0.1:12100/v1.0/api/briefings/brf_1/download?format=md');
    expect(File.downloadFileAsync.mock.calls[0][2].headers.Authorization).toMatch(/^Bearer /);
  });

  it('offers to regenerate expired files, and shows a composing briefing as pending', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: { ...briefingDetail, filesExpired: true } }),
      'POST /v1.0/api/briefings/brf_1/regenerate': () => ({ body: readyBriefing }),
    });
    await waitFor(() => expect(screen.getByTestId('briefing-regenerate')).toBeTruthy());
    expect(screen.getByTestId('briefing-share-md').props.accessibilityState.disabled).toBe(true);
  });

  it('polls a briefing that is still composing', async () => {
    await renderSignedIn(<BriefingDetail id="brf_2" />, { 'GET /v1.0/api/briefings/brf_2': () => ({ body: { briefing: { id: 'brf_2', kind: 'Edition', title: 'Morning News', status: 'Composing' }, sections: [], items: [], files: [] } }) });
    await waitFor(() => expect(screen.getByTestId('briefing-pending')).toBeTruthy());
  });
});

describe('findings', () => {
  it('lists findings with tier and score filters', async () => {
    const { server } = await renderSignedIn(<FindingsScreen />, { 'GET /v1.0/api/findings': () => page([finding]) });
    await waitFor(() => expect(screen.getByTestId('finding-row-fnd_1')).toBeTruthy());
    expect(screen.getAllByText('Must read')).toHaveLength(2);
    await fireEvent.press(screen.getByTestId('tier-MustRead'));
    await fireEvent.press(screen.getByTestId('min-score-80'));
    await waitFor(() => expect(server.calls.some((c) => c.url.includes('tier=MustRead') && c.url.includes('minScore=80'))).toBe(true));
  });

  it('shows the score breakdown and only verified evidence with its source', async () => {
    await renderSignedIn(<FindingDetail id="fnd_1" />, {
      'GET /v1.0/api/findings/fnd_1': () => ({
        body: {
          finding,
          topicName: 'AI developer tools',
          evidence: [
            { evidence: { id: 'evd_1', quote: 'Version 2 ships today.', verified: true }, role: 'Supports', sourceTitle: 'Release notes', sourceDomain: 'example.com', sourceUrl: 'https://example.com/notes' },
            { evidence: { id: 'evd_2', quote: 'Made up.', verified: false }, role: 'Supports' },
          ],
        },
      }),
    });
    await waitFor(() => expect(screen.getByTestId('evidence-evd_1')).toBeTruthy());
    expect(screen.queryByTestId('evidence-evd_2')).toBeNull();
    expect(screen.getByText('"Version 2 ships today."')).toBeTruthy();
    expect(screen.getByTestId('score-chart').props.accessibilityLabel).toContain('Model judgment: 90');
    expect(screen.getByText('You use it daily.')).toBeTruthy();
  });
});

describe('predictions', () => {
  const prediction = { id: 'prd_1', topicId: 'top_1', statement: 'X ships Y by March.', confidence: 0.7, horizonUtc: '2026-11-01T00:00:00Z', status: 'ProposedResolved', createdUtc: NOW };

  it('lists what is due to grade', async () => {
    await renderSignedIn(<PredictionsScreen />, { 'GET /v1.0/api/predictions': () => page([prediction, { ...prediction, id: 'prd_2', status: 'Accurate', statement: 'Old one.' }]) });
    await waitFor(() => expect(screen.getByTestId('prediction-row-prd_1')).toBeTruthy());
    expect(screen.queryByTestId('prediction-row-prd_2')).toBeNull();
    await fireEvent.press(screen.getByTestId('predictions-filter-resolved'));
    await waitFor(() => expect(screen.getByTestId('prediction-row-prd_2')).toBeTruthy());
  });

  it('accepts a proposed resolution and grades a prediction', async () => {
    const { server } = await renderSignedIn(<PredictionDetail id="prd_1" />, {
      'GET /v1.0/api/predictions/prd_1': () => ({ body: { prediction, reviews: [{ id: 'prv_1', predictionId: 'prd_1', proposedStatus: 'Accurate', rationale: 'It shipped.', confidence: 0.9, findingIds: ['fnd_1'], createdUtc: NOW }] } }),
      'POST /v1.0/api/prediction-reviews/prv_1/accept': () => ({ body: { ...prediction, status: 'Accurate' } }),
      'POST /v1.0/api/predictions/prd_1/resolve': (call) => ({ body: { ...prediction, status: call.body.status, resolutionNote: call.body.note } }),
    });
    await waitFor(() => expect(screen.getByTestId('review-prv_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('review-accept-prv_1'));
    await waitFor(() => expect(screen.getByText('You accepted this proposal.')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('resolve-PartiallyAccurate'));
    await fireEvent.changeText(screen.getByTestId('resolve-note'), 'Only half shipped.');
    await fireEvent.press(screen.getByTestId('resolve-submit'));
    await waitFor(() => expect(server.count('POST /v1.0/api/predictions/prd_1/resolve')).toBe(1));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/predictions/prd_1/resolve').body).toEqual({ status: 'PartiallyAccurate', note: 'Only half shipped.' });
    await waitFor(() => expect(screen.getByText('Only half shipped.')).toBeTruthy());
  });
});

describe('suggestions', () => {
  it('accepts, snoozes, and completes suggestions', async () => {
    const suggestion = { id: 'sug_1', title: 'Try the new agent', description: 'Spend an hour with it.', priority: 'High', effort: 'Hours', status: 'New', findingIds: ['fnd_1'], createdUtc: NOW };
    const { server } = await renderSignedIn(<SuggestionsScreen />, {
      'GET /v1.0/api/suggestions': () => page([suggestion, { ...suggestion, id: 'sug_2', title: 'Read the paper' }]),
      'PUT /v1.0/api/suggestions/sug_1/status': (call) => ({ body: { ...suggestion, ...call.body } }),
      'PUT /v1.0/api/suggestions/sug_2/status': (call) => ({ body: { ...suggestion, id: 'sug_2', ...call.body } }),
    });
    await waitFor(() => expect(screen.getByTestId('suggestion-sug_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('suggestion-snooze-sug_1'));
    await fireEvent.press(screen.getByTestId('snooze-7'));
    await waitFor(() => expect(screen.queryByTestId('suggestion-sug_1')).toBeNull());
    const snooze = server.calls.find((c) => c.key === 'PUT /v1.0/api/suggestions/sug_1/status').body;
    expect(snooze.status).toBe('Snoozed');
    expect(Date.parse(snooze.snoozedUntilUtc)).toBeGreaterThan(Date.now() + 6 * 86400000);
    await fireEvent.press(screen.getByTestId('suggestion-done-sug_2'));
    await waitFor(() => expect(screen.queryByTestId('suggestion-sug_2')).toBeNull());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/suggestions/sug_2/status').body).toEqual({ status: 'Done' });
  });
});

describe('one-time requests', () => {
  const request = { id: 'rrq_1', requestText: 'History of the shipping container', title: 'Shipping containers', deliverables: ['Report'], length: 'Standard', status: 'Clarifying', emailRecipients: [], createdUtc: NOW };

  it('lists requests', async () => {
    await renderSignedIn(<RequestsScreen />, { 'GET /v1.0/api/requests': () => page([request]) });
    await waitFor(() => expect(screen.getByTestId('request-row-rrq_1')).toBeTruthy());
    expect(screen.getByText('Planning')).toBeTruthy();
  });

  it('creates a request and opens it', async () => {
    const { server } = await renderSignedIn(<NewRequestScreen />, { 'POST /v1.0/api/requests': (call) => ({ status: 201, body: { request: { ...request, requestText: call.body.requestText }, topic: { id: 'top_r' }, questions: [] } }) });
    await fireEvent.changeText(screen.getByTestId('request-text'), 'History of the shipping container');
    await fireEvent.press(screen.getByTestId('deliverable-Email'));
    await fireEvent.press(screen.getByTestId('request-submit'));
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/today/requests/rrq_1'));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/requests').body).toMatchObject({ requestText: 'History of the shipping container', deliverables: ['Report', 'Email'], skipQuestions: false, runAtUtc: null });
  });

  it('explains a missing model and offers the saved request', async () => {
    await renderSignedIn(<NewRequestScreen />, {
      'POST /v1.0/api/requests': () => MODEL_ERROR,
      'GET /v1.0/api/requests': () => page([{ ...request, errorMessage: 'needs an API key' }]),
    });
    await fireEvent.changeText(screen.getByTestId('request-text'), 'History of the shipping container');
    await fireEvent.press(screen.getByTestId('request-submit'));
    await waitFor(() => expect(screen.getByText('The model is not available')).toBeTruthy());
    await waitFor(() => expect(screen.getByTestId('request-open-saved')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('request-open-saved'));
    expect(router.replace).toHaveBeenCalledWith('/today/requests/rrq_1');
  });

  it('answers questions, plans again, and confirms a planned request', async () => {
    const planned = {
      request,
      topic: { id: 'top_r', brief: '## Plan\n\nCover the 1950s.' },
      dimensions: [{ id: 'tdm_1', name: 'Origins', weight: 'High' }],
      questions: [{ question: { id: 'rfq_1', kind: 'YesNo', questionText: 'Include ports?', answered: false, skipped: false, sortOrder: 1 }, options: [] }],
    };
    const { server } = await renderSignedIn(<RequestDetail id="rrq_1" />, {
      'GET /v1.0/api/requests/rrq_1': () => ({ body: planned }),
      'PUT /v1.0/api/requests/rrq_1/questions/rfq_1': (call) => ({ body: { question: { ...planned.questions[0].question, answerText: call.body.answerText, answered: true }, options: [] } }),
      'POST /v1.0/api/requests/rrq_1/confirm': () => ({ body: { ...planned, request: { ...request, status: 'Scheduled' } } }),
    });
    await waitFor(() => expect(screen.getByTestId('question-rfq_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('yes-rfq_1'));
    await waitFor(() => expect(screen.getByText('Answered')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/requests/rrq_1/questions/rfq_1').body).toEqual({ answerText: 'yes', skipped: false });
    await fireEvent.press(screen.getByTestId('request-confirm'));
    await waitFor(() => expect(screen.getByText('Scheduled')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/requests/rrq_1/confirm').body).toEqual({});
  });
});

describe('topics', () => {
  const detail = { topic, dimensions: [{ id: 'tdm_1', name: 'Coding agents', weight: 'High', description: 'IDE agents' }], constraints: [{ id: 'tcn_1', kind: 'ExcludeKeyword', value: 'funding' }], recipients: [] };

  it('lists topics', async () => {
    await renderSignedIn(<TopicsScreen />, { 'GET /v1.0/api/topics': () => page([topic]) });
    await waitFor(() => expect(screen.getByTestId('topic-row-top_1')).toBeTruthy());
    expect(screen.getByText('Active')).toBeTruthy();
  });

  it('runs a topic now and follows the run', async () => {
    const { server } = await renderSignedIn(<TopicWorkspace id="top_1" />, {
      'GET /v1.0/api/topics/top_1': () => ({ body: detail }),
      'POST /v1.0/api/topics/top_1/run': () => ({ status: 202, body: { ...run, id: 'run_2', status: 'Queued' } }),
    });
    await waitFor(() => expect(screen.getByTestId('topic-overview')).toBeTruthy());
    expect(screen.getByText('Coding agents')).toBeTruthy();
    expect(screen.getByText('funding')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('topic-run'));
    await waitFor(() => expect(screen.getByTestId('run-started')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('open-started-run'));
    expect(router.push).toHaveBeenCalledWith('/topics/runs/run_2');
    await fireEvent.press(screen.getByTestId('topic-dry-run'));
    await waitFor(() => expect(server.calls.some((c) => c.key === 'POST /v1.0/api/topics/top_1/run' && c.url.includes('dryRun=true'))).toBe(true));
  });

  it('shows the findings, runs, and sources tabs', async () => {
    await renderSignedIn(<TopicWorkspace id="top_1" />, {
      'GET /v1.0/api/topics/top_1': () => ({ body: detail }),
      'GET /v1.0/api/findings': (call) => (call.url.includes('topicId=top_1') ? page([finding]) : page([])),
      'GET /v1.0/api/runs': () => page([run]),
      'GET /v1.0/api/sources': () => page([{ id: 'src_1', title: 'Release notes', domain: 'example.com', canonicalUrl: 'https://example.com/notes', timesSeen: 2 }]),
    });
    await waitFor(() => expect(screen.getByTestId('topic-tab-findings')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-tab-findings'));
    await waitFor(() => expect(screen.getByTestId('finding-row-fnd_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('finding-row-fnd_1'));
    expect(router.push).toHaveBeenCalledWith('/topics/findings/fnd_1');
    await fireEvent.press(screen.getByTestId('topic-tab-runs'));
    await waitFor(() => expect(screen.getByTestId('run-row-run_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-tab-sources'));
    await waitFor(() => expect(screen.getByTestId('source-src_1')).toBeTruthy());
  });

  it('adds a weekly schedule with a preview of the next runs', async () => {
    const { server } = await renderSignedIn(<TopicWorkspace id="top_1" />, {
      'GET /v1.0/api/topics/top_1': () => ({ body: detail }),
      'GET /v1.0/api/schedules': () => page([]),
      'POST /v1.0/api/schedules/preview': () => ({ body: { valid: true, errors: [], nextRunsUtc: ['2026-11-20T15:00:00Z', '2026-11-23T15:00:00Z'] } }),
      'POST /v1.0/api/schedules': (call) => ({ status: 201, body: { id: 'sch_1', ...call.body } }),
    });
    await waitFor(() => expect(screen.getByTestId('topic-tab-schedule')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-tab-schedule'));
    await waitFor(() => expect(screen.getByTestId('add-schedule')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('add-schedule'));
    await fireEvent.press(screen.getByTestId('day-6'));
    await fireEvent.changeText(screen.getByTestId('schedule-times'), '07:00, 18:00');
    await fireEvent.press(screen.getByTestId('schedule-preview-button'));
    await waitFor(() => expect(screen.getByTestId('schedule-preview')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('schedule-save'));
    await waitFor(() => expect(server.count('POST /v1.0/api/schedules')).toBe(1));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/schedules').body).toMatchObject({ ownerType: 'Topic', ownerId: 'top_1', intervalType: 'DaysOfWeek', daysOfWeekMask: 126, timesOfDay: ['07:00', '18:00'], enabled: true });
  });
});

describe('runs', () => {
  it('shows stages, totals, dispositions, and findings, and cancels an active run', async () => {
    const { server } = await renderSignedIn(<RunDetail id="run_1" />, {
      'GET /v1.0/api/runs/run_1': () => ({ body: { ...run, status: 'Running', completedUtc: undefined } }),
      'GET /v1.0/api/runs/run_1/stages': () => ({
        body: [
          { id: 'rst_1', stage: 'PlanQueries', status: 'Succeeded', durationMs: 1200, itemsIn: 1, itemsOut: 8, sortOrder: 2 },
          { id: 'rst_2', stage: 'Search', status: 'Running', durationMs: 3000, itemsIn: 8, itemsOut: 40, sortOrder: 3 },
        ],
      }),
      'GET /v1.0/api/runs/run_1/queries': () => ({ body: [{ id: 'sqy_1', queryText: 'coding agents release', resultCount: 10, provider: 'Searxng', succeeded: true }] }),
      'GET /v1.0/api/runs/run_1/results': () => ({ body: [{ id: 'srs_1', disposition: 'Fetched' }, { id: 'srs_2', disposition: 'SkippedSeen' }] }),
      'GET /v1.0/api/findings': () => page([finding]),
      'POST /v1.0/api/runs/run_1/cancel': () => ({ body: { ...run, status: 'Running', cancelRequested: true } }),
    });
    await waitFor(() => expect(screen.getByTestId('run-stages')).toBeTruthy());
    expect(screen.getByTestId('stage-PlanQueries')).toBeTruthy();
    expect(screen.getAllByText('Plan queries').length).toBeGreaterThan(0);
    expect(screen.getByTestId('stage-chart').props.accessibilityLabel).toContain('Search: 3 s');
    expect(screen.getByText('Seen before')).toBeTruthy();
    expect(screen.getByText('coding agents release')).toBeTruthy();
    expect(screen.getByText('$0.12')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('run-cancel'));
    await waitFor(() => expect(screen.getByText('Cancelling')).toBeTruthy());
    expect(server.count('POST /v1.0/api/runs/run_1/cancel')).toBe(1);
  });
});

describe('refinement wizard', () => {
  it('starts a topic, survives a missing model, and completes it by hand', async () => {
    const state = (extra = {}) => ({
      session: { id: 'rfs_1', topicId: 'top_9', status: 'Active', step: 'Describe', round: 0, seedText: 'AI developer tools' },
      topic: { id: 'top_9', name: 'AI developer tools', status: 'Draft', brief: '', audienceLevel: 'Practitioner', horizon: 'Weeks', lastUpdateUtc: NOW },
      dimensions: [],
      constraints: [],
      questions: [],
      messages: [],
      ...extra,
    });
    const { server } = await renderSignedIn(<WizardScreen />, {
      'POST /v1.0/api/refinement-sessions': () => ({ status: 201, body: state() }),
      'POST /v1.0/api/refinement-sessions/rfs_1/dimensions': () => MODEL_ERROR,
      'POST /v1.0/api/refinement-sessions/rfs_1/questions': () => MODEL_ERROR,
      'POST /v1.0/api/refinement-sessions/rfs_1/context': () => MODEL_ERROR,
      'POST /v1.0/api/topics/top_9/dimensions': (call) => ({ status: 201, body: { id: 'tdm_9', topicId: 'top_9', weight: 'Normal', origin: 'User', ...call.body } }),
      'PUT /v1.0/api/topics/top_9/dimensions/tdm_9': (call) => ({ body: { id: 'tdm_9', topicId: 'top_9', name: 'Coding agents', origin: 'User', weight: 'Normal', ...call.body } }),
      'PUT /v1.0/api/topics/top_9': (call) => ({ body: { ...state().topic, ...call.body, lastUpdateUtc: '2026-11-19T23:00:00Z' } }),
      'POST /v1.0/api/refinement-sessions/rfs_1/complete': () => ({ body: state({ session: { id: 'rfs_1', status: 'Completed' }, topic: { id: 'top_9', status: 'Active' } }) }),
    });
    await waitFor(() => expect(screen.getByTestId('wizard-seed')).toBeTruthy());
    await fireEvent.changeText(screen.getByTestId('wizard-seed'), 'AI developer tools');
    await fireEvent.press(screen.getByTestId('wizard-start'));
    await waitFor(() => expect(screen.getByText('The model is not available')).toBeTruthy());
    expect(screen.getByText('You can add dimensions yourself below and keep going.')).toBeTruthy();
    expect(screen.getByTestId('wizard-next').props.accessibilityState.disabled).toBe(true);
    await fireEvent.changeText(screen.getByTestId('dimension-name'), 'Coding agents');
    await fireEvent.press(screen.getByTestId('dimension-add'));
    await waitFor(() => expect(screen.getByTestId('dimension-tdm_9')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('weight-tdm_9-High'));
    await waitFor(() => expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/topics/top_9/dimensions/tdm_9').body).toEqual({ weight: 'High' }));
    await fireEvent.press(screen.getByTestId('wizard-next'));
    await waitFor(() => expect(screen.getByText('The questions are optional. Go on to the brief and write it yourself.')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('wizard-next'));
    await waitFor(() => expect(screen.getByText('Write the brief yourself below, save it, then review.')).toBeTruthy());
    expect(screen.getByTestId('wizard-next').props.accessibilityState.disabled).toBe(true);
    await fireEvent.changeText(screen.getByTestId('context-brief'), '## Brief\n\nCoding agents and IDEs.');
    await fireEvent.press(screen.getByTestId('context-save'));
    await waitFor(() => expect(screen.getByTestId('wizard-next').props.accessibilityState.disabled).toBe(false));
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/topics/top_9').body).toMatchObject({ brief: '## Brief\n\nCoding agents and IDEs.' });
    await fireEvent.press(screen.getByTestId('wizard-next'));
    await waitFor(() => expect(screen.getByTestId('wizard-review')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('wizard-complete'));
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith('/topics/top_9'));
    expect(server.calls.filter((c) => c.key.startsWith('POST /v1.0/api/refinement-sessions/rfs_1/')).map((c) => c.key.split('/').pop())).toEqual(['dimensions', 'questions', 'context', 'complete']);
  });

  it('resumes a session from the link', async () => {
    useLocalSearchParams.mockReturnValue({ session: 'rfs_2' });
    await renderSignedIn(<WizardScreen />, {
      'GET /v1.0/api/refinement-sessions/rfs_2': () => ({
        body: {
          session: { id: 'rfs_2', status: 'Active', step: 'Questions', round: 1 },
          topic: { id: 'top_2', name: 'Heat pumps', status: 'Draft', brief: '' },
          dimensions: [{ id: 'tdm_2', name: 'Rebates', weight: 'High' }],
          constraints: [],
          questions: [{ question: { id: 'rfq_2', round: 1, kind: 'FreeText', questionText: 'Which state?', answered: false, skipped: false }, options: [] }],
        },
      }),
    });
    await waitFor(() => expect(screen.getByTestId('wizard-step-title')).toBeTruthy());
    expect(screen.getByText(/Which state\?/)).toBeTruthy();
    useLocalSearchParams.mockReturnValue({});
  });
});

describe('editions', () => {
  const detail = { edition, topics: [], schedules: [{ id: 'sch_e', ownerType: 'Edition', ownerId: 'edn_1', intervalType: 'DaysOfWeek', daysOfWeekMask: 127, timesOfDay: ['06:30'], timeZone: 'America/Los_Angeles', enabled: true, nextRunUtc: '2026-11-20T14:30:00Z' }], coveredTopicIds: ['top_1'] };

  it('lists editions', async () => {
    await renderSignedIn(<EditionsScreen />, { 'GET /v1.0/api/editions': () => page([edition]) });
    await waitFor(() => expect(screen.getByTestId('edition-row-edn_1')).toBeTruthy());
  });

  it('edits settings and topics, shows the schedule, and composes now', async () => {
    const { server } = await renderSignedIn(<EditionDetail id="edn_1" />, {
      'GET /v1.0/api/editions/edn_1': () => ({ body: detail }),
      'GET /v1.0/api/topics': () => page([topic, { ...topic, id: 'top_2', name: 'Heat pumps' }]),
      'PUT /v1.0/api/editions/edn_1': (call) => ({ body: { ...detail, edition: { ...edition, ...call.body, lastUpdateUtc: '2026-11-19T23:00:00Z' } } }),
      'PUT /v1.0/api/editions/edn_1/topics': (call) => ({ body: { ...detail, topics: call.body.topicIds.map((id, i) => ({ id: 'edt_' + i, topicId: id, sortOrder: i })) } }),
      'POST /v1.0/api/editions/edn_1/compose': () => ({ status: 202, body: { id: 'brf_9', status: 'Ready', title: 'Morning News' } }),
    });
    await waitFor(() => expect(screen.getByTestId('edition-settings')).toBeTruthy());
    expect(screen.getByText('Every day at 06:30')).toBeTruthy();
    await fireEvent.changeText(screen.getByTestId('edition-name'), 'Evening News');
    await fireEvent.press(screen.getByTestId('edition-save'));
    await waitFor(() => expect(screen.getByText('Saved.')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/editions/edn_1').body).toMatchObject({ name: 'Evening News', lookbackMode: 'SinceLastEdition' });
    await waitFor(() => expect(screen.getByTestId('edition-topic-top_2')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('edition-topic-top_2'));
    await fireEvent.press(screen.getByTestId('edition-topic-top_1'));
    await fireEvent.press(screen.getByTestId('edition-topics-save'));
    await waitFor(() => expect(server.count('PUT /v1.0/api/editions/edn_1/topics')).toBe(1));
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/editions/edn_1/topics').body).toEqual({ topicIds: ['top_2', 'top_1'] });
    await act(async () => fireEvent.press(screen.getByTestId('edition-compose')));
    await waitFor(() => expect(screen.getByText('Your briefing is ready')).toBeTruthy());
  });
});
