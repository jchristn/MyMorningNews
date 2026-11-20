import { ACTIVE_RUN_STATUSES, FINISHED_RUN_STATUSES, FINISHED_STAGE_STATUSES, RESUMABLE_STAGES, RUN_STAGES } from '../../utils/enums.js';

export function isActiveRun(run) {
  return Boolean(run && ACTIVE_RUN_STATUSES.includes(run.status));
}

export function isFinishedRun(run) {
  return Boolean(run && FINISHED_RUN_STATUSES.includes(run.status));
}

// Replace one stage (matched by stage name) in a list, or add it. Used as stage events arrive.
export function mergeStage(stages, stage) {
  if (!stage || !stage.stage) return stages || [];
  const list = [...(stages || [])];
  const at = list.findIndex((s) => s.stage === stage.stage);
  if (at >= 0) list[at] = { ...list[at], ...stage };
  else list.push(stage);
  return list;
}

// Every pipeline stage in order, with Pending placeholders for those that have not reported yet, so a live run
// shows the whole timeline. sortOrder follows the pipeline order; stages the dashboard does not know come last.
export function withAllStages(stages) {
  const byName = new Map((stages || []).map((s) => [s.stage, s]));
  const known = RUN_STAGES.map((name, index) => ({ ...(byName.get(name) || { stage: name, status: 'Pending', itemsIn: 0, itemsOut: 0, durationMs: 0 }), sortOrder: index }));
  const extra = (stages || []).filter((s) => !RUN_STAGES.includes(s.stage)).map((s, i) => ({ ...s, sortOrder: RUN_STAGES.length + i }));
  return [...known, ...extra];
}

// The retry a stage row offers: { kind: 'resume', fromStage } re-queues the same run from a finished stage at
// Synthesize or later; { kind: 'new' } runs the topic again from a failed earlier stage. Null for an unfinished run or
// stage.
export function retryAction(run, stage) {
  if (!isFinishedRun(run) || !stage || !FINISHED_STAGE_STATUSES.includes(stage.status)) return null;
  if (RESUMABLE_STAGES.includes(stage.stage)) return { kind: 'resume', fromStage: stage.stage };
  return stage.status === 'Failed' ? { kind: 'new', fromStage: null } : null;
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
