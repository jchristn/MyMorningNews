// Briefing comparisons (plan section 6): the reader's "Compared with ..." card, the comparison settings of topics and
// editions with their baseline preview, and the topic keep order of a smaller plan (section 8.4). Pure helpers, so
// the screens and the tests share them.

/** Comparison modes the server accepts (HistoryModeEnum), in the order the settings offer them. */
export const HISTORY_MODES = ['Off', 'PreviousIssue', 'RecentIssues', 'SinceDate', 'SpecificIssue'];

/** Server bounds for the comparison settings. */
export const HISTORY_COUNT = { min: 1, max: 20, fallback: 5 };
export const HISTORY_BUDGET = { min: 256, max: 32000, fallback: 8000 };

/** Change kinds (BriefingDeltaKindEnum) in display order: corrections, changes, new, resolved, unchanged, unavailable. */
export const DELTA_KINDS = ['Correction', 'ChangedFact', 'NewDevelopment', 'Resolved', 'Unchanged', 'Unavailable'];

/** Badge tones per change kind. */
export const DELTA_TONES = { Correction: 'warning', ChangedFact: 'primary', NewDevelopment: 'success', Resolved: 'neutral', Unchanged: 'neutral', Unavailable: 'danger' };

/** The settings form for a topic or edition (strings for the text fields). */
export function historyForm(owner) {
  const o = owner || {};
  return {
    mode: HISTORY_MODES.includes(o.historyMode) ? o.historyMode : 'Off',
    count: String(o.historyCount ?? HISTORY_COUNT.fallback),
    date: typeof o.historySinceUtc === 'string' ? o.historySinceUtc.slice(0, 10) : '',
    issue: o.historyBriefingId || '',
    budget: String(o.historyTokenBudget ?? HISTORY_BUDGET.fallback),
  };
}

/** A whole number from a text field, or null when it is not one (the server then answers 400 with its own bounds). */
function wholeNumber(text) {
  const trimmed = String(text ?? '').trim();
  return /^\d{1,6}$/.test(trimmed) ? Number(trimmed) : null;
}

/**
 * The history fields of a TopicUpsertRequest, EditionUpsertRequest, or HistoryPreviewRequest for the form. Values are
 * sent as typed so the server's validation (count 1 to 20, budget 256 to 32000, a past date, a ready issue) explains
 * anything out of range; a field that is not a number is left out (null keeps the saved value).
 */
export function historyBody(form) {
  const body = { historyMode: form.mode };
  const count = wholeNumber(form.count);
  if (count !== null) body.historyCount = count;
  const budget = wholeNumber(form.budget);
  if (budget !== null) body.historyTokenBudget = budget;
  if (form.mode === 'SinceDate') body.historySinceUtc = /^\d{4}-\d{2}-\d{2}$/.test(form.date) ? new Date(form.date + 'T00:00:00Z').toISOString() : null;
  if (form.mode === 'SpecificIssue') body.historyBriefingId = form.issue || '';
  return body;
}

/** True when two forms would save the same settings. */
export function sameHistory(a, b) {
  return JSON.stringify(historyBody(a)) === JSON.stringify(historyBody(b));
}

/** What the comparison card shows: `standalone` (nothing compared), `noChanges`, or `changes`. */
export function comparisonState(comparison) {
  if (!comparison || !comparison.compared) return 'standalone';
  return comparison.noMaterialChanges ? 'noChanges' : 'changes';
}

/** The changes in display order (sortOrder, then kind order for servers that send ties). */
export function sortedDeltas(comparison) {
  const deltas = (comparison && Array.isArray(comparison.deltas) ? comparison.deltas : []).filter((d) => d && d.active !== false);
  const rank = (d) => {
    const i = DELTA_KINDS.indexOf(d.kind);
    return i < 0 ? DELTA_KINDS.length : i;
  };
  return deltas.slice().sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || rank(a) - rank(b));
}

/** Counts per change kind that are above zero, in display order: `[{ kind, count }]`. */
export function deltaCounts(comparison) {
  const counts = (comparison && comparison.counts) || {};
  return DELTA_KINDS.map((kind) => ({ kind, count: Number(counts[kind]) || 0 })).filter((c) => c.count > 0);
}

/** The comparison mode without the "Edition:" prefix of edition-level comparisons, and whether it had one. */
export function comparisonMode(comparison) {
  const mode = String((comparison && comparison.mode) || 'Off');
  return mode.startsWith('Edition:') ? { mode: mode.slice('Edition:'.length), edition: true } : { mode, edition: false };
}

/** Move one entry of an ordered list by `delta` places (clamped to the ends); returns a new list. */
export function moveItem(list, index, delta) {
  const next = list.slice();
  const to = Math.max(0, Math.min(next.length - 1, index + delta));
  if (index < 0 || index >= next.length || to === index) return next;
  const [item] = next.splice(index, 1);
  next.splice(to, 0, item);
  return next;
}

/** Whether position `index` (0-based) of the keep order would run under `limit` (null means unlimited). */
export function runsAt(index, limit) {
  return limit === null || limit === undefined ? true : index < limit;
}

/** Whether Account > Plan should offer the keep order: the account has custom topics and the plan limits them. */
export function offersTopicSlots(slots) {
  return Boolean(slots && slots.limit !== null && slots.limit !== undefined && (slots.counted > 0 || (slots.topics || []).length > 0));
}
