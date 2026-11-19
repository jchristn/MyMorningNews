import PropTypes from 'prop-types';

// Pill for statuses, roles, methods, and severities. Tone is never the only signal: the label is always shown.
export default function StatusBadge({ tone = 'neutral', children, title, mono = false }) {
  return (
    <span className={'badge badge-' + tone + (mono ? ' mono' : '')} title={title}>
      {children}
    </span>
  );
}

StatusBadge.propTypes = {
  tone: PropTypes.oneOf(['neutral', 'success', 'warning', 'danger', 'info', 'accent']),
  children: PropTypes.node,
  title: PropTypes.string,
  mono: PropTypes.bool
};

export function statusTone(code) {
  const n = Number(code);
  if (n >= 500) return 'danger';
  if (n >= 400) return 'warning';
  if (n >= 300) return 'info';
  if (n >= 200) return 'success';
  return 'neutral';
}

export function methodTone(method) {
  switch (String(method || '').toUpperCase()) {
    case 'GET':
      return 'info';
    case 'POST':
      return 'success';
    case 'PUT':
    case 'PATCH':
      return 'warning';
    case 'DELETE':
      return 'danger';
    default:
      return 'neutral';
  }
}
