import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';

const TOP = 8;
const BOTTOM = 4;

// Columns per category (model, task, user, tenant), either stacked by series or grouped side by side. Category
// labels sit below the plot and truncate with the full text in a title. Every column is focusable with a tooltip;
// click or Enter calls onCategoryClick.
export default function StackedColumnChart({ categories, series, mode = 'stacked', height = 200, ariaLabel, formatValue, onCategoryClick, showLegend = true }) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const [frameRef, width] = useChartWidth();
  const [tip, setTip] = useState(null);
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const count = Math.max(1, categories.length);
  const value = (s, i) => Math.max(0, Number(s.values[i]) || 0);
  const totals = categories.map((_, i) => series.reduce((sum, s) => sum + value(s, i), 0));
  const maximum = (mode === 'grouped' ? Math.max(0, ...series.flatMap((s) => categories.map((_, i) => value(s, i)))) : Math.max(0, ...totals)) || 1;
  const left = Math.max(40, Math.max(String(fmt(maximum)).length, String(fmt(0)).length) * 7 + 12);
  const plotWidth = Math.max(40, width - left - 4);
  const plotHeight = height - TOP - BOTTOM;
  const slot = plotWidth / count;
  const columnWidth = Math.max(4, Math.min(slot * 0.7, 56));
  const groupedWidth = Math.max(2, columnWidth / Math.max(1, series.length));

  function show(point, index) {
    setTip({
      x: point.clientX,
      y: point.clientY,
      title: categories[index].label,
      lines: [
        ...series.map((s) => ({ label: s.label, color: s.color, value: fmt(value(s, index)) })),
        ...(mode === 'stacked' && series.length > 1 ? [{ label: t('charts.total'), value: fmt(totals[index]) }] : []),
        ...(categories[index].extra || [])
      ]
    });
  }

  return (
    <div className="column-chart" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[1, 0.5, 0].map((ratio) => (
          <g key={ratio}>
            <line stroke="var(--color-border)" strokeDasharray={ratio === 0 ? undefined : '3 4'} x1={left} x2={width} y1={TOP + plotHeight * (1 - ratio)} y2={TOP + plotHeight * (1 - ratio)} />
            <text fill="var(--color-text-muted)" fontSize="10" textAnchor="end" x={left - 6} y={TOP + plotHeight * (1 - ratio) + 3}>
              {fmt(maximum * ratio)}
            </text>
          </g>
        ))}
        {categories.map((c, index) => {
          const x0 = left + index * slot + (slot - columnWidth) / 2;
          let y = TOP + plotHeight;
          return (
            <g key={c.key}>
              {series.map((s, si) => {
                const v = value(s, index);
                if (!v) return null;
                const h = Math.max(1, (v / maximum) * plotHeight);
                if (mode === 'grouped') {
                  return <rect key={s.key} data-series={s.key} fill={s.color} x={x0 + si * groupedWidth} y={TOP + plotHeight - h} width={Math.max(1, groupedWidth - 1)} height={h} rx={groupedWidth > 6 ? 1.5 : 0} />;
                }
                y -= h;
                return <rect key={s.key} data-series={s.key} fill={s.color} x={x0} y={y} width={columnWidth} height={h} rx={columnWidth > 6 ? 1.5 : 0} />;
              })}
            </g>
          );
        })}
        {categories.map((c, index) => (
          <rect
            key={'slot-' + c.key}
            className="chart-slot"
            x={left + index * slot}
            y={TOP}
            width={slot}
            height={plotHeight}
            fill="transparent"
            tabIndex={0}
            aria-label={c.label + ': ' + series.map((s) => s.label + ' ' + fmt(value(s, index))).join(', ')}
            style={{ cursor: onCategoryClick ? 'pointer' : 'default' }}
            onMouseMove={(e) => show(e, index)}
            onMouseLeave={() => setTip(null)}
            onFocus={(e) => {
              const r = e.currentTarget.getBoundingClientRect();
              show({ clientX: r.left + r.width / 2, clientY: r.top }, index);
            }}
            onBlur={() => setTip(null)}
            onClick={() => onCategoryClick && onCategoryClick(c)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && onCategoryClick) onCategoryClick(c);
            }}
          />
        ))}
      </svg>
      <div className="column-chart-labels" style={{ paddingInlineStart: left, gridTemplateColumns: 'repeat(' + count + ', minmax(0, 1fr))' }} aria-hidden="true">
        {categories.map((c) => (
          <span key={c.key} title={c.label}>
            {c.label}
          </span>
        ))}
      </div>
      {showLegend && series.length > 1 ? (
        <div className="chart-legend">
          {series.map((s) => (
            <span key={s.key}>
              <i style={{ background: s.color }} /> {s.label}
            </span>
          ))}
        </div>
      ) : null}
      <ChartTooltip tip={tip} />
    </div>
  );
}

StackedColumnChart.propTypes = {
  categories: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, extra: PropTypes.array })).isRequired,
  series: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, color: PropTypes.string.isRequired, values: PropTypes.arrayOf(PropTypes.number).isRequired })).isRequired,
  mode: PropTypes.oneOf(['stacked', 'grouped']),
  height: PropTypes.number,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func,
  onCategoryClick: PropTypes.func,
  showLegend: PropTypes.bool
};
