import PropTypes from 'prop-types';

// KPI tile. Clickable when onClick is set (it summarizes a navigable resource).
export default function KpiTile({ label, value, detail, tone = 'neutral', icon: Icon = null, onClick }) {
  const content = (
    <>
      <span className="kpi-label">
        {Icon ? <Icon size={14} aria-hidden="true" /> : null}
        {label}
      </span>
      <span className="kpi-value">{value}</span>
      {detail ? <span className="kpi-detail muted">{detail}</span> : null}
    </>
  );
  if (onClick) {
    return (
      <button type="button" className={'kpi-tile clickable tone-' + tone} onClick={onClick}>
        {content}
      </button>
    );
  }
  return <div className={'kpi-tile tone-' + tone}>{content}</div>;
}

KpiTile.propTypes = {
  label: PropTypes.string.isRequired,
  value: PropTypes.node,
  detail: PropTypes.node,
  tone: PropTypes.oneOf(['neutral', 'success', 'warning', 'danger', 'info', 'accent']),
  icon: PropTypes.elementType,
  onClick: PropTypes.func
};
