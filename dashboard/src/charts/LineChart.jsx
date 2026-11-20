import { useState } from 'react';
import PropTypes from 'prop-types';
import useFormat from '../hooks/useFormat.js';
import { formatNumber } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';

const TOP = 10;
const BOTTOM = 22;

function bucketLabel(startUtc, bucketMinutes, f) {
  const options = bucketMinutes >= 1440 ? { month: 'short', day: 'numeric' } : bucketMinutes >= 60 ? { month: 'short', day: 'numeric', hour: 'numeric' } : { hour: 'numeric', minute: '2-digit' };
  return f.dateTime(startUtc, options);
}

// Line chart over time buckets, one line per series. Every bucket is a focusable slot with a tooltip; click or Enter
// drills down through onBucketClick.
export default function LineChart({ buckets, series, bucketMinutes, height = 200, ariaLabel, formatValue, onBucketClick, area = true }) {
  const f = useFormat();
  const locale = f.locale;
  const [frameRef, width] = useChartWidth();
  const [tip, setTip] = useState(null);
  const [active, setActive] = useState(null);
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const count = Math.max(1, buckets.length);
  const maximum = Math.max(0, ...series.flatMap((s) => s.values.map((v) => Number(v) || 0)));
  const top = maximum > 0 ? maximum * 1.1 : 1;
  const left = Math.max(40, Math.max(String(fmt(top)).length, String(fmt(0)).length) * 7 + 12);
  const plotWidth = Math.max(40, width - left - 8);
  const plotHeight = height - TOP - BOTTOM;
  const slot = plotWidth / count;
  const x = (i) => left + i * slot + slot / 2;
  const y = (v) => TOP + plotHeight - ((Number(v) || 0) / top) * plotHeight;
  const labelEvery = Math.max(1, Math.ceil(count / Math.max(2, Math.floor(plotWidth / 90))));

  function show(point, index) {
    const b = buckets[index];
    setActive(index);
    setTip({
      x: point.clientX,
      y: point.clientY,
      title: f.dateTime(b.startUtc, { dateStyle: 'medium', timeStyle: 'short' }),
      lines: [...series.map((s) => ({ label: s.label, color: s.color, value: fmt(s.values[index] || 0) })), ...(b.extra || [])]
    });
  }

  function hide() {
    setActive(null);
    setTip(null);
  }

  return (
    <div className="time-chart" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[1, 0.5, 0].map((ratio) => (
          <g key={ratio}>
            <line stroke="var(--color-border)" strokeDasharray={ratio === 0 ? undefined : '3 4'} x1={left} x2={width} y1={TOP + plotHeight * (1 - ratio)} y2={TOP + plotHeight * (1 - ratio)} />
            <text fill="var(--color-text-muted)" fontSize="10" textAnchor="end" x={left - 6} y={TOP + plotHeight * (1 - ratio) + 3}>
              {fmt(top * ratio)}
            </text>
          </g>
        ))}
        {series.map((s) => {
          const points = s.values.map((v, i) => x(i) + ',' + y(v)).join(' ');
          return (
            <g key={s.key} data-series={s.key}>
              {area && buckets.length > 1 ? (
                <polygon points={x(0) + ',' + (TOP + plotHeight) + ' ' + points + ' ' + x(s.values.length - 1) + ',' + (TOP + plotHeight)} fill={s.color} opacity="0.1" />
              ) : null}
              <polyline points={points} fill="none" stroke={s.color} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
              {buckets.length <= 40 || active !== null
                ? s.values.map((v, i) => (buckets.length <= 40 || i === active ? <circle key={i} cx={x(i)} cy={y(v)} r={i === active ? 4 : 2.5} fill={s.color} /> : null))
                : null}
            </g>
          );
        })}
        {active !== null ? <line x1={x(active)} x2={x(active)} y1={TOP} y2={TOP + plotHeight} stroke="var(--color-border-strong)" strokeDasharray="2 3" /> : null}
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
              aria-label={bucketLabel(b.startUtc, bucketMinutes, f) + ': ' + series.map((s) => s.label + ' ' + fmt(s.values[index] || 0)).join(', ')}
              style={{ cursor: onBucketClick ? 'pointer' : 'default' }}
              onMouseMove={(e) => show(e, index)}
              onMouseLeave={hide}
              onFocus={(e) => {
                const r = e.currentTarget.getBoundingClientRect();
                show({ clientX: r.left + r.width / 2, clientY: r.top }, index);
              }}
              onBlur={hide}
              onClick={() => onBucketClick && onBucketClick(b)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && onBucketClick) onBucketClick(b);
              }}
            />
            {index % labelEvery === 0 ? (
              <text fill="var(--color-text-muted)" fontSize="10" x={left + index * slot + 2} y={TOP + plotHeight + 14}>
                {bucketLabel(b.startUtc, bucketMinutes, f)}
              </text>
            ) : null}
          </g>
        ))}
      </svg>
      {series.length > 1 ? (
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

LineChart.propTypes = {
  buckets: PropTypes.arrayOf(PropTypes.shape({ startUtc: PropTypes.string.isRequired, endUtc: PropTypes.string, extra: PropTypes.array })).isRequired,
  series: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, color: PropTypes.string.isRequired, values: PropTypes.arrayOf(PropTypes.number).isRequired })).isRequired,
  bucketMinutes: PropTypes.number.isRequired,
  height: PropTypes.number,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func,
  onBucketClick: PropTypes.func,
  area: PropTypes.bool
};
