import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Eye, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { FINDING_CATEGORIES, FINDING_TIERS } from '../../utils/enums.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import FindingDetailDrawer from './FindingDetailDrawer.jsx';
import { ScoreBadge, TierBadge } from './findingParts.jsx';

// Findings ranked by composite relevance with topic, tier, category, and minimum score filters, plus toggles for
// duplicates and dry runs. Pass topicId to scope to one topic or runId to one run. Rows open a detail drawer.
export default function FindingsTable({ topicId = null, runId = null, tableKey = 'findings', initialFindingId = null, onFindingClosed, onClearRun, initialIncludeDryRun = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [topics, setTopics] = useState([]);
  const [openId, setOpenId] = useState(initialFindingId);
  const [json, setJson] = useState(null);
  const [includeDuplicates, setIncludeDuplicates] = useState(false);
  const [includeDryRun, setIncludeDryRun] = useState(initialIncludeDryRun);

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

  useEffect(() => {
    if (initialFindingId) setOpenId(initialFindingId);
  }, [initialFindingId]);

  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const extraFilters = useMemo(
    () => ({ topicId: topicId || undefined, runId: runId || undefined, includeDuplicates: includeDuplicates || undefined, includeDryRun: includeDryRun || undefined }),
    [topicId, runId, includeDuplicates, includeDryRun]
  );
  const fetchPage = useCallback((q) => apiClient.listFindings(pagedQuery(q)), [apiClient]);

  const columns = [
    {
      key: 'title',
      header: t('findings.columns.title'),
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('findings.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.title}</strong>
          {r.summary ? <span className="muted small finding-summary-cell">{r.summary}</span> : null}
          {r.isDuplicate || r.isDryRun ? (
            <span className="chip-list">
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
            render: (r) => <span className="truncate">{topicNames.get(r.topicId) || r.topicId}</span>
          }
        ]),
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
        onRowClick={(r) => setOpenId(r.id)}
        toolbar={toolbar}
        rowActions={(r) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(r.id) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
        ]}
        emptyTitle={t('findings.empty.title')}
        emptyMessage={topicId ? t('findings.empty.topicMessage') : t('findings.empty.message')}
      />
      <FindingDetailDrawer
        findingId={openId}
        onClose={() => {
          setOpenId(null);
          if (onFindingClosed) onFindingClosed();
        }}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
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
