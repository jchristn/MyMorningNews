import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';

// Tooltip drawn on the document body so card overflow cannot clip it; it stays inside the window.
export default function ChartTooltip({ tip }) {
  if (!tip) return null;
  const left = Math.max(8, Math.min(tip.x + 12, window.innerWidth - 260));
  const top = Math.max(8, Math.min(tip.y + 12, window.innerHeight - 48 - tip.lines.length * 18));
  return createPortal(
    <div className="chart-tooltip" role="tooltip" style={{ left, top }}>
      {tip.title ? <strong>{tip.title}</strong> : null}
      {tip.lines.map((line) => (
        <div className="chart-tooltip-line" key={line.label}>
          {line.color ? <i style={{ background: line.color }} /> : null}
          <span>{line.label}</span>
          <b>{line.value}</b>
        </div>
      ))}
    </div>,
    document.body
  );
}

ChartTooltip.propTypes = {
  tip: PropTypes.shape({
    x: PropTypes.number,
    y: PropTypes.number,
    title: PropTypes.string,
    lines: PropTypes.arrayOf(PropTypes.shape({ label: PropTypes.string, value: PropTypes.string, color: PropTypes.string }))
  })
};
