// Chart math for the hand-rolled trend and calibration charts (no chart libraries; plan section 25.6). Pure, so it is
// unit tested and can move to the dashboard's shared chart math later.

const finite = (v) => (v === null || v === undefined || v === '' ? null : Number.isFinite(Number(v)) ? Number(v) : null);

/**
 * Lay out a time series as a line (points) and as columns, inside `width` by `height` with `pad` on every side.
 * Missing values (null) leave a gap in the line and no column. `max` fixes the top of the scale (1 for rates);
 * otherwise it is the largest value (at least 1).
 *
 * @param {{ label: string, value: number | null }[]} rows
 * @param {{ width: number, height: number, pad?: number, max?: number }} options
 * @returns {{ max: number, points: { x: number, y: number, value: number, label: string, index: number }[], columns: { x: number, y: number, width: number, height: number, value: number, label: string }[], segments: { x: number, y: number }[][], gridY: { y: number, value: number }[] }}
 */
export function seriesLayout(rows, { width, height, pad = 6, max } = {}) {
  const list = (Array.isArray(rows) ? rows : []).map((r) => ({ label: String(r.label ?? ''), value: finite(r.value) }));
  const top = Number.isFinite(max) && max > 0 ? max : Math.max(1, ...list.map((r) => (r.value === null ? 0 : r.value)));
  const innerW = Math.max(0, (width || 0) - pad * 2);
  const innerH = Math.max(0, (height || 0) - pad * 2);
  const n = list.length;
  const step = n > 1 ? innerW / (n - 1) : 0;
  const yFor = (v) => pad + innerH - (Math.min(Math.max(v, 0), top) / top) * innerH;
  const points = [];
  const segments = [];
  let current = null;
  list.forEach((r, index) => {
    if (r.value === null) {
      current = null;
      return;
    }
    const point = { x: n > 1 ? pad + index * step : pad + innerW / 2, y: yFor(r.value), value: r.value, label: r.label, index };
    points.push(point);
    if (!current) {
      current = [];
      segments.push(current);
    }
    current.push({ x: point.x, y: point.y });
  });
  const slot = n > 0 ? innerW / n : 0;
  const colWidth = Math.max(1, slot * 0.7);
  const columns = list
    .map((r, index) => {
      if (r.value === null) return null;
      const y = yFor(r.value);
      return { x: pad + index * slot + (slot - colWidth) / 2, y, width: colWidth, height: Math.max(r.value > 0 ? 1 : 0, pad + innerH - y), value: r.value, label: r.label };
    })
    .filter(Boolean);
  const gridY = [0, 0.5, 1].map((f) => ({ y: yFor(top * f), value: top * f }));
  return { max: top, points, columns, segments, gridY };
}

/** An SVG path for line segments: `M x y L x y ...` per segment. */
export function linePath(segments) {
  return (segments || [])
    .filter((s) => s.length > 0)
    .map((s) => s.map((p, i) => (i === 0 ? 'M' : 'L') + round(p.x) + ' ' + round(p.y)).join(' '))
    .join(' ');
}

function round(n) {
  return Math.round(n * 10) / 10;
}

/**
 * Lay out a reliability diagram (plan section 25.6, chart 15): confidence buckets on x (0 to 1), the observed share
 * on y (0 to 1), in a `size` by `size` square with `pad`. Each bucket gets a column across its range up to the observed
 * share, and a point at its mean confidence sized by how many predictions it holds. Buckets without resolved
 * predictions (observed null) get neither. The diagonal is perfect calibration.
 *
 * @param {{ lower: number, upper: number, count: number, meanConfidence?: number, observed?: number }[]} buckets
 */
export function calibrationLayout(buckets, { size, pad = 8 } = {}) {
  const inner = Math.max(0, (size || 0) - pad * 2);
  const x = (v) => pad + Math.min(Math.max(v, 0), 1) * inner;
  const y = (v) => pad + inner - Math.min(Math.max(v, 0), 1) * inner;
  const list = (Array.isArray(buckets) ? buckets : []).map((b) => ({ lower: finite(b.lower) ?? 0, upper: finite(b.upper) ?? 0, count: finite(b.count) ?? 0, meanConfidence: finite(b.meanConfidence), observed: finite(b.observed) }));
  const maxCount = Math.max(1, ...list.map((b) => b.count));
  const columns = [];
  const points = [];
  for (const b of list) {
    if (b.observed === null || b.count <= 0) continue;
    const x0 = x(b.lower);
    const x1 = x(b.upper);
    columns.push({ x: x0 + 1, width: Math.max(1, x1 - x0 - 2), y: y(b.observed), height: pad + inner - y(b.observed), bucket: b });
    const confidence = b.meanConfidence === null ? (b.lower + b.upper) / 2 : b.meanConfidence;
    points.push({ x: x(confidence), y: y(b.observed), r: 3 + 5 * Math.sqrt(b.count / maxCount), bucket: b });
  }
  return { diagonal: { x1: x(0), y1: y(0), x2: x(1), y2: y(1) }, grid: [0, 0.25, 0.5, 0.75, 1].map((v) => ({ x: x(v), y: y(v), value: v })), columns, points };
}
