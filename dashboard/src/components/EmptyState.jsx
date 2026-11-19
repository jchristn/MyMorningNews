import PropTypes from 'prop-types';
import { Inbox } from 'lucide-react';

export default function EmptyState({ title, message, icon: Icon = Inbox, action = null }) {
  return (
    <div className="state-block empty-state" role="status">
      <Icon size={28} aria-hidden="true" />
      <strong>{title}</strong>
      {message ? <p>{message}</p> : null}
      {action}
    </div>
  );
}

EmptyState.propTypes = {
  title: PropTypes.string.isRequired,
  message: PropTypes.string,
  icon: PropTypes.elementType,
  action: PropTypes.node
};
