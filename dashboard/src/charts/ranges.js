// Time ranges used by every request-over-time chart.
export const RANGES = {
  hour: { id: 'hour', bucketMinutes: 1, count: 60 },
  day: { id: 'day', bucketMinutes: 15, count: 96 },
  week: { id: 'week', bucketMinutes: 120, count: 84 },
  month: { id: 'month', bucketMinutes: 360, count: 120 }
};

export const RANGE_IDS = ['hour', 'day', 'week', 'month'];

// The window ending at the next bucket boundary after now, so the current bucket is included.
export function computeRange(rangeId, now = Date.now()) {
  const range = RANGES[rangeId] || RANGES.day;
  const size = range.bucketMinutes * 60000;
  const end = Math.ceil((now + 1) / size) * size;
  const start = end - range.count * size;
  return { fromUtc: new Date(start).toISOString(), toUtc: new Date(end).toISOString(), bucketMinutes: range.bucketMinutes };
}

// Fill any missing buckets with zeros (the server already gap-fills; this guards partial responses).
export function fillBuckets(buckets, range) {
  const size = range.bucketMinutes * 60000;
  const start = new Date(range.fromUtc).getTime();
  const end = new Date(range.toUtc).getTime();
  const byStart = new Map((buckets || []).map((b) => [new Date(b.bucketStartUtc).getTime(), b]));
  const out = [];
  for (let t = start; t < end; t += size) {
    const b = byStart.get(t);
    out.push(
      b || {
        bucketStartUtc: new Date(t).toISOString(),
        bucketEndUtc: new Date(t + size).toISOString(),
        successCount: 0,
        failureCount: 0,
        averageDurationMs: 0
      }
    );
  }
  return out;
}
