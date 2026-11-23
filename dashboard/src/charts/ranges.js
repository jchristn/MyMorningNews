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

// Ranges for the usage and cost pages: a longer horizon with coarser buckets.
export const USAGE_RANGES = {
  day: { id: 'day', bucketMinutes: 60, count: 24 },
  week: { id: 'week', bucketMinutes: 360, count: 28 },
  month: { id: 'month', bucketMinutes: 1440, count: 30 },
  quarter: { id: 'quarter', bucketMinutes: 10080, count: 13 }
};

export const USAGE_RANGE_IDS = ['day', 'week', 'month', 'quarter'];

export function computeUsageRange(rangeId, now = Date.now()) {
  const range = USAGE_RANGES[rangeId] || USAGE_RANGES.month;
  const size = range.bucketMinutes * 60000;
  const end = Math.ceil((now + 1) / size) * size;
  const start = end - range.count * size;
  return { from: new Date(start).toISOString(), to: new Date(end).toISOString(), bucketMinutes: range.bucketMinutes, count: range.count };
}

// A bucket width that shows a usage range in at most maxColumns columns: a whole multiple of the range's own bucket.
export function coarseBucketMinutes(range, maxColumns) {
  const count = Math.max(1, Number(range.count) || 1);
  return range.bucketMinutes * Math.max(1, Math.ceil(count / Math.max(1, maxColumns)));
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
