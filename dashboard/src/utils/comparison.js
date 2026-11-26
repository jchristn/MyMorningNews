// Briefing comparison ("compare with past briefings", plan section 6): the bounds the server enforces, the request
// body for a baseline preview, and helpers for the comparison provenance a briefing or catalog issue carries.

export const HISTORY_BOUNDS = { count: { min: 1, max: 20 }, budget: { min: 256, max: 32000 } };

export const DEFAULT_HISTORY_BUDGET = 8000;

// Delta kinds in the server's display order (corrections, changes, new, resolved, unchanged, unavailable).
export const DELTA_KINDS = ['Correction', 'ChangedFact', 'NewDevelopment', 'Resolved', 'Unchanged', 'Unavailable'];

// Kinds that count as a material change; the others are context.
export const MATERIAL_KINDS = ['Correction', 'ChangedFact', 'NewDevelopment', 'Resolved'];

// The comparison budget within the server's bounds; anything unreadable falls back to the default.
export function clampBudget(value) {
  const n = Math.round(Number(value));
  if (value === '' || value === null || value === undefined || !Number.isFinite(n)) return DEFAULT_HISTORY_BUDGET;
  return Math.min(HISTORY_BOUNDS.budget.max, Math.max(HISTORY_BOUNDS.budget.min, n));
}

// The comparison count within the server's bounds.
export function clampCount(value) {
  const n = Math.round(Number(value));
  if (value === '' || value === null || value === undefined || !Number.isFinite(n)) return 5;
  return Math.min(HISTORY_BOUNDS.count.max, Math.max(HISTORY_BOUNDS.count.min, n));
}

// The form values a history editor works with, from a saved topic or edition.
export function historyForm(owner) {
  const o = owner || {};
  return {
    historyMode: o.historyMode || 'Off',
    historyCount: o.historyCount ?? 5,
    historySinceUtc: o.historySinceUtc ? String(o.historySinceUtc).slice(0, 10) : '',
    historyBriefingId: o.historyBriefingId || '',
    historyTokenBudget: o.historyTokenBudget ?? DEFAULT_HISTORY_BUDGET
  };
}

// A date input value (yyyy-mm-dd) as the UTC midnight ISO timestamp the server expects, or null.
export function sinceIso(value) {
  if (!value) return null;
  const date = new Date(value + 'T00:00:00Z');
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

// Why the current form cannot be previewed yet ('date' or 'issue'), or null when it can.
export function previewBlocker(form) {
  if (!form) return null;
  if (form.historyMode === 'SinceDate' && !sinceIso(form.historySinceUtc)) return 'date';
  if (form.historyMode === 'SpecificIssue' && !form.historyBriefingId) return 'issue';
  return null;
}

// The HistoryPreviewRequest for unsaved form values. Values that do not apply to the mode are sent as null (keep),
// and a missing pinned issue is sent as an empty string (clear), so the preview reflects exactly what was typed.
export function historyPreviewBody(form) {
  const count = Number(form.historyCount);
  const budget = form.historyTokenBudget === undefined || form.historyTokenBudget === '' ? null : Number(form.historyTokenBudget);
  return {
    historyMode: form.historyMode,
    historyCount: Number.isFinite(count) ? count : null,
    historySinceUtc: form.historyMode === 'SinceDate' ? sinceIso(form.historySinceUtc) : null,
    historyBriefingId: form.historyMode === 'SpecificIssue' ? form.historyBriefingId || '' : null,
    historyTokenBudget: budget !== null && Number.isFinite(budget) ? budget : null
  };
}

// The comparison mode without the "Edition:" prefix, and whether it was an edition-level comparison.
export function parseMode(mode) {
  const value = String(mode || 'Off');
  if (value.startsWith('Edition:')) return { mode: value.slice('Edition:'.length) || 'Off', edition: true };
  return { mode: value, edition: false };
}

// Deltas grouped by kind in display order: [{ kind, deltas }], skipping empty kinds. Unknown kinds go last.
export function groupDeltas(deltas) {
  const sorted = (Array.isArray(deltas) ? deltas : []).slice().sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
  const groups = new Map();
  for (const delta of sorted) {
    if (!groups.has(delta.kind)) groups.set(delta.kind, []);
    groups.get(delta.kind).push(delta);
  }
  const known = DELTA_KINDS.filter((k) => groups.has(k)).map((kind) => ({ kind, deltas: groups.get(kind) }));
  const other = [...groups.keys()].filter((k) => !DELTA_KINDS.includes(k)).map((kind) => ({ kind, deltas: groups.get(kind) }));
  return known.concat(other);
}

// True when a response looks like a BriefingComparisonResponse (a fake or older server may answer otherwise).
export function isComparison(value) {
  return Boolean(value) && typeof value === 'object' && typeof value.compared === 'boolean';
}

// The dashboard link for a delta's current evidence, or null.
export function deltaHref(delta) {
  if (!delta) return null;
  if (delta.findingId) return '/dashboard/findings/' + encodeURIComponent(delta.findingId);
  if (delta.predictionId) return '/dashboard/predictions?id=' + encodeURIComponent(delta.predictionId);
  return null;
}
