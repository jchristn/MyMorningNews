import { useCallback } from 'react';
import PropTypes from 'prop-types';
import { Loader } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import { briefingFilename, saveBlob } from '../../utils/briefing.js';
import { ACTIVE_BRIEFING_STATUSES, BRIEFING_KIND_TONES, BRIEFING_STATUS_TONES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import StatusBadge from '../../components/StatusBadge.jsx';

export function BriefingKindBadge({ kind }) {
  const { t } = useTranslation();
  return <StatusBadge tone={BRIEFING_KIND_TONES[kind] || 'neutral'}>{t('enums.briefingKind.' + kind)}</StatusBadge>;
}

BriefingKindBadge.propTypes = { kind: PropTypes.string.isRequired };

export function BriefingStatusBadge({ status }) {
  const { t } = useTranslation();
  return (
    <StatusBadge tone={BRIEFING_STATUS_TONES[status] || 'neutral'}>
      {ACTIVE_BRIEFING_STATUSES.includes(status) ? <Loader size={11} className="spin" aria-hidden="true" /> : null}
      {t('enums.briefingStatus.' + status)}
    </StatusBadge>
  );
}

BriefingStatusBadge.propTypes = { status: PropTypes.string.isRequired };

export function isExpiredError(err) {
  return err instanceof ApiError && (err.status === 410 || err.code === 'ArtifactExpired');
}

// Download, pin, and regenerate for a briefing, with notifications. Each returns a promise; download resolves to
// 'expired' when the file passed retention so the caller can offer Regenerate. Errors other than expiry and the pin
// cap are rethrown after the notification.
export default function useBriefingActions() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();

  const download = useCallback(
    async (briefing, format) => {
      try {
        const file = await apiClient.downloadBriefing(briefing.id, format);
        saveBlob(file.filename || briefingFilename(briefing, format), file.blob);
        return 'ok';
      } catch (err) {
        if (isExpiredError(err)) {
          addNotification(t('briefings.expired.notice'), 'warning', 8000);
          return 'expired';
        }
        addNotification(describeError(err, t).message, 'error', 8000);
        return 'error';
      }
    },
    [apiClient, addNotification, t]
  );

  const setPinned = useCallback(
    async (briefing, pinned) => {
      try {
        const updated = await apiClient.pinBriefing(briefing.id, pinned);
        addNotification(pinned ? t('briefings.pin.pinned') : t('briefings.pin.unpinned'), 'success');
        return updated || { ...briefing, pinned };
      } catch (err) {
        if (err instanceof ApiError && (err.status === 429 || err.code === 'QuotaExceeded')) addNotification(t('briefings.pin.cap'), 'warning', 8000);
        else addNotification(describeError(err, t).message, 'error', 8000);
        return null;
      }
    },
    [apiClient, addNotification, t]
  );

  const regenerate = useCallback(
    async (briefing) => {
      try {
        const updated = await apiClient.regenerateBriefing(briefing.id);
        addNotification(t('briefings.expired.regenerated'), 'success');
        return updated || briefing;
      } catch (err) {
        addNotification(describeError(err, t).message, 'error', 8000);
        return null;
      }
    },
    [apiClient, addNotification, t]
  );

  return { download, setPinned, regenerate };
}
