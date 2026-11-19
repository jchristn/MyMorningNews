import PropTypes from 'prop-types';

// Label and value pairs for detail modals and info pages.
export default function KeyValueGrid({ rows }) {
  return (
    <dl className="kv-grid">
      {rows
        .filter((r) => !r.hidden)
        .map((r) => (
          <div className="kv-row" key={r.key}>
            <dt>{r.label}</dt>
            <dd className={r.mono ? 'mono' : ''}>{r.value === null || r.value === undefined || r.value === '' ? <span className="muted">-</span> : r.value}</dd>
          </div>
        ))}
    </dl>
  );
}

KeyValueGrid.propTypes = {
  rows: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, value: PropTypes.node, mono: PropTypes.bool, hidden: PropTypes.bool })).isRequired
};
