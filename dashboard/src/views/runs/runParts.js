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

const time = (value) => {
  const ms = value ? new Date(value).getTime() : NaN;
  return Number.isFinite(ms) ? ms : null;
};

// Place stages on a timeline: each stage's offset from the earliest stage start and its duration. A stage without a
// start time follows the previous one. A running stage without a duration runs to now.
export function layoutStages(stages, now = Date.now()) {
  const ordered = [...(stages || [])].sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const starts = ordered.map((s) => time(s.startedUtc)).filter((v) => v !== null);
  const origin = starts.length > 0 ? Math.min(...starts) : null;
  let cursor = 0;
  return ordered.map((stage) => {
    const start = time(stage.startedUtc);
    const end = time(stage.completedUtc);
    let duration = Number(stage.durationMs) || 0;
    if (!duration && start !== null) duration = Math.max(0, (end !== null ? end : stage.status === 'Running' ? now : start) - start);
    const offset = start !== null && origin !== null ? start - origin : cursor;
    cursor = Math.max(cursor, offset + duration);
    return { stage, offsetMs: offset, durationMs: duration };
  });
}

// Stages that failed, for the partial-success notice.
export function failedStages(stages) {
  return (stages || []).filter((s) => s.status === 'Failed');
}

// The findings page filtered to one run. Dry-run findings are hidden by default, so a dry run's link includes them.
export function findingsLink(run) {
  return '/dashboard/findings?runId=' + encodeURIComponent(run.id) + (run.isDryRun ? '&includeDryRun=true' : '');
}
