import { describe, expect, it } from 'vitest';
import { greeting, hourIn, partOfDay } from './greeting.js';
import { PLATFORM_CREDENTIAL_ID, keyProblem, modelKeyProblems, resolveTaskEndpoint } from './modelKeys.js';
import { coveringEditions, scheduleState } from './topicSchedule.js';
import { featureValue } from '../views/plans/planParts.jsx';
import { formatUsd } from '../i18n/formatters.js';

const t = (key) => key;

function endpoint(id, extra = {}) {
  return { id, name: id, provider: 'Anthropic', supportsChat: true, supportsEmbeddings: false, active: true, createdUtc: '2026-11-01T00:00:00Z', ...extra };
}

describe('model key problems', () => {
  it('flags a hosted endpoint with no credential and trusts the platform key', () => {
    expect(keyProblem(endpoint('a'), [])).toBe('missing');
    expect(keyProblem(endpoint('a', { credentialId: 'platform:anthropic' }), [])).toBeNull();
    expect(keyProblem(endpoint('o', { provider: 'Ollama' }), [])).toBeNull();
    expect(keyProblem(endpoint('c', { provider: 'OpenAiCompatible' }), [])).toBeNull();
  });

  it('flags a stored credential that is gone, off, or expired', () => {
    const now = new Date('2026-11-20T00:00:00Z');
    const creds = [
      { id: 'vlt_off', active: false },
      { id: 'vlt_old', active: true, expiresUtc: '2026-11-01T00:00:00Z' },
      { id: 'vlt_ok', active: true }
    ];
    expect(keyProblem(endpoint('a', { credentialId: 'vlt_gone' }), creds, now)).toBe('notFound');
    expect(keyProblem(endpoint('a', { credentialId: 'vlt_off' }), creds, now)).toBe('inactive');
    expect(keyProblem(endpoint('a', { credentialId: 'vlt_old' }), creds, now)).toBe('expired');
    expect(keyProblem(endpoint('a', { credentialId: 'vlt_ok' }), creds, now)).toBeNull();
    expect(keyProblem(endpoint('a', { credentialId: 'vlt_gone' }), null, now)).toBeNull();
  });

  it('checks the endpoints the tenant actually uses, once each', () => {
    const endpoints = [endpoint('first', { credentialId: 'vlt_ok' }), endpoint('second', { createdUtc: '2026-11-02T00:00:00Z' })];
    const creds = [{ id: 'vlt_ok', active: true }];
    expect(modelKeyProblems({ endpoints, assignments: [], credentials: creds })).toEqual([]);
    const assigned = [{ task: 'Refinement', modelEndpointId: 'second', active: true }];
    const problems = modelKeyProblems({ endpoints, assignments: assigned, credentials: creds });
    expect(problems).toHaveLength(1);
    expect(problems[0]).toMatchObject({ problem: 'missing', tasks: ['Refinement'] });
    expect(problems[0].endpoint.id).toBe('second');
    expect(modelKeyProblems({ endpoints, assignments: assigned, credentials: creds, tasks: ['Scoring'] })).toEqual([]);
  });

  it('resolves a topic assignment before the tenant one, like the server', () => {
    const endpoints = [endpoint('tenant'), endpoint('topic', { createdUtc: '2026-11-03T00:00:00Z' })];
    const assignments = [
      { task: 'Synthesis', modelEndpointId: 'tenant', active: true },
      { task: 'Synthesis', topicId: 'top_1', modelEndpointId: 'topic', active: true }
    ];
    expect(resolveTaskEndpoint(endpoints, assignments, 'Synthesis', 'top_1')).toMatchObject({ scope: 'topic', endpoint: { id: 'topic' } });
    expect(resolveTaskEndpoint(endpoints, assignments, 'Synthesis', 'top_2')).toMatchObject({ scope: 'tenant', endpoint: { id: 'tenant' } });
    expect(resolveTaskEndpoint(endpoints, [], 'Embedding')).toMatchObject({ scope: 'fallback', endpoint: null });
  });
});

describe('greeting', () => {
  it('uses the part of day in the given time zone', () => {
    const at = new Date('2026-11-20T15:30:00Z');
    expect(hourIn(at, 'UTC')).toBe(15);
    expect(partOfDay(at, 'UTC')).toBe('afternoon');
    expect(partOfDay(at, 'America/Los_Angeles')).toBe('morning');
    expect(partOfDay(at, 'Asia/Tokyo')).toBe('evening');
  });

  it('greets by first name, or plainly without one', () => {
    const at = new Date('2026-11-20T09:00:00Z');
    expect(greeting({ firstName: ' Demo ' }, at, 'UTC')).toEqual({ id: 'morning', values: { name: 'Demo' } });
    expect(greeting({ firstName: '', email: 'demo@example.test' }, at, 'UTC')).toEqual({ id: 'morningPlain', values: {} });
    expect(greeting(null, new Date('2026-11-20T20:00:00Z'), 'UTC').id).toBe('eveningPlain');
  });
});

describe('topic schedule state', () => {
  it('says paused when every schedule is disabled', () => {
    expect(scheduleState([]).state).toBe('none');
    expect(scheduleState([{ id: 's1', enabled: false }])).toMatchObject({ state: 'paused', nextRunUtc: null });
    expect(scheduleState([{ id: 's1', enabled: false }, { id: 's2', enabled: true, nextRunUtc: '2026-11-21T05:00:00Z' }])).toMatchObject({ state: 'active', nextRunUtc: '2026-11-21T05:00:00Z' });
  });

  it('finds the editions that research a topic and whether they are paused', () => {
    const details = [
      { edition: { id: 'edn_1', name: 'Morning News', enabled: true }, schedules: [{ enabled: false }], coveredTopicIds: ['top_1'] },
      { edition: { id: 'edn_2', name: 'Weekly', enabled: true }, schedules: [{ enabled: true, nextRunUtc: '2026-11-22T06:00:00Z' }], coveredTopicIds: ['top_1', 'top_2'] },
      { edition: { id: 'edn_3', name: 'Other', enabled: true }, schedules: [{ enabled: true }], coveredTopicIds: ['top_2'] },
      null
    ];
    expect(coveringEditions(details, 'top_1')).toEqual([
      { id: 'edn_1', name: 'Morning News', paused: true, nextRunUtc: null },
      { id: 'edn_2', name: 'Weekly', paused: false, nextRunUtc: '2026-11-22T06:00:00Z' }
    ]);
  });
});

describe('plan values and currency', () => {
  it('shows a huge count limit as unlimited and keeps amounts as stored', () => {
    expect(featureValue(t, '10000', 'artifacts.pinned.max')).toBe('promo.usage.unlimited');
    expect(featureValue(t, '2', 'topics.max')).toBe('2');
    expect(featureValue(t, '30.00', 'usage.monthlyCostUsd.max')).toBe('30.00');
    expect(featureValue(t, '1440', 'schedule.minIntervalMinutes', (n) => '#' + n)).toBe('#1440');
  });

  it('prints a plain dollar sign in Arabic', () => {
    expect(formatUsd(0, 'ar')).not.toMatch(/US/);
    expect(formatUsd(0, 'ar')).toContain('$');
  });
});

describe('platform model key', () => {
  it('reports the platform key only when the server says it is missing', () => {
    const endpoint = { id: 'mep_1', provider: 'Anthropic', credentialId: PLATFORM_CREDENTIAL_ID, active: true };
    expect(keyProblem(endpoint, [], new Date(), undefined)).toBeNull();
    expect(keyProblem(endpoint, [], new Date(), true)).toBeNull();
    expect(keyProblem(endpoint, [], new Date(), false)).toBe('platformMissing');
  });
});
