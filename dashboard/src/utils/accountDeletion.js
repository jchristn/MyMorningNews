// The account deletion this browser asked for, so the next sign-in can say it was cancelled. The server cancels a
// pending deletion on every successful sign-in. When its sign-in response says so (deletionCancelled), that answer
// wins; otherwise the deletion this browser remembers for the same email, still inside its grace period, counts.

export const PENDING_DELETION_KEY = 'mymorningnews.pendingDeletion';

function readPending() {
  try {
    const raw = window.localStorage.getItem(PENDING_DELETION_KEY);
    const value = raw ? JSON.parse(raw) : null;
    return value && typeof value === 'object' && typeof value.email === 'string' ? value : null;
  } catch {
    return null;
  }
}

function forgetPending() {
  try {
    window.localStorage.removeItem(PENDING_DELETION_KEY);
  } catch {
    // Storage may be unavailable; nothing was remembered then.
  }
}

// Remember a deletion just requested: the account's email and when its data goes (ISO, or null when unknown).
export function rememberPendingDeletion(email, purgeAfterUtc) {
  if (!email) return;
  try {
    window.localStorage.setItem(PENDING_DELETION_KEY, JSON.stringify({ email: String(email).trim().toLowerCase(), purgeAfterUtc: purgeAfterUtc || null }));
  } catch {
    // Storage may be unavailable; the sign-in after this one then cannot say the deletion was cancelled.
  }
}

// Whether the sign-in that just succeeded cancelled a pending deletion. issued is the server's sign-in response.
// Clears what this browser remembered for that email either way.
export function deletionCancelledBySignIn(email, issued, now = new Date()) {
  const address = String(email || '')
    .trim()
    .toLowerCase();
  const pending = readPending();
  const mine = Boolean(pending && address && pending.email === address);
  if (mine) forgetPending();
  if (issued && typeof issued.deletionCancelled === 'boolean') return issued.deletionCancelled;
  if (!mine) return false;
  const purge = pending.purgeAfterUtc ? new Date(pending.purgeAfterUtc).getTime() : NaN;
  return !Number.isFinite(purge) || purge > now.getTime();
}

// The date a deletion was scheduled for, from the signed-out page's query string, or null when missing or invalid.
export function parsePurgeDate(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? date : null;
}
