import { PREDICTION_OUTCOMES } from '../../utils/enums.js';

// The observed outcome for a graded prediction (1 accurate, 0.5 partially accurate, 0 inaccurate), or null.
export function outcomeOf(status) {
  return Object.prototype.hasOwnProperty.call(PREDICTION_OUTCOMES, status) ? PREDICTION_OUTCOMES[status] : null;
}

// Brier score for a confidence (0 to 1) and a status: the squared error, rounded to four places as the server does.
export function brierFor(confidence, status) {
  const outcome = outcomeOf(status);
  if (outcome === null) return null;
  const c = Math.max(0, Math.min(1, Number(confidence) || 0));
  return Math.round((c - outcome) ** 2 * 10000) / 10000;
}

// Predictions that have been graded (accurate, partially accurate, or inaccurate).
export function graded(predictions) {
  return (predictions || []).filter((p) => outcomeOf(p.status) !== null);
}

// Summary over a list: total, open, awaiting review, graded, mean Brier score, and mean observed accuracy. Means are
// null when nothing is graded. The stored brierScore wins when present. open counts status Open only, as the server's
// Insights overview does; a proposed resolution waiting for the user's grade is counted in proposed.
export function predictionSummary(predictions) {
  const list = predictions || [];
  const done = graded(list);
  const briers = done.map((p) => (p.brierScore === null || p.brierScore === undefined ? brierFor(p.confidence, p.status) : Number(p.brierScore)));
  const mean = (values) => (values.length > 0 ? values.reduce((s, v) => s + v, 0) / values.length : null);
  return {
    total: list.length,
    open: list.filter((p) => p.status === 'Open').length,
    proposed: list.filter((p) => p.status === 'ProposedResolved').length,
    graded: done.length,
    meanBrier: mean(briers),
    accuracy: mean(done.map((p) => outcomeOf(p.status)))
  };
}

// Reliability buckets: graded predictions grouped by stated confidence into bucketCount equal ranges, each with the
// mean confidence and the observed accuracy rate. Empty buckets keep count 0 and null rates.
export function calibrationBuckets(predictions, bucketCount = 5) {
  const n = Math.max(1, Math.floor(bucketCount));
  const buckets = Array.from({ length: n }, (_, i) => ({ index: i, from: i / n, to: (i + 1) / n, count: 0, sumConfidence: 0, sumOutcome: 0 }));
  for (const p of graded(predictions)) {
    const c = Math.max(0, Math.min(1, Number(p.confidence) || 0));
    const b = buckets[Math.min(n - 1, Math.floor(c * n))];
    b.count += 1;
    b.sumConfidence += c;
    b.sumOutcome += outcomeOf(p.status);
  }
  return buckets.map((b) => ({
    index: b.index,
    from: b.from,
    to: b.to,
    count: b.count,
    meanConfidence: b.count > 0 ? b.sumConfidence / b.count : null,
    observedRate: b.count > 0 ? b.sumOutcome / b.count : null
  }));
}
