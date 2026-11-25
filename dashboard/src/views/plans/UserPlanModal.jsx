import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from '../../components/Modal.jsx';
import usePlans from '../promo/usePlans.js';
import UserPlanManager from './UserPlanManager.jsx';

// The Plan drawer for one user on the Users page (global administrators): summary, plan change, limits, history.
export default function UserPlanModal({ tenantId, user, onClose }) {
  const { t } = useTranslation();
  const { plans } = usePlans();

  return (
    <Modal
      open
      drawer
      title={t('plans.userPanel.title', { email: user.email || user.id })}
      subtitle={t('plans.userPanel.subtitle')}
      onClose={onClose}
      footer={
        <button type="button" className="button primary" onClick={onClose}>
          {t('common.close')}
        </button>
      }
    >
      <UserPlanManager tenantId={tenantId} userId={user.id} email={user.email || ''} plans={plans} />
    </Modal>
  );
}

UserPlanModal.propTypes = { tenantId: PropTypes.string.isRequired, user: PropTypes.object.isRequired, onClose: PropTypes.func.isRequired };
