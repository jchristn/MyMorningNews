import PropTypes from 'prop-types';

// Labeled field with optional help, error, and badges. The child control should carry id={id}.
export default function FormField({ id, label, help, error, badges = null, children, wide = false }) {
  return (
    <div className={'form-field' + (wide ? ' wide' : '') + (error ? ' has-error' : '')}>
      <label htmlFor={id} className="field-label">
        {label}
        {badges}
      </label>
      {children}
      {error ? (
        <span className="field-error" role="alert">
          {error}
        </span>
      ) : help ? (
        <span className="field-help muted">{help}</span>
      ) : null}
    </div>
  );
}

FormField.propTypes = {
  id: PropTypes.string.isRequired,
  label: PropTypes.string.isRequired,
  help: PropTypes.string,
  error: PropTypes.string,
  badges: PropTypes.node,
  children: PropTypes.node,
  wide: PropTypes.bool
};
