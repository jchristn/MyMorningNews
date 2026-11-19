import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, CircleStop } from 'lucide-react';
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
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { isActiveRun, runDurationMs } from './runParts.js';

// Everything about one run. It refreshes while the run is queued or running.
export default function RunDetailDrawer({ runId, topicLabel, onClose, onChanged }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [run, setRun] = useState(null);
  const [error, setError] = useState(null);
  const [cancelling, setCancelling] = useState(false);
  const [json, setJson] = useState(false);

  const load = useCallback(async () => {
    if (!runId) return;
    try {
      setRun(await apiClient.getRun(runId));
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, runId]);

  useEffect(() => {
    setRun(null);
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

  const duration = runDurationMs(run);
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
          <KeyValueGrid
            rows={[
              { key: 'topic', label: t('runs.columns.topic'), value: <Link to={'/dashboard/topics/' + encodeURIComponent(run.topicId) + '?tab=runs'}>{(topicLabel && topicLabel(run.topicId)) || run.topicId}</Link> },
              { key: 'queued', label: t('runs.columns.queued'), value: <DateCell value={run.queuedUtc} /> },
              { key: 'started', label: t('runs.columns.started'), value: <DateCell value={run.startedUtc} /> },
              { key: 'completed', label: t('runs.columns.completed'), value: <DateCell value={run.completedUtc} /> },
              { key: 'cancel', label: t('runs.detail.cancelRequested'), value: run.cancelRequested ? t('common.yes') : t('common.no') },
              { key: 'schedule', label: t('runs.detail.schedule'), value: run.scheduleId ? <CopyableId value={run.scheduleId} /> : null },
              { key: 'request', label: t('runs.detail.request'), value: run.researchRequestId ? <Link to={'/dashboard/one-time/' + encodeURIComponent(run.researchRequestId)}>{run.researchRequestId}</Link> : null },
              { key: 'worker', label: t('runs.detail.worker'), value: run.workerId, mono: true }
            ]}
          />
        </div>
      ) : null}
      <JsonViewerModal open={json && Boolean(run)} value={run} onClose={() => setJson(false)} />
    </Modal>
  );
}

RunDetailDrawer.propTypes = {
  runId: PropTypes.string,
  topicLabel: PropTypes.func,
  onClose: PropTypes.func.isRequired,
  onChanged: PropTypes.func
};
