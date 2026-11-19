import PropTypes from 'prop-types';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { RANGE_IDS } from './ranges.js';

// Card around a chart: title, optional totals, range control (hour, day, week, month), and manual refresh.
export default function ChartCard({ title, rangeId, onRangeChange, onRefresh, loading = false, totals = null, children, ranges = RANGE_IDS }) {
  const { t } = useTranslation();
  return (
    <section className="panel chart-card">
      <div className="chart-card-header">
        <h2>{title}</h2>
        <div className="chart-card-controls">
          {onRangeChange ? (
            <div className="segmented" role="group" aria-label={t('charts.range')}>
              {ranges.map((id) => (
                <button type="button" key={id} className={rangeId === id ? 'active' : ''} aria-pressed={rangeId === id} onClick={() => onRangeChange(id)}>
                  {t('charts.ranges.' + id)}
                </button>
              ))}
            </div>
          ) : null}
          {onRefresh ? (
            <button type="button" className="icon-button" onClick={onRefresh} aria-label={t('common.refresh')} title={t('common.refresh')}>
              <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
            </button>
          ) : null}
        </div>
      </div>
      {totals ? <div className="chart-totals">{totals}</div> : null}
      {children}
    </section>
  );
}

ChartCard.propTypes = {
  title: PropTypes.string.isRequired,
  rangeId: PropTypes.string,
  onRangeChange: PropTypes.func,
  onRefresh: PropTypes.func,
  loading: PropTypes.bool,
  totals: PropTypes.node,
  children: PropTypes.node,
  ranges: PropTypes.arrayOf(PropTypes.string)
};
