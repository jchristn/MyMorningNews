import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { AlarmClock, Braces, Check, CircleCheckBig, Eye, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { SUGGESTION_PRIORITY_TONES, SUGGESTION_STATUSES, SUGGESTION_STATUS_TONES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import SnoozeModal from './SnoozeModal.jsx';

// Status changes a person can make, in menu order. Each is hidden when the suggestion already has that status.
const ACTIONS = [
  { key: 'accept', status: 'Accepted', icon: Check },
  { key: 'snooze', status: 'Snoozed', icon: AlarmClock },
  { key: 'dismiss', status: 'Dismissed', icon: X },
  { key: 'complete', status: 'Done', icon: CircleCheckBig }
];

export function SuggestionBadges({ suggestion }) {
  const { t } = useTranslation();
  return (
    <span className="chip-list badge-pair">
      <StatusBadge tone={SUGGESTION_PRIORITY_TONES[suggestion.priority] || 'neutral'}>{t('suggestions.priority', { priority: t('enums.suggestionPriority.' + suggestion.priority) })}</StatusBadge>
      <StatusBadge>{t('suggestions.effort', { effort: t('enums.suggestionEffort.' + suggestion.effort) })}</StatusBadge>
    </span>
  );
}

SuggestionBadges.propTypes = { suggestion: PropTypes.object.isRequired };

function SuggestionStatus({ suggestion }) {
  const { t } = useTranslation();
  return (
    <span className="date-cell">
      <StatusBadge tone={SUGGESTION_STATUS_TONES[suggestion.status] || 'neutral'}>{t('enums.suggestionStatus.' + suggestion.status)}</StatusBadge>
      {suggestion.status === 'Snoozed' && suggestion.snoozedUntilUtc ? (
        <span className="muted small">
          {t('suggestions.until')} <DateCell value={suggestion.snoozedUntilUtc} relative={false} />
        </span>
      ) : null}
    </span>
  );
}

SuggestionStatus.propTypes = { suggestion: PropTypes.object.isRequired };

// Suggestions to dig into, with a status filter, priority and effort badges, and Accept, Snooze, Dismiss, and
// Complete actions. Pass topicId to scope to one topic.
export default function SuggestionsTable({ topicId = null, tableKey = 'suggestions' }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [topics, setTopics] = useState([]);
  const [refresh, setRefresh] = useState(0);
  const [viewing, setViewing] = useState(null);
  const [snoozing, setSnoozing] = useState(null);
  const [json, setJson] = useState(null);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);

  useEffect(() => {
    if (topicId) return undefined;
    let cancelled = false;
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setTopics((r && r.items) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, topicId]);

  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const extraFilters = useMemo(() => ({ topicId: topicId || undefined }), [topicId]);
  const fetchPage = useCallback((q) => apiClient.listSuggestions(pagedQuery(q)), [apiClient]);

  async function setStatus(suggestion, status, snoozedUntilUtc) {
    const updated = await apiClient.setSuggestionStatus(suggestion.id, status, snoozedUntilUtc);
    addNotification(t('suggestions.changed', { status: t('enums.suggestionStatus.' + status) }), 'success');
    setViewing((v) => (v && v.id === suggestion.id ? updated || { ...v, status } : v));
    bump();
  }

  async function act(suggestion, action) {
    if (action.status === 'Snoozed') {
      setSnoozing(suggestion);
      return;
    }
    try {
      await setStatus(suggestion, action.status);
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    }
  }

  const actionsFor = (s) => ACTIONS.filter((a) => a.status !== s.status || a.status === 'Snoozed').map((a) => ({ ...a, label: t('suggestions.actions.' + a.key) }));

  const columns = [
    {
      key: 'title',
      header: t('suggestions.columns.title'),
      pinned: true,
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.title}</strong>
          {r.description ? <span className="muted small finding-summary-cell">{r.description}</span> : null}
        </span>
      )
    },
    {
      key: 'status',
      header: t('suggestions.columns.status'),
      filter: { type: 'select', param: 'status', options: SUGGESTION_STATUSES.map((s) => ({ value: s, label: t('enums.suggestionStatus.' + s) })) },
      render: (r) => <SuggestionStatus suggestion={r} />
    },
    { key: 'badges', header: t('suggestions.columns.priorityEffort'), render: (r) => <SuggestionBadges suggestion={r} /> },
    ...(topicId
      ? []
      : [
          {
            key: 'topicId',
            header: t('suggestions.columns.topic'),
            filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
            render: (r) => <span className="truncate">{topicNames.get(r.topicId) || r.topicId}</span>
          }
        ]),
    { key: 'createdUtc', header: t('suggestions.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('suggestions.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  return (
    <>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={extraFilters}
        refreshSignal={refresh}
        defaultSort={{ key: 'createdUtc', direction: 'desc' }}
        onRowClick={setViewing}
        rowActions={(r) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setViewing(r) },
          ...actionsFor(r).map((a) => ({ key: a.key, label: a.label, icon: a.icon, onClick: () => act(r, a) })),
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
        ]}
        emptyTitle={t('suggestions.empty.title')}
        emptyMessage={t('suggestions.empty.message')}
      />
      <Modal
        open={Boolean(viewing)}
        title={t('suggestions.detail.title')}
        subtitle={viewing ? <CopyableId value={viewing.id} /> : null}
        onClose={() => setViewing(null)}
        footer={
          viewing ? (
            <div className="row-actions" data-testid="suggestion-actions">
              {actionsFor(viewing).map((a) => (
                <button type="button" key={a.key} className={a.key === 'accept' ? 'button primary small' : 'button secondary small'} onClick={() => act(viewing, a)}>
                  <a.icon size={14} aria-hidden="true" /> {a.label}
                </button>
              ))}
            </div>
          ) : null
        }
      >
        {viewing ? (
          <div className="stack" data-testid="suggestion-detail">
            <h3 className="finding-title">{viewing.title}</h3>
            <span className="row-actions">
              <SuggestionStatus suggestion={viewing} />
              <SuggestionBadges suggestion={viewing} />
            </span>
            {viewing.description ? <p className="finding-text">{viewing.description}</p> : null}
            {viewing.rationale ? (
              <section className="detail-section">
                <div className="detail-section-header">
                  <strong>{t('suggestions.detail.rationale')}</strong>
                </div>
                <div className="detail-section-body finding-text">{viewing.rationale}</div>
              </section>
            ) : null}
            <KeyValueGrid
              rows={[
                { key: 'topic', label: t('suggestions.columns.topic'), value: <Link to={'/dashboard/topics/' + encodeURIComponent(viewing.topicId) + '?tab=suggestions'}>{topicNames.get(viewing.topicId) || viewing.topicId}</Link>, hidden: Boolean(topicId) },
                { key: 'run', label: t('findings.detail.run'), value: <Link to={'/dashboard/runs?run=' + encodeURIComponent(viewing.runId)}>{viewing.runId}</Link>, mono: true },
                { key: 'changed', label: t('suggestions.detail.statusChanged'), value: viewing.statusChangedUtc ? <DateCell value={viewing.statusChangedUtc} /> : null },
                { key: 'created', label: t('suggestions.columns.created'), value: <DateCell value={viewing.createdUtc} /> },
                {
                  key: 'findings',
                  label: t('suggestions.detail.findings'),
                  value:
                    viewing.findingIds && viewing.findingIds.length > 0 ? (
                      <span className="chip-list">
                        {viewing.findingIds.map((id) => (
                          <Link key={id} className="badge badge-info" to={'/dashboard/findings/' + encodeURIComponent(id)}>
                            {id}
                          </Link>
                        ))}
                      </span>
                    ) : null
                }
              ]}
            />
          </div>
        ) : null}
      </Modal>
      <SnoozeModal suggestion={snoozing} onClose={() => setSnoozing(null)} onSnooze={(s, until) => setStatus(s, 'Snoozed', until)} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
    </>
  );
}

SuggestionsTable.propTypes = {
  topicId: PropTypes.string,
  tableKey: PropTypes.string
};
