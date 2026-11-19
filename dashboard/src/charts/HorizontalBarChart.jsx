import PropTypes from 'prop-types';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';

// Ranked horizontal bars. Labels truncate with the full value in a tooltip; values are right-aligned.
export default function HorizontalBarChart({ items, ariaLabel, formatValue, onItemClick, color = 'var(--chart-series-1)' }) {
  const { locale } = useLocale();
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const max = Math.max(1, ...items.map((i) => i.value || 0));
  return (
    <div className="hbar-chart" role="list" aria-label={ariaLabel}>
      {items.map((item) => {
        const pct = Math.max(0, Math.min(100, ((item.value || 0) / max) * 100));
        const row = (
          <>
            <span className="hbar-label" title={item.label}>
              {item.label}
            </span>
            <span className="hbar-track">
              <span className="hbar-fill" style={{ width: pct + '%', background: item.color || color }} />
            </span>
            <span className="hbar-value">{fmt(item.value || 0)}</span>
          </>
        );
        return onItemClick ? (
          <button type="button" role="listitem" key={item.key} className="hbar-row clickable" onClick={() => onItemClick(item)}>
            {row}
          </button>
        ) : (
          <div role="listitem" key={item.key} className="hbar-row">
            {row}
          </div>
        );
      })}
    </div>
  );
}

HorizontalBarChart.propTypes = {
  items: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, value: PropTypes.number, color: PropTypes.string })).isRequired,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func,
  onItemClick: PropTypes.func,
  color: PropTypes.string
};
