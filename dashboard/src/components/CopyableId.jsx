import PropTypes from 'prop-types';
import CopyButton from './CopyButton.jsx';

// Monospace identifier that never wraps, with a copy control.
export default function CopyableId({ value, labelKey = 'common.copyId', truncate = true }) {
  if (!value) return null;
  return (
    <span className={truncate ? 'copyable-id truncate' : 'copyable-id'} title={value}>
      <code>{value}</code>
      <CopyButton value={value} labelKey={labelKey} size={12} />
    </span>
  );
}

CopyableId.propTypes = {
  value: PropTypes.string,
  labelKey: PropTypes.string,
  truncate: PropTypes.bool
};
