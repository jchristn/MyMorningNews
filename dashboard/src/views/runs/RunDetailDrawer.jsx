import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, CircleStop, ListChecks, Mail } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { RUN_STATUS_TONES } from '../../utils/enums.js';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import EmailPreviewModal from '../email/EmailPreviewModal.jsx';
import RunStages from './RunStages.jsx';
import { RunQueriesTable, RunResultsTable } from './RunSearchTables.jsx';
import { failedStages, findingsLink, isActiveRun, runDurationMs } from './runParts.js';

const TABS = ['stages', 'queries', 'results', 'details'];

// Everything about one run: totals, the stage timeline, queries, results with dispositions, and provenance. It
// refreshes while the run is queued or running.
export default function RunDetailDrawer({ runId, topicLabel, onClose, onChanged }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [run, setRun] = useState(null);
  const [stages, setStages] = useState(null);
  const [error, setError] = useState(null);
  const [cancelling, setCancelling] = useState(false);
  const [json, setJson] = useState(false);
  const [emailPreview, setEmailPreview] = useState(false);
  const loadEmailPreview = useCallback(() => apiClient.getRunEmailPreview(runId), [apiClient, runId]);
  const [tab, setTab] = useState('stages');

  const load = useCallback(async () => {
    if (!runId) return;
    try {
      const [r, s] = await Promise.all([apiClient.getRun(runId), apiClient.listRunStages(runId).catch(() => null)]);
      setRun(r);
      if (s) setStages(Array.isArray(s) ? s : s.items || []);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, runId]);

  useEffect(() => {
    setRun(null);
    setStages(null);
    setTab('stages');
    load();
  }, [load]);

  useInterval(load, runId && isActiveRun(run) ? 3000 : 0);

  async function cancel() {
    setCancelling(true);
    try {
      setRun(await apiClient.cancelRun(run.id));
      if (onChanged) onChanged();
    } catch (err) {
      setError(err);
    } finally {
      setCancelling(false);
    }
  }

  const duration = run && run.durationMs > 0 && !isActiveRun(run) ? run.durationMs : runDurationMs(run);
  const failed = failedStages(stages);
  const tokens = run ? (run.inputTokens || 0) + (run.outputTokens || 0) : 0;
  return (
    <Modal
      open={Boolean(runId)}
      drawer
      title={t('runs.detail.title')}
      subtitle={runId ? <CopyableId value={runId} /> : null}
      onClose={onClose}
      headerExtra={
        run ? (
          <div className="row-actions">
            {isActiveRun(run) ? (
              <button type="button" className="button secondary small" disabled={cancelling || run.cancelRequested} onClick={cancel}>
                <CircleStop size={14} aria-hidden="true" /> {run.cancelRequested ? t('runs.cancelRequested') : t('runs.cancel')}
              </button>
            ) : null}
            <button type="button" className="button secondary small" onClick={() => setEmailPreview(true)}>
              <Mail size={14} aria-hidden="true" /> {t('email.preview.runButton')}
            </button>
            <button type="button" className="icon-button" onClick={() => setJson(true)} aria-label={t('common.viewJson')} title={t('common.viewJson')}>
              <Braces size={16} aria-hidden="true" />
            </button>
          </div>
        ) : null
      }
    >
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      {!run && !error ? <LoadingState /> : null}
      {run ? (
        <div className="stack" data-testid="run-detail">
          <div className="result-grid">
            <StatusBadge tone={RUN_STATUS_TONES[run.status]}>{t('enums.runStatus.' + run.status)}</StatusBadge>
            <span>{t('enums.runTrigger.' + run.trigger)}</span>
            {run.isDryRun ? <StatusBadge tone="info">{t('runs.dryRun')}</StatusBadge> : null}
            {duration !== null ? <span>{f.duration(duration)}</span> : null}
          </div>
          {run.status === 'PartiallySucceeded' ? (
            <div className="notice notice-warning" data-testid="partial-notice">
              {failed.length > 0
                ? t('runs.detail.partialWithStages', { stages: f.list(failed.map((s) => t('enums.runStage.' + s.stage, { defaultValue: s.stage }))) })
                : t('runs.detail.partial')}
            </div>
          ) : null}
          {run.summary ? (
            <section className="detail-section">
              <div className="detail-section-header">
                <strong>{t('runs.detail.summary')}</strong>
              </div>
              <div className="detail-section-body run-summary">{run.summary}</div>
            </section>
          ) : null}
          {run.errorMessage ? <div className="notice notice-danger">{run.errorMessage}</div> : null}
          {run.skippedReason ? <div className="notice notice-warning">{run.skippedReason}</div> : null}
          <div className="kpi-grid run-totals" data-testid="run-totals">
            <KpiTile label={t('runs.totals.sources')} value={f.number(run.sourceCount || 0)} />
            <KpiTile label={t('runs.totals.newFindings')} value={f.number(run.newFindingCount || 0)} tone={run.newFindingCount > 0 ? 'success' : 'neutral'} />
            <KpiTile label={t('runs.totals.duplicates')} value={f.number(run.duplicateCount || 0)} />
            <KpiTile label={t('runs.totals.tokens')} value={f.compact(tokens)} detail={t('runs.totals.tokenSplit', { input: f.compact(run.inputTokens || 0), output: f.compact(run.outputTokens || 0) })} />
            <KpiTile label={t('runs.totals.cost')} value={run.estimatedCostUsd === null || run.estimatedCostUsd === undefined ? '-' : f.usd(run.estimatedCostUsd)} />
          </div>
          <div className="row-actions">
            <Link className="button secondary small" to={findingsLink(run)}>
              <ListChecks size={14} aria-hidden="true" /> {t('runs.detail.viewFindings', { count: run.newFindingCount || 0 })}
            </Link>
          </div>
          <div className="tabs" role="tablist" aria-label={t('runs.detail.tabsLabel')}>
            {TABS.map((id) => (
              <button type="button" role="tab" key={id} aria-selected={tab === id} className={tab === id ? 'active' : ''} onClick={() => setTab(id)}>
                {t('runs.detail.tabs.' + id)}
              </button>
            ))}
          </div>
          {tab === 'stages' ? stages ? <RunStages stages={stages} /> : <LoadingState /> : null}
          {tab === 'queries' ? <RunQueriesTable runId={run.id} /> : null}
          {tab === 'results' ? <RunResultsTable runId={run.id} /> : null}
          {tab === 'details' ? (
            <KeyValueGrid
              rows={[
                { key: 'topic', label: t('runs.columns.topic'), value: <Link to={'/dashboard/topics/' + encodeURIComponent(run.topicId) + '?tab=runs'}>{(topicLabel && topicLabel(run.topicId)) || run.topicId}</Link> },
                { key: 'queued', label: t('runs.columns.queued'), value: <DateCell value={run.queuedUtc} /> },
                { key: 'started', label: t('runs.columns.started'), value: <DateCell value={run.startedUtc} /> },
                { key: 'completed', label: t('runs.columns.completed'), value: <DateCell value={run.completedUtc} /> },
                { key: 'cancel', label: t('runs.detail.cancelRequested'), value: run.cancelRequested ? t('common.yes') : t('common.no') },
                { key: 'schedule', label: t('runs.detail.schedule'), value: run.scheduleId ? <CopyableId value={run.scheduleId} /> : null },
                { key: 'request', label: t('runs.detail.request'), value: run.researchRequestId ? <Link to={'/dashboard/one-time/' + encodeURIComponent(run.researchRequestId)}>{run.researchRequestId}</Link> : null },
                { key: 'worker', label: t('runs.detail.worker'), value: run.workerId, mono: true },
                { key: 'context', label: t('runs.detail.contextHash'), value: run.contextSnapshotHash ? <CopyableId value={run.contextSnapshotHash} /> : null }
              ]}
            />
          ) : null}
        </div>
      ) : null}
      <JsonViewerModal open={json && Boolean(run)} value={run} onClose={() => setJson(false)} />
      {emailPreview && runId ? <EmailPreviewModal title={t('email.preview.runTitle')} load={loadEmailPreview} onClose={() => setEmailPreview(false)} /> : null}
    </Modal>
  );
}

RunDetailDrawer.propTypes = {
  runId: PropTypes.string,
  topicLabel: PropTypes.func,
  onClose: PropTypes.func.isRequired,
  onChanged: PropTypes.func
};
