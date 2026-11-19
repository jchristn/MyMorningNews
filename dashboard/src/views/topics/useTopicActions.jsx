import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Archive, Braces, Copy, Eye, FlaskConical, Pause, Play, RotateCcw, Trash2, WandSparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';

// Topic actions shared by the Topics table and the topic workspace: View, Refine again, Run now, Dry run, Pause,
// Resume, Archive, Duplicate, View JSON, and Delete. onChanged runs after any change; onDeleted after a delete.
export default function useTopicActions({ onChanged, onDeleted } = {}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [busy, setBusy] = useState(null);

  async function act(topic, key, work, successKey) {
    setBusy(topic.id + key);
    try {
      const result = await work();
      if (successKey) addNotification(t(successKey, { name: topic.name }), 'success');
      if (onChanged) onChanged(result);
      return result;
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
      return null;
    } finally {
      setBusy(null);
    }
  }

  const run = (topic, dryRun) => act(topic, dryRun ? 'dry' : 'run', () => apiClient.runTopic(topic.id, { dryRun }), dryRun ? 'topics.actions.dryRunQueued' : 'topics.actions.runQueued');

  function actionsFor(topic) {
    const isDraft = topic.status === 'Draft';
    const working = busy && busy.startsWith(topic.id);
    return [
      { key: 'view', label: t('common.view'), icon: Eye, onClick: () => navigate('/dashboard/topics/' + encodeURIComponent(topic.id)) },
      {
        key: 'refine',
        label: isDraft ? t('topics.actions.continueRefining') : t('topics.actions.refine'),
        icon: WandSparkles,
        hidden: topic.isOneTime,
        onClick: () => navigate('/dashboard/topics/' + encodeURIComponent(topic.id) + '/refine' + (isDraft ? '' : '?start=1'))
      },
      { key: 'run', label: t('topics.actions.runNow'), icon: Play, disabled: working || isDraft || topic.status === 'Archived', onClick: () => run(topic, false) },
      { key: 'dry', label: t('topics.actions.dryRun'), icon: FlaskConical, disabled: working || topic.status === 'Archived', onClick: () => run(topic, true) },
      { key: 'pause', label: t('topics.actions.pause'), icon: Pause, hidden: topic.status !== 'Active', disabled: working, onClick: () => act(topic, 'pause', () => apiClient.pauseTopic(topic.id), 'topics.actions.paused') },
      {
        key: 'resume',
        label: t('topics.actions.resume'),
        icon: RotateCcw,
        hidden: topic.status !== 'Paused' && topic.status !== 'Archived',
        disabled: working,
        onClick: () => act(topic, 'resume', () => apiClient.resumeTopic(topic.id), 'topics.actions.resumed')
      },
      { key: 'archive', label: t('topics.actions.archive'), icon: Archive, hidden: topic.status === 'Archived' || isDraft, disabled: working, onClick: () => act(topic, 'archive', () => apiClient.archiveTopic(topic.id), 'topics.actions.archived') },
      {
        key: 'duplicate',
        label: t('common.duplicate'),
        icon: Copy,
        disabled: working,
        onClick: async () => {
          const copy = await act(topic, 'dup', () => apiClient.duplicateTopic(topic.id), 'topics.actions.duplicated');
          if (copy && copy.topic) navigate('/dashboard/topics/' + encodeURIComponent(copy.topic.id));
        }
      },
      { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(topic) },
      { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(topic) }
    ];
  }

  const modals = (
    <>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('topics.delete.title')}
        message={deleting ? t('topics.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteTopic(deleting.id);
          addNotification(t('topics.delete.done', { name: deleting.name }), 'success');
          if (onDeleted) onDeleted(deleting);
          else if (onChanged) onChanged(null);
        }}
      />
    </>
  );

  return { actionsFor, modals, run, setDeleting };
}
