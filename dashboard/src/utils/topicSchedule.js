// How a topic's research is scheduled, for the workspace overview: its own schedules, and the editions that research
// it before they compose. Pure functions so they are tested without rendering.

// The topic's own schedules split by state, with the soonest next run of the enabled ones.
// state is 'none' (no schedules), 'active' (at least one enabled), or 'paused' (every schedule disabled).
export function scheduleState(schedules) {
  const list = (schedules || []).filter((s) => s && s.active !== false);
  const enabled = list.filter((s) => s.enabled);
  const paused = list.filter((s) => !s.enabled);
  const nextRunUtc =
    enabled
      .map((s) => s.nextRunUtc)
      .filter(Boolean)
      .sort()[0] || null;
  return { state: list.length === 0 ? 'none' : enabled.length > 0 ? 'active' : 'paused', enabled, paused, nextRunUtc };
}

// Whether an edition (an edition detail: { edition, schedules, coveredTopicIds }) runs on its own: it is enabled
// and has an enabled schedule.
export function editionRuns(detail) {
  if (!detail || !detail.edition || detail.edition.enabled === false) return false;
  return (detail.schedules || []).some((s) => s.enabled && s.active !== false);
}

// The editions that cover a topic, each with whether it is paused.
export function coveringEditions(details, topicId) {
  return (details || [])
    .filter((d) => d && d.edition && (d.coveredTopicIds || []).includes(topicId))
    .map((d) => ({ id: d.edition.id, name: d.edition.name, paused: !editionRuns(d), nextRunUtc: nextEditionRun(d) }));
}

function nextEditionRun(detail) {
  return (
    (detail.schedules || [])
      .filter((s) => s.enabled && s.nextRunUtc)
      .map((s) => s.nextRunUtc)
      .sort()[0] || null
  );
}
