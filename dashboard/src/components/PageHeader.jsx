import PropTypes from 'prop-types';

// Page title, summary, meta, and actions. Title and summary may be user content (a topic name or description) in a
// different script from the interface, so their direction follows their own text.
export default function PageHeader({ title, summary, actions = null, meta = null }) {
  return (
    <header className="page-header">
      <div className="page-header-text">
        <h1 dir="auto">{title}</h1>
        {summary ? (
          <p className="muted" dir="auto">
            {summary}
          </p>
        ) : null}
        {meta}
      </div>
      {actions ? <div className="page-header-actions">{actions}</div> : null}
    </header>
  );
}

PageHeader.propTypes = {
  title: PropTypes.string.isRequired,
  summary: PropTypes.string,
  actions: PropTypes.node,
  meta: PropTypes.node
};
