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
} from './errors';
export { routes } from './routes';
export { authApi } from './auth';
export { accountApi, serverApi, devicesApi } from './account';
export { topicsApi, refinementApi, briefingsApi, editionsApi, findingsApi, predictionsApi, suggestionsApi, requestsApi, feedbackApi, insightsApi } from './research';
