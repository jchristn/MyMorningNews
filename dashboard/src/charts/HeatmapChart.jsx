import { useState } from 'react';
import PropTypes from 'prop-types';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';

// Grid of cells shaded by value (for example activity by day and hour). Every cell is a button with a tooltip on hover
// and on keyboard focus.
export default function HeatmapChart({ rows, columns, ariaLabel, formatValue, onCellClick }) {
  const { locale } = useLocale();
  const [tip, setTip] = useState(null);
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const max = Math.max(1, ...rows.flatMap((r) => r.values));
  return (
    <div className="heatmap" role="table" aria-label={ariaLabel}>
      <div className="heatmap-row heatmap-header" role="row">
        <span className="heatmap-label" role="columnheader" />
        {columns.map((c) => (
          <span key={c.key} className="heatmap-col-label" role="columnheader" title={c.label}>
            {c.short || c.label}
          </span>
        ))}
      </div>
      {rows.map((r) => (
        <div className="heatmap-row" role="row" key={r.key}>
          <span className="heatmap-label" role="rowheader" title={r.label}>
            {r.label}
          </span>
          {r.values.map((v, i) => {
            const intensity = v > 0 ? 0.15 + 0.85 * (v / max) : 0;
            return (
              <button
                type="button"
                role="cell"
                key={columns[i].key}
                className="heatmap-cell"
                style={{ background: v > 0 ? 'color-mix(in srgb, var(--chart-series-1) ' + Math.round(intensity * 100) + '%, transparent)' : undefined }}
                aria-label={r.label + ', ' + columns[i].label + ': ' + fmt(v)}
                onMouseMove={(e) => setTip({ x: e.clientX, y: e.clientY, title: r.label + ', ' + columns[i].label, lines: [{ label: '', value: fmt(v) }] })}
                onMouseLeave={() => setTip(null)}
                onFocus={(e) => {
                  const box = e.currentTarget.getBoundingClientRect();
                  setTip({ x: box.left + box.width / 2, y: box.top, title: r.label + ', ' + columns[i].label, lines: [{ label: '', value: fmt(v) }] });
                }}
                onBlur={() => setTip(null)}
                onClick={() => onCellClick && onCellClick(r, columns[i], v)}
              />
            );
          })}
        </div>
      ))}
      <ChartTooltip tip={tip} />
    </div>
  );
}

HeatmapChart.propTypes = {
  rows: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, values: PropTypes.arrayOf(PropTypes.number).isRequired })).isRequired,
  columns: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, short: PropTypes.string })).isRequired,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func,
  onCellClick: PropTypes.func
};
