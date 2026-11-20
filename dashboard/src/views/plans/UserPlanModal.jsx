import { useState } from 'react';
import PropTypes from 'prop-types';
import { Gift } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import Modal from '../../components/Modal.jsx';
import usePlans from '../promo/usePlans.js';
import GrantPlanModal from './GrantPlanModal.jsx';
import UserPlanAssignments from './UserPlanAssignments.jsx';

// The Plan panel for one user on the Users page (global administrators): assignments with revoke, and a grant.
export default function UserPlanModal({ tenantId, user, onClose }) {
  const { t } = useTranslation();
  const { addNotification } = useApp();
  const { plans } = usePlans();
  const [granting, setGranting] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const target = { tenantId, userId: user.id, email: user.email };

  return (
    <>
      <Modal
        open={!granting}
        size="large"
        title={t('plans.userPanel.title', { email: user.email })}
        onClose={onClose}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setGranting(true)} disabled={plans.length === 0}>
              <Gift size={14} aria-hidden="true" /> {t('plans.grant.open')}
            </button>
            <button type="button" className="button primary" onClick={onClose}>
              {t('common.close')}
            </button>
          </>
        }
      >
        <UserPlanAssignments tenantId={tenantId} userId={user.id} plans={plans} refreshSignal={refresh} />
      </Modal>
      {granting ? (
        <GrantPlanModal
          plans={plans}
          target={target}
          onClose={() => setGranting(false)}
          onGranted={() => {
            setGranting(false);
            setRefresh((n) => n + 1);
            addNotification(t('plans.grant.done', { email: user.email }), 'success');
          }}
        />
      ) : null}
    </>
  );
}

UserPlanModal.propTypes = { tenantId: PropTypes.string.isRequired, user: PropTypes.object.isRequired, onClose: PropTypes.func.isRequired };
