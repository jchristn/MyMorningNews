import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Braces, Eye, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import SourceDetailDrawer from './sources/SourceDetailDrawer.jsx';

// Every page the research fetched for the caller's topics, newest first, with search and topic filters. ?runId=
// scopes to one run and ?source= opens a source in the drawer with its full text.
export default function SourcesView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [params, setParams] = useSearchParams();
  const runId = params.get('runId');
  const [topics, setTopics] = useState([]);
  const [openId, setOpenId] = useState(params.get('source'));
  const [json, setJson] = useState(null);
  const [topicId, setTopicId] = useState(params.get('topicId') || '');

  useEffect(() => {
    let live = true;
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => live && setTopics((r && r.items) || []))
      .catch(() => live && setTopics([]));
    return () => {
      live = false;
    };
  }, [apiClient]);

  const without = (key) => {
    const next = new URLSearchParams(params);
    next.delete(key);
    setParams(next, { replace: true });
  };
  const extraFilters = useMemo(() => ({ runId: runId || undefined, topicId: topicId || undefined }), [runId, topicId]);
  const fetchPage = useCallback((q) => apiClient.listSources(pagedQuery(q)), [apiClient]);

  const columns = [
    {
      key: 'title',
      header: t('sources.columns.title'),
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('sources.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.title || r.canonicalUrl}</strong>
          {r.title ? <span className="muted small mono truncate">{r.canonicalUrl}</span> : null}
        </span>
      )
    },
    { key: 'domain', header: t('sources.columns.domain'), render: (r) => <span className="truncate">{r.domain || ''}</span> },
    { key: 'publishedUtc', header: t('sources.columns.published'), render: (r) => <DateCell value={r.publishedUtc} relative={false} /> },
    { key: 'contentType', header: t('sources.columns.contentType'), className: 'hide-narrow', render: (r) => <span className="mono small">{r.contentType || ''}</span> },
    {
      key: 'firstSeenRunId',
      header: t('sources.columns.firstRun'),
      render: (r) =>
        r.firstSeenRunId ? (
          <Link className="mono small" to={'/dashboard/runs?run=' + encodeURIComponent(r.firstSeenRunId)}>
            {r.firstSeenRunId}
          </Link>
        ) : null
    },
    { key: 'fetchedUtc', header: t('sources.columns.fetched'), defaultHidden: true, render: (r) => <DateCell value={r.fetchedUtc} /> },
    { key: 'timesSeen', header: t('sources.columns.timesSeen'), defaultHidden: true, align: 'right', render: (r) => r.timesSeen || 0 },
    { key: 'id', header: t('sources.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const toolbar = (
    <span className="row-actions table-toggles">
      {runId ? (
        <span className="badge badge-info filter-chip">
          {t('findings.runFilter', { id: runId })}
          <button type="button" className="icon-button" onClick={() => without('runId')} aria-label={t('sources.clearRun')} title={t('sources.clearRun')}>
            <X size={12} aria-hidden="true" />
          </button>
        </span>
      ) : null}
      <label className="inline-field">
        <span>{t('sources.topic')}</span>
        <select value={topicId} onChange={(e) => setTopicId(e.target.value)}>
          <option value="">{t('sources.allTopics')}</option>
          {topics.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
      </label>
    </span>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.sources')} summary={t('routes.sources.summary')} />
      <section className="panel">
        <DataTable
          key={runId || 'all'}
          tableKey="sources"
          columns={columns}
          fetchPage={fetchPage}
          extraFilters={extraFilters}
          toolbar={toolbar}
          onRowClick={(r) => setOpenId(r.id)}
          rowActions={(r) => [
            { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(r.id) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
          ]}
          emptyTitle={t('sources.empty.title')}
          emptyMessage={t('sources.empty.message')}
        />
      </section>
      <SourceDetailDrawer
        sourceId={openId}
        onClose={() => {
          setOpenId(null);
          if (params.get('source')) without('source');
        }}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
    </div>
  );
}
