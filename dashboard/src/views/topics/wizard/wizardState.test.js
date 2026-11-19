import { describe, expect, it } from 'vitest';
import { DIMENSION, question, state } from '../../../test/researchFixtures.js';
import {
  MAX_ROUNDS,
  advancePlan,
  answeredCount,
  canAskMore,
  initialStep,
  initialWizardState,
  isOutOfScope,
  nextStep,
  previousStep,
  questionRounds,
  stepAvailable,
  stepComplete,
  wizardReducer
} from './wizardState.js';

describe('wizard step logic', () => {
  it('resumes where the session left off, never past what exists', () => {
    expect(initialStep(null)).toBe('Describe');
    expect(initialStep(state({ session: { step: 'Describe' }, dimensions: [] }))).toBe('Dimensions');
    expect(initialStep(state({ session: { step: 'Questions', round: 1 } }))).toBe('Questions');
    expect(initialStep(state({ session: { step: 'Questions' }, dimensions: [] }))).toBe('Dimensions');
    expect(initialStep(state({ session: { step: 'Sources' } }))).toBe('Context');
    expect(initialStep(state({ session: { step: 'Sources' }, topic: { brief: '# b' } }))).toBe('Sources');
    expect(initialStep(state({ session: { status: 'Completed', step: 'Review' }, topic: { brief: '# b' } }))).toBe('Review');
  });

  it('gates steps on dimensions and the brief', () => {
    const noDims = state({ dimensions: [] });
    expect(stepAvailable('Describe', null)).toBe(true);
    expect(stepAvailable('Dimensions', null)).toBe(false);
    expect(stepAvailable('Questions', noDims)).toBe(false);
    const dims = state();
    expect(stepAvailable('Questions', dims)).toBe(true);
    expect(stepAvailable('Sources', dims)).toBe(false);
    const briefed = state({ topic: { brief: '# b' } });
    expect(stepAvailable('Sources', briefed)).toBe(true);
    expect(stepAvailable('Review', briefed)).toBe(true);
    const done = state({ session: { status: 'Completed' }, topic: { brief: '# b' } });
    expect(stepAvailable('Dimensions', done)).toBe(false);
    expect(stepAvailable('Review', done)).toBe(true);
  });

  it('marks steps complete from the data', () => {
    const s = state({ session: { round: 1 }, topic: { brief: '# b' }, constraints: [{ id: 'c', kind: 'FeedUrl', value: 'https://x' }] });
    expect(['Describe', 'Dimensions', 'Questions', 'Context', 'Sources', 'Delivery', 'Review'].map((step) => stepComplete(step, s))).toEqual([true, true, true, true, true, false, false]);
  });

  it('moves forward and back through the seven steps', () => {
    expect(nextStep('Describe')).toBe('Dimensions');
    expect(nextStep('Review')).toBe('Review');
    expect(previousStep('Dimensions')).toBe('Describe');
    expect(previousStep('Describe')).toBe('Describe');
  });

  it('generates on Next only when the next step has nothing yet', () => {
    expect(advancePlan('Dimensions', state({ session: { round: 0 } }), null)).toEqual({ generate: 'questions', to: 'Questions' });
    expect(advancePlan('Dimensions', state({ session: { round: 1 } }), null)).toEqual({ generate: null, to: 'Questions' });
    expect(advancePlan('Questions', state(), null)).toEqual({ generate: 'context', to: 'Context' });
    expect(advancePlan('Questions', state({ topic: { brief: '# b' } }), null)).toEqual({ generate: null, to: 'Context' });
    expect(advancePlan('Context', state({ topic: { brief: '# b' } }), null)).toEqual({ generate: 'sources', to: 'Sources' });
    expect(advancePlan('Context', state({ topic: { brief: '# b' } }), [])).toEqual({ generate: null, to: 'Sources' });
    expect(advancePlan('Sources', state(), [])).toEqual({ generate: null, to: 'Delivery' });
  });

  it('groups questions by round and counts answers and skips', () => {
    const qs = [question('b', 'FreeText', { round: 2 }), question('a2', 'YesNo', { sortOrder: 1, answered: true }), question('a1', 'Scale', { skipped: true })];
    const rounds = questionRounds(qs);
    expect(rounds.map((r) => r.round)).toEqual([1, 2]);
    expect(rounds[0].items.map((i) => i.question.id)).toEqual(['a1', 'a2']);
    expect(answeredCount(qs)).toBe(2);
  });

  it('caps question rounds and detects out of scope', () => {
    expect(canAskMore(state({ session: { round: MAX_ROUNDS - 1 } }))).toBe(true);
    expect(canAskMore(state({ session: { round: MAX_ROUNDS } }))).toBe(false);
    expect(isOutOfScope(state({ session: { outOfScopeReason: 'No' }, dimensions: [] }))).toBe(true);
    expect(isOutOfScope(state({ session: { outOfScopeReason: 'No' } }))).toBe(false);
  });
});

describe('wizard reducer', () => {
  const loaded = wizardReducer(initialWizardState, { type: 'loaded', data: state({ session: { step: 'Questions', round: 1 }, questions: [question('q1', 'YesNo')] }) });

  it('lands on the resumed step when a session loads', () => {
    expect(loaded.step).toBe('Questions');
    expect(loaded.generating).toBeNull();
  });

  it('tracks generation, failure, and stopping', () => {
    const generating = wizardReducer(loaded, { type: 'generate', kind: 'context' });
    expect(generating.generating).toBe('context');
    const failed = wizardReducer(generating, { type: 'failed', kind: 'context', error: new Error('x') });
    expect(failed.generating).toBeNull();
    expect(failed.failure.kind).toBe('context');
    expect(wizardReducer(failed, { type: 'dismissFailure' }).failure).toBeNull();
    expect(wizardReducer(generating, { type: 'stopped' }).generating).toBeNull();
    const sources = wizardReducer(generating, { type: 'sources', checks: [{ url: 'https://x' }] });
    expect(sources.step).toBe('Sources');
    expect(sources.sources).toHaveLength(1);
  });

  it('merges answers, dimensions, constraints, and topic edits', () => {
    const answered = wizardReducer(loaded, { type: 'question', item: question('q1', 'YesNo', { answered: true, answerText: 'yes' }) });
    expect(answered.data.questions[0].question.answerText).toBe('yes');
    const added = wizardReducer(loaded, { type: 'dimension', dimension: { ...DIMENSION, id: 'tdm_2', sortOrder: -1 } });
    expect(added.data.dimensions.map((d) => d.id)).toEqual(['tdm_2', 'tdm_1']);
    const locked = wizardReducer(added, { type: 'dimension', dimension: { ...DIMENSION, isLocked: true } });
    expect(locked.data.dimensions.find((d) => d.id === 'tdm_1').isLocked).toBe(true);
    expect(wizardReducer(locked, { type: 'dimensionRemoved', id: 'tdm_2' }).data.dimensions).toHaveLength(1);
    const c = wizardReducer(loaded, { type: 'constraint', constraint: { id: 'tcn_1', kind: 'FeedUrl', value: 'https://x' } });
    expect(c.data.constraints).toHaveLength(1);
    expect(wizardReducer(c, { type: 'constraintRemoved', id: 'tcn_1' }).data.constraints).toHaveLength(0);
    expect(wizardReducer(loaded, { type: 'topic', topic: { ...loaded.data.topic, name: 'New' } }).data.topic.name).toBe('New');
  });
});
