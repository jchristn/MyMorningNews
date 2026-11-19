import { useState } from 'react';
import PropTypes from 'prop-types';
import { useLocale } from '../i18n/index.js';
import { formatNumber, formatPercent } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';

const TOP = 12;
const BOTTOM = 34;
const LEFT = 44;
const RIGHT = 12;

// Reliability diagram: stated confidence buckets on the x axis against the observed accuracy rate on the y axis.
// Columns show each bucket's observed rate, the dot sits at the bucket's mean confidence, and the dashed diagonal is
// perfect calibration. Every bucket is a focusable slot with a tooltip carrying the count and both rates.
// buckets: [{ index, from, to, count, meanConfidence, observedRate }] from calibrationBuckets.
export default function CalibrationChart({ buckets, ariaLabel, labels, height = 240 }) {
  const { locale } = useLocale();
  const [frameRef, width] = useChartWidth(480);
  const [tip, setTip] = useState(null);
  const [active, setActive] = useState(null);
  const plotWidth = Math.max(80, width - LEFT - RIGHT);
  const plotHeight = height - TOP - BOTTOM;
  const x = (v) => LEFT + Math.max(0, Math.min(1, v)) * plotWidth;
  const y = (v) => TOP + plotHeight - Math.max(0, Math.min(1, v)) * plotHeight;
  const pct = (v) => formatPercent(v, locale, 0);
  const filled = buckets.filter((b) => b.count > 0);
  const maxCount = Math.max(1, ...buckets.map((b) => b.count));

  function lines(b) {
    return [
      { label: labels.count, value: formatNumber(b.count, locale) },
      { label: labels.meanConfidence, value: b.meanConfidence === null ? '-' : pct(b.meanConfidence), color: 'var(--chart-series-1)' },
      { label: labels.observed, value: b.observedRate === null ? '-' : pct(b.observedRate), color: 'var(--chart-series-2)' }
    ];
  }

  function show(point, b) {
    setActive(b.index);
    setTip({ x: point.clientX, y: point.clientY, title: labels.bucket(b), lines: lines(b) });
  }

  function hide() {
    setActive(null);
    setTip(null);
  }

  return (
    <div className="calibration-chart" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[0, 0.25, 0.5, 0.75, 1].map((tick) => (
          <g key={tick}>
            <line x1={LEFT} x2={LEFT + plotWidth} y1={y(tick)} y2={y(tick)} stroke="var(--color-border)" strokeDasharray={tick === 0 ? undefined : '3 4'} />
            <text x={LEFT - 6} y={y(tick) + 3} fontSize="10" textAnchor="end" fill="var(--color-text-muted)">
              {pct(tick)}
            </text>
            <text x={x(tick)} y={TOP + plotHeight + 14} fontSize="10" textAnchor={tick === 0 ? 'start' : tick === 1 ? 'end' : 'middle'} fill="var(--color-text-muted)">
              {pct(tick)}
            </text>
          </g>
        ))}
        <text x={LEFT + plotWidth / 2} y={height - 4} fontSize="11" textAnchor="middle" fill="var(--color-text-secondary)">
          {labels.xAxis}
        </text>
        <line x1={x(0)} y1={y(0)} x2={x(1)} y2={y(1)} stroke="var(--color-text-muted)" strokeDasharray="5 4" data-series="perfect">
          <title>{labels.perfect}</title>
        </line>
        {buckets.map((b) => {
          const left = x(b.from) + 3;
          const w = Math.max(2, x(b.to) - x(b.from) - 6);
          return (
            <g key={b.index} data-bucket={b.index}>
              {b.count > 0 ? <rect x={left} y={y(b.observedRate)} width={w} height={Math.max(1, y(0) - y(b.observedRate))} fill="var(--chart-series-2)" opacity={active === b.index ? 0.35 : 0.2} /> : null}
              <rect
                className="chart-slot"
                x={x(b.from)}
                y={TOP}
                width={x(b.to) - x(b.from)}
                height={plotHeight}
                fill="transparent"
                tabIndex={0}
                aria-label={labels.bucket(b) + ': ' + lines(b).map((l) => l.label + ' ' + l.value).join(', ')}
                onMouseMove={(e) => show(e, b)}
                onMouseLeave={hide}
                onFocus={(e) => {
                  const r = e.currentTarget.getBoundingClientRect();
                  show({ clientX: r.left + r.width / 2, clientY: r.top }, b);
                }}
                onBlur={hide}
              />
            </g>
          );
        })}
        {filled.length > 1 ? (
          <polyline points={filled.map((b) => x(b.meanConfidence) + ',' + y(b.observedRate)).join(' ')} fill="none" stroke="var(--chart-series-1)" strokeWidth="2" strokeLinejoin="round" pointerEvents="none" />
        ) : null}
        {filled.map((b) => (
          <circle key={b.index} cx={x(b.meanConfidence)} cy={y(b.observedRate)} r={3 + (b.count / maxCount) * 4} fill="var(--chart-series-1)" stroke="var(--color-surface)" strokeWidth="1.5" pointerEvents="none" />
        ))}
      </svg>
      <div className="chart-legend">
        <span>
          <i style={{ background: 'var(--chart-series-1)' }} /> {labels.meanConfidence}
        </span>
        <span>
          <i style={{ background: 'var(--chart-series-2)', opacity: 0.5 }} /> {labels.observed}
        </span>
        <span>
          <i style={{ background: 'var(--color-text-muted)' }} /> {labels.perfect}
        </span>
      </div>
      <ChartTooltip tip={tip} />
    </div>
  );
}

CalibrationChart.propTypes = {
  buckets: PropTypes.arrayOf(
    PropTypes.shape({ index: PropTypes.number.isRequired, from: PropTypes.number.isRequired, to: PropTypes.number.isRequired, count: PropTypes.number.isRequired, meanConfidence: PropTypes.number, observedRate: PropTypes.number })
  ).isRequired,
  ariaLabel: PropTypes.string.isRequired,
  labels: PropTypes.shape({
    xAxis: PropTypes.string.isRequired,
    count: PropTypes.string.isRequired,
    meanConfidence: PropTypes.string.isRequired,
    observed: PropTypes.string.isRequired,
    perfect: PropTypes.string.isRequired,
    bucket: PropTypes.func.isRequired
  }).isRequired,
  height: PropTypes.number
};
