// Fixtures for the topics, wizard, runs, and one-time request tests.
export const TOPIC = {
  id: 'top_1',
  tenantId: 'default',
  userId: 'usr_admin',
  name: 'AI developer tools',
  slug: 'ai-developer-tools',
  description: 'I want to learn about AI developer tools',
  brief: '## What to track\n\nCoding agents and **evals**.',
  audienceLevel: 'Practitioner',
  horizon: 'Months',
  language: 'en',
  status: 'Active',
  isOneTime: false,
  noveltyPreference: 70,
  emailOnRun: true,
  emailWhenEmpty: false,
  maxEmailFindings: 10,
  seedQueries: ['AI coding agent'],
  lastRunUtc: '2026-11-19T10:00:00Z',
  lastRefinedUtc: '2026-11-18T10:00:00Z',
  createdUtc: '2026-11-18T09:00:00Z',
  lastUpdateUtc: '2026-11-18T10:00:00Z'
};

export const DIMENSION = {
  id: 'tdm_1',
  topicId: 'top_1',
  name: 'Coding assistants',
  description: 'Copilots and agentic editors',
  rationale: 'You build developer tooling',
  weight: 'High',
  includeTerms: ['IDE agent'],
  excludeTerms: ['webinar'],
  isLocked: false,
  origin: 'Model',
  sortOrder: 0,
  lastUpdateUtc: '2026-11-18T10:00:00Z'
};

export function question(id, kind, extra = {}) {
  return {
    question: { id, sessionId: 'rfs_1', round: 1, questionText: 'Question ' + id, rationale: 'Because ' + id, kind, answered: false, skipped: false, sortOrder: 0, lastUpdateUtc: '2026-11-18T10:00:00Z', ...extra },
    options:
      kind === 'SingleChoice' || kind === 'MultiChoice'
        ? [
            { id: id + '_b', questionId: id, label: 'Second', selected: false, sortOrder: 1 },
            { id: id + '_a', questionId: id, label: 'First', selected: false, sortOrder: 0 }
          ]
        : []
  };
}

export function session(extra = {}) {
  return {
    id: 'rfs_1',
    topicId: 'top_1',
    userId: 'usr_admin',
    status: 'Active',
    step: 'Dimensions',
    round: 0,
    seedText: 'I want to learn about AI developer tools',
    sufficientContext: false,
    outOfScopeReason: null,
    lastError: null,
    createdUtc: '2026-11-18T09:00:00Z',
    ...extra
  };
}

export function state({ session: s = {}, topic = {}, dimensions = [DIMENSION], constraints = [], questions = [] } = {}) {
  return { session: session(s), topic: { ...TOPIC, status: 'Draft', brief: null, ...topic }, dimensions, constraints, questions, messages: [] };
}

export const RUN = {
  id: 'run_1',
  topicId: 'top_1',
  userId: 'usr_admin',
  trigger: 'Manual',
  status: 'Completed',
  isDryRun: false,
  cancelRequested: false,
  queuedUtc: '2026-11-19T10:00:00Z',
  startedUtc: '2026-11-19T10:00:01Z',
  completedUtc: '2026-11-19T10:00:03Z',
  summary: 'Queued and executed. The research stages are not installed on this server version yet.',
  createdUtc: '2026-11-19T10:00:00Z'
};

export const REQUEST = {
  id: 'rrq_1',
  userId: 'usr_admin',
  topicId: 'top_9',
  requestText: 'Write me a report on heat pump rebates in California',
  title: 'Heat pump rebates in California',
  deliverables: ['Report', 'Email'],
  length: 'Standard',
  status: 'Clarifying',
  runAtUtc: null,
  emailRecipients: ['admin@mymorningnews'],
  publishToFeed: false,
  createdUtc: '2026-11-19T10:00:00Z'
};

export function requestResponse(extra = {}, more = {}) {
  return {
    request: { ...REQUEST, ...extra },
    topic: { ...TOPIC, id: 'top_9', isOneTime: true, name: 'Heat pump rebates', brief: '## Plan\n\nState and utility rebates.' },
    dimensions: [{ ...DIMENSION, id: 'tdm_9', name: 'Utility rebates', weight: 'High' }],
    questions: [question('rfq_9', 'YesNo')],
    refinementSessionId: 'rfs_9',
    ...more
  };
}
