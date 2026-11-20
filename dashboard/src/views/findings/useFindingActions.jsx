import { useState } from 'react';
import { Eye, EyeOff, Pin, PinOff, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';

// Finding actions shared by the findings table and the finding detail: Pin or Unpin, Hide or Unhide, and Delete
// (confirmed). onChanged receives the updated finding; onDeleted receives the deleted one.
export default function useFindingActions({ onChanged, onDeleted } = {}) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [deleting, setDeleting] = useState(null);
  const [busy, setBusy] = useState(null);

  async function update(finding, body, successKey) {
    setBusy(finding.id);
    try {
      const result = await apiClient.updateFinding(finding.id, body);
      addNotification(t(successKey), 'success');
      if (onChanged) onChanged(result || { ...finding, ...body });
      return result;
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
      return null;
    } finally {
      setBusy(null);
    }
  }

  const togglePin = (finding) => update(finding, { pinned: !finding.pinned }, finding.pinned ? 'findings.actions.unpinned' : 'findings.actions.pinned');
  const toggleHidden = (finding) => update(finding, { hidden: !finding.hidden }, finding.hidden ? 'findings.actions.unhidden' : 'findings.actions.hidden');

  function actionsFor(finding) {
    const working = busy === finding.id;
    return [
      { key: 'pin', label: finding.pinned ? t('findings.actions.unpin') : t('findings.actions.pin'), icon: finding.pinned ? PinOff : Pin, disabled: working, onClick: () => togglePin(finding) },
      { key: 'hide', label: finding.hidden ? t('findings.actions.unhide') : t('findings.actions.hide'), icon: finding.hidden ? Eye : EyeOff, disabled: working, onClick: () => toggleHidden(finding) },
      { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: working, onClick: () => setDeleting(finding) }
    ];
  }

  const modals = (
    <ConfirmModal
      open={Boolean(deleting)}
      title={t('findings.actions.deleteTitle')}
      message={deleting ? t('findings.actions.deleteMessage', { title: deleting.title }) : ''}
      confirmLabel={t('common.delete')}
      danger
      onClose={() => setDeleting(null)}
      onConfirm={async () => {
        const target = deleting;
        await apiClient.deleteFinding(target.id);
        addNotification(t('findings.actions.deleted'), 'success');
        if (onDeleted) onDeleted(target);
      }}
    />
  );

  return { actionsFor, togglePin, toggleHidden, requestDelete: setDeleting, busy, modals };
}
