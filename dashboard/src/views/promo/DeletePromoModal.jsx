import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';

// Delete when unused. A used code keeps its history, so the dialog offers to disable it instead; a 409 from a race
// is shown as the same advice. onDone receives 'deleted' or 'disabled'.
export default function DeletePromoModal({ stats, onClose, onDone }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const code = stats ? stats.code : null;
  const used = Boolean(code && code.redemptionCount > 0);
  let message = '';
  if (code) message = used ? t('promo.delete.usedMessage', { code: code.code, count: code.redemptionCount }) : t('promo.delete.message', { code: code.code });
  return (
    <ConfirmModal
      open={Boolean(stats)}
      title={t('promo.delete.title')}
      message={message}
      confirmLabel={used ? (code.enabled ? t('promo.delete.disableInstead') : t('common.close')) : t('common.delete')}
      danger={!used}
      onClose={onClose}
      onConfirm={async () => {
        if (used) {
          if (code.enabled) {
            await apiClient.updatePromoCode(code.id, { enabled: false });
            onDone('disabled');
          }
          return;
        }
        try {
          await apiClient.deletePromoCode(code.id);
        } catch (err) {
          if (err instanceof ApiError && err.status === 409) throw new ApiError(409, err.body, t('promo.delete.conflict'));
          throw err;
        }
        onDone('deleted');
      }}
    />
  );
}

DeletePromoModal.propTypes = { stats: PropTypes.object, onClose: PropTypes.func.isRequired, onDone: PropTypes.func.isRequired };
