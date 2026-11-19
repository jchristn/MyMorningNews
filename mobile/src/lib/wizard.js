// Pure step logic for the compact refinement wizard (plan section 13; the same rules as the dashboard's
// views/topics/wizard/wizardState.js). The mobile flow is Describe, Dimensions, Questions, Context, and Review; feeds
// and seed pages (Sources) and the delivery schedule live in the topic workspace after the topic is created.

export const WIZARD_STEPS = ['Describe', 'Dimensions', 'Questions', 'Context', 'Review'];

/** Question rounds the app offers (the plan's MaxRounds default). */
export const MAX_ROUNDS = 3;

const dims = (data) => (data && Array.isArray(data.dimensions) ? data.dimensions : []);

export function hasBrief(data) {
  return Boolean(data && data.topic && data.topic.brief && String(data.topic.brief).trim());
}

export function isCompleted(data) {
  return Boolean(data && data.session && data.session.status === 'Completed');
}

/** The model declined the topic as out of scope and proposed nothing. */
export function isOutOfScope(data) {
  return Boolean(data && data.session && data.session.outOfScopeReason && dims(data).length === 0);
}

/** Where a session opens: Dimensions for a fresh one, otherwise the furthest step its data supports. */
export function initialStep(data) {
  if (!data || !data.session) return 'Describe';
  if (isCompleted(data)) return 'Review';
  const saved = data.session.step;
  if (dims(data).length === 0) return 'Dimensions';
  if (saved === 'Questions') return 'Questions';
  if (saved === 'Context') return 'Context';
  if (saved === 'Sources' || saved === 'Delivery' || saved === 'Review') return hasBrief(data) ? 'Review' : 'Context';
  return 'Dimensions';
}

/** Whether a step can be opened: Questions and Context need a dimension, Review needs a brief too. */
export function stepAvailable(step, data) {
  if (step === 'Describe') return !data;
  if (!data || !data.session) return false;
  if (isCompleted(data)) return step === 'Review';
  if (step === 'Dimensions') return true;
  if (step === 'Questions' || step === 'Context') return dims(data).length > 0;
  if (step === 'Review') return dims(data).length > 0 && hasBrief(data);
  return false;
}

export function nextStep(step) {
  const i = WIZARD_STEPS.indexOf(step);
  return i >= 0 && i < WIZARD_STEPS.length - 1 ? WIZARD_STEPS[i + 1] : step;
}

export function previousStep(step) {
  const i = WIZARD_STEPS.indexOf(step);
  return i > 1 ? WIZARD_STEPS[i - 1] : step;
}

/** Questions grouped by round, oldest first, each in display order. */
export function questionRounds(questions) {
  const byRound = new Map();
  for (const item of questions || []) {
    if (!item || !item.question) continue;
    const round = item.question.round || 1;
    if (!byRound.has(round)) byRound.set(round, []);
    byRound.get(round).push(item);
  }
  return [...byRound.entries()].sort((a, b) => a[0] - b[0]).map(([round, items]) => ({ round, items: items.sort((a, b) => (a.question.sortOrder || 0) - (b.question.sortOrder || 0)) }));
}

export function answeredCount(questions) {
  return (questions || []).filter((q) => q && q.question && (q.question.answered || q.question.skipped)).length;
}

export function canAskMore(data) {
  return Boolean(data && data.session && (data.session.round || 0) < MAX_ROUNDS);
}

/** Replace one item of a list by id (or append it). */
export function upsertById(list, item, key = (x) => x.id) {
  const items = Array.isArray(list) ? list : [];
  return items.some((x) => key(x) === key(item)) ? items.map((x) => (key(x) === key(item) ? item : x)) : [...items, item];
}

/** Scale questions are answered 1 to 5 (the option labels, when present, name the two ends). */
export const SCALE = [1, 2, 3, 4, 5];

/**
 * The QuestionAnswerRequest for an answer: chosen option IDs for single and multi choice (with optional text), the
 * text itself for scale ("1" to "5"), yes or no ("yes", "no"), and free text, or a skip.
 */
export function answerBody(kind, { text, optionIds, skipped } = {}) {
  if (skipped) return { skipped: true };
  const trimmed = String(text || '').trim();
  if (kind === 'SingleChoice' || kind === 'MultiChoice') {
    const ids = Array.isArray(optionIds) ? optionIds : [];
    return { selectedOptionIds: kind === 'SingleChoice' ? ids.slice(0, 1) : ids, answerText: trimmed || null, skipped: false };
  }
  return { answerText: trimmed, skipped: false };
}
