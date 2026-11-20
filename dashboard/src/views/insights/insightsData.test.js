import { describe, expect, it } from 'vitest';
import { actionOf, actionsFor, feedbackByTarget, targetPath } from '../../utils/feedback.js';
import { calibrationFromReport, dayBuckets, failureBars, insightsQuery, rateBars, reasonWeeks, suggestionOutcomes, weightsApplied, weightsBody } from './insightsData.js';

describe('feedback helpers', () => {
  it('maps stored ratings back to actions', () => {
    expect(actionOf({ relevance: 'SomewhatRelevant' })).toBe('somewhat');
    expect(actionOf({ accuracy: 'TooEarly' })).toBe('too-early');
    expect(actionOf({ usefulness: 'NotUseful' })).toBe('not-useful');
    expect(actionOf({})).toBeNull();
    expect(actionOf(null)).toBeNull();
    expect(actionsFor('Prediction')).toEqual(['accurate', 'partially', 'inaccurate', 'too-early']);
    expect(actionsFor('BriefingSection')).toEqual(['relevant', 'somewhat', 'not-relevant']);
  });

  it('keeps the newest feedback per target and links targets', () => {
    const map = feedbackByTarget([
      { targetId: 'a', createdUtc: '2026-11-01T00:00:00Z', id: 'old' },
      { targetId: 'a', createdUtc: '2026-11-02T00:00:00Z', id: 'new' },
      { targetId: 'b', createdUtc: '2026-11-01T00:00:00Z', id: 'b1' }
    ]);
    expect(map.get('a').id).toBe('new');
    expect(map.size).toBe(2);
    expect(targetPath('Prediction', 'prd 1')).toBe('/dashboard/predictions?id=prd%201');
    expect(targetPath('LearnedPreference', 'lrn_1')).toBe('/dashboard/insights/preferences?id=lrn_1');
    expect(targetPath('Domain', 'x.com')).toBeNull();
  });
});

describe('insights shaping', () => {
  it('builds the scope query', () => {
    const q = insightsQuery({ days: 30, topicId: 'top_1', everyone: true }, new Date('2026-11-19T00:00:00Z'));
    expect(q).toEqual({ from: '2026-10-20T00:00:00.000Z', to: '2026-11-19T00:00:00.000Z', topicId: 'top_1', all: true });
    expect(insightsQuery({}, new Date('2026-11-19T00:00:00Z'))).toMatchObject({ topicId: undefined, all: undefined });
  });

  it('orders day rows and drops non-day keys', () => {
    const b = dayBuckets([{ key: '2026-11-02', count: 1 }, { key: 'x' }, { key: '2026-11-01', count: 2 }]);
    expect(b.map((x) => x.startUtc)).toEqual(['2026-11-01T00:00:00.000Z', '2026-11-02T00:00:00.000Z']);
    expect(b[0].endUtc).toBe('2026-11-02T00:00:00.000Z');
  });

  it('turns breakdown rows into rate and failure bars', () => {
    expect(rateBars([{ key: 'a', rate: 0.5, count: 2 }, { key: 'b', count: 9 }, { key: 'c', label: 'C', rate: 1, count: 4 }])).toEqual([
      { key: 'c', label: 'C', value: 1, count: 4 },
      { key: 'a', label: 'a', value: 0.5, count: 2 }
    ]);
    expect(failureBars([{ key: 'm1', count: 10, positive: 9, rate: 0.9 }, { key: 'm2', count: 4, positive: 2 }, { key: 'm3', count: 0 }])).toEqual([
      { key: 'm2', label: 'm2', value: 0.5, count: 4 },
      { key: 'm1', label: 'm1', value: expect.closeTo(0.1, 5), count: 10 }
    ]);
  });

  it('maps calibration buckets, outcomes, and reason weeks', () => {
    expect(calibrationFromReport([{ lower: 0, upper: 0.2, count: 0 }, { lower: 0.8, upper: 1, count: 3, meanConfidence: 0.9, observed: 0.66 }])).toEqual([
      { index: 0, from: 0, to: 0.2, count: 0, meanConfidence: null, observedRate: null },
      { index: 1, from: 0.8, to: 1, count: 3, meanConfidence: 0.9, observedRate: 0.66 }
    ]);
    expect(suggestionOutcomes({ total: 4, new: 1, accepted: 1, done: 2, dismissed: 0, snoozed: 0 })[2]).toEqual({ key: 'done', count: 2, share: 0.5 });
    const weeks = reasonWeeks({ byWeek: [{ key: '2026-11-09', label: 'too basic', count: 2 }, { key: '2026-11-02', label: 'vendor', count: 1 }, { key: '2026-11-09', label: 'vendor', count: 3 }] });
    expect(weeks.weeks).toEqual(['2026-11-02', '2026-11-09']);
    expect(weeks.series).toEqual([
      { tag: 'vendor', values: [1, 3] },
      { tag: 'too basic', values: [0, 2] }
    ]);
  });

  it('builds and checks the weight body', () => {
    const body = weightsBody({ llm: 0.4, feedback: 0.2, novelty: 0.1, source: 0.15, recency: 0.15 });
    expect(body).toEqual({ weightLlm: 0.4, weightFeedback: 0.2, weightNovelty: 0.1, weightSource: 0.15, weightRecency: 0.15 });
    expect(weightsApplied({ ...body }, body)).toBe(true);
    expect(weightsApplied({ weightLlm: 0.45 }, body)).toBe(false);
  });
});
