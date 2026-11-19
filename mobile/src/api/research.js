import { routes } from './routes';

// Research and briefing routes from plan section 24. Topics, refinement, requests, schedules, runs, findings,
// predictions, suggestions, sources, briefings, and editions are live (Phases 4 to 7); feedback and insights arrive in
// Phase 10 and answer 404 until then, which screens show as "This arrives in a later server update".
//
// List calls take the shared list contract (pageNumber, pageSize, sortBy, sortDirection, search, fromUtc, toUtc, plus
// typed filters) and return `{ items, pageNumber, pageSize, totalCount, totalPages }`. Detail reads return wrapper
// objects such as `{ topic, dimensions, constraints, recipients }` or `{ briefing, sections, items, files }`.

/** Topics with their dimensions and constraints (Phase 4). */
export const topicsApi = {
  list: (client, query) => client.get(routes.topics, query),
  /** `{ topic, dimensions, constraints, recipients }` */
  get: (client, id) => client.get(routes.topic(id)),
  /** TopicUpsertRequest: `{ name, brief, audienceLevel, horizon, noveltyPreference, status, ... }` (any subset). */
  update: (client, id, body) => client.put(routes.topic(id), body),
  /** Queue a run (202 with the Run). `dryRun` sends no email and is not counted in history. */
  run: (client, id, { dryRun = false } = {}) => client.post(routes.topicRun(id), {}, { query: dryRun ? { dryRun: true } : undefined }),
  pause: (client, id) => client.post(routes.topicPause(id), {}),
  resume: (client, id) => client.post(routes.topicResume(id), {}),
  /** "Refine again": starts a wizard session on an existing topic and returns the session state. */
  refine: (client, id) => client.post(routes.topicRefine(id), {}),
  addDimension: (client, id, body) => client.post(routes.topicDimensions(id), body),
  updateDimension: (client, id, dimensionId, body) => client.put(routes.topicDimension(id, dimensionId), body),
  removeDimension: (client, id, dimensionId) => client.delete(routes.topicDimension(id, dimensionId)),
  /** ConstraintUpsertRequest: `{ kind, value, note?, dimensionId?, isLocked? }` */
  addConstraint: (client, id, body) => client.post(routes.topicConstraints(id), body),
  removeConstraint: (client, id, constraintId) => client.delete(routes.topicConstraint(id, constraintId)),
};

/**
 * The refinement wizard (Phase 4, plan section 13). Every call returns the session state
 * `{ session, topic, dimensions, constraints, questions, messages }`. The generation steps (dimensions, questions,
 * context) call a model and answer 502 ModelError when none is configured.
 */
export const refinementApi = {
  /** RefinementStartRequest: `{ seedText, goal?, depth?, horizon?, known? }`. Creates the draft topic. */
  start: (client, body) => client.post(routes.refinementSessions, body, { timeoutMs: 120000 }),
  list: (client, query) => client.get(routes.refinementSessions, query),
  get: (client, id) => client.get(routes.refinementSession(id)),
  proposeDimensions: (client, id, guidance) => client.post(routes.refinementStep(id, 'dimensions'), guidance ? { guidance } : {}, { timeoutMs: 180000 }),
  askQuestions: (client, id, guidance) => client.post(routes.refinementStep(id, 'questions'), guidance ? { guidance } : {}, { timeoutMs: 180000 }),
  /** QuestionAnswerRequest: `{ answerText?, selectedOptionIds?, skipped }`. Returns the updated `{ question, options }`. */
  answer: (client, id, questionId, body) => client.put(routes.refinementQuestion(id, questionId), body),
  draftContext: (client, id) => client.post(routes.refinementStep(id, 'context'), {}, { timeoutMs: 180000 }),
  /** Apply the topic: Draft becomes Active. 409 until the topic has a brief and at least one dimension. */
  complete: (client, id) => client.post(routes.refinementStep(id, 'complete'), {}),
  /** Abandon the session; a new draft topic is deleted with it. 204. */
  abandon: (client, id) => client.post(routes.refinementStep(id, 'abandon'), {}),
};

/** One-time requests (plan section 13.1): reports, podcasts, and email summaries researched once. */
export const requestsApi = {
  list: (client, query) => client.get(routes.requests, query),
  /** `{ request, topic, dimensions, questions, refinementSessionId }` */
  get: (client, id) => client.get(routes.request(id)),
  /**
   * ResearchRequestCreateRequest: `{ requestText, deliverables, length, runAtUtc?, emailRecipients, publishToFeed,
   * skipQuestions }`. Plans with a model; a 502 ModelError still leaves the request saved in Clarifying.
   */
  create: (client, body) => client.post(routes.requests, body, { timeoutMs: 180000 }),
  answer: (client, id, questionId, body) => client.put(routes.requestQuestion(id, questionId), body),
  replan: (client, id) => client.post(routes.requestAction(id, 'replan'), {}, { timeoutMs: 180000 }),
  /** Run once now, or at `runAtUtc` when given. */
  confirm: (client, id, runAtUtc) => client.post(routes.requestAction(id, 'confirm'), runAtUtc ? { runAtUtc } : {}),
  cancel: (client, id) => client.post(routes.requestAction(id, 'cancel'), {}),
  /** Make this a recurring topic; returns the Topic. */
  convert: (client, id) => client.post(routes.requestAction(id, 'convert'), {}),
  remove: (client, id) => client.delete(routes.request(id)),
};

/** Schedules for topics and editions (Phase 5, plan section 14). */
export const schedulesApi = {
  /** Filters: `ownerType` (Topic, Edition, Request) and `ownerId`. */
  list: (client, query) => client.get(routes.schedules, query),
  create: (client, body) => client.post(routes.schedules, body),
  update: (client, id, body) => client.put(routes.schedule(id), body),
  remove: (client, id) => client.delete(routes.schedule(id)),
  /** `{ valid, errors, nextRunsUtc, nextRunsLocal }` for a ScheduleUpsertRequest. */
  preview: (client, body) => client.post(routes.schedulePreview, body),
};

/** Runs and their drill-down (Phases 5 and 6). */
export const runsApi = {
  /** Filters: `topicId`, `status`, `trigger`. */
  list: (client, query) => client.get(routes.runs, query),
  get: (client, id) => client.get(routes.run(id)),
  cancel: (client, id) => client.post(routes.runCancel(id), {}),
  /** Stages in order (a bare array). */
  stages: (client, id) => client.get(routes.runStages(id)),
  queries: (client, id) => client.get(routes.runQueries(id)),
  results: (client, id, disposition) => client.get(routes.runResults(id), disposition ? { disposition } : undefined),
};

/** Findings with evidence (Phase 6). */
export const findingsApi = {
  /** Filters: `topicId`, `runId`, `tier`, `minScore`, `category`, `dimensionId`. */
  list: (client, query) => client.get(routes.findings, query),
  /** `{ finding, evidence: [{ evidence, sourceUrl, sourceTitle, sourceDomain, sourcePublishedUtc, role }], dimension, topicName }` */
  get: (client, id) => client.get(routes.finding(id)),
};

/** Fetched sources (Phase 6). */
export const sourcesApi = {
  /** Filters: `topicId`, `runId`. Content text is left out of the list. */
  list: (client, query) => client.get(routes.sources, query),
  get: (client, id) => client.get(routes.source(id)),
};

/** Predictions and their reviews (Phase 6). */
export const predictionsApi = {
  /** Filters: `topicId`, `status`. */
  list: (client, query) => client.get(routes.predictions, query),
  /** `{ prediction, reviews }` */
  get: (client, id) => client.get(routes.prediction(id)),
  /** `{ status, note }` with status Accurate, PartiallyAccurate, Inaccurate, Unresolvable, or Withdrawn. */
  resolve: (client, id, body) => client.post(routes.predictionResolve(id), body),
  acceptReview: (client, reviewId) => client.post(routes.predictionReviewAccept(reviewId), {}),
  rejectReview: (client, reviewId) => client.post(routes.predictionReviewReject(reviewId), {}),
};

/** Suggestions (Phase 6). */
export const suggestionsApi = {
  /** Filters: `topicId`, `status`, `priority`. */
  list: (client, query) => client.get(routes.suggestions, query),
  /** `{ status, snoozedUntilUtc? }` with status New, Accepted, Snoozed, Dismissed, or Done. */
  setStatus: (client, id, body) => client.put(routes.suggestionStatus(id), body),
};

/** Briefings (Phase 7) and listening progress (Phase 16). */
export const briefingsApi = {
  /** Filters: `kind` (Edition, Run, OneTime), `editionId`, `topicId`, `runId`, `status`, `pinned`. */
  list: (client, query) => client.get(routes.briefings, query),
  /** `{ briefing, sections, items, files, filesExpired, editionName, topicName }` */
  get: (client, id) => client.get(routes.briefing(id)),
  /** Absolute download URL for the `md` or `html` file (send the bearer token with it). */
  downloadUrl: (client, id, format) => client.url(routes.briefingDownload(id), { format }),
  pin: (client, id, pinned) => client.put(routes.briefingPin(id), { pinned: Boolean(pinned) }),
  /** Store fresh Markdown and HTML files after the old ones passed retention. */
  regenerate: (client, id) => client.post(routes.briefingRegenerate(id), {}),
  /** `{ positionSeconds, completed }` */
  saveProgress: (client, id, progress) => client.put(routes.briefingProgress(id), progress),
};

/** Editions (Phase 7, plan section 21.3). */
export const editionsApi = {
  list: (client, query) => client.get(routes.editions, query),
  /** `{ edition, topics, schedules, coveredTopicIds }` */
  get: (client, id) => client.get(routes.edition(id)),
  /** EditionUpsertRequest: `{ name, description, audioEnabled, emailEnabled, enabled, freshResearchBeforeCompose, lookbackMode, lookbackHours, ... }` */
  update: (client, id, body) => client.put(routes.edition(id), body),
  /** Topics in order; an empty list means all of the owner's active recurring topics. */
  setTopics: (client, id, topicIds) => client.put(routes.editionTopics(id), { topicIds }),
  /** Queue a briefing now (202 with the queued Briefing). */
  compose: (client, id) => client.post(routes.editionCompose(id), {}),
  /** A preview Briefing from the latest data, not saved. */
  preview: (client, id) => client.post(routes.editionPreview(id), {}, { timeoutMs: 180000 }),
};

/** Feedback (Phase 10): `{ targetType, targetId, rating, reason }`, the same API as the email feedback pages. */
export const feedbackApi = {
  submit: (client, body) => client.post(routes.feedback, body),
  list: (client, query) => client.get(routes.feedback, query),
};

/** Insights (Phase 10). */
export const insightsApi = {
  analytics: (client, name, query) => client.get(routes.analytics(name), query),
  learnedPreferences: (client, query) => client.get(routes.learnedPreferences, query),
};
