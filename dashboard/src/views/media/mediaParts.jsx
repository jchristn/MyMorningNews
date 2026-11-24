import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Loader } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import { ACTIVE_MEDIA_STATUSES, MEDIA_STATUS_TONES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import StatusBadge from '../../components/StatusBadge.jsx';

export function MediaStatusBadge({ status }) {
  const { t } = useTranslation();
  return (
    <StatusBadge tone={MEDIA_STATUS_TONES[status] || 'neutral'}>
      {ACTIVE_MEDIA_STATUSES.includes(status) ? <Loader size={11} className="spin" aria-hidden="true" /> : null}
      {t('enums.mediaStatus.' + status)}
    </StatusBadge>
  );
}

MediaStatusBadge.propTypes = { status: PropTypes.string.isRequired };

// The tenant's voice profiles for a picker, loaded once. A failed load leaves the list empty, so the picker still
// offers the server default.
export function useVoices() {
  const { apiClient } = useAuth();
  const [voices, setVoices] = useState(null);
  useEffect(() => {
    let cancelled = false;
    apiClient
      .listVoices({ pageSize: 100, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setVoices((r && r.objects) || []))
      .catch(() => !cancelled && setVoices([]));
    return () => {
      cancelled = true;
    };
  }, [apiClient]);
  return voices;
}

// A voice select whose first option is the default voice (the tenant default, else the server default).
export function VoicePicker({ id, value, onChange, voices, disabled = false }) {
  const { t } = useTranslation();
  return (
    <select id={id} value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled || voices === null} aria-label={t('audio.make.voice')}>
      <option value="">{t('audio.make.defaultVoice')}</option>
      {(voices || []).map((v) => (
        <option key={v.id} value={v.id}>
          {v.isDefault ? t('audio.make.voiceDefault', { name: v.name }) : v.name}
        </option>
      ))}
    </select>
  );
}

VoicePicker.propTypes = { id: PropTypes.string, value: PropTypes.string.isRequired, onChange: PropTypes.func.isRequired, voices: PropTypes.array, disabled: PropTypes.bool };

// Queue audio for a briefing with notifications. Resolves to the queued episode, or null when it was not queued (409
// means audio is already being made or audio is off on the server; the server's message says which).
export function useMakeAudio() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  return useCallback(
    async (briefingId, voiceProfileId) => {
      try {
        const queued = await apiClient.generateBriefingAudio(briefingId, voiceProfileId || null);
        addNotification(t('audio.make.queued'), 'success');
        return queued || null;
      } catch (err) {
        if (err instanceof ApiError && err.status === 409) addNotification(t('audio.make.conflict', { message: err.message || '' }), 'warning', 8000);
        else addNotification(describeError(err, t).message, 'error', 8000);
        return null;
      }
    },
    [apiClient, addNotification, t]
  );
}
