import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { useApp } from '../../context/AppContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';

// Automatic audio for a topic: when on, every briefing from the topic's research runs is also made into audio, read in
// the topic's voice. Saved on the topic right away. Shown beside the schedule (where runs are planned) and with the
// voice (where the audio is shaped), so it is found from either side.
export default function TopicAudio({ topic, onTopicUpdated, disabled = false, idPrefix = 'topic-audio' }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const on = Boolean(topic.audioEnabled);

  async function set(next) {
    setBusy(true);
    setError(null);
    try {
      const updated = await apiClient.updateTopic(topic.id, { audioEnabled: next });
      onTopicUpdated(updated);
      addNotification(t(next ? 'topicAudio.savedOn' : 'topicAudio.savedOff', { name: topic.name }), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="stack" data-testid="topic-audio">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <FormField id={idPrefix + '-enabled'} label={t('topicAudio.label')} help={t('topicAudio.help')}>
        <label className="checkbox-row">
          <input id={idPrefix + '-enabled'} type="checkbox" checked={on} disabled={disabled || busy} onChange={(e) => set(e.target.checked)} />
          <span>{on ? t('common.yes') : t('common.no')}</span>
        </label>
      </FormField>
    </div>
  );
}

TopicAudio.propTypes = {
  topic: PropTypes.shape({ id: PropTypes.string.isRequired, name: PropTypes.string, audioEnabled: PropTypes.bool }).isRequired,
  onTopicUpdated: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  idPrefix: PropTypes.string
};
