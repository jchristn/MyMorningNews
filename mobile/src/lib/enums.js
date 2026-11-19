// Stable server enum codes, the same lists as dashboard/src/utils/enums.js. Labels come from the catalog under
// mobile.enums.<name>.<code>; tones map onto the Badge tones (neutral, primary, success, warning, danger).

export const TOPIC_STATUSES = ['Draft', 'Active', 'Paused', 'Archived'];
export const TOPIC_STATUS_TONES = { Draft: 'primary', Active: 'success', Paused: 'warning', Archived: 'neutral' };

export const RUN_STATUS_TONES = { Queued: 'primary', Running: 'primary', Completed: 'success', PartiallySucceeded: 'warning', Failed: 'danger', Cancelled: 'neutral', Skipped: 'warning' };
export const ACTIVE_RUN_STATUSES = ['Queued', 'Running'];

export const STAGE_STATUS_TONES = { Pending: 'neutral', Running: 'primary', Succeeded: 'success', Failed: 'danger', Skipped: 'warning' };

export const REQUEST_STATUSES = ['Clarifying', 'Scheduled', 'Running', 'Completed', 'Failed', 'Cancelled', 'Declined'];
export const REQUEST_STATUS_TONES = { Clarifying: 'primary', Scheduled: 'primary', Running: 'primary', Completed: 'success', Failed: 'danger', Cancelled: 'neutral', Declined: 'warning' };
export const DELIVERABLES = ['Report', 'Podcast', 'Email'];
export const DELIVERABLE_LENGTHS = ['Short', 'Standard', 'Long'];

export const DIMENSION_WEIGHTS = ['High', 'Normal', 'Low', 'Exclude'];
export const DIMENSION_WEIGHT_TONES = { High: 'primary', Normal: 'neutral', Low: 'neutral', Exclude: 'danger' };
/** Constraint kinds the compact wizard edits (feeds and seed pages stay on the web Sources step). */
export const CONTEXT_CONSTRAINT_KINDS = ['IncludeKeyword', 'ExcludeKeyword', 'IncludeEntity', 'ExcludeEntity', 'PreferredDomain', 'BlockedDomain', 'TrackedQuestion'];
export const AUDIENCE_LEVELS = ['Beginner', 'Practitioner', 'Expert'];
export const HORIZONS = ['Days', 'Weeks', 'Months', 'Years'];

export const FINDING_TIERS = ['MustRead', 'WorthALook', 'Fyi', 'BelowThreshold'];
export const FINDING_TIER_TONES = { MustRead: 'primary', WorthALook: 'success', Fyi: 'neutral', BelowThreshold: 'neutral' };
export const EVIDENCE_ROLE_TONES = { Supports: 'success', Contradicts: 'danger', Context: 'neutral' };
/** Relevance components in display order (plan section 17), with the Finding field that holds each. */
export const SCORE_COMPONENTS = [
  { key: 'llm', field: 'llmScore' },
  { key: 'feedback', field: 'feedbackScore' },
  { key: 'novelty', field: 'noveltyScore' },
  { key: 'source', field: 'sourceScore' },
  { key: 'recency', field: 'recencyScore' },
];

export const PREDICTION_STATUSES = ['Open', 'ProposedResolved', 'Accurate', 'PartiallyAccurate', 'Inaccurate', 'Unresolvable', 'Withdrawn'];
export const PREDICTION_STATUS_TONES = { Open: 'primary', ProposedResolved: 'warning', Accurate: 'success', PartiallyAccurate: 'warning', Inaccurate: 'danger', Unresolvable: 'neutral', Withdrawn: 'neutral' };
/** Statuses a person can resolve a prediction to. Open and ProposedResolved are set by the pipeline. */
export const PREDICTION_RESOLUTIONS = ['Accurate', 'PartiallyAccurate', 'Inaccurate', 'Unresolvable', 'Withdrawn'];

export const SUGGESTION_STATUSES = ['New', 'Accepted', 'Snoozed', 'Dismissed', 'Done'];
export const SUGGESTION_STATUS_TONES = { New: 'primary', Accepted: 'primary', Snoozed: 'warning', Dismissed: 'neutral', Done: 'success' };
export const SUGGESTION_PRIORITY_TONES = { High: 'danger', Medium: 'warning', Low: 'neutral' };

export const BRIEFING_STATUS_TONES = { Queued: 'primary', Composing: 'primary', Ready: 'success', Failed: 'danger' };
export const PENDING_BRIEFING_STATUSES = ['Queued', 'Composing'];
export const LOOKBACK_MODES = ['SinceLastEdition', 'FixedHours'];

/** True when a prediction's horizon has passed and it still waits for a grade. */
export function isPredictionDue(prediction, now = Date.now()) {
  if (!prediction || (prediction.status !== 'Open' && prediction.status !== 'ProposedResolved')) return false;
  const horizon = Date.parse(prediction.horizonUtc);
  return Number.isFinite(horizon) && horizon <= now;
}
