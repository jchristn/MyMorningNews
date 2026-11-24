// Email outbox helpers: stable enum codes from EmailStatusEnum and EmailTemplateEnum, tones, and chart shaping.
export const EMAIL_STATUSES = ['Queued', 'Sending', 'Sent', 'Failed', 'Suppressed', 'Skipped'];
export const EMAIL_TEMPLATES = ['RunDigest', 'BriefingPublished', 'RunFailed', 'PreferenceProposal', 'Test', 'BriefingSupport'];

export const EMAIL_STATUS_TONES = { Queued: 'info', Sending: 'info', Sent: 'success', Failed: 'danger', Suppressed: 'warning', Skipped: 'neutral' };

export const EMAIL_STATUS_COLORS = {
  Sent: 'var(--chart-series-2)',
  Failed: 'var(--chart-series-5)',
  Queued: 'var(--chart-series-1)',
  Sending: 'var(--chart-series-3)',
  Suppressed: 'var(--chart-series-6)',
  Skipped: 'var(--chart-series-8)'
};

const DAY = 86400000;

function startOfUtcDay(ms) {
  const d = new Date(ms);
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
}

// Messages per UTC day by status for the last `days` days (gap-filled). Returns [{ startUtc, endUtc, counts }].
export function emailsByDay(messages, { now = new Date(), days = 30 } = {}) {
  const end = startOfUtcDay(now.getTime());
  const start = end - (days - 1) * DAY;
  const buckets = [];
  for (let t = start; t <= end; t += DAY) buckets.push({ startUtc: new Date(t).toISOString(), endUtc: new Date(t + DAY).toISOString(), counts: {} });
  for (const m of messages || []) {
    const t = startOfUtcDay(new Date(m.createdUtc).getTime());
    const index = Math.floor((t - start) / DAY);
    if (Number.isNaN(index) || index < 0 || index >= buckets.length) continue;
    const counts = buckets[index].counts;
    counts[m.status] = (counts[m.status] || 0) + 1;
  }
  return buckets;
}

// Chart series for the statuses that occur, in a stable order.
export function emailSeries(buckets, label) {
  const seen = new Set(buckets.flatMap((b) => Object.keys(b.counts)));
  return EMAIL_STATUSES.filter((s) => seen.has(s)).map((s) => ({ key: s, label: label(s), color: EMAIL_STATUS_COLORS[s], values: buckets.map((b) => b.counts[s] || 0) }));
}
