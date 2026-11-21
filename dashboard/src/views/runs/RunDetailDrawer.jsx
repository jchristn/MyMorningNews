import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, CircleStop, ListChecks, Mail, Play, Radio } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { costSignature, stageCosts } from '../../utils/cost.js';
import { RUN_STATUS_TONES } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
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
import { costText } from '../../components/CostValue.jsx';
import RunCostBreakdown from './RunCostBreakdown.jsx';
import RunCostHeader from './RunCostHeader.jsx';
import RunModelCalls from './RunModelCalls.jsx';
import RunToolCalls from './RunToolCalls.jsx';
import RunStages from './RunStages.jsx';
import { RunQueriesTable, RunResultsTable } from './RunSearchTables.jsx';
import { failedStages, findingsLink, isActiveRun, isFinishedRun, mergeStage, retryAction, runDurationMs, withAllStages } from './runParts.js';
import useRunCosts from './useRunCosts.js';
import useRunEvents from './useRunEvents.js';

const TABS = ['stages', 'costs', 'queries', 'results', 'calls', 'tools', 'details'];

// Everything about one run: what it cost (total, tokens, calls, cost per finding), totals, the stage timeline with each
// stage's cost, where the money went (by stage, model, and task), queries, results with dispositions, model calls,
// and provenance. While the run is queued or running it follows the run's server-sent events and shows every stage as
// a live timeline, falling back to polling when the stream cannot be used. The cost report is read again only when a
// stage finishes or the run's cost moves (see useRunCosts). A finished run can be retried from a stage at
// Synthesize or later (same run) or run again (a new run, opened through onOpenRun).
export default function RunDetailDrawer({ runId, topicLabel, onClose, onChanged, onOpenRun }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [retrying, setRetrying] = useState(null);
  const [now, setNow] = useState(() => Date.now());
  const [run, setRun] = useState(null);
  const [stages, setStages] = useState(null);
  const [error, setError] = useState(null);
  const [cancelling, setCancelling] = useState(false);
  const [json, setJson] = useState(false);
  const [emailPreview, setEmailPreview] = useState(false);
  const loadEmailPreview = useCallback(() => apiClient.getRunEmailPreview(runId), [apiClient, runId]);
  const [tab, setTab] = useState('stages');
  const [stageCalls, setStageCalls] = useState(null);

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
    setStageCalls(null);
    load();
  }, [load]);

  const active = Boolean(runId) && isActiveRun(run);
  const stream = useRunEvents(runId, active, {
    onRun: (r) => setRun(r),
    onStage: (stage) => setStages((current) => mergeStage(current, stage)),
    onDone: (r) => {
      if (r) setRun(r);
      if (onChanged) onChanged();
    }
  });
  const live = active && stream === 'live';
  useInterval(load, active && !live ? 3000 : 0);
  useInterval(() => setNow(Date.now()), active ? 1000 : 0);

  async function retry() {
    const result = await apiClient.retryRun(run.id, retrying.fromStage);
    if (result && result.id && result.id !== run.id) {
      addNotification(t('runs.retry.newQueued'), 'success');
      if (onChanged) onChanged();
      if (onOpenRun) onOpenRun(result.id);
      else setRun(result);
      return;
    }
    addNotification(t('runs.retry.resumeQueued', { stage: t('enums.runStage.' + retrying.fromStage, { defaultValue: retrying.fromStage }) }), 'success');
    if (result) setRun(result);
    if (onChanged) onChanged();
  }

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

  const costs = useRunCosts(runId, costSignature(run, stages));
  const shownStages = useMemo(() => (active ? withAllStages(stages) : stages), [active, stages]);
  const perStage = useMemo(() => stageCosts(shownStages, costs.report), [shownStages, costs.report]);
  const openStageCalls = (stage, cost) => {
    const id = (cost && cost.runStageId) || stage.id || stage.runStageId;
    if (id) setStageCalls({ id, stage: stage.stage, cost });
  };

  const duration = run && run.durationMs > 0 && !isActiveRun(run) ? run.durationMs : runDurationMs(run);
  const failed = failedStages(stages);
  const stageCallsLabel = stageCalls ? t('enums.runStage.' + stageCalls.stage, { defaultValue: stageCalls.stage }) : '';
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
            {isFinishedRun(run) ? (
              <button type="button" className="button secondary small" onClick={() => setRetrying({ kind: 'new', fromStage: null })}>
                <Play size={14} aria-hidden="true" /> {t('runs.retry.runAgain')}
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
          <RunCostHeader run={run} report={costs.report} active={active} onShowBreakdown={() => setTab('costs')} />
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
          {tab === 'stages' ? (
            <div className="stack" data-testid="run-stage-panel">
              {active ? (
                <div className="run-live" data-testid="run-live-state">
                  {live ? (
                    <span className="live-badge">
                      <Radio size={12} aria-hidden="true" /> {t('runs.live.streaming')}
                    </span>
                  ) : (
                    <span className="muted small">{t('runs.live.polling')}</span>
                  )}
                </div>
              ) : null}
              {shownStages ? (
                <RunStages stages={shownStages} now={now} costs={perStage} onOpenCalls={openStageCalls} retryFor={(stage) => retryAction(run, stage)} onRetry={(action) => setRetrying(action)} />
              ) : (
                <LoadingState />
              )}
            </div>
          ) : null}
          {tab === 'costs' ? <RunCostBreakdown report={costs.report} error={costs.error} onRetry={costs.reload} onOpenStage={(s) => openStageCalls(s, s)} /> : null}
          {tab === 'queries' ? <RunQueriesTable runId={run.id} /> : null}
          {tab === 'results' ? <RunResultsTable runId={run.id} /> : null}
          {tab === 'calls' ? <RunModelCalls runId={run.id} /> : null}
          {tab === 'tools' ? <RunToolCalls runId={run.id} /> : null}
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
      <Modal
        open={Boolean(stageCalls) && Boolean(run)}
        size="xlarge"
        title={t('costs.stages.callsTitle', { stage: stageCallsLabel })}
        subtitle={
          stageCalls && stageCalls.cost
            ? t('costs.stages.callsSummary', {
                cost: stageCalls.cost.estimatedCostUsd === null || stageCalls.cost.estimatedCostUsd === undefined ? t('costs.notPriced') : costText(stageCalls.cost.estimatedCostUsd, f, t),
                count: stageCalls.cost.llmCallCount || 0
              })
            : null
        }
        onClose={() => setStageCalls(null)}
      >
        {stageCalls && run ? <RunModelCalls runId={run.id} runStageId={stageCalls.id} /> : null}
      </Modal>
      <ConfirmModal
        open={Boolean(retrying)}
        title={retrying && retrying.kind === 'resume' ? t('runs.retry.resumeTitle') : t('runs.retry.newTitle')}
        message={
          retrying
            ? retrying.kind === 'resume'
              ? t('runs.retry.resumeMessage', { stage: t('enums.runStage.' + retrying.fromStage, { defaultValue: retrying.fromStage }) })
              : t('runs.retry.newMessage')
            : ''
        }
        confirmLabel={retrying && retrying.kind === 'resume' ? t('runs.retry.fromStage') : t('runs.retry.runAgain')}
        onClose={() => setRetrying(null)}
        onConfirm={retry}
      />
      {emailPreview && runId ? <EmailPreviewModal title={t('email.preview.runTitle')} load={loadEmailPreview} onClose={() => setEmailPreview(false)} /> : null}
    </Modal>
  );
}

RunDetailDrawer.propTypes = {
  runId: PropTypes.string,
  topicLabel: PropTypes.func,
  onClose: PropTypes.func.isRequired,
  onChanged: PropTypes.func,
  onOpenRun: PropTypes.func
};
