import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Eye, Gavel } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { pagedQuery } from '../../utils/api.js';
import { PREDICTION_STATUSES } from '../../utils/enums.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import FeedbackControl, { useFeedbackMap } from '../../components/FeedbackControl.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import PredictionDetailDrawer, { PredictionStatusBadge, isOverdue } from './PredictionDetailDrawer.jsx';
import PredictionInsights from './PredictionInsights.jsx';
import ResolveModal from './ResolveModal.jsx';

// Predictions with status and topic filters, a detail drawer with reviews, and a Resolve dialog. Pass topicId to
// scope to one topic. insights adds the KPI tiles and calibration chart above the table.
export default function PredictionsTable({ topicId = null, tableKey = 'predictions', insights = true, initialPredictionId = null, onPredictionClosed }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [topics, setTopics] = useState([]);
  const [openId, setOpenId] = useState(initialPredictionId);
  const [resolving, setResolving] = useState(null);
  const [json, setJson] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);

  useEffect(() => {
    if (topicId) return undefined;
    let cancelled = false;
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setTopics((r && r.objects) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, topicId]);

  useEffect(() => {
    if (initialPredictionId) setOpenId(initialPredictionId);
  }, [initialPredictionId]);

  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const extraFilters = useMemo(() => ({ topicId: topicId || undefined }), [topicId]);
  const fetchPage = useCallback((q) => apiClient.listPredictions(pagedQuery(q)), [apiClient]);
  const [ratings, setRating] = useFeedbackMap('Prediction', topicId);

  const columns = [
    {
      key: 'statement',
      header: t('predictions.columns.statement'),
      pinned: true,
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.statement}</strong>
          {r.rationale ? <span className="muted small finding-summary-cell">{r.rationale}</span> : null}
        </span>
      )
    },
    {
      key: 'status',
      header: t('predictions.columns.status'),
      filter: { type: 'select', param: 'status', options: PREDICTION_STATUSES.map((s) => ({ value: s, label: t('enums.predictionStatus.' + s) })) },
      render: (r) => (
        <span className="chip-list">
          <PredictionStatusBadge status={r.status} />
          {isOverdue(r) ? <StatusBadge tone="warning">{t('predictions.overdue')}</StatusBadge> : null}
        </span>
      )
    },
    { key: 'confidence', header: t('predictions.columns.confidence'), align: 'right', render: (r) => f.percent(r.confidence, 0) },
    { key: 'horizonUtc', header: t('predictions.columns.horizon'), sortKey: 'horizonUtc', render: (r) => <DateCell value={r.horizonUtc} /> },
    {
      key: 'brierScore',
      header: t('predictions.columns.brier'),
      align: 'right',
      render: (r) => (r.brierScore === null || r.brierScore === undefined ? <span className="muted">-</span> : f.number(r.brierScore, { minimumFractionDigits: 2, maximumFractionDigits: 4 }))
    },
    ...(topicId
      ? []
      : [
          {
            key: 'topicId',
            header: t('predictions.columns.topic'),
            filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
            render: (r) => <span className="topic-cell">{topicNames.get(r.topicId) || r.topicId}</span>
          }
        ]),
    {
      key: 'rating',
      header: t('predictions.columns.rating'),
      pinned: true,
      render: (r) => (ratings ? <FeedbackControl targetType="Prediction" targetId={r.id} current={ratings.get(r.id) || null} onChange={setRating} compact label={t('feedbackControl.rateItem', { title: r.statement })} /> : null)
    },
    { key: 'createdUtc', header: t('predictions.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('predictions.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  return (
    <div className="stack">
      {insights ? <PredictionInsights topicId={topicId} refreshSignal={refresh} /> : null}
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={extraFilters}
        refreshSignal={refresh}
        defaultSort={{ key: 'horizonUtc', direction: 'asc' }}
        onRowClick={(r) => setOpenId(r.id)}
        rowActions={(r) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(r.id) },
          { key: 'resolve', label: r.resolvedUtc ? t('predictions.actions.reresolve') : t('predictions.actions.resolve'), icon: Gavel, onClick: () => setResolving(r) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
        ]}
        emptyTitle={t('predictions.empty.title')}
        emptyMessage={t('predictions.empty.message')}
      />
      <PredictionDetailDrawer
        predictionId={openId}
        topicLabel={(id) => topicNames.get(id)}
        onClose={() => {
          setOpenId(null);
          if (onPredictionClosed) onPredictionClosed();
        }}
        onChanged={bump}
      />
      <ResolveModal prediction={resolving} onClose={() => setResolving(null)} onResolved={bump} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
    </div>
  );
}

PredictionsTable.propTypes = {
  topicId: PropTypes.string,
  tableKey: PropTypes.string,
  insights: PropTypes.bool,
  initialPredictionId: PropTypes.string,
  onPredictionClosed: PropTypes.func
};
