import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Ban, Braces, CircleCheck, Eye, Lightbulb, Repeat, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';

export const canConfirm = (r) => r.status === 'Clarifying' && !r.outOfScopeReason;
export const canCancel = (r) => r.status === 'Clarifying' || r.status === 'Scheduled';
export const canConvert = (r) => r.status !== 'Declined' && !r.convertedTopicId;

// A future run time stays; a past one runs now.
export function confirmTime(r, now = Date.now()) {
  return r && r.runAtUtc && new Date(r.runAtUtc).getTime() > now ? r.runAtUtc : null;
}

// Open, Confirm, Cancel, Make recurring, Open topic, View JSON, and Delete for one-time requests.
export default function useRequestActions({ onChanged, onDeleted } = {}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [converting, setConverting] = useState(null);

  async function act(work, successKey) {
    try {
      const result = await work();
      if (successKey) addNotification(t(successKey), 'success');
      if (onChanged) onChanged(result);
      return result;
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
      return null;
    }
  }

  const confirm = (r, runAtUtc) => act(() => apiClient.confirmResearchRequest(r.id, runAtUtc === undefined ? confirmTime(r) : runAtUtc), 'oneTime.actions.confirmed');
  const cancel = (r) => act(() => apiClient.cancelResearchRequest(r.id), 'oneTime.actions.cancelled');

  function actionsFor(r) {
    return [
      { key: 'open', label: t('oneTime.actions.open'), icon: Eye, onClick: () => navigate('/dashboard/one-time/' + encodeURIComponent(r.id)) },
      { key: 'confirm', label: t('oneTime.actions.confirm'), icon: CircleCheck, hidden: !canConfirm(r), onClick: () => confirm(r) },
      { key: 'cancel', label: t('oneTime.actions.cancel'), icon: Ban, hidden: !canCancel(r), onClick: () => cancel(r) },
      { key: 'convert', label: t('oneTime.actions.convert'), icon: Repeat, hidden: !canConvert(r), onClick: () => setConverting(r) },
      { key: 'topic', label: t('oneTime.actions.openTopic'), icon: Lightbulb, hidden: !r.convertedTopicId, onClick: () => navigate('/dashboard/topics/' + encodeURIComponent(r.convertedTopicId)) },
      { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
      { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(r) }
    ];
  }

  const modals = (
    <>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(converting)}
        title={t('oneTime.convert.title')}
        message={converting ? t('oneTime.convert.message', { title: converting.title || converting.requestText }) : ''}
        confirmLabel={t('oneTime.actions.convert')}
        onClose={() => setConverting(null)}
        onConfirm={async () => {
          const topic = await apiClient.convertResearchRequest(converting.id);
          addNotification(t('oneTime.convert.done'), 'success');
          navigate('/dashboard/topics/' + encodeURIComponent(topic.id) + '?tab=schedule');
        }}
      />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('oneTime.delete.title')}
        message={deleting ? t('oneTime.delete.message', { title: deleting.title || deleting.requestText }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteResearchRequest(deleting.id);
          addNotification(t('oneTime.delete.done'), 'success');
          if (onDeleted) onDeleted(deleting);
          else if (onChanged) onChanged(null);
        }}
      />
    </>
  );

  return { actionsFor, modals, confirm, cancel, setConverting, setDeleting, setJson };
}
