import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useLocale } from '../i18n/index.js';
import { formatDateTime, formatNumber } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';

const TOP = 8;
const BOTTOM = 22;

function bucketLabel(startUtc, bucketMinutes, locale) {
  const options = bucketMinutes >= 360 ? { month: 'short', day: 'numeric' } : bucketMinutes >= 60 ? { month: 'short', day: 'numeric', hour: 'numeric' } : { hour: 'numeric', minute: '2-digit' };
  return formatDateTime(startUtc, locale, options);
}

// Stacked column chart over time buckets. Every slot is focusable; click or Enter drills down through onBucketClick.
export default function TimeChart({ buckets, series, bucketMinutes, height = 200, onBucketClick, ariaLabel, formatValue }) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const [frameRef, width] = useChartWidth();
  const [tip, setTip] = useState(null);
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const count = Math.max(1, buckets.length);
  const totals = buckets.map((_, i) => series.reduce((sum, s) => sum + Math.max(0, s.values[i] || 0), 0));
  const maximum = Math.max(1, ...totals);
  const left = Math.max(36, String(fmt(maximum)).length * 7 + 12);
  const plotWidth = Math.max(40, width - left - 4);
  const plotHeight = height - TOP - BOTTOM;
  const slot = plotWidth / count;
  const barWidth = Math.max(1, Math.min(slot * 0.78, 40));
  const labelEvery = Math.max(1, Math.ceil(count / Math.max(2, Math.floor(plotWidth / 90))));

  function show(event, index) {
    const b = buckets[index];
    setTip({
      x: event.clientX,
      y: event.clientY,
      title: formatDateTime(b.startUtc, locale, { dateStyle: 'short', timeStyle: 'short' }) + ' - ' + formatDateTime(b.endUtc, locale, { timeStyle: 'short' }),
      lines: [
        ...series.map((s) => ({ label: s.label, color: s.color, value: fmt(s.values[index] || 0) })),
        { label: t('charts.total'), value: fmt(totals[index]) },
        ...(b.extra || [])
      ]
    });
  }

  function showAt(event, index) {
    const r = event.currentTarget.getBoundingClientRect();
    show({ clientX: r.left + r.width / 2, clientY: r.top }, index);
  }

  return (
    <div className="time-chart" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[1, 0.5, 0].map((ratio) => (
          <g key={ratio}>
            <line stroke="var(--color-border)" strokeDasharray={ratio === 0 ? undefined : '3 4'} x1={left} x2={width} y1={TOP + plotHeight * (1 - ratio)} y2={TOP + plotHeight * (1 - ratio)} />
            <text fill="var(--color-text-muted)" fontSize="10" textAnchor="end" x={left - 6} y={TOP + plotHeight * (1 - ratio) + 3}>
              {fmt(Math.round(maximum * ratio))}
            </text>
          </g>
        ))}
        {buckets.map((b, index) => {
          let y = TOP + plotHeight;
          return (
            <g key={b.startUtc}>
              {series.map((s) => {
                const v = Math.max(0, s.values[index] || 0);
                if (!v) return null;
                const h = (v / maximum) * plotHeight;
                y -= h;
                return <rect key={s.key} data-series={s.key} fill={s.color} x={left + index * slot + (slot - barWidth) / 2} y={y} width={barWidth} height={h} rx={barWidth > 6 ? 1.5 : 0} />;
              })}
            </g>
          );
        })}
        {buckets.map((b, index) => (
          <g key={'slot-' + b.startUtc}>
            <rect
              className="chart-slot"
              x={left + index * slot}
              y={TOP}
              width={slot}
              height={plotHeight}
              fill="transparent"
              tabIndex={0}
              aria-label={t('charts.bucketAria', { when: bucketLabel(b.startUtc, bucketMinutes, locale), total: fmt(totals[index]) })}
              style={{ cursor: onBucketClick ? 'pointer' : 'default' }}
              onMouseMove={(e) => show(e, index)}
              onMouseLeave={() => setTip(null)}
              onFocus={(e) => showAt(e, index)}
              onBlur={() => setTip(null)}
              onClick={() => onBucketClick && onBucketClick(b)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && onBucketClick) onBucketClick(b);
              }}
            />
            {index % labelEvery === 0 ? (
              <text fill="var(--color-text-muted)" fontSize="10" x={left + index * slot + 2} y={TOP + plotHeight + 14}>
                {bucketLabel(b.startUtc, bucketMinutes, locale)}
              </text>
            ) : null}
          </g>
        ))}
      </svg>
      <div className="chart-legend">
        {series.map((s) => (
          <span key={s.key}>
            <i style={{ background: s.color }} /> {s.label}
          </span>
        ))}
      </div>
      <ChartTooltip tip={tip} />
    </div>
  );
}

TimeChart.propTypes = {
  buckets: PropTypes.arrayOf(PropTypes.shape({ startUtc: PropTypes.string.isRequired, endUtc: PropTypes.string.isRequired, extra: PropTypes.array })).isRequired,
  series: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, color: PropTypes.string.isRequired, values: PropTypes.arrayOf(PropTypes.number).isRequired })).isRequired,
  bucketMinutes: PropTypes.number.isRequired,
  height: PropTypes.number,
  onBucketClick: PropTypes.func,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func
};
