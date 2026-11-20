import { useCallback, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, MessageSquare, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { FEEDBACK_CHANNELS } from '../../utils/enums.js';
import { ACTION_TONES, actionOf, targetPath } from '../../utils/feedback.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import { FeedbackReasonModal } from '../../components/FeedbackControl.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export const FEEDBACK_TARGET_TYPES = ['Finding', 'Prediction', 'PredictionReview', 'Suggestion', 'SourceItem', 'Domain', 'Briefing', 'BriefingSection', 'LearnedPreference'];

// Every feedback item with its reason, target, channel, and rating, with Edit why and Retract. Pass topicId to scope
// to one topic.
export default function FeedbackTable({ topicId = null, tableKey = 'feedback' }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [includeHistory, setIncludeHistory] = useState(false);
  const [editing, setEditing] = useState(null);
  const [retracting, setRetracting] = useState(null);
  const [json, setJson] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const bump = () => setRefresh((n) => n + 1);
  const extraFilters = useMemo(() => ({ topicId: topicId || undefined, includeHistory: includeHistory || undefined }), [topicId, includeHistory]);
  const fetchPage = useCallback((q) => apiClient.listFeedback(pagedQuery(q)), [apiClient]);

  const rating = (fb) => {
    const action = actionOf(fb);
    return action ? <StatusBadge tone={ACTION_TONES[action]}>{t('feedbackControl.actions.' + action)}</StatusBadge> : <span className="muted">-</span>;
  };

  const columns = [
    { key: 'createdUtc', header: t('insights.feedback.columns.when'), sortKey: 'createdUtc', pinned: true, render: (r) => <DateCell value={r.createdUtc} /> },
    {
      key: 'target',
      header: t('insights.feedback.columns.target'),
      filter: { type: 'select', param: 'targetType', options: FEEDBACK_TARGET_TYPES.map((x) => ({ value: x, label: t('insights.feedback.targetTypes.' + x) })) },
      render: (r) => {
        const path = targetPath(r.targetType, r.targetId);
        return (
          <span className="date-cell">
            <span>{t('insights.feedback.targetTypes.' + r.targetType, { defaultValue: r.targetType })}</span>
            {path ? (
              <Link to={path} className="mono small">
                {r.targetId}
              </Link>
            ) : (
              <span className="mono small">{r.targetId}</span>
            )}
          </span>
        );
      }
    },
    { key: 'rating', header: t('insights.feedback.columns.rating'), render: rating },
    { key: 'reason', header: t('insights.feedback.columns.reason'), render: (r) => (r.reason ? <span className="feedback-reason">{r.reason}</span> : <span className="muted">{t('insights.feedback.noReason')}</span>) },
    {
      key: 'channel',
      header: t('insights.feedback.columns.channel'),
      filter: { type: 'select', param: 'channel', options: FEEDBACK_CHANNELS.map((x) => ({ value: x, label: t('insights.feedback.channels.' + x) })) },
      render: (r) => t('insights.feedback.channels.' + r.channel, { defaultValue: r.channel })
    },
    {
      key: 'current',
      header: t('insights.feedback.columns.current'),
      render: (r) => (r.isCurrent ? <StatusBadge tone="success">{t('insights.feedback.current')}</StatusBadge> : <StatusBadge>{t('insights.feedback.superseded')}</StatusBadge>)
    }
  ];

  return (
    <>
      <div className="toolbar-row">
        <label className="checkbox-row">
          <input type="checkbox" checked={includeHistory} onChange={(e) => setIncludeHistory(e.target.checked)} />
          <span>{t('insights.feedback.includeHistory')}</span>
        </label>
      </div>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={extraFilters}
        refreshSignal={refresh}
        defaultSort={{ key: 'createdUtc', direction: 'desc' }}
        rowActions={(r) => [
          { key: 'why', label: t('insights.feedback.editWhy'), icon: MessageSquare, disabled: !r.isCurrent, onClick: () => setEditing(r) },
          { key: 'retract', label: t('feedbackControl.retract'), icon: Undo2, danger: true, disabled: !r.isCurrent, onClick: () => setRetracting(r) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
        ]}
        emptyTitle={t('insights.feedback.emptyTitle')}
        emptyMessage={t('insights.feedback.emptyMessage')}
      />
      {editing ? (
        <FeedbackReasonModal
          feedback={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            addNotification(t('insights.feedback.saved'), 'success');
            bump();
          }}
        />
      ) : null}
      <ConfirmModal
        open={Boolean(retracting)}
        title={t('insights.feedback.retractTitle')}
        message={t('insights.feedback.retractMessage')}
        confirmLabel={t('feedbackControl.retract')}
        danger
        onClose={() => setRetracting(null)}
        onConfirm={async () => {
          await apiClient.retractFeedback(retracting.id);
          addNotification(t('insights.feedback.retracted'), 'success');
          bump();
        }}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
    </>
  );
}

FeedbackTable.propTypes = { topicId: PropTypes.string, tableKey: PropTypes.string };
