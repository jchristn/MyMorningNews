import PropTypes from 'prop-types';
import OwnerSchedules from '../../components/OwnerSchedules.jsx';

// The research schedules of a topic. See OwnerSchedules for single, onSaved, disabled, and controlRef.
export default function TopicSchedules({ topicId, ...rest }) {
  return <OwnerSchedules ownerType="Topic" ownerId={topicId} {...rest} />;
}

TopicSchedules.propTypes = { topicId: PropTypes.string.isRequired };
