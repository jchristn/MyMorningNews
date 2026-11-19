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

// Phase 6 research results.
export const FINDING = {
  id: 'fnd_1',
  tenantId: 'default',
  runId: 'run_1',
  topicId: 'top_1',
  userId: 'usr_admin',
  dimensionId: 'tdm_1',
  sourceItemId: 'src_1',
  title: 'Agentic IDE ships eval harness',
  summary: 'A coding agent now runs evals on every change.',
  whyItMatters: 'You build developer tooling and evals.',
  category: 'Release',
  relevanceScore: 84.5,
  llmScore: 90,
  feedbackScore: 50,
  noveltyScore: 72,
  sourceScore: 60,
  recencyScore: 95,
  tier: 'MustRead',
  relevanceRationale: 'Directly about coding agents and evals.',
  confidence: 0.8,
  isDuplicate: false,
  duplicateOfFindingId: null,
  isUpdateOfFindingId: null,
  rank: 1,
  publishedUtc: '2026-11-18T08:00:00Z',
  isDryRun: false,
  createdUtc: '2026-11-19T10:00:02Z'
};

export const FINDING_DETAIL = {
  finding: FINDING,
  topicName: 'AI developer tools',
  dimension: DIMENSION,
  evidence: [
    {
      evidence: { id: 'evd_1', sourceItemId: 'src_1', runId: 'run_1', quote: 'Every pull request now triggers the eval suite.', verified: true },
      sourceUrl: 'https://example.com/agent-evals',
      sourceTitle: 'Agent evals on every change',
      sourceDomain: 'example.com',
      sourcePublishedUtc: '2026-11-18T08:00:00Z',
      role: 'Supports'
    }
  ]
};

export const PREDICTION = {
  id: 'prd_1',
  runId: 'run_1',
  topicId: 'top_1',
  userId: 'usr_admin',
  statement: 'A major IDE will ship a built-in eval runner by March.',
  rationale: 'Three vendors previewed one this quarter.',
  confidence: 0.7,
  horizonUtc: '2027-03-01T00:00:00Z',
  status: 'ProposedResolved',
  resolvedUtc: null,
  resolvedBy: null,
  resolutionNote: null,
  brierScore: null,
  isDryRun: false,
  createdUtc: '2026-11-19T10:00:02Z'
};

export const REVIEW = {
  id: 'prv_1',
  predictionId: 'prd_1',
  runId: 'run_2',
  proposedStatus: 'Accurate',
  rationale: 'The vendor announced it today.',
  confidence: 0.9,
  acceptedByUser: null,
  findingIds: ['fnd_1'],
  createdUtc: '2026-11-19T11:00:00Z'
};

export const SUGGESTION = {
  id: 'sug_1',
  runId: 'run_1',
  topicId: 'top_1',
  userId: 'usr_admin',
  title: 'Try the new eval harness on your repo',
  description: 'Run it against one service and compare.',
  rationale: 'It matches the tools you track.',
  priority: 'High',
  effort: 'Hours',
  status: 'New',
  snoozedUntilUtc: null,
  statusChangedUtc: null,
  findingIds: ['fnd_1'],
  isDryRun: false,
  createdUtc: '2026-11-19T10:00:02Z'
};

function stage(id, name, status, startOffsetMs, durationMs, extra = {}) {
  const start = new Date(Date.parse('2026-11-19T10:00:01Z') + startOffsetMs).toISOString();
  return {
    id,
    runId: 'run_p',
    stage: name,
    status,
    startedUtc: start,
    completedUtc: new Date(Date.parse(start) + durationMs).toISOString(),
    durationMs,
    itemsIn: 4,
    itemsOut: 3,
    errorMessage: null,
    sortOrder: Number(id.slice(-1)),
    ...extra
  };
}

export const STAGES = [
  stage('rst_0', 'AssembleContext', 'Succeeded', 0, 20),
  stage('rst_1', 'Search', 'Succeeded', 20, 1500),
  stage('rst_2', 'Extract', 'Succeeded', 1520, 3000),
  stage('rst_3', 'Predict', 'Failed', 4520, 400, { errorMessage: 'Prediction model timed out' })
];

export const PARTIAL_RUN = {
  ...RUN,
  id: 'run_p',
  status: 'PartiallySucceeded',
  summary: null,
  durationMs: 4920,
  newFindingCount: 3,
  duplicateCount: 2,
  sourceCount: 6,
  inputTokens: 12000,
  outputTokens: 3400,
  estimatedCostUsd: 0.42
};

export const QUERY = { id: 'qry_1', runId: 'run_p', queryText: 'AI coding agent evals', rationale: 'Seed query', provider: 'Searxng', resultCount: 2, durationMs: 1477, succeeded: true };

export const RESULTS = [
  { id: 'srh_1', searchQueryId: 'qry_1', runId: 'run_p', url: 'https://example.com/agent-evals', title: 'Agent evals on every change', snippet: 'Every pull request...', rank: 1, provider: 'Searxng', disposition: 'Fetched', sourceItemId: 'src_1' },
  { id: 'srh_2', searchQueryId: 'qry_1', runId: 'run_p', url: 'https://blocked.example/post', title: 'Blocked post', snippet: null, rank: 2, provider: 'Searxng', disposition: 'SkippedBlocked', sourceItemId: null }
];

export const SOURCE = {
  id: 'src_1',
  canonicalUrl: 'https://example.com/agent-evals',
  domain: 'example.com',
  title: 'Agent evals on every change',
  publishedUtc: '2026-11-18T08:00:00Z',
  fetchedUtc: '2026-11-19T10:00:02Z',
  httpStatus: 200,
  contentType: 'text/html',
  contentText: 'Every pull request now triggers the eval suite.',
  contentBytes: 2048,
  timesSeen: 1
};
