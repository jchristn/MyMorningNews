import { isPredictionDue } from '@/lib/enums';
import { briefingFileName, shareBriefingFile } from '@/lib/briefingFiles';
import { applyFindingChanges, findingActions, findingListFlags, findingUpdate, performFindingAction } from '@/lib/findings';
import { ApiClient } from '@/api/client';
import { citationsUsed, isSafeUrl, parseBlocks, parseBriefing, parseInline, plainText } from '@/lib/markdown';
import { isLocalDateTime, localToUtc, parseTimes, scheduleBody, scheduleForm, scheduleProblem, toggleDay, utcToLocal } from '@/lib/schedule';
import { answerBody, canAskMore, initialStep, nextStep, previousStep, questionRounds, stepAvailable, upsertById } from '@/lib/wizard';
import { requestBody, confirmTime, canCancel, canConfirm, canConvert } from '@/screens/requests/RequestScreens';
import { filterPredictions, predictionsQuery } from '@/screens/research/PredictionScreens';
import { suggestionStatusBody } from '@/screens/research/SuggestionsScreen';
import { findingsQuery, scoreRows } from '@/screens/research/FindingScreens';
import { briefingQuery } from '@/screens/today/TodayScreens';
import { dispositionCounts, formatDuration } from '@/screens/topics/RunDetail';
import { editionBody, editionForm, toggleTopic } from '@/screens/editions/EditionScreens';
import { itemHref } from '@/screens/today/BriefingDetail';

const BRIEFING = [
  '# Morning News for Thursday, November 19, 2026',
  '',
  'Opening paragraph with a citation [1] and two more [2, 3].',
  '',
  '## Top of the news',
  '',
  '- **Model release** [1]: something shipped.',
  '- Visit <https://example.com/a> or https://example.com/b.',
  '',
  '## Sources',
  '',
  '1. [First source](https://one.example/x), one.example, Nov 18, 2026',
  '2. [Second](https://two.example), two.example',
  '3. [Third](javascript:alert(1)), evil.example',
].join('\n');

describe('briefing Markdown', () => {
  it('parses inline bold, italic, code, safe links, autolinks, and citations', () => {
    const nodes = parseInline('A **bold** _it_ `code` [link](https://x.example) <mailto:a@b.example> [3] [4, 5] [x](javascript:alert(1))');
    expect(nodes.map((n) => n.type)).toEqual(['text', 'strong', 'text', 'em', 'text', 'code', 'text', 'link', 'text', 'link', 'text', 'cite', 'text', 'cite', 'cite', 'text']);
    expect(nodes.filter((n) => n.type === 'cite').map((n) => n.n)).toEqual([3, 4, 5]);
    expect(plainText(nodes)).toContain(' x');
    expect(nodes.filter((n) => n.type === 'link').map((n) => n.href)).toEqual(['https://x.example', 'mailto:a@b.example']);
  });

  it('allows only http, https, and mailto links', () => {
    expect(isSafeUrl('https://a.example')).toBe(true);
    expect(isSafeUrl('mailto:a@b.example')).toBe(true);
    expect(isSafeUrl('javascript:alert(1)')).toBe(false);
    expect(isSafeUrl('data:text/html,hi')).toBe(false);
    expect(isSafeUrl('/relative')).toBe(false);
  });

  it('keeps raw HTML as text and parses fences, rules, nested bullets, and numbered lists', () => {
    const blocks = parseBlocks('<script>alert(1)</script>\n\n```\ncode **not bold**\n```\n\n---\n\n- a\n  - b\n\n3. three\n4. four');
    expect(blocks.map((b) => b.type)).toEqual(['paragraph', 'code', 'rule', 'list', 'list']);
    expect(plainText(blocks[0].children)).toBe('<script>alert(1)</script>');
    expect(blocks[1].value).toBe('code **not bold**');
    expect(blocks[3].items.map((i) => i.indent)).toEqual([0, 1]);
    expect(blocks[4].items.map((i) => i.number)).toEqual([3, 4]);
  });

  it('builds the outline and the numbered sources, skipping the repeated title', () => {
    const parsed = parseBriefing(BRIEFING, { skipTitle: 'Morning News for Thursday, November 19, 2026' });
    expect(parsed.blocks[0].type).toBe('paragraph');
    expect(parsed.outline.map((h) => h.text)).toEqual(['Top of the news', 'Sources']);
    expect(Object.keys(parsed.sources)).toEqual(['1', '2', '3']);
    expect(parsed.sources[1]).toMatchObject({ href: 'https://one.example/x', title: 'First source', itemIndex: 0 });
    expect(parsed.sources[3].href).toBeNull();
    expect(parsed.sourcesId).toBe(parsed.outline[1].id);
    expect(citationsUsed(parsed.blocks)).toEqual([1, 2, 3]);
  });

  it('finds sources under the heading the template names', () => {
    const parsed = parseBriefing('## Works cited\n\n1. [A](https://a.example)', { sourcesHeadings: ['Works cited'] });
    expect(parsed.sources[1].title).toBe('A');
  });
});

describe('schedules', () => {
  it('parses times, local date-times, and day masks', () => {
    expect(parseTimes('7:00, 18:30;7:00 25:00 x')).toEqual(['07:00', '18:30']);
    expect(isLocalDateTime('2026-12-01 07:00')).toBe(true);
    expect(isLocalDateTime('2026-13-01 07:00')).toBe(false);
    expect(toggleDay(62, 0)).toBe(63);
    expect(toggleDay(63, 0)).toBe(62);
  });

  it('converts wall-clock times in a zone to UTC and back', () => {
    expect(localToUtc('2026-12-01 07:00', 'UTC')).toBe('2026-12-01T07:00:00.000Z');
    expect(localToUtc('2026-12-01 07:00', 'America/Los_Angeles')).toBe('2026-12-01T15:00:00.000Z');
    expect(utcToLocal('2026-12-01T15:00:00.000Z', 'America/Los_Angeles')).toBe('2026-12-01 07:00');
    expect(localToUtc('nope', 'UTC')).toBeNull();
  });

  it('sends only the fields an interval type uses', () => {
    const weekly = scheduleBody({ ...scheduleForm(null, 'UTC'), times: '18:00, 07:00' }, { ownerType: 'Topic', ownerId: 'top_1' });
    expect(weekly).toEqual({ intervalType: 'DaysOfWeek', timeZone: 'UTC', enabled: true, ownerType: 'Topic', ownerId: 'top_1', daysOfWeekMask: 62, timesOfDay: ['07:00', '18:00'] });
    expect(scheduleBody({ intervalType: 'EveryNHours', intervalValue: '500', minuteOffset: '75', timeZone: 'UTC', enabled: true })).toEqual({ intervalType: 'EveryNHours', timeZone: 'UTC', enabled: true, intervalValue: 168, minuteOffset: 59 });
    expect(scheduleBody({ intervalType: 'Once', runOnce: '2026-12-01 07:00', timeZone: 'UTC', enabled: true })).toMatchObject({ runOnceUtc: '2026-12-01T07:00:00.000Z' });
    expect(scheduleBody({ intervalType: 'Manual', timeZone: 'UTC', enabled: false })).toEqual({ intervalType: 'Manual', timeZone: 'UTC', enabled: false });
  });

  it('explains what is missing before saving', () => {
    expect(scheduleProblem({ intervalType: 'DaysOfWeek', daysOfWeekMask: 0, times: '07:00' })).toBe('days');
    expect(scheduleProblem({ intervalType: 'EveryNDays', times: '' })).toBe('times');
    expect(scheduleProblem({ intervalType: 'Once', runOnce: 'tomorrow' })).toBe('once');
    expect(scheduleProblem({ intervalType: 'Manual' })).toBeNull();
  });

  it('reads an existing schedule into the form', () => {
    expect(scheduleForm({ intervalType: 'DaysOfWeek', daysOfWeekMask: 127, timesOfDay: ['06:30'], timeZone: 'America/Los_Angeles', enabled: true })).toMatchObject({ daysOfWeekMask: 127, times: '06:30', timeZone: 'America/Los_Angeles' });
  });
});

describe('wizard steps', () => {
  const session = (extra) => ({ session: { id: 'rfs_1', status: 'Active', step: 'Describe', round: 0, ...extra }, topic: { id: 'top_1', brief: '' }, dimensions: [], questions: [] });

  it('opens a fresh session on Dimensions and gates later steps on dimensions and a brief', () => {
    const fresh = session();
    expect(initialStep(null)).toBe('Describe');
    expect(initialStep(fresh)).toBe('Dimensions');
    expect(stepAvailable('Questions', fresh)).toBe(false);
    const withDims = { ...fresh, dimensions: [{ id: 'd1' }], session: { ...fresh.session, step: 'Questions' } };
    expect(initialStep(withDims)).toBe('Questions');
    expect(stepAvailable('Context', withDims)).toBe(true);
    expect(stepAvailable('Review', withDims)).toBe(false);
    const ready = { ...withDims, topic: { id: 'top_1', brief: '# Brief' }, session: { ...withDims.session, step: 'Review' } };
    expect(stepAvailable('Review', ready)).toBe(true);
    expect(initialStep(ready)).toBe('Review');
    expect(initialStep({ ...ready, session: { ...ready.session, status: 'Completed' } })).toBe('Review');
  });

  it('moves between steps, groups question rounds, and limits rounds', () => {
    expect(nextStep('Dimensions')).toBe('Questions');
    expect(previousStep('Dimensions')).toBe('Dimensions');
    expect(previousStep('Context')).toBe('Questions');
    const rounds = questionRounds([
      { question: { id: 'b', round: 2, sortOrder: 1 } },
      { question: { id: 'a2', round: 1, sortOrder: 2 } },
      { question: { id: 'a1', round: 1, sortOrder: 1 } },
    ]);
    expect(rounds.map((r) => [r.round, r.items.map((i) => i.question.id)])).toEqual([
      [1, ['a1', 'a2']],
      [2, ['b']],
    ]);
    expect(canAskMore(session({ round: 2 }))).toBe(true);
    expect(canAskMore(session({ round: 3 }))).toBe(false);
    expect(upsertById([{ id: 1, v: 'a' }], { id: 1, v: 'b' })).toEqual([{ id: 1, v: 'b' }]);
    expect(upsertById([{ id: 1 }], { id: 2 })).toEqual([{ id: 1 }, { id: 2 }]);
  });

  it('builds answers for every question kind', () => {
    expect(answerBody('SingleChoice', { optionIds: ['o1', 'o2'] })).toEqual({ selectedOptionIds: ['o1'], answerText: null, skipped: false });
    expect(answerBody('MultiChoice', { optionIds: ['o1', 'o2'], text: ' also X ' })).toEqual({ selectedOptionIds: ['o1', 'o2'], answerText: 'also X', skipped: false });
    expect(answerBody('Scale', { text: '4' })).toEqual({ answerText: '4', skipped: false });
    expect(answerBody('YesNo', { text: 'yes' })).toEqual({ answerText: 'yes', skipped: false });
    expect(answerBody('FreeText', { text: ' Rust ' })).toEqual({ answerText: 'Rust', skipped: false });
    expect(answerBody('FreeText', { skipped: true })).toEqual({ skipped: true });
  });
});

describe('screen helpers', () => {
  it('builds one-time request bodies', () => {
    const body = requestBody({ requestText: ' Heat pumps ', deliverables: ['Report', 'Email'], length: 'Short', timing: 'at', runAt: '2026-12-01 07:00', emailRecipients: 'a@b.example, c@d.example', publishToFeed: true, skipQuestions: true }, 'UTC');
    expect(body).toEqual({ requestText: 'Heat pumps', deliverables: ['Report', 'Email'], length: 'Short', runAtUtc: '2026-12-01T07:00:00.000Z', emailRecipients: ['a@b.example', 'c@d.example'], publishToFeed: false, skipQuestions: true });
    expect(requestBody({ requestText: 'x', deliverables: [], length: 'Standard', timing: 'now', runAt: '', emailRecipients: 'a', publishToFeed: false, skipQuestions: false }).deliverables).toEqual(['Report']);
  });

  it('decides which request actions apply', () => {
    expect([canConfirm({ status: 'Clarifying' }), canConfirm({ status: 'Clarifying', outOfScopeReason: 'no' }), canConfirm({ status: 'Scheduled' })]).toEqual([true, false, false]);
    expect([canCancel({ status: 'Scheduled' }), canCancel({ status: 'Completed' })]).toEqual([true, false]);
    expect([canConvert({ status: 'Completed' }), canConvert({ status: 'Declined' }), canConvert({ status: 'Completed', convertedTopicId: 'top_1' })]).toEqual([true, false, false]);
    const now = Date.parse('2026-11-19T00:00:00Z');
    expect(confirmTime({ runAtUtc: '2026-11-20T00:00:00Z' }, now)).toBe('2026-11-20T00:00:00Z');
    expect(confirmTime({ runAtUtc: '2026-11-18T00:00:00Z' }, now)).toBeNull();
  });

  it('filters predictions that are due, and builds list queries', () => {
    const now = Date.parse('2026-11-19T00:00:00Z');
    const items = [
      { id: 'a', status: 'Open', horizonUtc: '2026-11-01T00:00:00Z' },
      { id: 'b', status: 'Open', horizonUtc: '2027-01-01T00:00:00Z' },
      { id: 'c', status: 'ProposedResolved', horizonUtc: '2027-01-01T00:00:00Z' },
      { id: 'd', status: 'Accurate', horizonUtc: '2026-11-01T00:00:00Z' },
    ];
    expect(filterPredictions(items, 'due', now).map((p) => p.id)).toEqual(['a', 'c']);
    expect(filterPredictions(items, 'resolved', now).map((p) => p.id)).toEqual(['d']);
    expect(isPredictionDue(items[0], now)).toBe(true);
    expect(predictionsQuery('Open', 'top_1')).toMatchObject({ status: 'Open', topicId: 'top_1', sortBy: 'horizonUtc' });
    expect(predictionsQuery('due').status).toBeUndefined();
    expect(findingsQuery({ tier: 'all', minScore: 0 })).toMatchObject({ tier: undefined, minScore: undefined, sortBy: 'relevanceScore' });
    expect(findingsQuery({ tier: 'MustRead', minScore: 60, runId: 'run_1' })).toMatchObject({ tier: 'MustRead', minScore: 60, runId: 'run_1' });
    expect(findingsQuery({ tier: 'all' })).toMatchObject({ pinned: undefined, includeHidden: undefined });
    expect(findingsQuery({ tier: 'all', options: ['pinned', 'hidden'] })).toMatchObject({ pinned: true, includeHidden: true });
    expect(briefingQuery('pinned')).toMatchObject({ pinned: true });
    expect(briefingQuery('Run')).toMatchObject({ kind: 'Run' });
    expect(briefingQuery('all').kind).toBeUndefined();
  });

  it('builds suggestion status bodies with a snooze date', () => {
    const now = Date.parse('2026-11-19T00:00:00Z');
    expect(suggestionStatusBody('Snoozed', 7, now)).toEqual({ status: 'Snoozed', snoozedUntilUtc: '2026-11-26T00:00:00.000Z' });
    expect(suggestionStatusBody('Done', 7, now)).toEqual({ status: 'Done' });
  });

  it('formats run durations, counts dispositions, and lists score components', () => {
    expect(formatDuration(850, 'en')).toBe('850 ms');
    expect(formatDuration(4200, 'en')).toBe('4.2 s');
    expect(formatDuration(190000, 'en')).toBe('3 min 10 s');
    expect(dispositionCounts([{ disposition: 'Fetched' }, { disposition: 'SkippedSeen' }, { disposition: 'Fetched' }])).toEqual([
      { disposition: 'Fetched', count: 2 },
      { disposition: 'SkippedSeen', count: 1 },
    ]);
    expect(scoreRows({ llmScore: 80, feedbackScore: 50, noveltyScore: 10, sourceScore: 60, recencyScore: 90 }, (k) => k.split('.').pop())).toEqual([
      { label: 'llm', value: 80 },
      { label: 'feedback', value: 50 },
      { label: 'novelty', value: 10 },
      { label: 'source', value: 60 },
      { label: 'recency', value: 90 },
    ]);
  });

  it('round-trips edition settings and orders chosen topics', () => {
    const form = editionForm({ name: 'Morning', enabled: true, audioEnabled: true, emailEnabled: false, freshResearchBeforeCompose: true, lookbackMode: 'FixedHours', lookbackHours: 48 });
    expect(editionBody({ ...form, lookbackHours: '9999' })).toEqual({ name: 'Morning', description: null, enabled: true, audioEnabled: true, emailEnabled: false, freshResearchBeforeCompose: true, lookbackMode: 'FixedHours', lookbackHours: 720 });
    expect(toggleTopic(['a'], 'b')).toEqual(['a', 'b']);
    expect(toggleTopic(['a', 'b'], 'a')).toEqual(['b']);
  });

  it('links briefing items to their screens', () => {
    expect(itemHref({ itemType: 'Finding', itemId: 'fnd_1' })).toBe('/today/findings/fnd_1');
    expect(itemHref({ itemType: 'Prediction', itemId: 'prd_1' })).toBe('/today/predictions/prd_1');
    expect(itemHref({ itemType: 'Suggestion', itemId: 'sug_1' })).toBe('/today/suggestions');
  });
});

describe('finding pin, hide, and delete', () => {
  it('offers pin or unpin, hide or unhide, and delete, with the update bodies', () => {
    expect(findingActions({ pinned: false, hidden: false })).toEqual(['pin', 'hide', 'delete']);
    expect(findingActions({ pinned: true, hidden: true })).toEqual(['unpin', 'unhide', 'delete']);
    expect(findingActions(null)).toEqual(['pin', 'hide', 'delete']);
    expect(['pin', 'unpin', 'hide', 'unhide', 'delete'].map(findingUpdate)).toEqual([{ pinned: true }, { pinned: false }, { hidden: true }, { hidden: false }, null]);
    expect(findingListFlags([])).toEqual({ pinned: undefined, includeHidden: undefined });
    expect(findingListFlags(['hidden'])).toEqual({ pinned: undefined, includeHidden: true });
  });

  it('applies local changes and drops what no longer matches the list options', () => {
    const items = [
      { id: 'a', pinned: false, hidden: false },
      { id: 'b', pinned: true, hidden: false },
      { id: 'c', pinned: false, hidden: false },
    ];
    const changes = { a: { hidden: true }, b: { pinned: false }, c: { deleted: true } };
    expect(applyFindingChanges(items, changes, []).map((f) => f.id)).toEqual(['b']);
    expect(applyFindingChanges(items, changes, ['hidden'])).toEqual([
      { id: 'a', pinned: false, hidden: true },
      { id: 'b', pinned: false, hidden: false },
    ]);
    expect(applyFindingChanges(items, { a: { pinned: true } }, ['pinned']).map((f) => f.id)).toEqual(['a', 'b']);
    expect(applyFindingChanges(null, changes, [])).toEqual([]);
  });

  it('runs an action against the server and returns the change', async () => {
    const calls = [];
    const fetchImpl = async (url, init = {}) => {
      calls.push({ method: init.method || 'GET', path: url.replace(/^https?:\/\/[^/]+/, ''), body: init.body ? JSON.parse(init.body) : undefined });
      if (init.method === 'DELETE') return { status: 204, text: async () => '' };
      return { status: 200, text: async () => JSON.stringify({ id: 'fnd_1', pinned: true, hidden: false }) };
    };
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl });
    const finding = { id: 'fnd_1', pinned: false, hidden: false };
    expect(await performFindingAction(client, finding, 'pin')).toEqual({ pinned: true, hidden: false });
    expect(await performFindingAction(client, finding, 'delete')).toEqual({ deleted: true });
    expect(calls).toEqual([
      { method: 'PUT', path: '/v1.0/api/findings/fnd_1', body: { pinned: true } },
      { method: 'DELETE', path: '/v1.0/api/findings/fnd_1', body: undefined },
    ]);
    await expect(performFindingAction(client, finding, 'archive')).rejects.toThrow('Unknown finding action: archive');
  });
});

describe('briefing files', () => {
  const detail = { briefing: { id: 'brf_1', title: 'Morning News: Nov 19' }, files: [{ kind: 'BriefingHtml', fileName: '2026-11-19 morning.html' }] };

  it('names files from the server or the title', () => {
    expect(briefingFileName(detail, 'html')).toBe('2026-11-19_morning.html');
    expect(briefingFileName(detail, 'md')).toBe('morning-news-nov-19.md');
  });

  it('downloads with the bearer token and opens the share sheet', async () => {
    const downloadFileAsync = jest.fn(async (url, target) => ({ uri: 'file:///cache/' + target.name }));
    class FakeFile {
      constructor(dir, name) {
        this.name = name;
        this.exists = false;
      }
    }
    FakeFile.downloadFileAsync = downloadFileAsync;
    const sharing = { isAvailableAsync: jest.fn(async () => true), shareAsync: jest.fn(async () => undefined) };
    const client = { url: (path, query) => 'http://127.0.0.1:12100' + path + '?format=' + query.format, get: jest.fn() };
    const result = await shareBriefingFile({ client, token: 'tok', detail, format: 'md', fs: { File: FakeFile, Paths: { cache: {} } }, sharing });
    expect(downloadFileAsync).toHaveBeenCalledWith('http://127.0.0.1:12100/v1.0/api/briefings/brf_1/download?format=md', expect.any(FakeFile), { headers: { Authorization: 'Bearer tok' }, idempotent: true });
    expect(sharing.shareAsync).toHaveBeenCalledWith('file:///cache/morning-news-nov-19.md', expect.objectContaining({ mimeType: 'text/markdown' }));
    expect(result).toEqual({ uri: 'file:///cache/morning-news-nov-19.md', shared: true });
  });

  it('reports the typed error when the file passed retention', async () => {
    class FakeFile {
      constructor() {
        this.exists = false;
      }
    }
    FakeFile.downloadFileAsync = jest.fn(async () => Promise.reject(new Error('HTTP 410')));
    const expired = Object.assign(new Error('The report file is not available.'), { code: 'ArtifactExpired' });
    const client = { url: () => 'http://x', get: jest.fn(async () => Promise.reject(expired)) };
    await expect(shareBriefingFile({ client, token: 't', detail, format: 'html', fs: { File: FakeFile, Paths: { cache: {} } }, sharing: {} })).rejects.toBe(expired);
  });
});
