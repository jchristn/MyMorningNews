import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from './Modal.jsx';
import ErrorState from './ErrorState.jsx';

// Custom confirmation dialog. Never use window.confirm. onConfirm may return a promise; errors are shown inline.
export default function ConfirmModal({ open, title, message, confirmLabel, danger = false, onConfirm, onClose, children = null }) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function confirm() {
    setBusy(true);
    setError(null);
    try {
      await onConfirm();
      onClose();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={open}
      title={title}
      onClose={() => !busy && onClose()}
      size="small"
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className={danger ? 'button danger' : 'button primary'} onClick={confirm} disabled={busy}>
            {busy ? t('common.working') : confirmLabel || t('common.confirm')}
          </button>
        </>
      }
    >
      <p>{message}</p>
      {children}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
    </Modal>
  );
}

ConfirmModal.propTypes = {
  open: PropTypes.bool.isRequired,
  title: PropTypes.string.isRequired,
  message: PropTypes.string.isRequired,
  confirmLabel: PropTypes.string,
  danger: PropTypes.bool,
  onConfirm: PropTypes.func.isRequired,
  onClose: PropTypes.func.isRequired,
  children: PropTypes.node
};
