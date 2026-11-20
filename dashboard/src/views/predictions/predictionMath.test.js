import { describe, expect, it } from 'vitest';
import { layoutStages } from '../runs/runParts.js';
import { brierFor, calibrationBuckets, outcomeOf, predictionSummary } from './predictionMath.js';

const p = (confidence, status, extra = {}) => ({ confidence, status, brierScore: null, ...extra });

describe('prediction math', () => {
  it('maps statuses to outcomes and Brier scores the way the server does', () => {
    expect(outcomeOf('Accurate')).toBe(1);
    expect(outcomeOf('PartiallyAccurate')).toBe(0.5);
    expect(outcomeOf('Inaccurate')).toBe(0);
    expect(outcomeOf('Withdrawn')).toBeNull();
    expect(brierFor(0.8, 'Accurate')).toBeCloseTo(0.04, 6);
    expect(brierFor(0.7, 'Inaccurate')).toBeCloseTo(0.49, 6);
    expect(brierFor(0.7, 'Open')).toBeNull();
  });

  it('summarizes open, graded, accuracy, and mean Brier, preferring stored scores', () => {
    const s = predictionSummary([p(0.8, 'Accurate'), p(0.6, 'Inaccurate', { brierScore: 0.5 }), p(0.5, 'Open'), p(0.9, 'ProposedResolved'), p(0.4, 'Withdrawn')]);
    expect(s).toMatchObject({ total: 5, open: 1, proposed: 1, graded: 2 });
    expect(s.accuracy).toBeCloseTo(0.5, 6);
    expect(s.meanBrier).toBeCloseTo((0.04 + 0.5) / 2, 6);
    expect(predictionSummary([]).meanBrier).toBeNull();
  });

  it('buckets graded predictions by confidence with observed rates', () => {
    const buckets = calibrationBuckets([p(0.1, 'Inaccurate'), p(0.15, 'Accurate'), p(0.9, 'Accurate'), p(1, 'PartiallyAccurate'), p(0.5, 'Open')], 5);
    expect(buckets).toHaveLength(5);
    expect(buckets[0]).toMatchObject({ count: 2, observedRate: 0.5 });
    expect(buckets[0].meanConfidence).toBeCloseTo(0.125, 6);
    expect(buckets[2]).toMatchObject({ count: 0, observedRate: null, meanConfidence: null });
    expect(buckets[4]).toMatchObject({ count: 2, observedRate: 0.75 });
  });
});

describe('stage layout', () => {
  it('offsets stages from the earliest start and chains stages without start times', () => {
    const layout = layoutStages([
      { stage: 'B', sortOrder: 1, startedUtc: '2026-11-19T10:00:01.500Z', durationMs: 250 },
      { stage: 'A', sortOrder: 0, startedUtc: '2026-11-19T10:00:01.000Z', durationMs: 500 },
      { stage: 'C', sortOrder: 2, status: 'Pending', startedUtc: null, durationMs: 0 }
    ]);
    expect(layout.map((x) => [x.stage.stage, x.offsetMs, x.durationMs])).toEqual([
      ['A', 0, 500],
      ['B', 500, 250],
      ['C', 750, 0]
    ]);
  });
});
