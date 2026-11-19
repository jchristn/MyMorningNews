/**
 * What the biometric lock does when the app state changes.
 *
 * The Face ID sheet itself moves the app to `inactive` and back to `active`, so only a real trip to the `background`
 * counts. The app locks on return when it was locked already, or when it stayed in the background at least
 * `timeoutMs` (0 locks on every return).
 *
 * @param {string} next The new AppState (`active`, `background`, `inactive`).
 * @param {{ locked: boolean, backgroundedAt: number|null }} current
 * @param {number} now Milliseconds.
 * @param {number} timeoutMs
 * @returns {{ locked: boolean, backgroundedAt: number|null, prompt: boolean }}
 */
export function lockTransition(next, current, now, timeoutMs) {
  const { locked, backgroundedAt } = current;
  if (next === 'background') {
    return { locked, backgroundedAt: backgroundedAt === null || backgroundedAt === undefined ? now : backgroundedAt, prompt: false };
  }
  if (next === 'active' && backgroundedAt !== null && backgroundedAt !== undefined) {
    const shouldLock = locked || now - backgroundedAt >= Math.max(0, timeoutMs || 0);
    return { locked: shouldLock, backgroundedAt: null, prompt: shouldLock };
  }
  return { locked, backgroundedAt: backgroundedAt === undefined ? null : backgroundedAt, prompt: false };
}
