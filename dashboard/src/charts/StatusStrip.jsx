import PropTypes from 'prop-types';
import { useLocale } from '../i18n/index.js';
import { formatNumber, formatPercent } from '../i18n/formatters.js';

// One proportional bar split by status, with a legend that carries the counts (color is never the only signal).
export default function StatusStrip({ segments, ariaLabel, onSegmentClick }) {
  const { locale } = useLocale();
  const total = segments.reduce((s, x) => s + (x.value || 0), 0);
  return (
    <div className="status-strip" aria-label={ariaLabel} role="group">
      <div className="status-strip-bar">
        {segments.map((s) =>
          s.value > 0 ? (
            <span key={s.key} style={{ width: (s.value / Math.max(1, total)) * 100 + '%', background: s.color }} title={s.label + ': ' + formatNumber(s.value, locale)} />
          ) : null
        )}
      </div>
      <div className="status-strip-legend">
        {segments.map((s) => (
          <button type="button" key={s.key} className="legend-item" onClick={() => onSegmentClick && onSegmentClick(s)} disabled={!onSegmentClick}>
            <i style={{ background: s.color }} />
            <span>{s.label}</span>
            <b>{formatNumber(s.value, locale)}</b>
            <span className="muted">{total > 0 ? formatPercent(s.value / total, locale, 0) : ''}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

StatusStrip.propTypes = {
  segments: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, value: PropTypes.number, color: PropTypes.string.isRequired })).isRequired,
  ariaLabel: PropTypes.string.isRequired,
  onSegmentClick: PropTypes.func
};
