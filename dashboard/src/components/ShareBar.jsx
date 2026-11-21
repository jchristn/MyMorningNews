import PropTypes from 'prop-types';
import useFormat from '../hooks/useFormat.js';

// A thin proportional bar with the percent beside it, for a part's share of a whole (a stage's share of a run's
// cost). share is 0 to 1; null shows a dash. top marks the largest part in the accent color.
export default function ShareBar({ share, top = false }) {
  const f = useFormat();
  if (share === null || share === undefined || Number.isNaN(Number(share))) return <span className="muted">-</span>;
  const value = Math.max(0, Math.min(1, Number(share)));
  return (
    <span className={'share-bar' + (top ? ' top' : '')}>
      <span className="share-bar-track" aria-hidden="true">
        <span className="share-bar-fill" style={{ width: value * 100 + '%' }} />
      </span>
      <span className="share-bar-value">{f.percent(value, value > 0 && value < 0.1 ? 1 : 0)}</span>
    </span>
  );
}

ShareBar.propTypes = { share: PropTypes.number, top: PropTypes.bool };
