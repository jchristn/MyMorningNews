// Feedback actions and ratings (plan sections 18 and 20). The server maps an action to one of three rating
// fields; these helpers go the other way so a stored rating shows as the action that produced it.

// The actions offered per target type, in display order. Predictions take an accuracy grade; suggestions take
// usefulness; findings and everything else take relevance.
export const FEEDBACK_ACTIONS = {
  Finding: ['relevant', 'somewhat', 'not-relevant'],
  Prediction: ['accurate', 'partially', 'inaccurate', 'too-early'],
  Suggestion: ['useful', 'not-useful']
};

export const ACTION_TONES = {
  relevant: 'success',
  somewhat: 'info',
  'not-relevant': 'danger',
  accurate: 'success',
  partially: 'info',
  inaccurate: 'danger',
  'too-early': 'neutral',
  useful: 'success',
  'not-useful': 'danger'
};

const RELEVANCE = { Relevant: 'relevant', SomewhatRelevant: 'somewhat', NotRelevant: 'not-relevant' };
const ACCURACY = { Accurate: 'accurate', PartiallyAccurate: 'partially', Inaccurate: 'inaccurate', TooEarly: 'too-early' };
const USEFULNESS = { Useful: 'useful', NotUseful: 'not-useful' };

export function actionsFor(targetType) {
  return FEEDBACK_ACTIONS[targetType] || FEEDBACK_ACTIONS.Finding;
}

// The action a stored feedback row stands for, or null (a preference decision or a snooze).
export function actionOf(feedback) {
  if (!feedback) return null;
  if (feedback.relevance && RELEVANCE[feedback.relevance]) return RELEVANCE[feedback.relevance];
  if (feedback.accuracy && ACCURACY[feedback.accuracy]) return ACCURACY[feedback.accuracy];
  if (feedback.usefulness && USEFULNESS[feedback.usefulness]) return USEFULNESS[feedback.usefulness];
  return null;
}

// Current feedback rows keyed by target id. When a target has several (the server keeps one current row per
// user and target), the newest wins.
export function feedbackByTarget(items) {
  const map = new Map();
  for (const f of items || []) {
    const prev = map.get(f.targetId);
    if (!prev || String(f.createdUtc) > String(prev.createdUtc)) map.set(f.targetId, f);
  }
  return map;
}

// Where the dashboard shows a feedback target, or null when it has no page.
export function targetPath(targetType, targetId) {
  const id = encodeURIComponent(targetId || '');
  switch (targetType) {
    case 'Finding':
      return '/dashboard/findings/' + id;
    case 'Prediction':
      return '/dashboard/predictions?id=' + id;
    case 'Suggestion':
      return '/dashboard/suggestions?id=' + id;
    case 'Briefing':
      return '/dashboard/briefings/' + id;
    case 'LearnedPreference':
      return '/dashboard/insights/preferences?id=' + id;
    default:
      return null;
  }
}
