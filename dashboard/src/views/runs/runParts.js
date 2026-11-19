import { ACTIVE_RUN_STATUSES } from '../../utils/enums.js';

export function isActiveRun(run) {
  return Boolean(run && ACTIVE_RUN_STATUSES.includes(run.status));
}

// Milliseconds from start to completion, or to now for a running run. Null before it starts.
export function runDurationMs(run, now = Date.now()) {
  if (!run || !run.startedUtc) return null;
  const start = new Date(run.startedUtc).getTime();
  const end = run.completedUtc ? new Date(run.completedUtc).getTime() : now;
  return Number.isFinite(start) && Number.isFinite(end) ? Math.max(0, end - start) : null;
}
