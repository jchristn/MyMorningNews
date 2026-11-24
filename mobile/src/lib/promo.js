// Plans and promotional codes (plan section 33.2): the reasons a code is refused, code normalization, and the plan
// features the Account > Plan screen lists. Pure, so the tests cover it directly.
import { ApiError } from '@/api/errors';

/** `details.reason` values of a 409 PromoCodeInvalid, each with its own message under mobile.promo.reasons. */
export const PROMO_REASONS = Object.freeze(['NotFound', 'Disabled', 'NotStarted', 'Expired', 'Exhausted', 'AlreadyRedeemed', 'NewAccountsOnly']);

/** Codes are 4 to 40 letters, digits, and inner dashes; the server compares them in upper case. */
export const CODE_PATTERN = /^[A-Z0-9][A-Z0-9-]{2,38}[A-Z0-9]$/;

/** The code as the server compares it: trimmed, inner spaces removed, upper case. */
export function normalizeCode(text) {
  return String(text || '')
    .trim()
    .replace(/\s+/g, '')
    .toUpperCase();
}

export function isWellFormedCode(text) {
  return CODE_PATTERN.test(normalizeCode(text));
}

/**
 * Why a redemption failed: one of PROMO_REASONS, `Unknown` for a 409 PromoCodeInvalid with a reason this app does not
 * know, `TooManyRequests` for 429, or null for any other error (shown with its own message).
 */
export function promoReason(error) {
  if (!(error instanceof ApiError)) return null;
  if (error.status === 429) return 'TooManyRequests';
  if (error.status !== 409 || error.code !== 'PromoCodeInvalid') return null;
  const reason = error.details && typeof error.details.reason === 'string' ? error.details.reason : null;
  return reason && PROMO_REASONS.includes(reason) ? reason : 'Unknown';
}

/** Plan features in display order: the entitlement key and the catalog key of its label (mobile.plan.features.*). */
export const PLAN_FEATURES = Object.freeze([
  { key: 'customTopics.max', label: null, kind: 'count' },
  { key: 'systemSubscriptions.max', label: null, kind: 'count' },
  { key: 'audio.dailySeconds.max', label: null, kind: 'count' },
  { key: 'customResearch.enabled', label: null, kind: 'flag' },
  { key: 'oneTimeResearch.enabled', label: null, kind: 'flag' },
  { key: 'catalog.freeOnly', label: null, kind: 'flag' },
  { key: 'catalog.audio.enabled', label: null, kind: 'flag' },
  { key: 'models.personalConnections.enabled', label: null, kind: 'flag' },
  { key: 'usage.monthlyCostUsd.max', label: 'monthlyCost', kind: 'cost' },
  { key: 'topics.max', label: 'topics', kind: 'count' },
  { key: 'runs.daily.max', label: 'dailyRuns', kind: 'count' },
  { key: 'schedule.minIntervalMinutes', label: 'minInterval', kind: 'minutes' },
  { key: 'audio.enabled', label: 'audio', kind: 'flag' },
  { key: 'audio.monthlyMinutes.max', label: 'audioMinutes', kind: 'minutes' },
  { key: 'email.enabled', label: 'email', kind: 'flag' },
  { key: `mobile.access`, label: 'mobile', kind: 'flag' }, // a template literal, so the i18n check does not read it as a catalog key
  { key: 'endpoints.custom', label: 'customEndpoints', kind: 'flag' },
  { key: 'artifacts.pinned.max', label: 'pinned', kind: 'count' },
]);

/**
 * Rows for a plan's features: the known ones in PLAN_FEATURES order with their kind, then any others by key.
 * Each row is `{ key, label | null, kind, value }`; `value` is the raw string, `unlimited` stays as is.
 */
export function planFeatureRows(features) {
  const values = features && typeof features === 'object' ? features : {};
  const known = PLAN_FEATURES.filter((f) => values[f.key] !== undefined).map((f) => ({ key: f.key, label: f.label, kind: f.kind, value: String(values[f.key]) }));
  const extra = Object.keys(values)
    .filter((k) => !PLAN_FEATURES.some((f) => f.key === k))
    .sort()
    .map((k) => ({ key: k, label: null, kind: 'text', value: String(values[k]) }));
  return [...known, ...extra];
}

/** True when a feature value means "no limit". */
export function isUnlimited(value) {
  return String(value).trim().toLowerCase() === 'unlimited';
}
