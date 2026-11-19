// Chart math for the hand-rolled horizontal bar chart (no chart libraries; plan section 25.6). Pure, so it is unit
// tested and can move to the dashboard's shared chart math later.

/**
 * Lay out horizontal bars.
 * @param {{ label: string, value: number }[]} rows
 * @param {{ width: number, rowHeight?: number, gap?: number, labelWidth?: number, valueWidth?: number }} options
 * @returns {{ height: number, bars: { label: string, value: number, x: number, y: number, width: number, height: number, share: number }[] }}
 */
export function barLayout(rows, { width, rowHeight = 22, gap = 14, labelWidth = 0, valueWidth = 0 }) {
  const clean = (Array.isArray(rows) ? rows : []).map((r) => ({ label: String(r.label ?? ''), value: Number.isFinite(Number(r.value)) && Number(r.value) > 0 ? Number(r.value) : 0 }));
  const max = clean.reduce((m, r) => Math.max(m, r.value), 0);
  const total = clean.reduce((sum, r) => sum + r.value, 0);
  const track = Math.max(0, width - labelWidth - valueWidth);
  const bars = clean.map((r, i) => {
    const barWidth = max > 0 ? (r.value / max) * track : 0;
    return {
      label: r.label,
      value: r.value,
      x: labelWidth,
      y: i * (rowHeight + gap),
      // A non-zero value always shows at least a sliver, so it is not mistaken for zero.
      width: r.value > 0 ? Math.max(2, barWidth) : 0,
      height: rowHeight,
      share: total > 0 ? r.value / total : 0,
    };
  });
  const height = clean.length > 0 ? clean.length * rowHeight + (clean.length - 1) * gap : 0;
  return { height, bars };
}

/** Rows sorted by value, top `limit`, with the rest folded into one "other" row when `otherLabel` is given. */
export function topRows(rows, limit, otherLabel) {
  const sorted = [...(Array.isArray(rows) ? rows : [])].sort((a, b) => (Number(b.value) || 0) - (Number(a.value) || 0));
  if (sorted.length <= limit) return sorted;
  const head = sorted.slice(0, limit);
  if (!otherLabel) return head;
  const rest = sorted.slice(limit).reduce((sum, r) => sum + (Number(r.value) || 0), 0);
  return [...head, { label: otherLabel, value: rest }];
}
