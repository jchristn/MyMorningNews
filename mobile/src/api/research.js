import { routes } from './routes';

// Research and briefing routes from plan section 24. None exist on the server yet; each returns 404 until its phase
// lands, and the screens show "This arrives in a later server update". List calls take the shared list contract
// (pageNumber, pageSize, sortBy, sortDirection, search, fromUtc, toUtc, plus typed filters) and return
// `{ items, pageNumber, pageSize, totalCount, totalPages }`. Response fields beyond that are read defensively.

/** Topics (Phase 4). */
export const topicsApi = {
  list: (client, query) => client.get(routes.topics, query),
  get: (client, id) => client.get(routes.topic(id)),
  run: (client, id) => client.post(routes.topicRun(id), {}),
  pause: (client, id) => client.post(routes.topicPause(id), {}),
  resume: (client, id) => client.post(routes.topicResume(id), {}),
};

/** Refinement wizard sessions (Phase 4): start with `{ seedText, goal?, depth?, horizon? }` (RefinementStartRequest). */
export const refinementApi = {
  start: (client, body) => client.post(routes.refinementSessions, body),
  get: (client, id) => client.get(routes.refinementSession(id)),
};

/** Briefings (Phase 7) and listening progress (Phase 16). */
export const briefingsApi = {
  list: (client, query) => client.get(routes.briefings, query),
  get: (client, id) => client.get(routes.briefing(id)),
  markdown: (client, id) => client.get(routes.briefingMarkdown(id)),
  /** `{ positionSeconds, completed }` */
  saveProgress: (client, id, progress) => client.put(routes.briefingProgress(id), progress),
};

/** Editions (Phase 7). */
export const editionsApi = {
  list: (client, query) => client.get(routes.editions, query),
  get: (client, id) => client.get(routes.edition(id)),
  compose: (client, id) => client.post(routes.editionCompose(id), {}),
};

/** Findings (Phase 6). */
export const findingsApi = {
  list: (client, query) => client.get(routes.findings, query),
  get: (client, id) => client.get(routes.finding(id)),
};

/** Predictions (Phase 6). */
export const predictionsApi = {
  list: (client, query) => client.get(routes.predictions, query),
  get: (client, id) => client.get(routes.prediction(id)),
  resolve: (client, id, body) => client.post(routes.predictionResolve(id), body),
};

/** Suggestions (Phase 6). */
export const suggestionsApi = {
  list: (client, query) => client.get(routes.suggestions, query),
  setStatus: (client, id, body) => client.put(routes.suggestionStatus(id), body),
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
