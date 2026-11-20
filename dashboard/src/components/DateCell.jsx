import PropTypes from 'prop-types';
import useFormat from '../hooks/useFormat.js';

const SHORT = { dateStyle: 'short', timeStyle: 'short' };

// Readable timestamp to the minute with relative time; the exact ISO value is in the tooltip. Minutes keep table
// columns narrow.
export default function DateCell({ value, relative = true }) {
  const f = useFormat();
  if (!value) return <span className="muted">-</span>;
  return (
    <span className="date-cell" title={new Date(value).toISOString()}>
      <span>{f.dateTime(value, SHORT)}</span>
      {relative ? <span className="muted small">{f.relative(value)}</span> : null}
    </span>
  );
}

DateCell.propTypes = {
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.instanceOf(Date)]),
  relative: PropTypes.bool
};
