import PropTypes from 'prop-types';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// One time range control for a group of charts, shown above them rather than inside any one card: the range buttons
// (for example last week, month, quarter) and a refresh button that reloads every chart in the group.
export default function ChartRangeBar({ ranges, rangeId, onRangeChange, onRefresh = null, loading = false }) {
  const { t } = useTranslation();
  return (
    <div className="chart-range-bar" data-testid="chart-range-bar">
      <span className="chart-range-label" id="chart-range-label">
        {t('charts.range')}
      </span>
      <div className="segmented" role="group" aria-labelledby="chart-range-label">
        {ranges.map((id) => (
          <button type="button" key={id} className={rangeId === id ? 'active' : ''} aria-pressed={rangeId === id} onClick={() => onRangeChange(id)}>
            {t('charts.ranges.' + id)}
          </button>
        ))}
      </div>
      {onRefresh ? (
        <button type="button" className="icon-button" onClick={onRefresh} aria-label={t('common.refresh')} title={t('common.refresh')}>
          <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
        </button>
      ) : null}
    </div>
  );
}

ChartRangeBar.propTypes = {
  ranges: PropTypes.arrayOf(PropTypes.string).isRequired,
  rangeId: PropTypes.string.isRequired,
  onRangeChange: PropTypes.func.isRequired,
  onRefresh: PropTypes.func,
  loading: PropTypes.bool
};
