// Pin, hide, and delete for findings: which actions a finding offers, the list options (Pinned only, Show hidden) as
// query flags, and how a list applies a change made on a row or in the detail without reloading. Pure apart from
// `performFindingAction`, which takes the client, so the tests cover these directly.
import { findingsApi } from '@/api/research';

/** List options, in display order: only pinned findings, and hidden findings included. */
export const FINDING_LIST_OPTIONS = Object.freeze(['pinned', 'hidden']);

/** Ionicons for each finding action. */
export const FINDING_ACTION_ICONS = Object.freeze({ pin: 'bookmark-outline', unpin: 'bookmark', hide: 'eye-off-outline', unhide: 'eye-outline', delete: 'trash-outline' });

/** The actions a finding offers, in menu order: pin or unpin, hide or unhide, and delete. */
export function findingActions(finding) {
  const f = finding && typeof finding === 'object' ? finding : {};
  return [f.pinned ? 'unpin' : 'pin', f.hidden ? 'unhide' : 'hide', 'delete'];
}

/** The FindingUpdateRequest body for an action (`PUT /v1.0/api/findings/{id}`), or null for delete or an unknown one. */
export function findingUpdate(action) {
  switch (action) {
    case 'pin':
      return { pinned: true };
    case 'unpin':
      return { pinned: false };
    case 'hide':
      return { hidden: true };
    case 'unhide':
      return { hidden: false };
    default:
      return null;
  }
}

/** Query flags for the chosen list options: `pinned=true` for Pinned only, `includeHidden=true` for Show hidden. */
export function findingListFlags(options) {
  const chosen = Array.isArray(options) ? options : [];
  return { pinned: chosen.includes('pinned') ? true : undefined, includeHidden: chosen.includes('hidden') ? true : undefined };
}

/**
 * Apply local changes (`{ [id]: { pinned?, hidden?, deleted? } }`) to the findings a list loaded. Deleted findings
 * drop out, and so do findings that no longer match the options: hidden ones while Show hidden is off, and unpinned
 * ones under Pinned only.
 */
export function applyFindingChanges(items, changes, options) {
  const chosen = Array.isArray(options) ? options : [];
  const map = changes && typeof changes === 'object' ? changes : {};
  return (Array.isArray(items) ? items : [])
    .map((f) => {
      const change = map[f.id];
      if (!change) return f;
      if (change.deleted) return null;
      return { ...f, ...change };
    })
    .filter((f) => f && (chosen.includes('hidden') || !f.hidden) && (!chosen.includes('pinned') || f.pinned));
}

/**
 * Run a finding action against the server and return the change to apply: `{ deleted: true }` after a delete (204),
 * otherwise `{ pinned, hidden }` from the updated Finding (falling back to the requested values when the server
 * returns no body).
 *
 * @throws {Error} for an unknown action, and whatever the API call throws.
 */
export async function performFindingAction(client, finding, action) {
  if (action === 'delete') {
    await findingsApi.remove(client, finding.id);
    return { deleted: true };
  }
  const body = findingUpdate(action);
  if (!body) throw new Error('Unknown finding action: ' + action);
  const updated = await findingsApi.update(client, finding.id, body);
  const pick = (field) => (updated && typeof updated[field] === 'boolean' ? updated[field] : body[field] !== undefined ? body[field] : Boolean(finding[field]));
  return { pinned: pick('pinned'), hidden: pick('hidden') };
}
