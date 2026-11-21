// The app's API module. Kept in the app until the dashboard's host-agnostic client moves to dashboard/src/shared
// (sharing.config.js); the shapes follow dashboard/src/utils/api.js so the move is mechanical.
export { ApiClient, buildQuery, listItems, DEFAULT_TIMEOUT_MS } from './client';
export {
  ApiError,
  NetworkError,
  TimeoutError,
  SessionExpiredError,
  TenantSelectionRequiredError,
  isNotAvailable,
  isItemNotFound,
  isNetworkError,
  isSessionExpired,
  fieldErrors,
  isModelError,
  isConflict,
  isSetupRequired,
} from './errors';
export { routes } from './routes';
export { authApi } from './auth';
export { accountApi, serverApi, setupApi, devicesApi, planApi, emailPreferencesApi, feedTokensApi } from './account';
export {
  topicsApi,
  refinementApi,
  requestsApi,
  schedulesApi,
  runsApi,
  findingsApi,
  sourcesApi,
  predictionsApi,
  suggestionsApi,
  briefingsApi,
  editionsApi,
  feedbackApi,
  learningApi,
  insightsApi,
  mediaApi,
  ANALYTICS_REPORTS,
  FEEDBACK_CHANNEL,
} from './research';
