import { ApiError } from './api.js';

// Normalize any error into { title, message, requestId, status, details } with translated text.
export function describeError(error, t) {
  if (!error) return { title: '', message: '' };
  if (error instanceof ApiError) {
    const status = error.status;
    let title = t('errors.generic');
    if (status === 0) title = t('errors.network');
    else if (status === 400) title = t('errors.badRequest');
    else if (status === 401) title = t('errors.unauthorized');
    else if (status === 403) title = t('errors.forbidden');
    else if (status === 404) title = t('errors.notFound');
    else if (status === 402) title = t('errors.capReached');
    else if (status === 409) title = t('errors.conflict');
    else if (status === 429) title = t('errors.tooMany');
    else if (status >= 500) title = t('errors.server');
    const reason = error.body && error.body.reason ? error.body.reason : null;
    if (status === 400 && error.code === 'EgressBlocked') title = t('errors.egressBlocked');
    if (isSetupRequiredError(error)) title = t('errors.setupRequired');
    return {
      title,
      message: reason || error.message || t('errors.generic'),
      requestId: error.requestId,
      status,
      details: error.details
    };
  }
  return { title: t('errors.generic'), message: error.message || String(error), requestId: null, status: null, details: null };
}

// Per-field validation errors from a 400 response, keyed by camelCase field path.
export function fieldErrors(error) {
  if (error instanceof ApiError && error.details) return error.details;
  return {};
}

// True for the 409 SetupRequired refusal: the tenant's model key or search is missing or failed verification.
export function isSetupRequiredError(error) {
  return error instanceof ApiError && error.status === 409 && error.code === 'SetupRequired';
}
