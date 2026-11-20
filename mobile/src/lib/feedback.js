// Feedback actions per target (plan sections 20 and 23.2), the same set the email feedback pages offer. The server
// maps each action to a rating (FeedbackService): relevant, somewhat, and not-relevant set relevance; accurate,
// partially, inaccurate, and too-early set accuracy (predictions only); useful and not-useful set usefulness
// (suggestions only). Pure, so the tests cover it directly.

/** The rating actions the app offers on each target type. */
export const FEEDBACK_ACTIONS = Object.freeze({
  Finding: ['relevant', 'somewhat', 'not-relevant'],
  Briefing: ['relevant', 'somewhat', 'not-relevant'],
  BriefingSection: ['relevant', 'somewhat', 'not-relevant'],
  SourceItem: ['relevant', 'somewhat', 'not-relevant'],
  Prediction: ['accurate', 'partially', 'inaccurate', 'too-early'],
  Suggestion: ['useful', 'not-useful'],
});

/** Every action the server accepts (done and snooze also change a suggestion's status). */
export const ALL_FEEDBACK_ACTIONS = Object.freeze(['relevant', 'somewhat', 'not-relevant', 'accurate', 'partially', 'inaccurate', 'too-early', 'useful', 'not-useful', 'done', 'snooze']);

/** Catalog keys for actions (mobile.feedback.actions.<key>): ICU keys cannot contain dashes. */
export function actionKey(action) {
  return String(action || '').replace(/-([a-z])/g, (_, c) => c.toUpperCase());
}

export const ACTION_ICONS = Object.freeze({
  relevant: 'thumbs-up-outline',
  somewhat: 'remove-circle-outline',
  'not-relevant': 'thumbs-down-outline',
  accurate: 'checkmark-circle-outline',
  partially: 'contrast-outline',
  inaccurate: 'close-circle-outline',
  'too-early': 'time-outline',
  useful: 'thumbs-up-outline',
  'not-useful': 'thumbs-down-outline',
  done: 'checkmark-done-outline',
  snooze: 'alarm-outline',
});

const RELEVANCE = { Relevant: 'relevant', SomewhatRelevant: 'somewhat', NotRelevant: 'not-relevant' };
const ACCURACY = { Accurate: 'accurate', PartiallyAccurate: 'partially', Inaccurate: 'inaccurate', TooEarly: 'too-early' };
const USEFULNESS = { Useful: 'useful', NotUseful: 'not-useful' };

/** The action a saved Feedback stands for (from its relevance, accuracy, or usefulness), or null for a reason only. */
export function actionOf(feedback) {
  if (!feedback || typeof feedback !== 'object') return null;
  return RELEVANCE[feedback.relevance] || ACCURACY[feedback.accuracy] || USEFULNESS[feedback.usefulness] || null;
}

/** Actions offered for a target type (findings' set for unknown types). */
export function actionsFor(targetType) {
  return FEEDBACK_ACTIONS[targetType] || FEEDBACK_ACTIONS.Finding;
}

/** Map of targetId to the current Feedback, from a feedback list response. */
export function feedbackByTarget(items) {
  const map = {};
  for (const f of Array.isArray(items) ? items : []) {
    if (!f || !f.targetId || f.isCurrent === false) continue;
    const seen = map[f.targetId];
    if (!seen || String(f.createdUtc || '') > String(seen.createdUtc || '')) map[f.targetId] = f;
  }
  return map;
}

/** Where the target of a feedback item opens in the app, or null. */
export function feedbackTargetHref(feedback) {
  if (!feedback || !feedback.targetId) return null;
  const id = encodeURIComponent(feedback.targetId);
  switch (feedback.targetType) {
    case 'Finding':
      return '/today/findings/' + id;
    case 'Prediction':
      return '/today/predictions/' + id;
    case 'Briefing':
      return '/today/briefings/' + id;
    case 'Suggestion':
      return '/today/suggestions';
    default:
      return null;
  }
}

/** Target types the feedback history filters by. */
export const FEEDBACK_TARGET_FILTERS = Object.freeze(['all', 'Finding', 'Prediction', 'Suggestion', 'Briefing']);
