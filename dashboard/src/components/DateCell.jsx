import PropTypes from 'prop-types';
import useFormat from '../hooks/useFormat.js';

// Readable timestamp with relative time; the exact ISO value is in the tooltip.
export default function DateCell({ value, relative = true }) {
  const f = useFormat();
  if (!value) return <span className="muted">-</span>;
  return (
    <span className="date-cell" title={new Date(value).toISOString()}>
      <span>{f.dateTime(value)}</span>
      {relative ? <span className="muted small">{f.relative(value)}</span> : null}
    </span>
  );
}

DateCell.propTypes = {
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.instanceOf(Date)]),
  relative: PropTypes.bool
};
