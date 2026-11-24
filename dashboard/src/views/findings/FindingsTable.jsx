import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Eye, Pin, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { FINDING_CATEGORIES, FINDING_TIERS } from '../../utils/enums.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import FeedbackControl, { useFeedbackMap } from '../../components/FeedbackControl.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import FindingDetailDrawer from './FindingDetailDrawer.jsx';
import { ScoreBadge, TierBadge } from './findingParts.jsx';
import useFindingActions from './useFindingActions.jsx';

// Findings ranked by composite relevance with topic, tier, category, and minimum score filters, plus toggles for
// duplicates, dry runs, hidden findings, and pinned only. Pass topicId to scope to one topic or runId to one run.
// Rows open a detail drawer; row actions pin, hide, and delete.
export default function FindingsTable({ topicId = null, runId = null, tableKey = 'findings', initialFindingId = null, onFindingClosed, onClearRun, initialIncludeDryRun = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [topics, setTopics] = useState([]);
  const [openId, setOpenId] = useState(initialFindingId);
  const [json, setJson] = useState(null);
  const [includeDuplicates, setIncludeDuplicates] = useState(false);
  const [includeDryRun, setIncludeDryRun] = useState(initialIncludeDryRun);
  const [includeHidden, setIncludeHidden] = useState(false);
  const [pinnedOnly, setPinnedOnly] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const bump = () => setRefresh((n) => n + 1);
  const actions = useFindingActions({
    onChanged: bump,
    onDeleted: (finding) => {
      if (openId === finding.id) setOpenId(null);
      bump();
    }
  });

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
    if (initialFindingId) setOpenId(initialFindingId);
  }, [initialFindingId]);

  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const extraFilters = useMemo(
    () => ({
      topicId: topicId || undefined,
      runId: runId || undefined,
      includeDuplicates: includeDuplicates || undefined,
      includeDryRun: includeDryRun || undefined,
      includeHidden: includeHidden || undefined,
      pinned: pinnedOnly || undefined
    }),
    [topicId, runId, includeDuplicates, includeDryRun, includeHidden, pinnedOnly]
  );
  const fetchPage = useCallback((q) => apiClient.listFindings(pagedQuery(q)), [apiClient]);
  const [ratings, setRating] = useFeedbackMap('Finding', topicId);

  const columns = [
    {
      key: 'title',
      header: t('findings.columns.title'),
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('findings.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong className="finding-title-cell">
            {r.pinned ? <Pin size={13} className="pin-marker" aria-label={t('findings.pinnedMarker')} role="img" /> : null}
            {r.title}
          </strong>
          {r.summary ? <span className="muted small finding-summary-cell">{r.summary}</span> : null}
          {r.isDuplicate || r.isDryRun || r.hidden ? (
            <span className="chip-list">
              {r.hidden ? <StatusBadge tone="neutral">{t('findings.hiddenBadge')}</StatusBadge> : null}
              {r.isDuplicate ? <StatusBadge tone="warning">{t('findings.duplicate')}</StatusBadge> : null}
              {r.isDryRun ? <StatusBadge tone="info">{t('runs.dryRun')}</StatusBadge> : null}
            </span>
          ) : null}
        </span>
      )
    },
    {
      key: 'tier',
      header: t('findings.columns.tier'),
      filter: { type: 'select', param: 'tier', options: FINDING_TIERS.map((s) => ({ value: s, label: t('enums.findingTier.' + s) })) },
      render: (r) => <TierBadge tier={r.tier} />
    },
    {
      key: 'relevanceScore',
      header: t('findings.columns.score'),
      sortKey: 'relevanceScore',
      filter: { type: 'number', param: 'minScore', placeholder: t('findings.minScore') },
      render: (r) => <ScoreBadge finding={r} />
    },
    {
      key: 'category',
      header: t('findings.columns.category'),
      filter: { type: 'select', param: 'category', options: FINDING_CATEGORIES.map((s) => ({ value: s, label: t('enums.findingCategory.' + s) })) },
      render: (r) => t('enums.findingCategory.' + r.category, { defaultValue: r.category })
    },
    ...(topicId
      ? []
      : [
          {
            key: 'topicId',
            header: t('findings.columns.topic'),
            filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
            render: (r) => <span className="topic-cell">{topicNames.get(r.topicId) || r.topicId}</span>
          }
        ]),
    {
      key: 'rating',
      header: t('findings.columns.rating'),
      pinned: true,
      render: (r) => (ratings ? <FeedbackControl targetType="Finding" targetId={r.id} current={ratings.get(r.id) || null} onChange={setRating} compact label={t('feedbackControl.rateItem', { title: r.title })} /> : null)
    },
    { key: 'publishedUtc', header: t('findings.columns.published'), sortKey: 'publishedUtc', render: (r) => <DateCell value={r.publishedUtc} /> },
    { key: 'createdUtc', header: t('findings.columns.found'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('findings.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const toolbar = (
    <span className="row-actions table-toggles">
      {runId ? (
        <span className="badge badge-info filter-chip">
          {t('findings.runFilter', { id: runId })}
          {onClearRun ? (
            <button type="button" className="icon-button" onClick={onClearRun} aria-label={t('findings.clearRun')} title={t('findings.clearRun')}>
              <X size={12} aria-hidden="true" />
            </button>
          ) : null}
        </span>
      ) : null}
      <label className="checkbox-row small">
        <input type="checkbox" checked={includeDuplicates} onChange={(e) => setIncludeDuplicates(e.target.checked)} />
        {t('findings.includeDuplicates')}
      </label>
      <label className="checkbox-row small">
        <input type="checkbox" checked={includeDryRun} onChange={(e) => setIncludeDryRun(e.target.checked)} />
        {t('findings.includeDryRuns')}
      </label>
      <label className="checkbox-row small">
        <input type="checkbox" checked={includeHidden} onChange={(e) => setIncludeHidden(e.target.checked)} />
        {t('findings.showHidden')}
      </label>
      <label className="checkbox-row small">
        <input type="checkbox" checked={pinnedOnly} onChange={(e) => setPinnedOnly(e.target.checked)} />
        {t('findings.pinnedOnly')}
      </label>
    </span>
  );

  return (
    <>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={extraFilters}
        defaultSort={{ key: 'relevanceScore', direction: 'desc' }}
        refreshSignal={refresh}
        onRowClick={(r) => setOpenId(r.id)}
        toolbar={toolbar}
        rowActions={(r) => {
          const [pin, hide, remove] = actions.actionsFor(r);
          return [{ key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(r.id) }, pin, hide, { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }, remove];
        }}
        emptyTitle={t('findings.empty.title')}
        emptyMessage={topicId ? t('findings.empty.topicMessage') : t('findings.empty.message')}
      />
      <FindingDetailDrawer
        findingId={openId}
        onChanged={bump}
        onDeleted={() => {
          setOpenId(null);
          if (onFindingClosed) onFindingClosed();
          bump();
        }}
        onClose={() => {
          setOpenId(null);
          if (onFindingClosed) onFindingClosed();
        }}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      {actions.modals}
    </>
  );
}

FindingsTable.propTypes = {
  topicId: PropTypes.string,
  runId: PropTypes.string,
  tableKey: PropTypes.string,
  initialFindingId: PropTypes.string,
  onFindingClosed: PropTypes.func,
  onClearRun: PropTypes.func,
  initialIncludeDryRun: PropTypes.bool
};
