import PropTypes from 'prop-types';

export default function PageHeader({ title, summary, actions = null, meta = null }) {
  return (
    <header className="page-header">
      <div className="page-header-text">
        <h1>{title}</h1>
        {summary ? <p className="muted">{summary}</p> : null}
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
