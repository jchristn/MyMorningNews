import PropTypes from 'prop-types';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';
import useChartWidth from './useChartWidth.js';

const ROW = 26;
const BAR = 12;
const TOP = 18;
const BOTTOM = 18;

// Composite relevance components on a 0 to 100 scale, one bar per component, with the composite score drawn as a
// dashed marker across every bar. Each bar carries its value as text, so color is never the only signal.
export default function ScoreBreakdownChart({ components, composite, compositeLabel, ariaLabel }) {
  const { locale } = useLocale();
  const [frameRef, width] = useChartWidth(480);
  const labelWidth = Math.min(140, Math.max(84, Math.max(0, ...components.map((c) => c.label.length)) * 7 + 12));
  const valueWidth = 40;
  const plotWidth = Math.max(60, width - labelWidth - valueWidth - 8);
  const height = TOP + components.length * ROW + BOTTOM;
  const x = (v) => labelWidth + (Math.max(0, Math.min(100, Number(v) || 0)) / 100) * plotWidth;
  const fmt = (v) => formatNumber(Number(v) || 0, locale, { maximumFractionDigits: 1 });
  const hasComposite = composite !== null && composite !== undefined;

  return (
    <div className="score-breakdown" ref={frameRef}>
      <svg role="img" aria-label={ariaLabel} width={width} height={height} viewBox={'0 0 ' + width + ' ' + height}>
        {[0, 25, 50, 75, 100].map((tick) => (
          <g key={tick}>
            <line x1={x(tick)} x2={x(tick)} y1={TOP - 4} y2={height - BOTTOM} stroke="var(--color-border)" strokeDasharray={tick === 0 ? undefined : '2 4'} />
            <text x={x(tick)} y={height - 4} fontSize="10" textAnchor="middle" fill="var(--color-text-muted)">
              {fmt(tick)}
            </text>
          </g>
        ))}
        {components.map((c, i) => {
          const y = TOP + i * ROW + (ROW - BAR) / 2;
          return (
            <g key={c.key} data-component={c.key}>
              <title>{c.label + ': ' + fmt(c.value)}</title>
              <text x={labelWidth - 8} y={y + BAR - 2} fontSize="12" textAnchor="end" fill="var(--color-text-secondary)">
                {c.label}
              </text>
              <rect x={labelWidth} y={y} width={plotWidth} height={BAR} rx="3" fill="var(--color-surface-alt)" />
              <rect x={labelWidth} y={y} width={Math.max(0, x(c.value) - labelWidth)} height={BAR} rx="3" fill={c.color} />
              <text x={labelWidth + plotWidth + 8} y={y + BAR - 2} fontSize="12" fill="var(--color-text)" fontWeight="600">
                {fmt(c.value)}
              </text>
            </g>
          );
        })}
        {hasComposite ? (
          <g data-component="composite">
            <title>{compositeLabel + ': ' + fmt(composite)}</title>
            <line x1={x(composite)} x2={x(composite)} y1={TOP - 6} y2={height - BOTTOM} stroke="var(--color-text)" strokeWidth="1.5" strokeDasharray="4 3" />
            <text x={x(composite)} y={TOP - 8} fontSize="10" textAnchor={composite > 80 ? 'end' : composite < 20 ? 'start' : 'middle'} fill="var(--color-text)">
              {compositeLabel + ' ' + fmt(composite)}
            </text>
          </g>
        ) : null}
      </svg>
    </div>
  );
}

ScoreBreakdownChart.propTypes = {
  components: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, value: PropTypes.number, color: PropTypes.string.isRequired })).isRequired,
  composite: PropTypes.number,
  compositeLabel: PropTypes.string,
  ariaLabel: PropTypes.string.isRequired
};
