import { routes } from './routes';

// Research and briefing routes from plan section 24. Topics, refinement, requests, schedules, runs, findings,
// predictions, suggestions, sources, briefings, and editions are live (Phases 4 to 7), as are audio (Phase 8) and
// feedback, learned preferences, and analytics (Phases 9 and 10). An older server answers 404, which screens show as
// "This arrives in a later server update".
//
// List calls take the shared list contract (pageNumber, pageSize, sortBy, sortDirection, search, fromUtc, toUtc, plus
// typed filters) and return `{ objects, pageNumber, pageSize, totalRecords, totalPages }`. Detail reads return wrapper
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
  /** Stages in order (EnumerationResult). */
  stages: (client, id, query) => client.get(routes.runStages(id), query),
  queries: (client, id, query) => client.get(routes.runQueries(id), query),
  results: (client, id, disposition, query) => client.get(routes.runResults(id), { ...query, disposition }),
  /** What the run cost (RunCostReport): totals, each stage, each model, and each task role. Current while it runs. */
  costs: (client, id) => client.get(routes.runCosts(id)),
};

/**
 * Findings with evidence (Phase 6), pinned and hidden by their owner. Hidden findings are left out of lists (unless
 * `includeHidden` is true), briefings, and emails. Deleting removes the finding and its evidence links (204); shared
 * evidence and sources stay, and ratings of it stay in the feedback history.
 */
export const findingsApi = {
  /** Filters: `topicId`, `runId`, `tier`, `minScore`, `category`, `dimensionId`, `includeHidden`, `pinned`. */
  list: (client, query) => client.get(routes.findings, query),
  /** `{ finding, evidence: [{ evidence, sourceUrl, sourceTitle, sourceDomain, sourcePublishedUtc, role }], dimension, topicName }` */
  get: (client, id) => client.get(routes.finding(id)),
  /** FindingUpdateRequest `{ pinned?, hidden? }` (at least one); returns the updated Finding. */
  update: (client, id, body) => client.put(routes.finding(id), body),
  pin: (client, id, pinned) => client.put(routes.finding(id), { pinned: Boolean(pinned) }),
  hide: (client, id, hidden) => client.put(routes.finding(id), { hidden: Boolean(hidden) }),
  remove: (client, id) => client.delete(routes.finding(id)),
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
  /** `{ briefing, sections, items, files, filesExpired, editionName, topicName, itemTitles, audio }`; audio is the newest episode in any status. */
  get: (client, id) => client.get(routes.briefing(id)),
  /** Absolute download URL for the `md` or `html` file (send the bearer token with it). */
  downloadUrl: (client, id, format) => client.url(routes.briefingDownload(id), { format }),
  pin: (client, id, pinned) => client.put(routes.briefingPin(id), { pinned: Boolean(pinned) }),
  /** Store fresh Markdown and HTML files after the old ones passed retention. */
  /** Queue a failed briefing report for support. Idempotent per briefing and signed-in user. */
  support: (client, id) => client.post(routes.briefingSupport(id), {}),
  regenerate: (client, id) => client.post(routes.briefingRegenerate(id), {}),
  /** `{ positionSeconds, completed }` */
  saveProgress: (client, id, progress) => client.put(routes.briefingProgress(id), progress),
  /** A page of the briefing's public shares: `{ id, expiresUtc, revokedUtc, viewCount, lastViewedUtc, tokenPrefix, showOwnerName }`. */
  shares: (client, id, query) => client.get(routes.briefingShares(id), query),
  /**
   * Create a public share: `{ share, secret, readUrl, listenUrl, audioReady }`, shown once. `expiresInDays` is 1 to 14
   * (the server maximum); the server answers 400 for anything else and 409 SharingDisabled when sharing is off.
   */
  share: (client, id, { expiresInDays, showOwnerName } = {}) => client.post(routes.briefingShares(id), { expiresInDays, showOwnerName: Boolean(showOwnerName) }),
  /** Revoke a live share; with `purge`, delete one that is already revoked or expired. */
  revokeShare: (client, id, shareId, { purge = false } = {}) => client.delete(routes.briefingShare(id, shareId), { query: purge ? { purge: true } : undefined }),
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

/** The channel the app reports on every rating (FeedbackChannelEnum `Mobile`, plan section 20). */
export const FEEDBACK_CHANNEL = 'Mobile';

/**
 * Feedback (Phases 9 and 10), the same API as the email feedback pages. `submit()` takes `{ targetType, targetId,
 * action, reason }` with an action from src/lib/feedback.js (relevant, somewhat, not-relevant, accurate, partially,
 * inaccurate, too-early, useful, not-useful, done, snooze) and returns the saved Feedback; a newer rating on the same
 * target supersedes the older one. `update()` edits the reason only; `retract()` deletes the rating (the earlier one,
 * if any, becomes current again).
 */
export const feedbackApi = {
  submit: (client, { targetType, targetId, action, reason }) => client.post(routes.feedback, { targetType, targetId, action: action || undefined, reason: reason || undefined, channel: FEEDBACK_CHANNEL }),
  /** Filters: `targetType`, `targetId`, `topicId`, `channel`, `includeHistory`. Current ratings only by default. */
  list: (client, query) => client.get(routes.feedback, query),
  /** The current rating on one item, or null. */
  current: async (client, targetType, targetId) => {
    const result = await client.get(routes.feedback, { targetType, targetId, pageNumber: 1, pageSize: 5 });
    const items = (result && result.objects) || [];
    return items.find((f) => f.isCurrent !== false) || null;
  },
  update: (client, id, reason) => client.put(routes.feedbackItem(id), { reason: reason || '' }),
  retract: (client, id) => client.delete(routes.feedbackItem(id)),
};

/**
 * Learned preferences (Phase 10, plan section 18). Statuses: Proposed, Active, Rejected, Disabled. Kinds: Prefer,
 * Avoid, PreferSource, AvoidSource, StyleNote. Proposed ones can be accepted or rejected, Active ones disabled, and
 * Disabled ones enabled. Your own (`create`) are Active at once.
 */
export const learningApi = {
  /** Filters: `topicId`, `status`, `origin` (Distilled, User). */
  list: (client, query) => client.get(routes.learnedPreferences, query),
  /** `{ preference, support }` (support: the feedback it was learned from). */
  get: (client, id) => client.get(routes.learnedPreference(id)),
  /** `{ statement, kind?, topicId? }` */
  create: (client, body) => client.post(routes.learnedPreferences, body),
  update: (client, id, body) => client.put(routes.learnedPreference(id), body),
  remove: (client, id) => client.delete(routes.learnedPreference(id)),
  /** `accept`, `reject`, `disable`, or `enable`; returns the updated preference. */
  act: (client, id, action) => client.post(routes.learnedPreferenceAction(id, action), {}),
};

/** The analytics reports (Phase 10). */
export const ANALYTICS_REPORTS = ['overview', 'relevance', 'accuracy', 'feedback', 'suggestions', 'reasons', 'quality'];

/** Insights (Phase 10). Each report takes `{ topicId?, from?, to? }` and defaults to my data for the last 90 days. */
export const insightsApi = {
  analytics: (client, name, query) => client.get(routes.analytics(name), query),
  /**
   * Headline numbers (OverviewReport): runs (with completed, partially succeeded, and failed), findings shown and
   * rated, relevance precision, briefings, emails, predictions open and resolved with accuracy and mean Brier,
   * feedback (with a reason), suggestions, quote failure rate, and model calls and cost. Rates are 0 to 1 or null.
   */
  overview: (client, query) => client.get(routes.analytics('overview'), query),
  learnedPreferences: (client, query) => client.get(routes.learnedPreferences, query),
};

/**
 * Audio (Phase 8, plan section 22). `get()` is `{ media, chapters, audioPath, chaptersPath }`: the episode, its
 * chapters (`{ title, startSeconds, endSeconds, briefingSectionId? }`), and the artifact paths that stream with the
 * bearer token and HTTP Range. `make()` queues new audio for a ready briefing (202 with the episode); its 409 codes are
 * AudioInProgress, AudioDisabled, and BriefingNotReady.
 */
export const mediaApi = {
  /** Filters: `briefingId`, `editionId`, `status` (Queued, Scripting, Synthesizing, Ready, Failed), `kind`. */
  list: (client, query) => client.get(routes.mediaList, query),
  get: (client, id) => client.get(routes.media(id)),
  make: (client, briefingId, voiceProfileId) => client.post(routes.briefingAudio(briefingId), voiceProfileId ? { voiceProfileId } : {}),
};
