// Fixtures for comparison provenance, baseline previews, edition system briefings, and topic slots.
export const COUNTS = { NewDevelopment: 1, ChangedFact: 1, Resolved: 0, Correction: 1, Unchanged: 1, Unavailable: 0 };

export function delta(id, kind, sortOrder, extra = {}) {
  return {
    id,
    tenantId: 'default',
    briefingId: 'brf_1',
    kind,
    title: kind + ' item',
    summary: null,
    findingId: null,
    predictionId: null,
    baselineBriefingId: 'brf_0',
    baselineItemId: null,
    sortOrder,
    active: true,
    createdUtc: '2026-11-25T06:30:00Z',
    lastUpdateUtc: '2026-11-25T06:30:00Z',
    ...extra
  };
}

export function comparison(extra = {}) {
  return {
    briefingId: 'brf_1',
    compared: true,
    mode: 'PreviousIssue',
    note: null,
    truncated: false,
    cutoffUtc: '2026-11-25T06:00:00Z',
    baselines: [{ id: 'brf_0', title: 'Rebates', createdUtc: '2026-11-24T06:30:00Z', available: true }],
    counts: COUNTS,
    noMaterialChanges: false,
    summary: 'Compared with Rebates of November 24: 1 new, 1 changed, 1 correction, 1 unchanged.',
    deltas: [
      delta('bdl_1', 'Correction', 0, { title: 'Rebate cap corrected', summary: 'The cap is $8,000, not $10,000.', findingId: 'fnd_9' }),
      delta('bdl_2', 'ChangedFact', 1, { title: 'Deadline moved', findingId: 'fnd_8' }),
      delta('bdl_3', 'NewDevelopment', 2, { title: 'New state program', findingId: 'fnd_7', baselineBriefingId: null }),
      delta('bdl_4', 'Unchanged', 3, { title: 'Eligibility rules' })
    ],
    ...extra
  };
}

export const STANDALONE = {
  briefingId: 'brf_1',
  compared: false,
  mode: 'Off',
  note: 'Comparison is off for this topic, so this briefing stands alone.',
  truncated: false,
  cutoffUtc: null,
  baselines: [],
  counts: { NewDevelopment: 0, ChangedFact: 0, Resolved: 0, Correction: 0, Unchanged: 0, Unavailable: 0 },
  noMaterialChanges: false,
  summary: null,
  deltas: []
};

export function historyPreview(extra = {}) {
  return {
    mode: 'RecentIssues',
    baselines: [
      { id: 'brf_2', title: 'Heat pumps weekly', createdUtc: '2026-11-24T06:30:00Z', available: true },
      { id: 'brf_3', title: 'Heat pumps catch-up', createdUtc: '2026-11-17T06:30:00Z', available: true }
    ],
    contextBytes: 4000,
    budgetBytes: 8000,
    truncated: false,
    note: null,
    cutoffUtc: '2026-11-26T08:00:00Z',
    ...extra
  };
}

export function slot(topicId, name, position, withinPlan, extra = {}) {
  return { topicId, name, status: 'Active', position, chosen: false, withinPlan, lastRunUtc: '2026-11-25T06:00:00Z', ...extra };
}

// TopicSlotsResponse summary.
export function topicSlots(extra = {}) {
  return { limit: 2, counted: 3, overLimit: 1, customized: false, ...extra };
}

// The default slot order, as GET /account/topic-slots/topics objects.
export function slotTopics() {
  return [slot('top_1', 'Solar', 1, true), slot('top_2', 'Wind', 2, true), slot('top_3', 'Hydro', 3, false)];
}

// An EnumerationResult<TopicSlotResponse> holding every topic on one page.
export function slotPage(topics = slotTopics()) {
  return { success: true, objects: topics, pageNumber: 1, pageSize: 200, totalRecords: topics.length, totalPages: topics.length ? 1 : 0 };
}

export function catalogEntry(id, title, extra = {}) {
  return { id, title, category: 'Science', language: 'en', status: 'Published', freeEligible: true, eligible: true, subscribed: true, suspended: false, ...extra };
}
