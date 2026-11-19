import { useState } from 'react';
import PropTypes from 'prop-types';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';

const ROW = 24;
const BAR = 12;
const TOP = 4;
const BOTTOM = 18;

// Run stages as a timeline: one row per stage, each bar placed at the stage's offset from the run start and sized by
// its duration, colored by status. The duration is written beside every bar and each row is focusable with a
// tooltip, so color is never the only signal. items: [{ key, label, offsetMs, durationMs, color, durationText, lines }].
export default function StageTimeline({ items, ariaLabel, formatDuration }) {
  const [frameRef, width] = useChartWidth(560);
  const [tip, setTip] = useState(null);
  const [active, setActive] = useState(null);
  const labelWidth = Math.min(150, Math.max(90, Math.max(0, ...items.map((s) => s.label.length)) * 7 + 12));
  const valueWidth = 64;
  const plotWidth = Math.max(60, width - labelWidth - valueWidth - 8);
  const span = Math.max(1, ...items.map((s) => (s.offsetMs || 0) + (s.durationMs || 0)));
  const height = TOP + items.length * ROW + BOTTOM;
  const x = (ms) => labelWidth + (Math.max(0, ms) / span) * plotWidth;

  function show(point, item) {
    setActive(item.key);
    setTip({ x: point.clientX, y: point.clientY, title: item.label, lines: item.lines || [] });
  }

  function hide() {
    setActive(null);
    setTip(null);
  }

  return (
    <div className="stage-timeline" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[0, 0.5, 1].map((ratio) => (
          <g key={ratio}>
            <line x1={x(span * ratio)} x2={x(span * ratio)} y1={TOP} y2={height - BOTTOM} stroke="var(--color-border)" strokeDasharray={ratio === 0 ? undefined : '2 4'} />
            <text x={x(span * ratio)} y={height - 4} fontSize="10" textAnchor={ratio === 0 ? 'start' : ratio === 1 ? 'end' : 'middle'} fill="var(--color-text-muted)">
              {formatDuration(span * ratio)}
            </text>
          </g>
        ))}
        {items.map((item, i) => {
          const y = TOP + i * ROW;
          const barX = x(item.offsetMs || 0);
          const barWidth = Math.max(2, x((item.offsetMs || 0) + (item.durationMs || 0)) - barX);
          return (
            <g
              key={item.key}
              className="chart-slot"
              data-stage={item.key}
              tabIndex={0}
              aria-label={item.label + ': ' + (item.lines || []).map((l) => l.label + ' ' + l.value).join(', ')}
              onMouseMove={(e) => show(e, item)}
              onMouseLeave={hide}
              onFocus={(e) => {
                const r = e.currentTarget.getBoundingClientRect();
                show({ clientX: r.left + r.width / 2, clientY: r.top }, item);
              }}
              onBlur={hide}
            >
              <rect x={0} y={y} width={width} height={ROW} fill={active === item.key ? 'var(--color-row-hover)' : 'transparent'} />
              <text x={labelWidth - 8} y={y + ROW / 2 + 4} fontSize="12" textAnchor="end" fill="var(--color-text-secondary)">
                {item.label}
              </text>
              <rect x={barX} y={y + (ROW - BAR) / 2} width={barWidth} height={BAR} rx="3" fill={item.color} />
              <text x={labelWidth + plotWidth + 8} y={y + ROW / 2 + 4} fontSize="11" fill="var(--color-text)">
                {item.durationText}
              </text>
            </g>
          );
        })}
      </svg>
      <ChartTooltip tip={tip} />
    </div>
  );
}

StageTimeline.propTypes = {
  items: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string.isRequired,
      label: PropTypes.string.isRequired,
      offsetMs: PropTypes.number,
      durationMs: PropTypes.number,
      color: PropTypes.string.isRequired,
      durationText: PropTypes.string,
      lines: PropTypes.arrayOf(PropTypes.shape({ label: PropTypes.string, value: PropTypes.string, color: PropTypes.string }))
    })
  ).isRequired,
  ariaLabel: PropTypes.string.isRequired,
  formatDuration: PropTypes.func.isRequired
};
