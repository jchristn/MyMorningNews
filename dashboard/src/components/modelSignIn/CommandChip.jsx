import PropTypes from 'prop-types';
import CopyButton from '../CopyButton.jsx';

// A command or path to type, in monospace with a copy button.
export default function CommandChip({ command }) {
  return (
    <span className="command-chip">
      <code>{command}</code>
      <CopyButton value={command} labelKey="setup.copyCommand" />
    </span>
  );
}

CommandChip.propTypes = { command: PropTypes.string.isRequired };
